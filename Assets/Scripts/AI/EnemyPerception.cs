using Game.Combat;
using Game.Core;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// What an enemy knows about its target (SPEC.md section 17: patrol, investigate,
    /// chase, search all depend on this).
    ///
    /// Perception is deliberately separate from <see cref="EnemyController"/> so the
    /// state machine asks questions ("can I see it?", "where did I last see it?")
    /// rather than doing its own raycasting, and so the sight test can be exercised
    /// in isolation.
    /// </summary>
    public class EnemyPerception : MonoBehaviour
    {
        [SerializeField] private EnemyArchetype archetype;

        [Tooltip("Where sight is measured from. Falls back to this object's transform.")]
        [SerializeField] private Transform eye;

        [Tooltip("What blocks line of sight. Colliders on the target itself are ignored.")]
        [SerializeField] private LayerMask sightBlockers = ~0;

        private Transform target;
        private float lastSeenTime = float.NegativeInfinity;
        private bool everSeen;

        /// <summary>The player transform, resolved on first use.</summary>
        public Transform Target => ResolveTarget();

        /// <summary>True on the frame the target is visible right now.</summary>
        public bool HasLineOfSight { get; private set; }

        /// <summary>
        /// Where the target was last actually seen. Chase and Search both steer to
        /// this, which is what stops an enemy tracking a player it cannot see.
        /// </summary>
        public Vector3 LastKnownPosition { get; private set; }

        /// <summary>Seconds since the target was last seen, or a large number if never.</summary>
        public float TimeSinceSeen => everSeen ? Time.time - lastSeenTime : float.MaxValue;

        public bool HasEverSeenTarget => everSeen;

        private Vector3 EyePosition => eye != null ? eye.position : transform.position + Vector3.up * 1.2f;

        private void Update()
        {
            HasLineOfSight = Evaluate();

            if (HasLineOfSight)
            {
                everSeen = true;
                lastSeenTime = Time.time;
                LastKnownPosition = Target.position;
            }
        }

        /// <summary>
        /// Plants a last-known position without the enemy having seen anything — used
        /// when a noise or an ally's alert should send it to investigate.
        /// </summary>
        public void ReportDisturbance(Vector3 position)
        {
            LastKnownPosition = position;
        }

        /// <summary>Forgets the target entirely, so a leashed enemy starts clean when it gets home.</summary>
        public void Forget()
        {
            everSeen = false;
            HasLineOfSight = false;
            lastSeenTime = float.NegativeInfinity;
        }

        /// <summary>
        /// The pure geometric half of the sight test: is <paramref name="targetPosition"/>
        /// inside a cone of <paramref name="coneAngleDegrees"/> total width, within
        /// <paramref name="range"/>? Separated from the raycast so it can be tested
        /// without colliders.
        /// </summary>
        public static bool IsWithinCone(Vector3 eyePosition, Vector3 forward, Vector3 targetPosition,
            float range, float coneAngleDegrees)
        {
            var offset = targetPosition - eyePosition;
            var distance = offset.magnitude;

            if (distance > range)
            {
                return false;
            }

            if (distance < 0.0001f)
            {
                return true;
            }

            var angle = Vector3.Angle(forward.normalized, offset / distance);
            return angle <= coneAngleDegrees * 0.5f;
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(EnemyArchetype enemyArchetype, Transform eyeTransform = null)
        {
            archetype = enemyArchetype;
            eye = eyeTransform;
        }

        private bool Evaluate()
        {
            var resolved = ResolveTarget();
            if (resolved == null || archetype == null)
            {
                return false;
            }

            // A dead player stops being a target, or corpses would keep the city hostile.
            var targetHealth = resolved.GetComponentInParent<HealthComponent>();
            if (targetHealth != null && targetHealth.IsDead)
            {
                return false;
            }

            var eyePosition = EyePosition;
            var targetPosition = resolved.position + Vector3.up * 1f;
            var offset = targetPosition - eyePosition;
            var distance = offset.magnitude;

            // Hearing: close enough to notice regardless of which way the enemy faces.
            var heard = distance <= archetype.HearingRange;

            if (!heard && !IsWithinCone(eyePosition, transform.forward, targetPosition,
                    archetype.SightRange, archetype.SightAngle))
            {
                return false;
            }

            if (distance <= 0.0001f)
            {
                return true;
            }

            // Line of sight. Hits on the target's own hierarchy do not count as blocking.
            if (Physics.Raycast(eyePosition, offset / distance, out var hit, distance, sightBlockers,
                    QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(resolved) && hit.transform != resolved)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Finds the player the same way the rest of the project does — by
        /// <see cref="PlayerDeath"/>, not by tag — and re-resolves if the reference
        /// goes stale after a scene load.
        /// </summary>
        private Transform ResolveTarget()
        {
            if (target != null)
            {
                return target;
            }

            var player = Object.FindAnyObjectByType<PlayerDeath>(FindObjectsInactive.Exclude);
            if (player != null)
            {
                target = player.transform;
                return target;
            }

            return null;
        }
    }
}

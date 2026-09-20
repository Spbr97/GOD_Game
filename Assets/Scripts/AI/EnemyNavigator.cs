using Game.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Game.AI
{
    /// <summary>
    /// Movement for an enemy (SPEC.md section 17: "use navigation meshes"), plus the
    /// navigation-failure fallback SPEC.md section 50 requires — stop, recalculate,
    /// choose the nearest valid point, and failing that tell the caller to go back to
    /// patrol.
    ///
    /// The <see cref="NavMeshAgent"/> is optional. With no agent, or with an agent
    /// that is not on a baked surface, this steers the transform straight at the
    /// destination instead. That is worse movement, not broken movement, which keeps
    /// a scene with no baked NavMesh playable rather than filling it with enemies
    /// frozen in place.
    /// </summary>
    public class EnemyNavigator : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent agent;

        [Tooltip("Distance at which a destination counts as reached.")]
        [SerializeField] private float arrivalThreshold = 0.6f;

        [Tooltip("How far to look for a valid point when a destination is off the NavMesh.")]
        [SerializeField] private float resampleRadius = 4f;

        [Tooltip("Used when steering directly, with no usable NavMeshAgent.")]
        [SerializeField] private float fallbackTurnDegreesPerSecond = 360f;

        private Vector3 destination;
        private bool hasDestination;
        private float speed = 3f;
        private bool warnedAboutMissingNavMesh;

        /// <summary>
        /// True when the last <see cref="SetDestination"/> could not be pathed at all,
        /// even after resampling. The state machine reads this and returns to patrol.
        /// </summary>
        public bool LastMoveFailed { get; private set; }

        /// <summary>True while steering the transform because no NavMesh is usable.</summary>
        public bool IsUsingFallbackSteering => !AgentIsUsable();

        public Vector3 Destination => destination;

        public bool HasArrived
        {
            get
            {
                if (!hasDestination)
                {
                    return true;
                }

                var flat = destination - transform.position;
                flat.y = 0f;
                return flat.magnitude <= arrivalThreshold;
            }
        }

        private void Awake()
        {
            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }
        }

        public void SetSpeed(float newSpeed)
        {
            speed = Mathf.Max(0f, newSpeed);
            if (agent != null)
            {
                agent.speed = speed;
            }
        }

        /// <summary>
        /// Heads for <paramref name="target"/>. Returns false when the point could not
        /// be reached by any means, having already applied the section 50 fallback.
        /// </summary>
        public bool SetDestination(Vector3 target)
        {
            destination = target;
            hasDestination = true;
            LastMoveFailed = false;

            if (!AgentIsUsable())
            {
                // No NavMesh: steer directly. Not a failure, just a worse path.
                WarnAboutMissingNavMeshOnce();
                return true;
            }

            agent.isStopped = false;

            if (agent.SetDestination(target))
            {
                return true;
            }

            // Fallback step 1 and 2: stop, then recalculate against the nearest valid point.
            agent.ResetPath();

            if (NavMesh.SamplePosition(target, out var hit, resampleRadius, NavMesh.AllAreas)
                && agent.SetDestination(hit.position))
            {
                destination = hit.position;
                GameLogger.LogFallback(
                    LogCategory.AI,
                    "Navigation to destination",
                    $"{name}",
                    "the requested point is not on the NavMesh",
                    $"moving to the nearest valid point {hit.position}",
                    this);
                return true;
            }

            // Fallback step 4: give up on this point. The caller returns to patrol.
            LastMoveFailed = true;
            hasDestination = false;
            GameLogger.LogFallback(
                LogCategory.AI,
                "Navigation to destination",
                $"{name}",
                $"no valid NavMesh point within {resampleRadius}m of {target}",
                "the enemy abandons this destination and returns to patrol",
                this);
            return false;
        }

        public void Stop()
        {
            hasDestination = false;

            if (AgentIsUsable())
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        /// <summary>Turns to face a point without moving — used while attacking and while idle.</summary>
        public void FaceTowards(Vector3 point, float turnDegreesPerSecond)
        {
            var offset = point - transform.position;
            offset.y = 0f;

            if (offset.sqrMagnitude < 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(offset),
                turnDegreesPerSecond * Time.deltaTime);
        }

        private void Update()
        {
            if (!hasDestination || !IsUsingFallbackSteering)
            {
                return;
            }

            // Direct steering. Deliberately ignores obstacles: a NavMesh is the right
            // answer and this only exists so an unbaked scene is still playable.
            var offset = destination - transform.position;
            offset.y = 0f;

            if (offset.magnitude <= arrivalThreshold)
            {
                return;
            }

            transform.position += offset.normalized * (speed * Time.deltaTime);
            FaceTowards(destination, fallbackTurnDegreesPerSecond);
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(NavMeshAgent navAgent, float arrival = 0.6f)
        {
            agent = navAgent;
            arrivalThreshold = arrival;
        }

        private bool AgentIsUsable()
        {
            return agent != null && agent.enabled && agent.isOnNavMesh;
        }

        private void WarnAboutMissingNavMeshOnce()
        {
            if (warnedAboutMissingNavMesh || !Application.isPlaying)
            {
                return;
            }

            warnedAboutMissingNavMesh = true;
            GameLogger.LogFallback(
                LogCategory.AI,
                "NavMeshAgent pathfinding",
                $"{name} in scene {gameObject.scene.name}",
                agent == null ? "no NavMeshAgent on this enemy" : "the agent is not on a baked NavMesh",
                "steering straight at the destination instead, ignoring obstacles",
                this);
        }
    }
}

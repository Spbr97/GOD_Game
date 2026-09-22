using Game.Combat;
using Game.Core;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// The ground a boss fight is allowed to happen on (SPEC.md section 54's edge
    /// case 7, section 55's "important boss arenas must reset safely" and section 56's
    /// "boss arena escape").
    ///
    /// Walking out does not trap the player and does not kill them — the arena is not
    /// a cage. It simply ends the fight: the boss goes back to full health, phase 1
    /// and its authored spot, and the player can walk in and start again. That is
    /// both halves of the requirement at once. Section 55 wants the arena to reset
    /// safely, and section 56 wants the escape not to pay — a boss that stayed at 10%
    /// health while the player healed up outside is the cheese, not the walking out.
    ///
    /// A radius rather than a trigger volume: a trigger needs the player to have a
    /// collider that keeps reporting, and an arena whose reset depends on an exit
    /// event fails open in the one case that matters — the player leaving by a route
    /// the collider does not cover, which is exactly how an unintended escape happens
    /// (section 54's edge case 30). Asking "where is the player now" cannot miss.
    ///
    /// Section 56 also says not to over-restrict harmless experimentation, so
    /// <see cref="graceSeconds"/> exists: a dodge that carries the player a step past
    /// the line, or a knockback, must not end the encounter.
    /// </summary>
    public class BossArena : MonoBehaviour
    {
        [Tooltip("The boss this arena belongs to. Found on this object or its children when left empty.")]
        [SerializeField] private BossController boss;

        [Tooltip("Centre of the arena. This object's own position when left empty.")]
        [SerializeField] private Transform arenaCentre;

        [Tooltip("How far from the centre the fight may range.")]
        [SerializeField] private float radius = 25f;

        [Tooltip("Extra distance past the radius before the player counts as having left, so standing on the boundary does not flicker.")]
        [SerializeField] private float escapeMargin = 3f;

        [Tooltip("How long the player must stay outside before the encounter resets. Covers a dodge or a knockback across the line.")]
        [SerializeField] private float graceSeconds = 3f;

        [Tooltip("Seconds between checks. A fight does not leave an arena in a single frame.")]
        [SerializeField] private float checkInterval = 0.25f;

        private Transform player;
        private float nextCheckAt;
        private float outsideSince = -1f;

        /// <summary>Whether the player is currently outside the arena. Read by tests and the debug overlay.</summary>
        public bool PlayerIsOutside => outsideSince >= 0f;

        /// <summary>How many times this arena has reset its encounter.</summary>
        public int ResetCount { get; private set; }

        public Vector3 Centre => arenaCentre != null ? arenaCentre.position : transform.position;
        public float Radius => radius;

        private void Awake()
        {
            if (boss == null)
            {
                boss = GetComponentInChildren<BossController>();
            }

            if (boss == null)
            {
                GameLogger.LogError(LogCategory.AI, $"BossArena on '{name}' has no BossController; it will do nothing.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (Time.time < nextCheckAt)
            {
                return;
            }

            nextCheckAt = Time.time + checkInterval;

            if (!boss.EncounterStarted || boss.Defeated)
            {
                // Nothing to leave. Clearing the timer matters: a player who wandered
                // near the arena before the fight must not arrive with the grace
                // period already half spent.
                outsideSince = -1f;
                return;
            }

            if (!TryGetPlayer(out var playerTransform))
            {
                return;
            }

            if (Contains(playerTransform.position))
            {
                outsideSince = -1f;
                return;
            }

            if (outsideSince < 0f)
            {
                outsideSince = Time.time;
                return;
            }

            if (Time.time - outsideSince >= graceSeconds)
            {
                ResetEncounter();
            }
        }

        /// <summary>Whether a position is inside the arena, boundary margin included.</summary>
        public bool Contains(Vector3 position)
        {
            // Horizontal only. Vertical distance is a balcony or a staircase, not an
            // escape, and a boss arena with a height limit would reset the fight every
            // time the player jumped.
            var offset = position - Centre;
            offset.y = 0f;

            var limit = radius + escapeMargin;
            return offset.sqrMagnitude <= limit * limit;
        }

        /// <summary>Ends the encounter and puts the boss back. Public so a test and the debug console can force it.</summary>
        public void ResetEncounter()
        {
            outsideSince = -1f;
            ResetCount++;

            GameLogger.LogFallback(
                LogCategory.AI,
                $"the boss fight in '{name}' was abandoned",
                "BossArena.ResetEncounter",
                $"the player stayed more than {graceSeconds:0.#}s outside the arena",
                "the boss is restored to full health at phase 1 and returned to its start, so the fight can be begun again",
                this);

            boss.ResetEncounter();
        }

        private bool TryGetPlayer(out Transform playerTransform)
        {
            // The same lookup HudUI and SaveManager use: PlayerDeath marks the player,
            // so this depends on no tag and no name.
            if (player == null)
            {
                var found = FindAnyObjectByType<PlayerDeath>();
                player = found != null ? found.transform : null;
            }

            playerTransform = player;
            return playerTransform != null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.3f, 0.3f, 0.8f);
            DrawCircle(Centre, radius);

            Gizmos.color = new Color(0.9f, 0.6f, 0.2f, 0.4f);
            DrawCircle(Centre, radius + escapeMargin);
        }

        private static void DrawCircle(Vector3 centre, float circleRadius)
        {
            const int segments = 48;
            var previous = centre + new Vector3(circleRadius, 0f, 0f);

            for (var i = 1; i <= segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var next = centre + new Vector3(Mathf.Cos(angle) * circleRadius, 0f, Mathf.Sin(angle) * circleRadius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(BossController controller, Transform centre, float arenaRadius, float margin = 3f, float grace = 3f)
        {
            boss = controller;
            arenaCentre = centre;
            radius = arenaRadius;
            escapeMargin = margin;
            graceSeconds = grace;
        }
    }
}

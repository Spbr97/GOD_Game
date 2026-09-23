using Game.Combat;
using Game.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Game.AI
{
    /// <summary>
    /// The three-phase boss framework (SPEC.md section 18), layered on top of the same
    /// <see cref="HealthComponent"/>/<see cref="EnemyController"/>/<see cref="EnemyCombatant"/>
    /// every other enemy uses — a boss is an enemy with phases, not a separate creature
    /// type. Health, telegraphs, stagger and death/save-persistence are already correct
    /// by reuse; this adds only what section 18 asks for on top:
    ///
    /// - phase transitions, driven by health fraction thresholds;
    /// - each phase raises attack speed and move speed rather than damage or max
    ///   health, per section 18's "do not make bosses difficult by only increasing HP";
    /// - a reward revealed on death;
    /// - <see cref="onEncounterStarted"/>/<see cref="onPhase2Entered"/>/
    ///   <see cref="onPhase3Entered"/>/<see cref="onDefeated"/> hooks for the
    ///   environmental interaction, supernatural transformation, scripted cinematic
    ///   moment and victory sequence the real content (unique arena, unique music,
    ///   VFX) will wire in once TASK 014's cinematic system and TASK 018's animation
    ///   pass exist (SPEC.md section 78, placeholder-first).
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public class BossController : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string bossId = "BOSS_UNNAMED";
        [SerializeField] private string displayName = "Unnamed Boss";

        [Header("Wiring")]
        [SerializeField] private EnemyController enemyController;
        [SerializeField] private EnemyCombatant combatant;

        [Tooltip("Optional. If set, a defeat survives a save (SPEC.md TASK 009 pattern) and the reward is revealed on load without replaying the victory hooks.")]
        [SerializeField] private SaveIdentity identity;

        [Header("Phases (SPEC.md section 18)")]
        [Tooltip("Health fraction at or below which phase 2 begins.")]
        [Range(0f, 1f)][SerializeField] private float phase2Threshold = 0.66f;

        [Tooltip("Health fraction at or below which phase 3 — the supernatural transformation — begins.")]
        [Range(0f, 1f)][SerializeField] private float phase3Threshold = 0.33f;

        [Tooltip("Attacks recur faster each phase. Movement and attack speed, not damage or HP, are what section 18 asks difficulty to come from.")]
        [SerializeField] private float phase2CooldownMultiplier = 0.75f;

        [SerializeField] private float phase3CooldownMultiplier = 0.55f;
        [SerializeField] private float phase2SpeedMultiplier = 1.1f;
        [SerializeField] private float phase3SpeedMultiplier = 1.25f;

        [Tooltip("Placeholder supernatural-transformation tint for phase 3, until real VFX exists (SPEC.md section 78).")]
        [SerializeField] private Color phase3Tint = new(0.6f, 0.1f, 0.75f);

        [Header("Reward (SPEC.md section 18)")]
        [Tooltip("Made active once the boss is defeated. Inactive in the authored scene until then.")]
        [SerializeField] private GameObject reward;

        [Header("Hooks for later systems")]
        [SerializeField] private UnityEvent onEncounterStarted;
        [SerializeField] private UnityEvent onPhase2Entered;
        [SerializeField] private UnityEvent onPhase3Entered;
        [SerializeField] private UnityEvent onDefeated;

        private HealthComponent health;

        /// <summary>
        /// True once <see cref="onDefeated"/> has run. Separate from <see cref="Defeated"/>
        /// because of a real ordering hazard: <see cref="Game.Combat.EnemyHealth"/> also
        /// listens for <c>health.Died</c> and, on this exact death, marks the world flag
        /// this component listens for — synchronously, before this component's own
        /// <c>Died</c> subscription gets its turn. Without this second flag, that nested
        /// flag event would set <see cref="Defeated"/> first and an <c>if (Defeated) return;</c>
        /// guard in <see cref="OnDied"/> would then swallow a genuinely live kill's
        /// victory fanfare. It is set only in <see cref="OnDied"/>, so a save restore —
        /// which never raises <c>Died</c> — correctly never plays it.
        /// </summary>
        private bool fanfarePlayed;

        public string BossId => bossId;
        public string DisplayName => displayName;
        public int Phase { get; private set; } = 1;
        public bool EncounterStarted { get; private set; }
        public bool Defeated { get; private set; }

        private void Awake()
        {
            health = GetComponent<HealthComponent>();

            if (enemyController == null) { enemyController = GetComponent<EnemyController>(); }
            if (combatant == null) { combatant = GetComponent<EnemyCombatant>(); }
            if (identity == null) { identity = GetComponent<SaveIdentity>(); }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<EnemyAlertedEvent>(OnAlerted);

            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Died += OnDied;

                // Resuming after a checkpoint respawn or a mid-fight load: apply the
                // phase the current health already implies, silently — this is
                // continuity, not a transition moment, so the phase-entered hooks
                // (music stings, transformation VFX) do not replay.
                ApplyPhaseEffects(PhaseFor(health.HealthFraction, phase2Threshold, phase3Threshold));
            }

            if (identity == null)
            {
                return;
            }

            EventBus.Subscribe<WorldFlagChangedEvent>(OnWorldFlagChanged);

            // See EnemyHealth.OnEnable for why both a check-now and a subscription are
            // needed: this covers a save already applied before this boss existed.
            if (WorldObjectState.IsMarked(WorldObjectState.DeadFlag(identity.Id)))
            {
                ApplyRestoredDefeat();
            }
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnemyAlertedEvent>(OnAlerted);

            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }

            if (identity != null)
            {
                EventBus.Unsubscribe<WorldFlagChangedEvent>(OnWorldFlagChanged);
            }
        }

        /// <summary>
        /// The phase table as a pure function, testable without a scene — the same
        /// shape as <see cref="EnemyController.Decide"/>. Highest phase wins so a
        /// single big hit that skips past phase 2's threshold still lands on phase 3.
        /// </summary>
        public static int PhaseFor(float healthFraction, float phase2Threshold, float phase3Threshold)
        {
            if (healthFraction <= phase3Threshold)
            {
                return 3;
            }

            if (healthFraction <= phase2Threshold)
            {
                return 2;
            }

            return 1;
        }

        private void OnAlerted(EnemyAlertedEvent alerted)
        {
            if (alerted.Enemy != gameObject || EncounterStarted || Defeated)
            {
                return;
            }

            EncounterStarted = true;
            GameLogger.Log(LogCategory.AI, $"Boss encounter started: {displayName}.", this);
            EventBus.Publish(new BossEncounterStartedEvent(gameObject, bossId, displayName));
            onEncounterStarted?.Invoke();
        }

        private void OnDamaged(DamageData damage)
        {
            if (health == null || health.IsDead)
            {
                return;
            }

            var next = PhaseFor(health.HealthFraction, phase2Threshold, phase3Threshold);
            if (next <= Phase)
            {
                return;
            }

            Phase = next;
            ApplyPhaseEffects(Phase);

            GameLogger.Log(LogCategory.AI, $"Boss {displayName} entered phase {Phase}.", this);
            EventBus.Publish(new BossPhaseChangedEvent(gameObject, Phase));

            (Phase == 3 ? onPhase3Entered : onPhase2Entered)?.Invoke();
        }

        /// <summary>Applies a phase's speed/cooldown/tint without publishing anything — used both on a real transition and when silently resuming.</summary>
        private void ApplyPhaseEffects(int phase)
        {
            Phase = phase;

            var speedMultiplier = phase switch { 3 => phase3SpeedMultiplier, 2 => phase2SpeedMultiplier, _ => 1f };
            var cooldownMultiplier = phase switch { 3 => phase3CooldownMultiplier, 2 => phase2CooldownMultiplier, _ => 1f };

            enemyController?.SetSpeedMultiplier(speedMultiplier);
            combatant?.SetCooldownMultiplier(cooldownMultiplier);

            if (phase == 3)
            {
                enemyController?.ApplyPhaseTint(phase3Tint);
            }
        }

        private void OnDied(DamageData killingBlow)
        {
            if (fanfarePlayed)
            {
                return;
            }

            fanfarePlayed = true;
            MarkDefeated();

            GameLogger.Log(LogCategory.AI, $"Boss {displayName} defeated.", this);
            onDefeated?.Invoke();
        }

        /// <summary>
        /// Abandons a live encounter and puts the boss back as it was authored
        /// (SPEC.md section 54's edge case 7, section 55's "important boss arenas must
        /// reset safely", section 56's "boss arena escape").
        ///
        /// Full health is the point, not a side effect: without it a player could chip
        /// the boss down from outside the arena in complete safety, which is exactly
        /// the cheese section 56 names. Phase goes back to 1 so the transformation
        /// tint and the faster pace go with it, and <see cref="EncounterStarted"/>
        /// clears so re-entering plays the encounter's opening properly rather than
        /// dropping the player into a fight already in progress.
        ///
        /// A defeated boss is never reset: death is permanent and saved.
        /// </summary>
        public void ResetEncounter()
        {
            if (Defeated || (!EncounterStarted && (health == null || Mathf.Approximately(health.HealthFraction, 1f))))
            {
                return;
            }

            EncounterStarted = false;
            Phase = 1;

            health?.ResetHealth();
            ApplyPhaseEffects(1);
            enemyController?.ResetToHome();

            GameLogger.Log(LogCategory.AI, $"Boss {displayName} encounter reset.", this);
            EventBus.Publish(new BossEncounterResetEvent(gameObject, bossId));
        }

        private void OnWorldFlagChanged(WorldFlagChangedEvent changed)
        {
            if (changed.Value && identity != null && changed.Flag == WorldObjectState.DeadFlag(identity.Id))
            {
                ApplyRestoredDefeat();
            }
        }

        /// <summary>
        /// Applies a remembered defeat directly: reveals the reward and tells
        /// listeners (the journal, a boss-gated door) the boss is gone, without
        /// invoking <see cref="onDefeated"/> — the victory fanfare is a moment, not a
        /// fact, and there is no moment to react to on a load. Also the path a live
        /// kill's nested flag event takes; see <see cref="fanfarePlayed"/> for why that
        /// does not steal the fanfare from <see cref="OnDied"/>.
        /// </summary>
        private void ApplyRestoredDefeat()
        {
            MarkDefeated();
        }

        /// <summary>The fact of defeat, idempotent and shared by both the live and restore paths.</summary>
        private void MarkDefeated()
        {
            if (Defeated)
            {
                return;
            }

            Defeated = true;
            RevealReward();
            EventBus.Publish(new BossDefeatedEvent(gameObject, bossId));
        }

        private void RevealReward()
        {
            if (reward != null)
            {
                reward.SetActive(true);
            }
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(string id, string label, EnemyController controller, EnemyCombatant enemyCombatant,
            GameObject rewardObject = null, float phase2At = 0.66f, float phase3At = 0.33f)
        {
            bossId = id;
            displayName = label;
            enemyController = controller;
            combatant = enemyCombatant;
            reward = rewardObject;
            phase2Threshold = phase2At;
            phase3Threshold = phase3At;
        }
    }
}

using UnityEngine;

namespace Game.AI
{
    /// <summary>The ten enemy classes from SPEC.md section 16.</summary>
    public enum EnemyClass
    {
        ForgottenSoldier,
        AshCreature,
        NagaRaksh,
        StoneGuardian,
        SkyHunter,
        DreamStalker,
        TimeWraith,
        DivineGuardian,
        MiniBoss,
        Boss
    }

    /// <summary>
    /// The tuning for one enemy class (SPEC.md section 16). Everything an enemy is —
    /// how far it sees, how hard it hits, how long it telegraphs — lives here as data
    /// rather than in per-instance serialized fields, so a class can be rebalanced in
    /// one place and so the same prefab can be reskinned into a different enemy.
    ///
    /// Difficulty scaling is applied when a value is read, not baked in: see
    /// <see cref="TelegraphFor"/> and <see cref="DamageFor"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Enemy_", menuName = "God Game/Enemy Archetype")]
    public class EnemyArchetype : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string archetypeId = "ENEMY_FORGOTTEN_SOLDIER";
        [SerializeField] private string displayName = "Forgotten Soldier";
        [SerializeField] private EnemyClass enemyClass = EnemyClass.ForgottenSoldier;

        [Tooltip("Placeholder visual identity until real art exists (SPEC.md section 78).")]
        [SerializeField] private Color bodyTint = new(0.45f, 0.42f, 0.38f);

        [Header("Vitals")]
        [SerializeField] private float maxHealth = 60f;

        [Tooltip("Damage absorbed before poise breaks and the enemy staggers. Zero means it staggers on every hit.")]
        [SerializeField] private float poise = 24f;

        [SerializeField] private float poiseRecoveryPerSecond = 8f;
        [SerializeField] private float staggerDuration = 0.9f;

        [Header("Movement")]
        [SerializeField] private float patrolSpeed = 1.6f;
        [SerializeField] private float chaseSpeed = 4.2f;
        [SerializeField] private float turnSpeedDegrees = 360f;

        [Tooltip("How far from home the enemy will chase before giving up. Anti-cheese: prevents permanent aggro (SPEC.md section 56).")]
        [SerializeField] private float leashRange = 28f;

        [Header("Perception")]
        [SerializeField] private float sightRange = 16f;

        [Tooltip("Full width of the vision cone in degrees.")]
        [SerializeField] private float sightAngle = 120f;

        [Tooltip("Radius within which the enemy notices the player regardless of facing.")]
        [SerializeField] private float hearingRange = 6f;

        [Tooltip("Seconds of no line of sight before the target counts as lost.")]
        [SerializeField] private float loseTargetAfter = 3f;

        [SerializeField] private float investigateDuration = 4f;
        [SerializeField] private float searchDuration = 6f;

        [Tooltip("How far from the last known position the enemy wanders while searching.")]
        [SerializeField] private float searchRadius = 6f;

        [Header("Attack")]
        [SerializeField] private float attackRange = 2.4f;
        [SerializeField] private float attackDamage = 10f;

        [Tooltip("Readable wind-up before the hitbox opens (SPEC.md section 16 telegraphs).")]
        [SerializeField] private float telegraphDuration = 0.55f;

        [SerializeField] private float attackActiveDuration = 0.18f;
        [SerializeField] private float attackRecovery = 0.5f;
        [SerializeField] private float attackCooldown = 1.6f;

        [Tooltip("Colour flashed during the telegraph, so the swing is readable without animation.")]
        [SerializeField] private Color telegraphColour = new(1f, 0.72f, 0.2f);

        [Header("Retreat")]
        [Tooltip("Health fraction below which this enemy backs off. Zero means it never retreats.")]
        [Range(0f, 1f)][SerializeField] private float retreatHealthFraction;

        [SerializeField] private float retreatDuration = 2.5f;

        public string ArchetypeId => archetypeId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public EnemyClass Class => enemyClass;
        public Color BodyTint => bodyTint;

        public float MaxHealth => maxHealth;
        public float Poise => poise;
        public float PoiseRecoveryPerSecond => poiseRecoveryPerSecond;
        public float StaggerDuration => staggerDuration;

        public float PatrolSpeed => patrolSpeed;
        public float ChaseSpeed => chaseSpeed;
        public float TurnSpeedDegrees => turnSpeedDegrees;
        public float LeashRange => leashRange;

        public float SightRange => sightRange;
        public float SightAngle => sightAngle;
        public float HearingRange => hearingRange;
        public float LoseTargetAfter => loseTargetAfter;
        public float InvestigateDuration => investigateDuration;
        public float SearchDuration => searchDuration;
        public float SearchRadius => searchRadius;

        public float AttackRange => attackRange;
        public float AttackActiveDuration => attackActiveDuration;
        public float AttackRecovery => attackRecovery;
        public Color TelegraphColour => telegraphColour;

        public float RetreatHealthFraction => retreatHealthFraction;
        public float RetreatDuration => retreatDuration;

        /// <summary>Telegraph length at the current difficulty. Story lengthens it, Mythic shortens it.</summary>
        public float TelegraphFor(DifficultyModifiersSource source = DifficultyModifiersSource.Current)
        {
            return Mathf.Max(0.05f, telegraphDuration * Modifiers(source).EnemyTelegraph);
        }

        /// <summary>Damage at the current difficulty.</summary>
        public float DamageFor(DifficultyModifiersSource source = DifficultyModifiersSource.Current)
        {
            return Mathf.Max(0f, attackDamage * Modifiers(source).EnemyDamage);
        }

        /// <summary>Gap between attacks at the current difficulty.</summary>
        public float CooldownFor(DifficultyModifiersSource source = DifficultyModifiersSource.Current)
        {
            return Mathf.Max(0.1f, attackCooldown * Modifiers(source).EnemyAttackCooldown);
        }

        /// <summary>The unscaled authored values, for the Inspector and for tests.</summary>
        public float BaseTelegraphDuration => telegraphDuration;
        public float BaseAttackDamage => attackDamage;
        public float BaseAttackCooldown => attackCooldown;

        private static Game.Core.DifficultyModifiers Modifiers(DifficultyModifiersSource source)
        {
            return source == DifficultyModifiersSource.Unscaled
                ? Game.Core.Difficulty.For(Game.Core.DifficultyMode.Normal)
                : Game.Core.Difficulty.Modifiers;
        }

        /// <summary>
        /// Test and tooling seam for the thresholds the state machine branches on.
        /// Separate from <see cref="Configure"/> because the two are tuned by
        /// different people: combat numbers above, behaviour here.
        /// </summary>
        public void ConfigureBehaviour(float leash, float retreatFraction, float retreatSeconds,
            float loseTargetSeconds, float investigateSeconds, float searchSeconds)
        {
            leashRange = leash;
            retreatHealthFraction = Mathf.Clamp01(retreatFraction);
            retreatDuration = retreatSeconds;
            loseTargetAfter = loseTargetSeconds;
            investigateDuration = investigateSeconds;
            searchDuration = searchSeconds;
        }

        /// <summary>Test and tooling seam; archetypes are normally authored in the Inspector.</summary>
        public void Configure(string id, string label, EnemyClass archetypeClass, float health,
            float damage, float range, float telegraph, float enemyPoise, float sight, float sightCone)
        {
            archetypeId = id;
            displayName = label;
            enemyClass = archetypeClass;
            maxHealth = health;
            attackDamage = damage;
            attackRange = range;
            telegraphDuration = telegraph;
            poise = enemyPoise;
            sightRange = sight;
            sightAngle = sightCone;
        }
    }

    /// <summary>Whether a reader wants difficulty-scaled numbers or the authored ones.</summary>
    public enum DifficultyModifiersSource
    {
        Current,
        Unscaled
    }
}

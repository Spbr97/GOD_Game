using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// A collider that forwards incoming damage to a <see cref="HealthComponent"/>
    /// (SPEC.md TASK 002). Separating the two lets one entity carry several hurtboxes
    /// — a weak point on the head, an armoured torso — without duplicating health.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Hurtbox : MonoBehaviour
    {
        [SerializeField] private HealthComponent health;

        /// <summary>Scales incoming damage. Above 1 makes this collider a weak point.</summary>
        [SerializeField] private float damageMultiplier = 1f;

        [Tooltip("Attacks of this same faction will not land. Neutral is hit by everything.")]
        [SerializeField] private Faction faction = Faction.Neutral;

        /// <summary>Which side this hurtbox belongs to. See <see cref="Combat.Faction"/>.</summary>
        public Faction Faction => faction;

        private bool warnedAboutMissingHealth;

        /// <summary>The entity this hurtbox belongs to, used so an attack cannot hit its own owner.</summary>
        public GameObject Owner
        {
            get
            {
                ResolveHealth();
                return health != null ? health.gameObject : gameObject;
            }
        }

        public HealthComponent Health
        {
            get
            {
                ResolveHealth();
                return health;
            }
        }

        private void Awake()
        {
            ResolveHealth();
        }

        /// <summary>
        /// Applies damage to the owning health pool, scaled by this hurtbox's multiplier.
        /// Returns true when health actually changed.
        /// </summary>
        public bool ApplyDamage(DamageData damage)
        {
            if (!enabled || !ResolveHealth())
            {
                return false;
            }

            damage.Amount *= damageMultiplier;
            return health.TakeDamage(damage);
        }

        /// <summary>Editor/test seam for wiring a hurtbox that is not under its health pool.</summary>
        public void Configure(HealthComponent owningHealth, float multiplier = 1f,
            Faction hurtboxFaction = Faction.Neutral)
        {
            health = owningHealth;
            damageMultiplier = multiplier;
            faction = hurtboxFaction;
        }

        /// <summary>
        /// Finds the owning health pool on demand rather than only in Awake. Awake does
        /// not run in EditMode tests, and does not re-run if a hurtbox is reparented
        /// after being added, so the lookup has to be lazy to be reliable.
        /// </summary>
        private bool ResolveHealth()
        {
            if (health != null)
            {
                return true;
            }

            health = GetComponentInParent<HealthComponent>();
            if (health != null)
            {
                return true;
            }

            if (!warnedAboutMissingHealth)
            {
                warnedAboutMissingHealth = true;
                GameLogger.LogFallback(
                    LogCategory.Combat,
                    "Hurtbox has no HealthComponent",
                    $"{name} in scene {gameObject.scene.name}",
                    "none assigned and none found on a parent",
                    "the hurtbox absorbs hits without applying damage",
                    this);
            }

            return false;
        }
    }
}

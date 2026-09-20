using Game.Combat;
using Game.Core;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Poise, hit reactions and knockback (SPEC.md section 16). Closes the TASK 002
    /// limitation that <see cref="DamageData"/> carried a direction nothing consumed.
    ///
    /// Poise is a pool that damage drains and time refills. Breaking it interrupts
    /// whatever the enemy was doing, which is the only thing that makes an enemy with
    /// a long telegraph punishable rather than merely slow.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public class EnemyStagger : MonoBehaviour
    {
        [SerializeField] private EnemyArchetype archetype;

        [Tooltip("Metres pushed back when poise breaks. Placeholder for a knockback animation.")]
        [SerializeField] private float knockbackDistance = 0.6f;

        private HealthComponent health;
        private float poiseRemaining;
        private float staggerEndsAt;
        private bool initialized;

        /// <summary>True while the enemy is reeling and cannot act.</summary>
        public bool IsStaggered => Time.time < staggerEndsAt;

        /// <summary>Poise left before the next break, for debug overlays and tests.</summary>
        public float PoiseRemaining => poiseRemaining;

        private float MaxPoise => archetype != null ? archetype.Poise : 0f;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            if (health != null)
            {
                health.Damaged += HandleDamaged;
            }

            EventBus.Subscribe<ParryEvent>(HandleParry);
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
            }

            EventBus.Unsubscribe<ParryEvent>(HandleParry);
        }

        /// <summary>
        /// "Successful parry staggers enemy" (SPEC.md section 15). Arrives as an
        /// event rather than a call because Combat must not reference AI; the guard
        /// announces the parry and the attacker recognises itself.
        /// </summary>
        private void HandleParry(ParryEvent parry)
        {
            if (parry.Attacker != gameObject || health == null || health.IsDead)
            {
                return;
            }

            ForceStagger(parry.StaggerDuration);
            GameLogger.Log(LogCategory.AI, $"{name} was parried by {parry.Defender?.name}.", this);
        }

        private void Update()
        {
            if (IsStaggered || archetype == null)
            {
                return;
            }

            poiseRemaining = Mathf.Min(MaxPoise, poiseRemaining + archetype.PoiseRecoveryPerSecond * Time.deltaTime);
        }

        /// <summary>
        /// Drains poise. Returns true when this was the hit that broke it, so the
        /// caller can play the reaction. A max poise of zero means every hit staggers.
        /// </summary>
        public bool ApplyPoiseDamage(float amount)
        {
            EnsureInitialized();

            if (IsStaggered)
            {
                return false;
            }

            poiseRemaining -= Mathf.Max(0f, amount);
            if (poiseRemaining > 0f)
            {
                return false;
            }

            Break();
            return true;
        }

        /// <summary>
        /// Staggers regardless of poise. Used by parry (SPEC.md section 15) via
        /// <see cref="HandleParry"/>, and available to scripted encounters.
        /// </summary>
        public void ForceStagger(float duration = 0f)
        {
            EnsureInitialized();
            staggerEndsAt = Time.time + (duration > 0f ? duration : StaggerDuration());
            poiseRemaining = MaxPoise;
            EventBus.Publish(new EnemyStaggeredEvent(gameObject, staggerEndsAt - Time.time));
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(EnemyArchetype enemyArchetype)
        {
            archetype = enemyArchetype;
            initialized = false;
            EnsureInitialized();
        }

        private void HandleDamaged(DamageData damage)
        {
            if (health != null && health.IsDead)
            {
                return;
            }

            if (ApplyPoiseDamage(damage.Amount))
            {
                ApplyKnockback(damage.Direction);
            }
        }

        private void Break()
        {
            poiseRemaining = MaxPoise;
            staggerEndsAt = Time.time + StaggerDuration();

            GameLogger.Log(LogCategory.AI, $"{name} staggered.", this);
            EventBus.Publish(new EnemyStaggeredEvent(gameObject, StaggerDuration()));
        }

        private float StaggerDuration()
        {
            return archetype != null ? archetype.StaggerDuration : 0.5f;
        }

        private void ApplyKnockback(Vector3 direction)
        {
            if (knockbackDistance <= 0f || !Application.isPlaying)
            {
                return;
            }

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            // Snapped rather than animated: there is no knockback animation to sync
            // with yet (SPEC.md section 78, placeholder-first).
            transform.position += direction.normalized * knockbackDistance;
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            poiseRemaining = MaxPoise;

            if (health == null)
            {
                health = GetComponent<HealthComponent>();
            }
        }
    }
}

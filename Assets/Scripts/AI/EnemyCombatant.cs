using System.Collections;
using Game.Combat;
using Game.Core;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// One enemy attack: telegraph, active frames, recovery, cooldown (SPEC.md
    /// section 16 requires attack patterns and telegraphs).
    ///
    /// This drives a <see cref="Hitbox"/> directly rather than reusing
    /// <see cref="WeaponController"/>, because the player's weapon takes its damage
    /// and timings from serialized fields on the weapon, while an enemy's come from
    /// its <see cref="EnemyArchetype"/> and are rescaled by difficulty on every
    /// swing. The deduplication guarantees are unaffected — they live in
    /// <see cref="Hitbox"/> and <see cref="HealthComponent"/>, which are shared.
    /// </summary>
    public class EnemyCombatant : MonoBehaviour
    {
        [SerializeField] private EnemyArchetype archetype;
        [SerializeField] private Hitbox hitbox;

        [Tooltip("Tinted during the telegraph so the swing is readable without animation (SPEC.md section 78).")]
        [SerializeField] private Renderer[] telegraphRenderers;

        private Coroutine attackRoutine;
        private float nextAttackAllowedAt;
        private MaterialPropertyBlock propertyBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>True from the start of a telegraph to the end of a recovery.</summary>
        public bool IsAttacking => attackRoutine != null;

        /// <summary>True while the damaging window is open. Exposed for tests.</summary>
        public bool IsHitboxOpen => hitbox != null && hitbox.IsActive;

        /// <summary>True while the wind-up is playing and the player can still react.</summary>
        public bool IsTelegraphing { get; private set; }

        public bool IsOnCooldown => Time.time < nextAttackAllowedAt;

        public bool CanAttack => !IsAttacking && !IsOnCooldown && isActiveAndEnabled;

        private void Awake()
        {
            if (hitbox == null)
            {
                hitbox = GetComponentInChildren<Hitbox>(true);
            }

            if (telegraphRenderers == null || telegraphRenderers.Length == 0)
            {
                telegraphRenderers = GetComponentsInChildren<Renderer>();
            }

            if (hitbox != null)
            {
                // This enemy owns its weapon, not whatever it happens to be parented
                // under. Without this an enemy under an encounter object would share a
                // transform root with its allies and could damage itself.
                hitbox.SetOwner(gameObject);
            }

            if (hitbox == null)
            {
                GameLogger.LogFallback(
                    LogCategory.AI,
                    "EnemyCombatant has no Hitbox",
                    $"{name}",
                    "none assigned and none found in children",
                    "the enemy telegraphs and swings but deals no damage",
                    this);
            }
        }

        /// <summary>
        /// Starts a swing. Returns false when one is already running or the cooldown
        /// has not elapsed, so the state machine can decide to close distance instead.
        /// </summary>
        public bool TryAttack()
        {
            if (!CanAttack)
            {
                return false;
            }

            attackRoutine = StartCoroutine(AttackRoutine());
            return true;
        }

        /// <summary>
        /// Interrupts a swing. Called when poise breaks — the whole point of stagger
        /// is that it stops the attack that was coming.
        /// </summary>
        public void CancelAttack()
        {
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }

            IsTelegraphing = false;
            hitbox?.Deactivate();
            ClearTint();
        }

        private void OnDisable()
        {
            CancelAttack();
        }

        private IEnumerator AttackRoutine()
        {
            var telegraph = archetype != null ? archetype.TelegraphFor() : 0.5f;
            var active = archetype != null ? archetype.AttackActiveDuration : 0.18f;
            var recovery = archetype != null ? archetype.AttackRecovery : 0.4f;
            var damage = archetype != null ? archetype.DamageFor() : 10f;
            var cooldown = archetype != null ? archetype.CooldownFor() : 1.5f;

            IsTelegraphing = true;
            ApplyTint(archetype != null ? archetype.TelegraphColour : Color.yellow);
            yield return new WaitForSeconds(telegraph);
            IsTelegraphing = false;
            ClearTint();

            hitbox?.Activate(new DamageData
            {
                Amount = damage,
                Type = DamageType.Physical
            });

            yield return new WaitForSeconds(active);
            hitbox?.Deactivate();

            yield return new WaitForSeconds(recovery);

            // The cooldown is measured from the end of recovery, so a difficulty that
            // shortens it shortens the gap rather than overlapping the swing itself.
            nextAttackAllowedAt = Time.time + cooldown;
            attackRoutine = null;
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(EnemyArchetype enemyArchetype, Hitbox weaponHitbox, Renderer[] renderers = null)
        {
            archetype = enemyArchetype;
            hitbox = weaponHitbox;

            if (renderers != null)
            {
                telegraphRenderers = renderers;
            }
        }

        private void ApplyTint(Color colour)
        {
            SetColour(colour, clear: false);
        }

        private void ClearTint()
        {
            SetColour(default, clear: true);
        }

        private void SetColour(Color colour, bool clear)
        {
            if (telegraphRenderers == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();

            foreach (var renderer in telegraphRenderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (clear)
                {
                    renderer.SetPropertyBlock(null);
                    continue;
                }

                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, colour);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}

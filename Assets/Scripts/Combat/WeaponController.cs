using System.Collections;
using Game.Core;
using Game.Progression;
using UnityEngine;

namespace Game.Combat
{
    public enum AttackType
    {
        Light,
        Heavy,

        /// <summary>
        /// The execution on an enemy below the finisher threshold (SPEC.md section
        /// 14). Slow, unblockable and lethal by design; the controller only offers
        /// it when a target qualifies.
        /// </summary>
        Finisher
    }

    /// <summary>
    /// Drives the Astra Blade's damage windows (SPEC.md section 13, TASK 002).
    /// It owns the timing of a swing — windup, active frames, recovery — and opens
    /// the <see cref="Hitbox"/> only during the active window.
    ///
    /// Timings are serialized numbers rather than animation events because TASK 002
    /// runs on placeholder geometry with no attack animations yet (SPEC.md section 78).
    /// When real animations land, these should move to animation events.
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        [SerializeField] private Hitbox hitbox;

        [Header("Light attack")]
        [SerializeField] private float lightDamage = 12f;
        [SerializeField] private float lightWindup = 0.12f;
        [SerializeField] private float lightActive = 0.14f;
        [SerializeField] private float lightRecovery = 0.18f;

        [Header("Heavy attack")]
        [SerializeField] private float heavyDamage = 28f;
        [SerializeField] private float heavyWindup = 0.32f;
        [SerializeField] private float heavyActive = 0.20f;
        [SerializeField] private float heavyRecovery = 0.38f;

        [Header("Finisher")]
        [SerializeField] private float finisherDamage = 250f;
        [SerializeField] private float finisherWindup = 0.25f;
        [SerializeField] private float finisherActive = 0.2f;
        [SerializeField] private float finisherRecovery = 0.6f;

        private Coroutine swingRoutine;

        /// <summary>True from the start of a windup to the end of a recovery.</summary>
        public bool IsSwinging => swingRoutine != null;

        /// <summary>True only during the active damage window. Exposed for tests.</summary>
        public bool IsHitboxOpen => hitbox != null && hitbox.IsActive;

        /// <summary>The attack currently playing, meaningful only while <see cref="IsSwinging"/>.</summary>
        public AttackType CurrentAttack { get; private set; }

        public float TotalDuration(AttackType type)
        {
            return type switch
            {
                AttackType.Heavy => heavyWindup + heavyActive + heavyRecovery,
                AttackType.Finisher => finisherWindup + finisherActive + finisherRecovery,
                _ => lightWindup + lightActive + lightRecovery
            };
        }

        /// <summary>Base damage before combo scaling, for HUD and tests.</summary>
        public float BaseDamage(AttackType type)
        {
            return type switch
            {
                AttackType.Heavy => heavyDamage,
                AttackType.Finisher => finisherDamage,
                _ => lightDamage
            };
        }

        private void Awake()
        {
            if (hitbox == null)
            {
                hitbox = GetComponentInChildren<Hitbox>(true);
            }

            if (hitbox == null)
            {
                GameLogger.LogFallback(
                    LogCategory.Combat,
                    "WeaponController has no Hitbox",
                    $"{name}",
                    "none assigned and none found in children",
                    "attacks will play their timing but deal no damage",
                    this);
            }
        }

        /// <summary>
        /// Starts a swing. Returns false if one is already running, so the caller
        /// does not spend stamina on an input that was dropped.
        /// <paramref name="damageMultiplier"/> is the reward for the combo chain
        /// this swing completes (SPEC.md section 14); 1 is a plain swing.
        /// </summary>
        public bool TrySwing(AttackType type, float damageMultiplier = 1f)
        {
            if (IsSwinging || !isActiveAndEnabled)
            {
                return false;
            }

            CurrentAttack = type;
            swingRoutine = StartCoroutine(SwingRoutine(type, Mathf.Max(0f, damageMultiplier)));
            return true;
        }

        /// <summary>Cancels a swing in progress, e.g. when the player dodges out of it.</summary>
        public void CancelSwing()
        {
            if (swingRoutine != null)
            {
                StopCoroutine(swingRoutine);
                swingRoutine = null;
            }

            hitbox?.Deactivate();
        }

        private IEnumerator SwingRoutine(AttackType type, float damageMultiplier)
        {
            float windup, active, recovery;
            switch (type)
            {
                case AttackType.Heavy:
                    windup = heavyWindup; active = heavyActive; recovery = heavyRecovery;
                    break;
                case AttackType.Finisher:
                    windup = finisherWindup; active = finisherActive; recovery = finisherRecovery;
                    break;
                default:
                    windup = lightWindup; active = lightActive; recovery = lightRecovery;
                    break;
            }

            // Warrior branch's "attack damage" skill (SPEC.md section 30, TASK 016):
            // a bonus fraction added to a base multiplier of 1, so an unlocked skill
            // never needs a matching baseline change here.
            var skillBonus = 1f + (SkillTreeManager.Instance?.GetBonus(SkillEffectType.AttackDamageMultiplier) ?? 0f);
            var damage = BaseDamage(type) * damageMultiplier * skillBonus;

            // Heavies and finishers go through a block (SPEC.md section 15 still
            // lets them be parried, which is the guard's decision, not the weapon's).
            var unblockable = type != AttackType.Light;

            yield return new WaitForSeconds(windup);

            if (hitbox != null)
            {
                hitbox.Activate(new DamageData
                {
                    Amount = damage,
                    Type = DamageType.Physical,
                    Unblockable = unblockable
                });
            }

            yield return new WaitForSeconds(active);

            hitbox?.Deactivate();

            yield return new WaitForSeconds(recovery);

            swingRoutine = null;
        }
    }
}

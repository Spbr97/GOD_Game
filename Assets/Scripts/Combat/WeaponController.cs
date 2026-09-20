using System.Collections;
using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    public enum AttackType
    {
        Light,
        Heavy
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

        [Header("Combo")]
        [Tooltip("Damage multiplier applied per step of the current combo chain.")]
        [SerializeField] private float comboDamageStep = 0.15f;

        private Coroutine swingRoutine;

        /// <summary>True from the start of a windup to the end of a recovery.</summary>
        public bool IsSwinging => swingRoutine != null;

        /// <summary>True only during the active damage window. Exposed for tests.</summary>
        public bool IsHitboxOpen => hitbox != null && hitbox.IsActive;

        public float TotalDuration(AttackType type)
        {
            return type == AttackType.Heavy
                ? heavyWindup + heavyActive + heavyRecovery
                : lightWindup + lightActive + lightRecovery;
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
        /// </summary>
        public bool TrySwing(AttackType type, int comboIndex = 0)
        {
            if (IsSwinging || !isActiveAndEnabled)
            {
                return false;
            }

            swingRoutine = StartCoroutine(SwingRoutine(type, comboIndex));
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

        private IEnumerator SwingRoutine(AttackType type, int comboIndex)
        {
            var isHeavy = type == AttackType.Heavy;
            var windup = isHeavy ? heavyWindup : lightWindup;
            var active = isHeavy ? heavyActive : lightActive;
            var recovery = isHeavy ? heavyRecovery : lightRecovery;
            var damage = (isHeavy ? heavyDamage : lightDamage) * (1f + comboDamageStep * comboIndex);

            yield return new WaitForSeconds(windup);

            if (hitbox != null)
            {
                hitbox.Activate(new DamageData
                {
                    Amount = damage,
                    Type = DamageType.Physical,
                    Unblockable = isHeavy
                });
            }

            yield return new WaitForSeconds(active);

            hitbox?.Deactivate();

            yield return new WaitForSeconds(recovery);

            swingRoutine = null;
        }
    }
}

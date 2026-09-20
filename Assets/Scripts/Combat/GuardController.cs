using System;
using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Block and parry (SPEC.md section 15, TASK 007). One input drives both: the
    /// press opens a short parry window, and holding past it is a block.
    ///
    /// Parry: a hit inside the window is deflected, the attacker is staggered via
    /// <see cref="ParryEvent"/>, and a hit inside the first part of the window is a
    /// perfect parry that also restores divine energy. Block: a hit while holding is
    /// absorbed at a stamina cost proportional to its damage; running out breaks the
    /// guard, the hit lands, and the player is stunned briefly. Unblockable attacks
    /// (heavies) break the guard outright but can still be parried, so there is
    /// always a skilled answer to every attack.
    ///
    /// Timing windows scale with <see cref="Difficulty.Modifiers"/>. Difficulty
    /// moves the window, never the numbers on the enemy (SPEC.md section 15).
    ///
    /// This registers itself as the <see cref="HealthComponent.Guard"/>; the health
    /// component consults it and nothing else needs to know a guard exists.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public class GuardController : MonoBehaviour, IDamageGuard
    {
        [Header("Parry")]
        [Tooltip("Seconds after the press during which a hit is parried, at Normal difficulty.")]
        [SerializeField] private float parryWindow = 0.2f;

        [Tooltip("Seconds after the press during which a parry is perfect, at Normal difficulty.")]
        [SerializeField] private float perfectParryWindow = 0.08f;

        [Tooltip("Divine energy restored by a perfect parry (SPEC.md section 15).")]
        [SerializeField] private float perfectParryDivineEnergy = 15f;

        [Tooltip("How long the parried enemy reels. Zero uses the enemy's own stagger duration.")]
        [SerializeField] private float parryStaggerDuration = 1.2f;

        [Header("Block")]
        [Tooltip("Stamina spent per point of damage absorbed.")]
        [SerializeField] private float staminaPerDamageBlocked = 0.6f;

        [Tooltip("Seconds the player cannot guard or act after a guard break.")]
        [SerializeField] private float guardBreakStun = 0.8f;

        private HealthComponent health;
        private StaminaComponent stamina;
        private DivineEnergyComponent divineEnergy;

        private float parryWindowEndsAt = float.NegativeInfinity;
        private float perfectWindowEndsAt = float.NegativeInfinity;
        private float guardBrokenUntil = float.NegativeInfinity;

        // Time is read through a delegate so EditMode tests, which have no frame
        // clock, can step it deterministically. Play mode uses the engine clock.
        private Func<float> clock = () => Time.time;
        private float Now => clock();

        /// <summary>True while the guard input is held (after or during the parry window).</summary>
        public bool IsBlocking { get; private set; }

        /// <summary>True while a hit would be parried rather than blocked.</summary>
        public bool IsParryWindowOpen => Now <= parryWindowEndsAt;

        /// <summary>True while reeling from a guard break; no action is possible.</summary>
        public bool IsGuardBroken => Now < guardBrokenUntil;

        /// <summary>Raised on a successful parry, with whether it was perfect.</summary>
        public event Action<bool> Parried;

        /// <summary>The parry window in seconds after difficulty scaling, for HUD hints and tests.</summary>
        public float ScaledParryWindow => parryWindow * Difficulty.Modifiers.PlayerTimingWindow;

        public float ScaledPerfectParryWindow => perfectParryWindow * Difficulty.Modifiers.PlayerTimingWindow;

        private void Awake()
        {
            Resolve();
        }

        private void OnEnable()
        {
            Resolve();
            if (health != null)
            {
                health.Guard = this;
            }
        }

        /// <summary>
        /// Lazy so EditMode tests, where Awake and OnEnable do not run on AddComponent,
        /// still find their sibling components.
        /// </summary>
        private void Resolve()
        {
            if (health == null) { health = GetComponent<HealthComponent>(); }
            if (stamina == null) { stamina = GetComponent<StaminaComponent>(); }
            if (divineEnergy == null) { divineEnergy = GetComponent<DivineEnergyComponent>(); }
        }

        private void OnDisable()
        {
            EndGuard();
            if (health != null && ReferenceEquals(health.Guard, this))
            {
                health.Guard = null;
            }
        }

        /// <summary>
        /// Raises the guard. Opens the parry window and starts blocking. Returns
        /// false when the guard is broken or the player is dead.
        /// </summary>
        public bool BeginGuard()
        {
            Resolve();
            if (!isActiveAndEnabled || IsGuardBroken || (health != null && health.IsDead))
            {
                return false;
            }

            var now = Now;
            parryWindowEndsAt = now + ScaledParryWindow;
            perfectWindowEndsAt = now + ScaledPerfectParryWindow;
            IsBlocking = true;
            return true;
        }

        /// <summary>Lowers the guard. The parry window from the press is left to run out on its own.</summary>
        public void EndGuard()
        {
            IsBlocking = false;
        }

        public bool TryGuard(ref DamageData damage)
        {
            if (!isActiveAndEnabled)
            {
                return false;
            }

            Resolve();

            // Hazards are not swings. There is nothing to deflect and a shield does
            // not help against standing in fire.
            if (damage.Type == DamageType.Environmental)
            {
                return false;
            }

            if (IsParryWindowOpen)
            {
                Parry(damage, perfect: Now <= perfectWindowEndsAt);
                return true;
            }

            if (!IsBlocking)
            {
                return false;
            }

            if (damage.Unblockable)
            {
                BreakGuard("unblockable attack");
                return false;
            }

            var cost = damage.Amount * staminaPerDamageBlocked;
            if (stamina != null && !stamina.TrySpend(cost))
            {
                BreakGuard("out of stamina");
                return false;
            }

            GameLogger.Log(LogCategory.Combat, $"{name} blocked {damage.Amount:0.#} damage.", this);
            EventBus.Publish(new AttackBlockedEvent(gameObject, damage));
            return true;
        }

        /// <summary>Test seam so the timing can be exercised without real input.</summary>
        public void Configure(float parrySeconds, float perfectSeconds, float staminaPerDamage, float stunSeconds,
            Func<float> timeSource = null)
        {
            parryWindow = parrySeconds;
            perfectParryWindow = perfectSeconds;
            staminaPerDamageBlocked = staminaPerDamage;
            guardBreakStun = stunSeconds;
            if (timeSource != null)
            {
                clock = timeSource;
            }
        }

        private void Parry(DamageData damage, bool perfect)
        {
            // A parry consumes the window: one press deflects one attack.
            parryWindowEndsAt = float.NegativeInfinity;
            perfectWindowEndsAt = float.NegativeInfinity;

            if (perfect && divineEnergy != null)
            {
                divineEnergy.Gain(perfectParryDivineEnergy);
            }

            GameLogger.Log(LogCategory.Combat,
                $"{name} {(perfect ? "perfectly " : string.Empty)}parried {damage.Source?.name ?? "an attack"}.", this);

            Parried?.Invoke(perfect);
            EventBus.Publish(new ParryEvent(gameObject, damage.Source, perfect, parryStaggerDuration));
        }

        private void BreakGuard(string reason)
        {
            IsBlocking = false;
            parryWindowEndsAt = float.NegativeInfinity;
            perfectWindowEndsAt = float.NegativeInfinity;
            guardBrokenUntil = Now + guardBreakStun;

            GameLogger.Log(LogCategory.Combat, $"{name}'s guard broke ({reason}).", this);
            EventBus.Publish(new GuardBrokenEvent(gameObject, guardBreakStun));
        }
    }
}

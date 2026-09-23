using System.Collections;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Combat
{
    /// <summary>
    /// Turns player input into attacks, dodges, guards and the divine ability (SPEC.md
    /// sections 13-15, TASK 002, TASK 007 and TASK 011). It owns the rules — can I act,
    /// can I pay for it, which chain does this complete, does this target qualify for a
    /// finisher — and delegates the swing to <see cref="WeaponController"/>, the guard
    /// to <see cref="GuardController"/> and targeting to <see cref="LockOnController"/>.
    ///
    /// Of SPEC.md section 13's actions, Jump is locomotion; everything else lives here.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public class CombatController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private WeaponController weapon;
        [SerializeField] private StaminaComponent stamina;
        [SerializeField] private GuardController guard;
        [SerializeField] private LockOnController lockOn;
        [SerializeField] private DivineEnergyComponent divineEnergy;
        [SerializeField] private Game.Player.PlayerController locomotion;

        [Header("Stamina costs")]
        [SerializeField] private float lightAttackCost = 12f;
        [SerializeField] private float heavyAttackCost = 25f;
        [SerializeField] private float dodgeCost = 20f;
        [SerializeField] private float finisherCost = 10f;

        [Header("Ember Step (SPEC.md section 8.1)")]
        [Tooltip("Divine energy spent per use.")]
        [SerializeField] private float abilityCost = 20f;

        [Tooltip("Require the corresponding quest reward flag before Ember Step can be used.")]
        [SerializeField] private bool requireAbilityUnlock;
        [SerializeField] private string abilityId = "EMBER_STEP";

        [Tooltip("Seconds before Ember Step can be used again.")]
        [SerializeField] private float abilityCooldown = 3f;

        [SerializeField] private float abilityDashDuration = 0.25f;
        [SerializeField] private float abilityDashSpeed = 14f;
        [Tooltip("Seconds into the dash when invulnerability starts.")]
        [SerializeField] private float abilityInvulnerabilityStart = 0.03f;
        [Tooltip("Seconds into the dash when invulnerability ends at Normal difficulty; difficulty scales its length.")]
        [SerializeField] private float abilityInvulnerabilityEnd = 0.18f;

        [Header("Combo")]
        [Tooltip("How long after a swing a follow-up still counts as part of the chain.")]
        [SerializeField] private float comboWindow = 0.7f;

        [Header("Finisher")]
        [Tooltip("An enemy at or below this fraction of health can be finished (SPEC.md section 14).")]
        [SerializeField] private float finisherHealthThreshold = 0.2f;

        [Tooltip("How close the enemy must be for a light attack to become a finisher.")]
        [SerializeField] private float finisherRange = 2.5f;

        [Header("Dodge")]
        [SerializeField] private float dodgeDuration = 0.35f;
        [SerializeField] private float dodgeSpeed = 9f;
        [Tooltip("Seconds into the dodge when invulnerability starts.")]
        [SerializeField] private float invulnerabilityStart = 0.05f;
        [Tooltip("Seconds into the dodge when invulnerability ends at Normal difficulty. SPEC.md section 15 asks for a short window; difficulty scales its length.")]
        [SerializeField] private float invulnerabilityEnd = 0.26f;

        private HealthComponent health;
        private InputAction lightAttackAction;
        private InputAction heavyAttackAction;
        private InputAction dodgeAction;
        private InputAction guardAction;
        private InputAction abilityAction;
        private InputAction moveAction;

        private ComboTracker combo;
        private Coroutine dodgeRoutine;
        private Coroutine abilityRoutine;
        private float abilityReadyAt;
        private int abilityUseCount;

        public bool IsDodging => dodgeRoutine != null;

        /// <summary>Ember Step is mid-dash. Distinct from <see cref="IsDodging"/> so tests and animation can tell which one is playing.</summary>
        public bool IsUsingAbility => abilityRoutine != null;

        /// <summary>The chain the last swing completed, or null. See <see cref="ComboTracker"/>.</summary>
        public ComboChain CurrentChain => combo?.Current;

        /// <summary>Recent steps inside the combo window, for HUD and tests.</summary>
        public ComboTracker Combo => combo;

        public bool IsGuarding => guard != null && guard.IsBlocking;

        /// <summary>Length of the dodge i-frame window after difficulty scaling.</summary>
        public float ScaledInvulnerabilityDuration =>
            Mathf.Max(0f, invulnerabilityEnd - invulnerabilityStart) * Difficulty.Modifiers.PlayerTimingWindow;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            combo ??= new ComboTracker(ComboChain.DefaultChains(), comboWindow);

            if (weapon == null) { weapon = GetComponentInChildren<WeaponController>(true); }
            if (stamina == null) { stamina = GetComponent<StaminaComponent>(); }
            if (guard == null) { guard = GetComponent<GuardController>(); }
            if (lockOn == null) { lockOn = GetComponent<LockOnController>(); }
            if (divineEnergy == null) { divineEnergy = GetComponent<DivineEnergyComponent>(); }
            if (locomotion == null) { locomotion = GetComponent<Game.Player.PlayerController>(); }

            if (inputActions == null)
            {
                GameLogger.LogError(LogCategory.Combat, "No InputActionAsset assigned to CombatController.", this);
                enabled = false;
                return;
            }

            var gameplayMap = inputActions.FindActionMap("Gameplay");
            if (gameplayMap == null)
            {
                GameLogger.LogError(LogCategory.Combat, "Gameplay action map not found in InputActionAsset.", this);
                enabled = false;
                return;
            }

            lightAttackAction = gameplayMap.FindAction("LightAttack");
            heavyAttackAction = gameplayMap.FindAction("HeavyAttack");
            dodgeAction = gameplayMap.FindAction("Dodge");
            guardAction = gameplayMap.FindAction("Guard");
            abilityAction = gameplayMap.FindAction("Ability");
            moveAction = gameplayMap.FindAction("Move");
        }

        private void OnEnable()
        {
            lightAttackAction?.Enable();
            heavyAttackAction?.Enable();
            dodgeAction?.Enable();
            guardAction?.Enable();
            abilityAction?.Enable();

            if (guard != null)
            {
                guard.Parried += HandleParried;
            }
        }

        private void OnDisable()
        {
            lightAttackAction?.Disable();
            heavyAttackAction?.Disable();
            dodgeAction?.Disable();
            guardAction?.Disable();
            abilityAction?.Disable();

            if (guard != null)
            {
                guard.Parried -= HandleParried;
            }
        }

        private void Update()
        {
            if (guardAction != null)
            {
                if (guardAction.WasPressedThisFrame())
                {
                    TryGuard();
                }
                else if (guardAction.WasReleasedThisFrame())
                {
                    ReleaseGuard();
                }
            }

            if (dodgeAction != null && dodgeAction.WasPressedThisFrame())
            {
                TryDodge();
            }

            if (abilityAction != null && abilityAction.WasPressedThisFrame())
            {
                TryAbility();
            }

            if (lightAttackAction != null && lightAttackAction.WasPressedThisFrame())
            {
                TryAttack(AttackType.Light);
            }

            if (heavyAttackAction != null && heavyAttackAction.WasPressedThisFrame())
            {
                TryAttack(AttackType.Heavy);
            }
        }

        /// <summary>
        /// Attempts an attack. Stamina is only spent once the swing has actually
        /// started, so a rejected input costs nothing. A light attack against an
        /// enemy below the finisher threshold becomes a finisher. Attacking lowers
        /// the guard.
        /// </summary>
        public bool TryAttack(AttackType type)
        {
            if (!CanAct() || weapon == null || weapon.IsSwinging)
            {
                return false;
            }

            HealthComponent finisherTarget = null;
            if (type == AttackType.Light)
            {
                finisherTarget = FindFinisherTarget();
                if (finisherTarget != null)
                {
                    type = AttackType.Finisher;
                }
            }

            var cost = type switch
            {
                AttackType.Heavy => heavyAttackCost,
                AttackType.Finisher => finisherCost,
                _ => lightAttackCost
            };

            if (stamina != null && !stamina.HasStamina(cost))
            {
                GameLogger.Log(LogCategory.Combat, $"{name} lacks stamina for a {type} attack.", this);
                return false;
            }

            var multiplier = 1f;
            ComboChain chain = null;
            if (type != AttackType.Finisher)
            {
                chain = combo.Record(type == AttackType.Heavy ? ComboStep.Heavy : ComboStep.Light,
                    Time.time, weapon.TotalDuration(type));
                multiplier = chain?.DamageMultiplier ?? 1f;
            }

            ReleaseGuard();

            if (finisherTarget != null)
            {
                FaceTarget(finisherTarget.transform);
            }

            if (!weapon.TrySwing(type, multiplier))
            {
                return false;
            }

            stamina?.TrySpend(cost);

            if (chain != null)
            {
                GameLogger.Log(LogCategory.Combat, $"{name} performed {chain.Name} (x{chain.DamageMultiplier:0.##}).", this);
                EventBus.Publish(new ComboPerformedEvent(gameObject, chain.Name, chain.DamageMultiplier));
            }

            if (finisherTarget != null)
            {
                combo.Reset();
                GameLogger.Log(LogCategory.Combat, $"{name} finishes {finisherTarget.name}.", this);
                EventBus.Publish(new FinisherStartedEvent(gameObject, finisherTarget.gameObject));
            }

            return true;
        }

        /// <summary>
        /// Attempts a dodge. Cancels a swing in progress, which is what makes combat
        /// feel responsive rather than animation-locked (SPEC.md section 14).
        /// </summary>
        public bool TryDodge()
        {
            if (!CanAct() || IsDodging)
            {
                return false;
            }

            if (stamina != null && !stamina.TrySpend(dodgeCost))
            {
                return false;
            }

            weapon?.CancelSwing();
            ReleaseGuard();
            combo.Record(ComboStep.Dodge, Time.time, dodgeDuration);
            dodgeRoutine = StartCoroutine(DodgeRoutine());
            return true;
        }

        /// <summary>
        /// Attempts Ember Step (SPEC.md section 8.1): a short fire dash paid for with
        /// divine energy rather than stamina, on its own cooldown so it cannot replace
        /// the dodge. Publishes <see cref="EmberStepUsedEvent"/> so
        /// <see cref="Game.Memory.MemoryManager"/> can apply the ability's cost —
        /// "repeated use temporarily removes minor memories" — without this class
        /// knowing Memory exists.
        /// </summary>
        public bool TryAbility()
        {
            if ((requireAbilityUnlock && (WorldState.Instance == null || !WorldState.Instance.GetFlag("ABILITY_UNLOCKED_" + abilityId)))
                || !CanAct() || IsUsingAbility || Time.time < abilityReadyAt)
            {
                return false;
            }

            if (divineEnergy == null || !divineEnergy.TrySpend(abilityCost))
            {
                return false;
            }

            weapon?.CancelSwing();
            ReleaseGuard();
            combo.Record(ComboStep.Ability, Time.time, abilityDashDuration);
            abilityReadyAt = Time.time + abilityCooldown;
            abilityUseCount++;
            abilityRoutine = StartCoroutine(AbilityRoutine());

            GameLogger.Log(LogCategory.Combat, $"{name} used Ember Step (use #{abilityUseCount}).", this);
            EventBus.Publish(new EmberStepUsedEvent(gameObject, abilityUseCount));
            return true;
        }

        /// <summary>
        /// Raises the guard: a parry window now, a block if held. Refused mid-swing
        /// so a whiffed attack cannot be cancelled into a free parry.
        /// </summary>
        public bool TryGuard()
        {
            if (!CanAct() || guard == null || IsDodging || (weapon != null && weapon.IsSwinging))
            {
                return false;
            }

            return guard.BeginGuard();
        }

        public void ReleaseGuard()
        {
            guard?.EndGuard();
        }

        /// <summary>
        /// Test and tooling seam for supplying input without the Inspector. Must be
        /// called before Awake — build the object inactive, configure, then activate —
        /// because Awake disables this component outright when no input asset is set.
        /// </summary>
        public void Configure(InputActionAsset actions, WeaponController weaponController = null,
            StaminaComponent staminaComponent = null, DivineEnergyComponent divineEnergyComponent = null)
        {
            inputActions = actions;

            if (weaponController != null)
            {
                weapon = weaponController;
            }

            if (staminaComponent != null)
            {
                stamina = staminaComponent;
            }

            if (divineEnergyComponent != null)
            {
                divineEnergy = divineEnergyComponent;
            }
        }

        /// <summary>Replaces the chain table, for tuning and tests. Resets the current chain.</summary>
        public void ConfigureCombos(ComboChain[] chains, float window)
        {
            comboWindow = window;
            combo = new ComboTracker(chains, window);
        }

        /// <summary>Test and tuning seam for Ember Step's cost and cooldown.</summary>
        public void ConfigureAbility(float cost, float cooldown)
        {
            abilityCost = cost;
            abilityCooldown = cooldown;
        }

        /// <summary>Test and content seam for abilities granted by quest rewards.</summary>
        public void ConfigureAbilityUnlock(string id, bool required)
        {
            abilityId = id;
            requireAbilityUnlock = required;
        }
        private bool CanAct()
        {
            if (!isActiveAndEnabled || health == null || health.IsDead)
            {
                return false;
            }

            // A broken guard is a stun: nothing until it passes (SPEC.md section 15,
            // "failed parry: player receives damage" — and loses the initiative).
            return guard == null || !guard.IsGuardBroken;
        }

        private void HandleParried(bool perfect)
        {
            combo.Record(ComboStep.Parry, Time.time);
        }

        /// <summary>
        /// The enemy a light attack would finish: the lock-on target if it qualifies,
        /// otherwise the nearest qualifying hurtbox owner in front and in range.
        /// </summary>
        private HealthComponent FindFinisherTarget()
        {
            if (lockOn != null && lockOn.Target != null && Qualifies(lockOn.Target))
            {
                return lockOn.Target;
            }

            HealthComponent best = null;
            var bestDistance = float.PositiveInfinity;
            foreach (var hurtbox in FindObjectsByType<Hurtbox>())
            {
                var candidate = hurtbox.Health;
                if (candidate == null || candidate == health || hurtbox.Faction == Faction.Player || !Qualifies(candidate))
                {
                    continue;
                }

                var offset = candidate.transform.position - transform.position;
                offset.y = 0f;
                var distance = offset.magnitude;
                if (distance < bestDistance && Vector3.Dot(transform.forward, offset.normalized) > 0.3f)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        private bool Qualifies(HealthComponent candidate)
        {
            if (candidate.IsDead || candidate.HealthFraction > finisherHealthThreshold)
            {
                return false;
            }

            var offset = candidate.transform.position - transform.position;
            offset.y = 0f;
            return offset.magnitude <= finisherRange;
        }

        private void FaceTarget(Transform target)
        {
            var toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(toTarget, Vector3.up);
            }
        }

        private IEnumerator DodgeRoutine()
        {
            var input = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            var direction = input.sqrMagnitude > 0.0001f
                ? transform.TransformDirection(new Vector3(input.x, 0f, input.y)).normalized
                : -transform.forward;

            locomotion?.BeginDodge(direction, dodgeSpeed, dodgeDuration);

            // The window's length, not its start, is what difficulty scales: Story
            // gives more forgiving i-frames, Mythic fewer (SPEC.md sections 15, 44).
            var windowEnd = invulnerabilityStart + ScaledInvulnerabilityDuration;
            var elapsed = 0f;

            // Two one-shot flags rather than one toggle: the window opens once and
            // closes once, so re-entering the "start" condition after it closed
            // cannot make the player invulnerable again for the rest of the dodge.
            var windowOpened = false;
            var windowClosed = false;

            while (elapsed < dodgeDuration)
            {
                if (!windowOpened && elapsed >= invulnerabilityStart)
                {
                    windowOpened = true;
                    health.IsInvulnerable = true;
                }
                else if (windowOpened && !windowClosed && elapsed >= windowEnd)
                {
                    windowClosed = true;
                    if (!health.IsDead)
                    {
                        health.IsInvulnerable = false;
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!health.IsDead)
            {
                health.IsInvulnerable = false;
            }

            dodgeRoutine = null;
        }

        /// <summary>Ember Step's dash. Shorter and faster than a dodge, always forward — it is a fire dash, not an evade.</summary>
        private IEnumerator AbilityRoutine()
        {
            locomotion?.BeginDodge(transform.forward, abilityDashSpeed, abilityDashDuration);

            var windowEnd = abilityInvulnerabilityStart
                + Mathf.Max(0f, abilityInvulnerabilityEnd - abilityInvulnerabilityStart) * Difficulty.Modifiers.PlayerTimingWindow;
            var elapsed = 0f;
            var windowOpened = false;
            var windowClosed = false;

            while (elapsed < abilityDashDuration)
            {
                if (!windowOpened && elapsed >= abilityInvulnerabilityStart)
                {
                    windowOpened = true;
                    health.IsInvulnerable = true;
                }
                else if (windowOpened && !windowClosed && elapsed >= windowEnd)
                {
                    windowClosed = true;
                    if (!health.IsDead)
                    {
                        health.IsInvulnerable = false;
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!health.IsDead)
            {
                health.IsInvulnerable = false;
            }

            abilityRoutine = null;
        }
    }
}

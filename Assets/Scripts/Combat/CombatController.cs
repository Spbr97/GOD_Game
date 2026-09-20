using System.Collections;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Combat
{
    /// <summary>
    /// Turns player input into attacks and dodges (SPEC.md sections 13-15, TASK 002).
    /// It owns the rules — can I act, can I pay for it, does this extend a combo —
    /// and delegates the swing itself to <see cref="WeaponController"/>.
    ///
    /// Scope note: TASK 002 implements light, heavy and dodge. Block, parry, divine
    /// ability, finisher and lock-on from SPEC.md section 13 are not implemented yet.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public class CombatController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private WeaponController weapon;
        [SerializeField] private StaminaComponent stamina;
        [SerializeField] private Game.Player.PlayerController locomotion;

        [Header("Stamina costs")]
        [SerializeField] private float lightAttackCost = 12f;
        [SerializeField] private float heavyAttackCost = 25f;
        [SerializeField] private float dodgeCost = 20f;

        [Header("Combo")]
        [Tooltip("How long after a swing a follow-up still counts as part of the chain.")]
        [SerializeField] private float comboWindow = 0.7f;

        [Tooltip("Longest chain before the counter resets. SPEC.md section 14 asks for three light hits.")]
        [SerializeField] private int maxComboLength = 3;

        [Header("Dodge")]
        [SerializeField] private float dodgeDuration = 0.35f;
        [SerializeField] private float dodgeSpeed = 9f;
        [Tooltip("Seconds into the dodge when invulnerability starts.")]
        [SerializeField] private float invulnerabilityStart = 0.05f;
        [Tooltip("Seconds into the dodge when invulnerability ends. SPEC.md section 15 asks for a short window.")]
        [SerializeField] private float invulnerabilityEnd = 0.26f;

        private HealthComponent health;
        private InputAction lightAttackAction;
        private InputAction heavyAttackAction;
        private InputAction dodgeAction;
        private InputAction moveAction;

        private int comboIndex;
        private float comboExpiresAt;
        private Coroutine dodgeRoutine;

        public bool IsDodging => dodgeRoutine != null;
        public int ComboIndex => comboIndex;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();

            if (weapon == null)
            {
                weapon = GetComponentInChildren<WeaponController>(true);
            }

            if (stamina == null)
            {
                stamina = GetComponent<StaminaComponent>();
            }

            if (locomotion == null)
            {
                locomotion = GetComponent<Game.Player.PlayerController>();
            }

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
            moveAction = gameplayMap.FindAction("Move");
        }

        private void OnEnable()
        {
            lightAttackAction?.Enable();
            heavyAttackAction?.Enable();
            dodgeAction?.Enable();
        }

        private void OnDisable()
        {
            lightAttackAction?.Disable();
            heavyAttackAction?.Disable();
            dodgeAction?.Disable();
        }

        private void Update()
        {
            if (Time.time > comboExpiresAt)
            {
                comboIndex = 0;
            }

            if (dodgeAction != null && dodgeAction.WasPressedThisFrame())
            {
                TryDodge();
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
        /// started, so a rejected input costs nothing.
        /// </summary>
        public bool TryAttack(AttackType type)
        {
            if (!CanAct() || weapon == null || weapon.IsSwinging)
            {
                return false;
            }

            var cost = type == AttackType.Heavy ? heavyAttackCost : lightAttackCost;
            if (stamina != null && !stamina.HasStamina(cost))
            {
                GameLogger.Log(LogCategory.Combat, $"{name} lacks stamina for a {type} attack.", this);
                return false;
            }

            if (!weapon.TrySwing(type, comboIndex))
            {
                return false;
            }

            stamina?.TrySpend(cost);

            comboIndex = (comboIndex + 1) % Mathf.Max(1, maxComboLength);
            comboExpiresAt = Time.time + weapon.TotalDuration(type) + comboWindow;
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
            dodgeRoutine = StartCoroutine(DodgeRoutine());
            return true;
        }

        /// <summary>
        /// Test and tooling seam for supplying input without the Inspector. Must be
        /// called before Awake — build the object inactive, configure, then activate —
        /// because Awake disables this component outright when no input asset is set.
        /// </summary>
        public void Configure(InputActionAsset actions, WeaponController weaponController = null,
            StaminaComponent staminaComponent = null)
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
        }

        private bool CanAct()
        {
            return isActiveAndEnabled && health != null && !health.IsDead;
        }

        private IEnumerator DodgeRoutine()
        {
            var input = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            var direction = input.sqrMagnitude > 0.0001f
                ? transform.TransformDirection(new Vector3(input.x, 0f, input.y)).normalized
                : -transform.forward;

            locomotion?.BeginDodge(direction, dodgeSpeed, dodgeDuration);

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
                else if (windowOpened && !windowClosed && elapsed >= invulnerabilityEnd)
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
    }
}

using Game.Combat;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Third-person orbit/follow camera with collision avoidance so it does not
    /// clip through geometry (SPEC.md section 38).
    /// </summary>
    public class PlayerCamera : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 4.5f;
        [SerializeField] private float minDistance = 0.8f;
        [SerializeField] private float height = 1.6f;
        [SerializeField] private float sensitivity = 0.12f;
        [SerializeField] private float minPitch = -30f;
        [SerializeField] private float maxPitch = 60f;
        [SerializeField] private LayerMask collisionMask = ~0;

        [Tooltip("Degrees per second at full stick deflection (SPEC.md section 43/KNOWN_ISSUES: mouse delta is per-frame pixels, a gamepad stick is a -1..1 rate, so the two cannot share one raw multiplier).")]
        [SerializeField] private float gamepadLookDegreesPerSecond = 220f;

        [Header("Lock-on")]
        [Tooltip("How quickly the camera swings to frame a lock-on target, in degrees per second.")]
        [SerializeField] private float lockOnTurnSpeed = 360f;

        [Tooltip("Pitch used while locked on, so the target and the player both stay in frame.")]
        [SerializeField] private float lockOnPitch = 18f;

        [Header("Shake (SPEC.md section 43's camera shake toggle)")]
        [SerializeField] private float perfectParryShakeDuration = 0.15f;
        [SerializeField] private float perfectParryShakeMagnitude = 0.12f;
        [SerializeField] private float guardBrokenShakeDuration = 0.25f;
        [SerializeField] private float guardBrokenShakeMagnitude = 0.2f;

        private InputAction lookAction;
        private float yaw;
        private float pitch = 10f;
        private float shakeTimeRemaining;
        private float shakeDuration;
        private float shakeMagnitude;
        private Vector3 shakeOffset;

        /// <summary>
        /// When set, the camera frames this instead of following the look input. Set
        /// by Game.Combat.LockOnController; releasing it hands control back to the
        /// stick without a jump because yaw and pitch were kept current all along.
        /// </summary>
        public Transform LookTarget { get; set; }

        private void Awake()
        {
            if (target == null)
            {
                GameLogger.LogError(LogCategory.Player, "PlayerCamera has no target assigned.", this);
                enabled = false;
                return;
            }

            if (inputActions == null)
            {
                GameLogger.LogError(LogCategory.Player, "No InputActionAsset assigned to PlayerCamera.", this);
                enabled = false;
                return;
            }

            var gameplayMap = inputActions.FindActionMap("Gameplay");
            lookAction = gameplayMap?.FindAction("Look");
        }

        private void OnEnable()
        {
            lookAction?.Enable();
            EventBus.Subscribe<ParryEvent>(OnParry);
            EventBus.Subscribe<GuardBrokenEvent>(OnGuardBroken);
        }

        private void OnDisable()
        {
            lookAction?.Disable();
            EventBus.Unsubscribe<ParryEvent>(OnParry);
            EventBus.Unsubscribe<GuardBrokenEvent>(OnGuardBroken);
        }

        /// <summary>
        /// A brief positional kick, decaying to zero over <paramref name="duration"/>.
        /// No-ops when the player has turned camera shake off (SPEC.md section 43).
        /// </summary>
        public void Shake(float duration, float magnitude)
        {
            if (SettingsManager.Instance != null && !SettingsManager.Instance.Current.CameraShakeEnabled)
            {
                return;
            }

            if (duration <= 0f || magnitude <= 0f)
            {
                return;
            }

            shakeDuration = duration;
            shakeTimeRemaining = duration;
            shakeMagnitude = magnitude;
        }

        private void OnParry(ParryEvent parry)
        {
            if (parry.Perfect)
            {
                Shake(perfectParryShakeDuration, perfectParryShakeMagnitude);
            }
        }

        private void OnGuardBroken(GuardBrokenEvent broken)
        {
            Shake(guardBrokenShakeDuration, guardBrokenShakeMagnitude);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var look = lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
            var sensitivityScale = SettingsManager.Instance != null ? SettingsManager.Instance.Current.MouseSensitivity : 1f;
            var invertY = SettingsManager.Instance != null && SettingsManager.Instance.Current.InvertY ? -1f : 1f;

            if (LookTarget != null)
            {
                var toTarget = LookTarget.position - target.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    var targetYaw = Quaternion.LookRotation(toTarget, Vector3.up).eulerAngles.y;
                    var step = lockOnTurnSpeed * Time.deltaTime;
                    yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, step);
                    pitch = Mathf.MoveTowards(pitch, lockOnPitch, step);
                }
            }
            else
            {
                // Mouse delta is per-frame pixels; a gamepad stick is a -1..1 rate that
                // needs multiplying by deltaTime to mean "degrees per second" instead of
                // "degrees per frame" (KNOWN_ISSUES: gamepad look was far too slow).
                var usingGamepad = lookAction != null && lookAction.activeControl?.device is Gamepad;
                var effectiveLook = usingGamepad
                    ? look * gamepadLookDegreesPerSecond * Time.deltaTime
                    : look * sensitivity;

                yaw += effectiveLook.x * sensitivityScale;
                pitch -= effectiveLook.y * sensitivityScale * invertY;
            }

            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            var pivot = target.position + Vector3.up * height;
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var desiredPosition = pivot - rotation * Vector3.forward * distance;

            var clampedDistance = distance;
            if (Physics.Linecast(pivot, desiredPosition, out var hit, collisionMask, QueryTriggerInteraction.Ignore))
            {
                clampedDistance = Mathf.Clamp(Vector3.Distance(pivot, hit.point) - 0.1f, minDistance, distance);
            }

            shakeOffset = Vector3.zero;
            if (shakeTimeRemaining > 0f)
            {
                shakeTimeRemaining -= Time.deltaTime;
                var strength = shakeDuration > 0f ? Mathf.Clamp01(shakeTimeRemaining / shakeDuration) : 0f;
                shakeOffset = Random.insideUnitSphere * (shakeMagnitude * strength);
            }

            transform.position = pivot - rotation * Vector3.forward * clampedDistance + shakeOffset;
            transform.rotation = rotation;
        }
    }
}

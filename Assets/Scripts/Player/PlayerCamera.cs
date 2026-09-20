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

        [Header("Lock-on")]
        [Tooltip("How quickly the camera swings to frame a lock-on target, in degrees per second.")]
        [SerializeField] private float lockOnTurnSpeed = 360f;

        [Tooltip("Pitch used while locked on, so the target and the player both stay in frame.")]
        [SerializeField] private float lockOnPitch = 18f;

        private InputAction lookAction;
        private float yaw;
        private float pitch = 10f;

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
        }

        private void OnDisable()
        {
            lookAction?.Disable();
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
                yaw += look.x * sensitivity * sensitivityScale;
                pitch -= look.y * sensitivity * sensitivityScale * invertY;
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

            transform.position = pivot - rotation * Vector3.forward * clampedDistance;
            transform.rotation = rotation;
        }
    }
}

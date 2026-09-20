using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Basic third-person locomotion (SPEC.md TASK 001). Reads actions by name
    /// from a serialized InputActionAsset rather than a generated wrapper class,
    /// since no Editor code-gen step ran when this was written.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float sprintSpeed = 7f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float rotationSpeed = 12f;

        private CharacterController controller;
        private InputAction moveAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private Transform cameraTransform;
        private Vector3 verticalVelocity;

        private Vector3 dodgeDirection;
        private float dodgeSpeed;
        private float dodgeEndsAt;

        /// <summary>True while a dodge impulse is overriding normal movement input.</summary>
        public bool IsDodging => Time.time < dodgeEndsAt;

        /// <summary>
        /// When set, the player faces this instead of their movement direction, so
        /// movement becomes strafing. Set by Game.Combat.LockOnController; this class
        /// only knows it has something to face, not why.
        /// </summary>
        public Transform FacingTarget { get; set; }

        /// <summary>The camera the player's movement is relative to. Assigned from Camera.main when unset.</summary>
        public Transform CameraTransform
        {
            get => cameraTransform;
            set => cameraTransform = value;
        }

        /// <summary>
        /// Takes over horizontal movement for the duration of a dodge. Called by
        /// Game.Combat.CombatController, which owns the stamina cost and the
        /// invulnerability window; this class only supplies the motion so that all
        /// CharacterController.Move calls stay in one place.
        /// </summary>
        public void BeginDodge(Vector3 direction, float speed, float duration)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = transform.forward;
            }

            dodgeDirection = direction.normalized;
            dodgeSpeed = speed;
            dodgeEndsAt = Time.time + duration;
        }

        public void CancelDodge()
        {
            dodgeEndsAt = 0f;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (inputActions == null)
            {
                GameLogger.LogError(LogCategory.Player, "No InputActionAsset assigned to PlayerController.", this);
                enabled = false;
                return;
            }

            var gameplayMap = inputActions.FindActionMap("Gameplay");
            if (gameplayMap == null)
            {
                GameLogger.LogError(LogCategory.Player, "Gameplay action map not found in InputActionAsset.", this);
                enabled = false;
                return;
            }

            moveAction = gameplayMap.FindAction("Move");
            jumpAction = gameplayMap.FindAction("Jump");
            sprintAction = gameplayMap.FindAction("Sprint");

            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            else
            {
                GameLogger.LogWarning(LogCategory.Player, "No main camera found at startup; movement will use world-space axes until one is found.", this);
            }
        }

        private void OnEnable()
        {
            moveAction?.Enable();
            jumpAction?.Enable();
            sprintAction?.Enable();
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            jumpAction?.Disable();
            sprintAction?.Disable();
        }

        private void Update()
        {
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            HandleMovement();
        }

        private void HandleMovement()
        {
            var input = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            var isSprinting = sprintAction != null && sprintAction.IsPressed();
            var speed = isSprinting ? sprintSpeed : walkSpeed;

            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDirection = forward * input.y + right * input.x;

            var dodging = IsDodging;
            if (dodging)
            {
                moveDirection = dodgeDirection;
                speed = dodgeSpeed;
            }

            if (controller.isGrounded && verticalVelocity.y < 0f)
            {
                verticalVelocity.y = -2f;
            }

            if (!dodging && controller.isGrounded && jumpAction != null && jumpAction.WasPressedThisFrame())
            {
                verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity.y += gravity * Time.deltaTime;

            var motion = moveDirection * speed + verticalVelocity;
            controller.Move(motion * Time.deltaTime);

            // Facing is held during a dodge: a backstep should not spin the player
            // around to look at the direction they are travelling.
            if (dodging)
            {
                return;
            }

            Vector3 facing;
            if (FacingTarget != null)
            {
                facing = FacingTarget.position - transform.position;
                facing.y = 0f;
            }
            else
            {
                facing = moveDirection;
            }

            if (facing.sqrMagnitude > 0.0001f)
            {
                var targetRotation = Quaternion.LookRotation(facing, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }
}

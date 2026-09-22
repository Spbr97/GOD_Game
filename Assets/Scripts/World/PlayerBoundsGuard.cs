using Game.Combat;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// SPEC.md section 50's "Invalid player position — teleport player to latest valid
    /// checkpoint", section 54's edge case 10 (player falls outside the world) and
    /// section 56's out-of-bounds traversal.
    ///
    /// Recovery, not death: falling through a hole in the floor is a bug in the level,
    /// and section 55 asks that no player action permanently block the story. Killing
    /// the player would also cost them whatever death costs, for something they did
    /// not do. So this restores position and lets them carry on.
    ///
    /// The checkpoint is preferred because the spec names it, but a scene may not have
    /// one yet, so the last position the player stood on solid ground is kept as a
    /// second fallback — closer to where they were, and always available.
    /// </summary>
    public class PlayerBoundsGuard : MonoBehaviour
    {
        [Tooltip("Seconds between checks. Falling out of the world does not need frame accuracy, and a per-frame check on every enemy would not be free.")]
        [SerializeField] private float checkInterval = 0.25f;

        [Tooltip("Ignore further recoveries for this long after one, so a checkpoint that is itself out of bounds cannot loop.")]
        [SerializeField] private float recoveryCooldown = 1f;

        [Tooltip("Shown by RecoveryMessageUI. SPEC.md section 50 does not dictate the wording, only that the position is corrected.")]
        [SerializeField] private string recoveryMessage = "You fell out of the world. Returned to the last checkpoint.";

        private CharacterController characterController;
        private Game.Player.PlayerController playerController;
        private float nextCheckAt;
        private float recoveryAllowedAt;
        private Vector3 lastGroundedPosition;
        private Quaternion lastGroundedRotation;
        private bool hasGroundedPosition;

        /// <summary>How many times this guard has pulled the player back. Read by tests and the debug overlay.</summary>
        public int RecoveryCount { get; private set; }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            playerController = GetComponent<Game.Player.PlayerController>();
        }

        private void Start()
        {
            CaptureGroundedPosition();
        }

        private void Update()
        {
            if (Time.time < nextCheckAt)
            {
                return;
            }

            nextCheckAt = Time.time + checkInterval;

            if (WorldBounds.IsInsideWorld(transform.position))
            {
                CaptureGroundedPosition();
                return;
            }

            if (Time.time < recoveryAllowedAt)
            {
                return;
            }

            Recover();
        }

        /// <summary>
        /// Remembers somewhere valid to fall back to. Only while grounded: a position
        /// captured mid-jump over a pit would drop the player straight back into it.
        /// </summary>
        private void CaptureGroundedPosition()
        {
            var grounded = characterController == null || characterController.isGrounded;
            if (!grounded)
            {
                return;
            }

            lastGroundedPosition = transform.position;
            lastGroundedRotation = transform.rotation;
            hasGroundedPosition = true;
        }

        /// <summary>Puts the player back. Public so a test and the debug console can force it.</summary>
        public void Recover()
        {
            var fellFrom = transform.position;

            if (!TryFindRecoveryPoint(out var position, out var rotation))
            {
                GameLogger.LogFallback(
                    LogCategory.Player,
                    "the player left the world and could not be put back",
                    "PlayerBoundsGuard.Recover",
                    "no checkpoint is active and no grounded position was ever captured",
                    "the player is left where they are; expect a continuing fall",
                    this);
                return;
            }

            Teleport(position, rotation);

            RecoveryCount++;
            recoveryAllowedAt = Time.time + recoveryCooldown;

            GameLogger.LogFallback(
                LogCategory.Player,
                "the player left the world",
                "PlayerBoundsGuard.Recover",
                $"position {fellFrom} is outside the scene's WorldBounds",
                $"teleported to {position}",
                this);

            EventBus.Publish(new OutOfWorldRecoveryEvent(gameObject, fellFrom, position, true, recoveryMessage));
        }

        private bool TryFindRecoveryPoint(out Vector3 position, out Quaternion rotation)
        {
            var checkpoints = CheckpointManager.Instance;
            if (checkpoints != null && checkpoints.TryGetRespawn(out position, out rotation, out _)
                && WorldBounds.IsInsideWorld(position))
            {
                return true;
            }

            if (hasGroundedPosition && WorldBounds.IsInsideWorld(lastGroundedPosition))
            {
                position = lastGroundedPosition;
                rotation = lastGroundedRotation;
                return true;
            }

            position = transform.position;
            rotation = transform.rotation;
            return false;
        }

        /// <summary>
        /// The same disable-move-enable dance <see cref="PlayerDeath"/> does — a
        /// CharacterController caches its own position and would otherwise drag the
        /// player back over the edge — plus clearing the fall speed, which by now is
        /// large enough to punch straight back through the floor on the next frame.
        /// </summary>
        private void Teleport(Vector3 position, Quaternion rotation)
        {
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            transform.SetPositionAndRotation(position, rotation);

            if (characterController != null)
            {
                characterController.enabled = true;
            }

            playerController?.CancelVerticalVelocity();
        }

        /// <summary>Test seam: sets the fallback without waiting for a grounded frame.</summary>
        public void ConfigureFallback(Vector3 position, Quaternion rotation)
        {
            lastGroundedPosition = position;
            lastGroundedRotation = rotation;
            hasGroundedPosition = true;
        }
    }
}

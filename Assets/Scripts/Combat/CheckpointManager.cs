using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Tracks which checkpoint the player respawns at (SPEC.md section 33).
    ///
    /// Not named in the TASK 002 component list, but the listed <see cref="Checkpoint"/>
    /// and <see cref="PlayerDeath"/> both need somewhere neutral to agree on "the
    /// current respawn point" — putting it in either of them would make one depend
    /// on the other.
    ///
    /// Nothing is persisted yet; the active checkpoint is lost on scene reload. That
    /// belongs with the save system (SPEC.md section 32).
    /// </summary>
    public class CheckpointManager : MonoBehaviour
    {
        public static CheckpointManager Instance { get; private set; }

        [Tooltip("Used before any checkpoint has been activated. Falls back to the player's start position.")]
        [SerializeField] private Transform defaultSpawnPoint;

        private Vector3 fallbackPosition;
        private Quaternion fallbackRotation;
        private bool fallbackCaptured;

        public Checkpoint ActiveCheckpoint { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // The component, not the GameObject: see GameManager.Awake for why.
                Destroy(this);
                return;
            }

            Instance = this;

            if (defaultSpawnPoint != null)
            {
                fallbackPosition = defaultSpawnPoint.position;
                fallbackRotation = defaultSpawnPoint.rotation;
                fallbackCaptured = true;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SetActiveCheckpoint(Checkpoint checkpoint)
        {
            if (checkpoint == null || ActiveCheckpoint == checkpoint)
            {
                return;
            }

            ActiveCheckpoint = checkpoint;
        }

        /// <summary>
        /// Finds the checkpoint a save named and makes it current, for
        /// <see cref="Game.Save.SaveManager.Apply"/> (SPEC.md TASK 009). Looks it up by
        /// id rather than storing a direct reference, because a save is data and must
        /// survive the referenced object not existing in whatever build loads it later
        /// (SPEC.md section 54, edge case 16). A checkpoint no longer in this build is
        /// logged and the player keeps the default spawn instead of the load failing.
        /// </summary>
        public void RestoreActiveCheckpoint(string checkpointId)
        {
            if (string.IsNullOrEmpty(checkpointId))
            {
                return;
            }

            foreach (var checkpoint in FindObjectsByType<Checkpoint>(FindObjectsInactive.Include))
            {
                if (checkpoint.CheckpointId == checkpointId)
                {
                    checkpoint.RestoreActivated();
                    SetActiveCheckpoint(checkpoint);
                    return;
                }
            }

            GameLogger.LogFallback(
                LogCategory.Game,
                $"Could not restore checkpoint '{checkpointId}' from the save",
                "CheckpointManager.RestoreActiveCheckpoint",
                "no checkpoint with that id exists in this scene",
                "the player keeps the default spawn instead",
                this);
        }

        /// <summary>
        /// Records where the player began, so the first death before any checkpoint
        /// still has somewhere to send them.
        /// </summary>
        public void CaptureFallback(Transform player)
        {
            if (fallbackCaptured || player == null)
            {
                return;
            }

            fallbackPosition = player.position;
            fallbackRotation = player.rotation;
            fallbackCaptured = true;
        }

        public bool TryGetRespawn(out Vector3 position, out Quaternion rotation, out bool restore)
        {
            if (ActiveCheckpoint != null)
            {
                position = ActiveCheckpoint.RespawnPosition;
                rotation = ActiveCheckpoint.RespawnRotation;
                restore = ActiveCheckpoint.RestoreOnActivation;
                return true;
            }

            if (fallbackCaptured)
            {
                position = fallbackPosition;
                rotation = fallbackRotation;
                restore = true;
                return true;
            }

            GameLogger.LogFallback(
                LogCategory.Game,
                "No respawn point available",
                "CheckpointManager.TryGetRespawn",
                "no checkpoint activated and no start position captured",
                "the player is revived where they died",
                this);

            position = Vector3.zero;
            rotation = Quaternion.identity;
            restore = true;
            return false;
        }
    }
}

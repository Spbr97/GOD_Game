using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// A respawn point the player activates by walking into it (SPEC.md section 33, TASK 002).
    ///
    /// Activation is idempotent: the second and later activations register the
    /// checkpoint as current but raise no event and grant nothing, so nothing that
    /// hangs off <see cref="CheckpointActivatedEvent"/> can be collected twice.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        [SerializeField] private string checkpointId;
        [SerializeField] private Transform respawnPoint;

        [Tooltip("Only colliders on these layers can activate the checkpoint.")]
        [SerializeField] private LayerMask activatorLayers = ~0;

        [Tooltip("Refill the player's health and stamina on activation.")]
        [SerializeField] private bool restoreOnActivation = true;

        public string CheckpointId => string.IsNullOrEmpty(checkpointId) ? name : checkpointId;
        public bool HasBeenActivated { get; private set; }
        public bool RestoreOnActivation => restoreOnActivation;

        public Vector3 RespawnPosition => respawnPoint != null ? respawnPoint.position : transform.position;
        public Quaternion RespawnRotation => respawnPoint != null ? respawnPoint.rotation : transform.rotation;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if ((activatorLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            if (other.GetComponentInParent<PlayerDeath>() == null)
            {
                return;
            }

            Activate();
        }

        /// <summary>
        /// Marks this checkpoint as the current respawn point. Safe to call repeatedly;
        /// only the first call has side effects beyond becoming current.
        /// </summary>
        public void Activate()
        {
            CheckpointManager.Instance?.SetActiveCheckpoint(this);

            if (HasBeenActivated)
            {
                return;
            }

            HasBeenActivated = true;
            GameLogger.Log(LogCategory.Game, $"Checkpoint '{CheckpointId}' activated.", this);
            EventBus.Publish(new CheckpointActivatedEvent(this));
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = HasBeenActivated ? Color.cyan : new Color(1f, 0.85f, 0.3f);
            Gizmos.DrawWireSphere(RespawnPosition, 0.5f);
            Gizmos.DrawLine(RespawnPosition, RespawnPosition + Vector3.up * 2f);
        }
    }
}

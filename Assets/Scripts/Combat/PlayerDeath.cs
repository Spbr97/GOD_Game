using System.Collections;
using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Player-side reaction to death and the respawn that follows (SPEC.md TASK 002).
    /// Also marks the object as the player for <see cref="Checkpoint"/> trigger checks.
    ///
    /// Respawn restores the player at the current checkpoint without rolling back
    /// quests or memories. Encounter listeners reset surviving enemies and live bosses.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public class PlayerDeath : MonoBehaviour
    {
        [Tooltip("Seconds between dying and respawning. Stands in for a death animation (SPEC.md section 78).")]
        [SerializeField] private float respawnDelay = 2f;

        [SerializeField] private Behaviour[] disableWhileDead;

        private HealthComponent health;
        private StaminaComponent stamina;
        private CharacterController characterController;
        private Coroutine respawnRoutine;

        public bool IsDead => health != null && health.IsDead;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            stamina = GetComponent<StaminaComponent>();
            characterController = GetComponent<CharacterController>();

            if (disableWhileDead == null || disableWhileDead.Length == 0)
            {
                disableWhileDead = new Behaviour[]
                {
                    GetComponent<Game.Player.PlayerController>(),
                    GetComponent<CombatController>(),
                    GetComponent<GuardController>(),
                    GetComponent<LockOnController>()
                };
            }
        }

        private void OnEnable()
        {
            health.Died += HandleDied;
        }

        private void OnDisable()
        {
            health.Died -= HandleDied;
        }

        private void Start()
        {
            CheckpointManager.Instance?.CaptureFallback(transform);
        }

        private void HandleDied(DamageData killingBlow)
        {
            GameLogger.Log(LogCategory.Player, "Player died.", this);
            EventBus.Publish(new PlayerDiedEvent(gameObject));

            SetControlsEnabled(false);

            if (respawnRoutine == null)
            {
                respawnRoutine = StartCoroutine(RespawnRoutine());
            }
        }

        /// <summary>Respawns immediately, skipping the delay. Used by tests and debug tooling.</summary>
        public void RespawnNow()
        {
            if (respawnRoutine != null)
            {
                StopCoroutine(respawnRoutine);
                respawnRoutine = null;
            }

            Respawn();
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(respawnDelay);
            respawnRoutine = null;
            Respawn();
        }

        private void Respawn()
        {
            var manager = CheckpointManager.Instance;
            if (manager != null && manager.TryGetRespawn(out var position, out var rotation, out _))
            {
                Teleport(position, rotation);
            }
            else
            {
                GameLogger.LogWarning(LogCategory.Player, "Respawning in place; no checkpoint is available.", this);
            }

            health.ResetHealth();
            stamina?.ResetStamina();
            SetControlsEnabled(true);

            GameLogger.Log(LogCategory.Player, $"Player respawned at {transform.position}.", this);
            EventBus.Publish(new PlayerRespawnedEvent(gameObject, transform.position));
        }

        /// <summary>
        /// Moves the player. The CharacterController is disabled across the move
        /// because it caches its own position and would otherwise drag the player
        /// back to where they died.
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
        }

        private void SetControlsEnabled(bool value)
        {
            foreach (var behaviour in disableWhileDead)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = value;
                }
            }
        }
    }
}

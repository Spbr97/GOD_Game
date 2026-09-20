using System.Collections;
using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Enemy-side reaction to the shared <see cref="HealthComponent"/> (SPEC.md TASK 002):
    /// hit flash, then death cleanup and despawn.
    ///
    /// The health pool itself is deliberately not duplicated here. This component only
    /// reacts, which keeps enemies and the player on one damage path.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public class EnemyHealth : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderersToFlash;
        [SerializeField] private Color hitFlashColor = new(1f, 0.35f, 0.25f);
        [SerializeField] private float hitFlashDuration = 0.1f;

        [Header("Death")]
        [Tooltip("Seconds between dying and despawning. Placeholder stand-in for a death animation (SPEC.md section 78).")]
        [SerializeField] private float despawnDelay = 2f;

        [SerializeField] private bool despawnOnDeath = true;

        private HealthComponent health;
        private Color[] originalColours;
        private Coroutine flashRoutine;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();

            if (renderersToFlash == null || renderersToFlash.Length == 0)
            {
                renderersToFlash = GetComponentsInChildren<Renderer>();
            }

            originalColours = new Color[renderersToFlash.Length];
            for (var i = 0; i < renderersToFlash.Length; i++)
            {
                originalColours[i] = renderersToFlash[i].material.color;
            }
        }

        private void OnEnable()
        {
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
        }

        private void OnDisable()
        {
            health.Damaged -= HandleDamaged;
            health.Died -= HandleDied;
        }

        private void HandleDamaged(DamageData damage)
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(FlashRoutine());
        }

        private void HandleDied(DamageData killingBlow)
        {
            GameLogger.Log(LogCategory.Combat, $"Enemy {name} defeated by {killingBlow.Source?.name ?? "unknown"}.", this);

            // Stop the corpse blocking movement or absorbing further swings.
            foreach (var hurtbox in GetComponentsInChildren<Hurtbox>())
            {
                hurtbox.enabled = false;
            }

            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }

            foreach (var hitbox in GetComponentsInChildren<Hitbox>())
            {
                hitbox.Deactivate();
            }

            if (despawnOnDeath)
            {
                StartCoroutine(DespawnRoutine());
            }
        }

        private IEnumerator FlashRoutine()
        {
            SetColour(hitFlashColor, useOriginal: false);
            yield return new WaitForSeconds(hitFlashDuration);
            SetColour(default, useOriginal: true);
            flashRoutine = null;
        }

        private void SetColour(Color colour, bool useOriginal)
        {
            for (var i = 0; i < renderersToFlash.Length; i++)
            {
                if (renderersToFlash[i] == null)
                {
                    continue;
                }

                renderersToFlash[i].material.color = useOriginal ? originalColours[i] : colour;
            }
        }

        private IEnumerator DespawnRoutine()
        {
            yield return new WaitForSeconds(despawnDelay);
            Destroy(gameObject);
        }
    }
}

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

        [Tooltip("Optional. If set, this enemy's death survives a save (SPEC.md TASK 009) — a load will not resurrect it.")]
        [SerializeField] private SaveIdentity identity;

        private HealthComponent health;
        private Color[] originalColours;
        private Coroutine flashRoutine;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();

            if (identity == null)
            {
                identity = GetComponent<SaveIdentity>();
            }

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

            if (identity == null)
            {
                return;
            }

            EventBus.Subscribe<WorldFlagChangedEvent>(OnWorldFlagChanged);

            // Covers the case where the flag was already set before this enemy's
            // OnEnable ran, e.g. a test that restores a save before spawning the scene.
            // The ordinary case — a save applied by this scene's own SaveManager.Start()
            // — fires after every object's OnEnable, so the subscription above catches it.
            if (WorldObjectState.IsMarked(WorldObjectState.DeadFlag(identity.Id)))
            {
                ApplyRestoredDeath();
            }
        }

        private void OnDisable()
        {
            health.Damaged -= HandleDamaged;
            health.Died -= HandleDied;

            if (identity != null)
            {
                EventBus.Unsubscribe<WorldFlagChangedEvent>(OnWorldFlagChanged);
            }
        }

        private void OnWorldFlagChanged(WorldFlagChangedEvent changed)
        {
            if (changed.Value && identity != null && changed.Flag == WorldObjectState.DeadFlag(identity.Id))
            {
                ApplyRestoredDeath();
            }
        }

        /// <summary>
        /// Applies a remembered death from a save directly, without going through
        /// <see cref="HealthComponent.Kill"/> — that would fire <c>Died</c> again and
        /// re-report anything hanging off it (a quest kill, a dropped item), which the
        /// save already restored by itself. No flash, no despawn delay: there is no
        /// moment of death to react to, only a fact to apply.
        ///
        /// Guarded by <c>health.IsDead</c> because <see cref="HandleDied"/> marking the
        /// flag publishes the very event this method listens for — an ordinary combat
        /// kill would otherwise immediately re-trigger on itself and skip the corpse's
        /// despawn delay. By the time <c>Kill</c> raises <c>Died</c>, <c>IsDead</c> is
        /// already true, so that case is told apart from a genuine restore, where the
        /// enemy is still alive the moment the flag turns true.
        /// </summary>
        private void ApplyRestoredDeath()
        {
            if (health.IsDead)
            {
                return;
            }

            health.RestoreTo(0f, dead: true);
            DisableCombatParts();
            Destroy(gameObject);
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

            DisableCombatParts();

            if (identity != null)
            {
                WorldObjectState.Mark(WorldObjectState.DeadFlag(identity.Id));
            }

            if (despawnOnDeath)
            {
                StartCoroutine(DespawnRoutine());
            }
        }

        /// <summary>Stops the corpse blocking movement or absorbing further swings.</summary>
        private void DisableCombatParts()
        {
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

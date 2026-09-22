using Game.Combat;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The screen-effects toggle from SPEC.md section 43: a brief full-screen flash
    /// when the player takes damage, the one screen effect the game currently has.
    /// Gated by <see cref="Game.Core.GameSettings.ScreenEffectsEnabled"/> so a player
    /// sensitive to flashing can turn it off entirely, same shape as
    /// <see cref="Game.Player.PlayerCamera.Shake"/>'s own toggle.
    ///
    /// Resolves the player through <see cref="PlayerDeath"/>, as <see cref="HudUI"/> does.
    /// </summary>
    public class ScreenEffectsUI : MonoBehaviour
    {
        [SerializeField] private Image flashImage;
        [SerializeField] private Color flashColour = new(0.6f, 0.05f, 0.05f, 0.35f);
        [SerializeField] private float flashFadeSeconds = 0.25f;

        private HealthComponent health;
        private float fadeUntil;
        private float fadeStartedAt;

        private void OnEnable()
        {
            SetAlpha(0f);

            if (health != null)
            {
                health.Damaged += OnPlayerDamaged;
            }
        }

        private void Update()
        {
            if (health == null)
            {
                ResolvePlayer();
                return;
            }

            if (fadeUntil <= 0f)
            {
                return;
            }

            var span = fadeUntil - fadeStartedAt;
            var fraction = span > 0f ? Mathf.Clamp01((fadeUntil - Time.time) / span) : 0f;
            SetAlpha(fraction);

            if (Time.time >= fadeUntil)
            {
                fadeUntil = 0f;
            }
        }

        private void ResolvePlayer()
        {
            var player = Object.FindAnyObjectByType<PlayerDeath>();
            if (player == null)
            {
                return;
            }

            health = player.GetComponent<HealthComponent>();
            if (health != null)
            {
                health.Damaged += OnPlayerDamaged;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnPlayerDamaged;
            }
        }

        private void OnPlayerDamaged(DamageData damage)
        {
            if (SettingsManager.Instance != null && !SettingsManager.Instance.Current.ScreenEffectsEnabled)
            {
                return;
            }

            fadeStartedAt = Time.time;
            fadeUntil = Time.time + Mathf.Max(0.01f, flashFadeSeconds);
            SetAlpha(1f);
        }

        private void SetAlpha(float alpha)
        {
            if (flashImage == null)
            {
                return;
            }

            var colour = flashColour;
            colour.a = flashColour.a * alpha;
            flashImage.color = colour;
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(Image image, HealthComponent playerHealth)
        {
            flashImage = image;
            health = playerHealth;
            if (health != null)
            {
                health.Damaged += OnPlayerDamaged;
            }
        }
    }
}

using Game.Core;
using Game.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The presentation half of TASK 014's cinematic system: letterbox bars, a
    /// subtitle line, and a "press to skip" hint, driven entirely by
    /// <see cref="CinematicPlayer"/>'s events rather than holding any sequencing
    /// state of its own (the same shape as <see cref="RecoveryMessageUI"/>).
    ///
    /// Reads the <c>Skip</c> action directly rather than through <see cref="GameManager"/>'s
    /// state, since a cinematic freezes <c>Time.timeScale</c> but the Input System's
    /// action callbacks are unaffected by it — the same reason <see cref="PauseMenu"/>
    /// can still read its own action while the game is paused.
    /// </summary>
    public class CinematicUI : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private GameObject root;
        [SerializeField] private Text subtitleLabel;

        [Tooltip("A backing box directly behind the subtitle, hidden when SPEC.md section 43's subtitle-background setting is off. Separate from the always-on letterbox bars.")]
        [SerializeField] private Image subtitleBackground;

        private InputAction skipAction;
        private CinematicPlayer currentPlayer;
        private int baseSubtitleFontSize;

        private void Awake()
        {
            if (inputActions != null)
            {
                var gameplayMap = inputActions.FindActionMap("Gameplay");
                skipAction = gameplayMap?.FindAction("Skip");
            }

            if (subtitleLabel != null)
            {
                baseSubtitleFontSize = subtitleLabel.fontSize;
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<CinematicStartedEvent>(OnStarted);
            EventBus.Subscribe<CinematicBeatShownEvent>(OnBeatShown);
            EventBus.Subscribe<CinematicCompletedEvent>(OnCompleted);

            skipAction?.Enable();
            if (skipAction != null)
            {
                skipAction.performed += OnSkipPressed;
            }

            SetVisible(false);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CinematicStartedEvent>(OnStarted);
            EventBus.Unsubscribe<CinematicBeatShownEvent>(OnBeatShown);
            EventBus.Unsubscribe<CinematicCompletedEvent>(OnCompleted);

            if (skipAction != null)
            {
                skipAction.performed -= OnSkipPressed;
            }

            skipAction?.Disable();
        }

        private void OnStarted(CinematicStartedEvent started)
        {
            currentPlayer = started.Player;
            SetText(subtitleLabel, string.Empty);
            ApplyAccessibilitySettings();
            SetVisible(true);
        }

        /// <summary>SPEC.md section 43: subtitle background and text scale, applied once per cinematic.</summary>
        private void ApplyAccessibilitySettings()
        {
            var settings = SettingsManager.Instance?.Current;

            if (subtitleBackground != null)
            {
                var colour = subtitleBackground.color;
                colour.a = (settings?.SubtitleBackground ?? true) ? 1f : 0f;
                subtitleBackground.color = colour;
            }

            if (subtitleLabel != null)
            {
                var scale = settings?.TextScale ?? 1f;
                subtitleLabel.fontSize = Mathf.Max(1, Mathf.RoundToInt(baseSubtitleFontSize * scale));
            }
        }

        private void OnBeatShown(CinematicBeatShownEvent beat)
        {
            SetText(subtitleLabel, beat.Subtitle);
        }

        private void OnCompleted(CinematicCompletedEvent completed)
        {
            currentPlayer = null;
            SetVisible(false);
        }

        private void OnSkipPressed(InputAction.CallbackContext context)
        {
            currentPlayer?.Skip();
        }

        private void SetVisible(bool visible)
        {
            if (root != null)
            {
                root.SetActive(visible);
            }
        }

        private static void SetText(Text label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }
    }
}

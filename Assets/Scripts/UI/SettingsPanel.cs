using System.Collections.Generic;
using Game.Core;
using Game.Core.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The Settings screen, wherever it is opened from
    /// (STANDALONE_RELEASE_ROADMAP.md TASK 054).
    ///
    /// **One screen, not three.** SPEC.md section 42 asks for settings; KNOWN_ISSUES.md has
    /// carried "Settings and remapping are only reachable from the Main Menu" since TASK
    /// 017, because the screen was a hand-laid panel inside <c>MainMenu.unity</c> and
    /// nothing in a gameplay scene could reach it. A player who wanted to invert the camera
    /// or make the text bigger had to abandon their run to do it — and text size is exactly
    /// the setting someone discovers they need once they are reading subtitles, not before.
    ///
    /// The fix is not to copy the panel into the two gameplay scenes. Three authored copies
    /// means three edits per new setting, and the third is the one that gets forgotten. The
    /// panel builds itself from <see cref="UiBuilder"/>, so the Main Menu and the pause menu
    /// mount the same component and cannot drift apart.
    ///
    /// Rebuilding it also fixed what the old layout did: a slider's fill had escaped its
    /// track and covered the Normal difficulty button and the whole Text Size row with a
    /// green rectangle. Both controls were unreachable, in a shipped-candidate build, and
    /// nothing caught it because nothing had ever rendered the screen.
    ///
    /// Every control writes through <see cref="SettingsManager"/> and saves immediately.
    /// There is no Apply button and no Cancel: a setting that does not take effect until
    /// the player finds a second button is a setting they will report as broken, and an
    /// accessibility option they cannot evaluate until they commit to it is worse than that.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [Tooltip("Where the controls are built. Defaults to this object's own RectTransform.")]
        [SerializeField] private RectTransform container;

        [Tooltip("Width of the built panel in reference pixels.")]
        [SerializeField] private float panelWidth = 1180f;

        [Tooltip("Draw the panel's own background. Off when the host already provides one.")]
        [SerializeField] private bool drawBackground = true;

        [Tooltip("Show the Display section. Off where changing resolution mid-run would be disruptive.")]
        [SerializeField] private bool includeDisplay = true;

        /// <summary>Raised when the player presses Back. The host decides what "back" means.</summary>
        public event System.Action BackRequested;

        private bool built;
        private bool wiring;

        private Slider volume;
        private Text volumeValue;
        private Slider sensitivity;
        private Text sensitivityValue;
        private Toggle invertY;
        private Text invertYState;

        private Text difficultyValue;
        private Button languageButton;
        private Text languageValue;

        private Slider textScale;
        private Text textScaleValue;
        private Toggle subtitleBackground;
        private Text subtitleBackgroundState;
        private Toggle cameraShake;
        private Text cameraShakeState;
        private Toggle screenEffects;
        private Text screenEffectsState;
        private Toggle aimAssist;
        private Text aimAssistState;

        private Text resolutionValue;
        private Text qualityValue;
        private Toggle fullscreen;
        private Text fullscreenState;

        private Button backButton;

        /// <summary>The Back button, so a host can give it controller focus when the panel opens.</summary>
        public Button BackButton => backButton;

        /// <summary>
        /// The resolutions this screen offers, de-duplicated by size.
        ///
        /// Rebuilt whenever the panel opens rather than cached, because the list changes
        /// when a monitor is plugged in or the window is dragged to another one.
        /// </summary>
        private readonly List<Resolution> offeredResolutions = new();

        private void OnEnable()
        {
            EnsureBuilt();
            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
            Refresh();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        /// <summary>
        /// Builds the controls once. Public and callable while the object is inactive, because
        /// a host refreshes the panel before showing it and a panel that has not been built
        /// yet would refresh nothing.
        /// </summary>
        public void EnsureBuilt()
        {
            if (built)
            {
                return;
            }

            built = true;
            Build();
        }

        private void Build()
        {
            var host = container != null ? container : (RectTransform)transform;

            // Fitted rather than sized-to-content: at the largest text size this screen is
            // taller than the screen, and the part that fell off the bottom was the Back
            // button. An accessibility option that strands the player on the accessibility
            // screen is worse than one that does nothing.
            var panel = UiBuilder.FittedPanel(host, "SettingsContent", panelWidth);

            UiBuilder.Label(panel, StringKeys.MenuLabelSettingsTitle, UiTheme.TextRole.Title);

            var scrollContent = UiBuilder.Scroll(panel, "SettingsScroll", 600f);

            // Two columns rather than one list. At a readable size the full set of rows is
            // taller than a 1080 screen, and the two ways to fit it in one column were a
            // scroll view a gamepad does not follow focus into, or smaller text on the
            // screen that holds the accessibility options.
            var columns = UiBuilder.Columns(scrollContent, 2);
            var left = columns[0];
            var right = columns[1];

            UiBuilder.SectionHeader(left, StringKeys.MenuSectionAudio);
            volume = UiBuilder.Slider(Row(left, StringKeys.MenuLabelMasterVolume), 0f, 1f, out volumeValue);
            sensitivity = UiBuilder.Slider(Row(left, StringKeys.MenuLabelLookSensitivity), 0.1f, 3f,
                out sensitivityValue);
            invertY = UiBuilder.Toggle(Row(left, StringKeys.MenuLabelInvertY), out invertYState);

            UiBuilder.SectionHeader(left, StringKeys.MenuSectionGameplay);
            var difficultyButton = UiBuilder.Cycler(Row(left, StringKeys.MenuLabelDifficulty), out difficultyValue);
            languageButton = UiBuilder.Cycler(Row(left, StringKeys.MenuLabelLanguage), out languageValue);

            UiBuilder.SectionHeader(right, StringKeys.MenuSectionAccessibility);
            textScale = UiBuilder.Slider(Row(right, StringKeys.MenuLabelTextSize), 0.75f, 1.5f, out textScaleValue);
            subtitleBackground = UiBuilder.Toggle(Row(right, StringKeys.MenuLabelSubtitleBackground),
                out subtitleBackgroundState);
            cameraShake = UiBuilder.Toggle(Row(right, StringKeys.MenuLabelCameraShake), out cameraShakeState);
            screenEffects = UiBuilder.Toggle(Row(right, StringKeys.MenuLabelScreenEffects), out screenEffectsState);
            aimAssist = UiBuilder.Toggle(Row(right, StringKeys.MenuLabelAimAssist), out aimAssistState);

            Button resolutionButton = null;
            Button qualityButton = null;
            if (includeDisplay)
            {
                UiBuilder.SectionHeader(right, StringKeys.MenuSectionDisplay);
                resolutionButton = UiBuilder.Cycler(Row(right, StringKeys.MenuLabelResolution), out resolutionValue);
                qualityButton = UiBuilder.Cycler(Row(right, StringKeys.MenuLabelQuality), out qualityValue);
                fullscreen = UiBuilder.Toggle(Row(right, StringKeys.MenuLabelFullscreen), out fullscreenState);
            }

            UiBuilder.Spacer(panel, UiTheme.Spacing * 2f);
            backButton = UiBuilder.Button(panel, StringKeys.MenuLabelBack);

            // Explicit, because Unity's automatic navigation left the whole right-hand
            // column unreachable — every accessibility option among them.
            UiBuilder.WireColumnNavigation(columns, backButton);

            volume.onValueChanged.AddListener(OnVolumeChanged);
            sensitivity.onValueChanged.AddListener(OnSensitivityChanged);
            invertY.onValueChanged.AddListener(OnInvertYChanged);
            difficultyButton.onClick.AddListener(CycleDifficulty);
            languageButton.onClick.AddListener(CycleLanguage);
            textScale.onValueChanged.AddListener(OnTextScaleChanged);
            subtitleBackground.onValueChanged.AddListener(OnSubtitleBackgroundChanged);
            cameraShake.onValueChanged.AddListener(OnCameraShakeChanged);
            screenEffects.onValueChanged.AddListener(OnScreenEffectsChanged);
            aimAssist.onValueChanged.AddListener(OnAimAssistChanged);
            resolutionButton?.onClick.AddListener(CycleResolution);
            qualityButton?.onClick.AddListener(CycleQuality);
            fullscreen?.onValueChanged.AddListener(OnFullscreenChanged);
            backButton.onClick.AddListener(() => BackRequested?.Invoke());
        }

        /// <summary>
        /// A settings row inside a column. The caption is narrower than
        /// <see cref="UiBuilder.Row"/>'s default because a column is half a panel wide.
        /// </summary>
        private static RectTransform Row(Transform column, string captionKey) =>
            UiBuilder.Row(column, captionKey, 260f);

        /// <summary>
        /// Redraws every control from the stored settings.
        ///
        /// <see cref="wiring"/> is held for the duration because
        /// <c>SetValueWithoutNotify</c> covers the widget's own callback and not the readout
        /// beside it; without the guard, writing the current value back into a slider would
        /// be indistinguishable from the player moving it, and the panel would save settings
        /// nobody changed.
        /// </summary>
        public void Refresh()
        {
            EnsureBuilt();

            var settings = SettingsManager.Instance?.Current;
            wiring = true;

            if (settings != null)
            {
                volume.SetValueWithoutNotify(settings.MasterVolume);
                sensitivity.SetValueWithoutNotify(settings.MouseSensitivity);
                invertY.SetIsOnWithoutNotify(settings.InvertY);
                textScale.SetValueWithoutNotify(settings.TextScale);
                subtitleBackground.SetIsOnWithoutNotify(settings.SubtitleBackground);
                cameraShake.SetIsOnWithoutNotify(settings.CameraShakeEnabled);
                screenEffects.SetIsOnWithoutNotify(settings.ScreenEffectsEnabled);
                aimAssist.SetIsOnWithoutNotify(settings.AimAssistEnabled);

                if (fullscreen != null)
                {
                    fullscreen.SetIsOnWithoutNotify(settings.Fullscreen);
                }
            }

            RefreshReadouts(settings);
            wiring = false;
        }

        private void RefreshReadouts(GameSettings settings)
        {
            volumeValue.text = Percent(volume.value);
            sensitivityValue.text = sensitivity.value.ToString("0.0");
            invertYState.text = OnOff(invertY.isOn);
            textScaleValue.text = Percent(textScale.value);
            subtitleBackgroundState.text = OnOff(subtitleBackground.isOn);
            cameraShakeState.text = OnOff(cameraShake.isOn);
            screenEffectsState.text = OnOff(screenEffects.isOn);
            aimAssistState.text = OnOff(aimAssist.isOn);

            difficultyValue.text = DifficultyName(Difficulty.Current);
            languageValue.text = CurrentLanguageName();

            // A build with one language installed cannot cycle anywhere. Leaving the button
            // live makes it look broken; greying it keeps the row as a statement of which
            // language is in force, which is the honest thing for an English-only release.
            languageButton.interactable = SettingsManager.AvailableLanguages().Count > 1;

            if (!includeDisplay)
            {
                return;
            }

            RefreshResolutions(settings);
            fullscreenState.text = OnOff(fullscreen.isOn);

            var level = settings != null && settings.QualityLevel >= 0
                ? settings.QualityLevel
                : QualitySettings.GetQualityLevel();
            var names = QualitySettings.names;
            qualityValue.text = names.Length > 0
                ? names[Mathf.Clamp(level, 0, names.Length - 1)]
                : string.Empty;
        }

        private void RefreshResolutions(GameSettings settings)
        {
            offeredResolutions.Clear();
            var seen = new HashSet<string>();

            foreach (var resolution in Screen.resolutions)
            {
                // Screen.resolutions lists the same size once per refresh rate, and the
                // player is choosing a size.
                var label = Strings.Format(StringKeys.MenuResolution, resolution.width, resolution.height);
                if (seen.Add(label))
                {
                    offeredResolutions.Add(resolution);
                }
            }

            var width = settings != null && settings.HasResolution ? settings.ResolutionWidth : Screen.width;
            var height = settings != null && settings.HasResolution ? settings.ResolutionHeight : Screen.height;
            resolutionValue.text = Strings.Format(StringKeys.MenuResolution, width, height);
        }

        // ------------------------------------------------------------------- handlers

        private void OnVolumeChanged(float value)
        {
            if (Ignore())
            {
                return;
            }

            SettingsManager.Instance.Current.MasterVolume = value;
            SettingsManager.Instance.Apply();
            SettingsManager.Instance.Save();
            volumeValue.text = Percent(value);
        }

        private void OnSensitivityChanged(float value)
        {
            if (Ignore())
            {
                return;
            }

            SettingsManager.Instance.Current.MouseSensitivity = value;
            SettingsManager.Instance.Save();
            sensitivityValue.text = value.ToString("0.0");
        }

        private void OnInvertYChanged(bool value)
        {
            if (Ignore())
            {
                return;
            }

            SettingsManager.Instance.Current.InvertY = value;
            SettingsManager.Instance.Save();
            invertYState.text = OnOff(value);
        }

        private void OnTextScaleChanged(float value)
        {
            if (Ignore())
            {
                return;
            }

            SettingsManager.Instance.Current.TextScale = value;
            SettingsManager.Instance.Save();
            textScaleValue.text = Percent(value);

            // Applied while the player is looking at it. UIScaleApplier reads this on
            // enable, which was enough when settings only existed in the Main Menu; from
            // inside a run, a text-size control that does nothing until the next scene
            // load is a control the player will conclude is broken.
            EventBus.Publish(new TextScaleChangedEvent(value));
        }

        private void OnSubtitleBackgroundChanged(bool value)
        {
            if (Ignore())
            {
                return;
            }

            SettingsManager.Instance.Current.SubtitleBackground = value;
            SettingsManager.Instance.Save();
            subtitleBackgroundState.text = OnOff(value);
        }

        private void OnCameraShakeChanged(bool value)
        {
            if (Ignore())
            {
                return;
            }

            SettingsManager.Instance.Current.CameraShakeEnabled = value;
            SettingsManager.Instance.Save();
            cameraShakeState.text = OnOff(value);
        }

        private void OnScreenEffectsChanged(bool value)
        {
            if (Ignore())
            {
                return;
            }

            SettingsManager.Instance.Current.ScreenEffectsEnabled = value;
            SettingsManager.Instance.Save();
            screenEffectsState.text = OnOff(value);
        }

        private void OnAimAssistChanged(bool value)
        {
            if (Ignore())
            {
                return;
            }

            SettingsManager.Instance.Current.AimAssistEnabled = value;
            SettingsManager.Instance.Save();
            aimAssistState.text = OnOff(value);
        }

        private void OnFullscreenChanged(bool value)
        {
            if (Ignore())
            {
                return;
            }

            var current = SettingsManager.Instance.Current;
            SettingsManager.Instance.SetDisplay(current.ResolutionWidth, current.ResolutionHeight, value,
                current.QualityLevel);
            fullscreenState.text = OnOff(value);
        }

        private void CycleDifficulty()
        {
            if (wiring)
            {
                return;
            }

            var modes = System.Enum.GetValues(typeof(DifficultyMode));
            var next = (DifficultyMode)(((int)Difficulty.Current + 1) % modes.Length);

            if (SettingsManager.Instance != null)
            {
                // Through SettingsManager, never Difficulty.Set: this is the player
                // changing it, which marks a loaded run as differing from its save
                // (TASK 040). Difficulty.Set would change the game silently.
                SettingsManager.Instance.SetDifficulty(next);
            }
            else
            {
                Difficulty.Set(next);
            }

            difficultyValue.text = DifficultyName(Difficulty.Current);
        }

        private void CycleLanguage()
        {
            if (wiring)
            {
                return;
            }

            var languages = SettingsManager.AvailableLanguages();
            if (languages.Count == 0)
            {
                return;
            }

            var current = Strings.Active != null ? Strings.Active.LanguageCode : Strings.DefaultLanguage;
            var index = languages.FindIndex(table => table.LanguageCode == current);
            var next = languages[(index + 1 + languages.Count) % languages.Count];

            SettingsManager.Instance?.SetLanguage(next.LanguageCode);
        }

        private void CycleResolution()
        {
            if (wiring || SettingsManager.Instance == null || offeredResolutions.Count == 0)
            {
                return;
            }

            var current = SettingsManager.Instance.Current;
            var width = current.HasResolution ? current.ResolutionWidth : Screen.width;
            var height = current.HasResolution ? current.ResolutionHeight : Screen.height;

            var index = offeredResolutions.FindIndex(r => r.width == width && r.height == height);
            var chosen = offeredResolutions[(index + 1 + offeredResolutions.Count) % offeredResolutions.Count];

            SettingsManager.Instance.SetDisplay(chosen.width, chosen.height, current.Fullscreen, current.QualityLevel);
            resolutionValue.text = Strings.Format(StringKeys.MenuResolution, chosen.width, chosen.height);
        }

        private void CycleQuality()
        {
            if (wiring || SettingsManager.Instance == null || QualitySettings.names.Length == 0)
            {
                return;
            }

            var current = SettingsManager.Instance.Current;
            var level = current.QualityLevel >= 0 ? current.QualityLevel : QualitySettings.GetQualityLevel();
            var next = (level + 1) % QualitySettings.names.Length;

            SettingsManager.Instance.SetDisplay(current.ResolutionWidth, current.ResolutionHeight,
                current.Fullscreen, next);
            qualityValue.text = QualitySettings.names[next];
        }

        private void OnLanguageChanged(LanguageChangedEvent changed) => Refresh();

        /// <summary>True when a callback came from the panel writing to itself, or has nowhere to write.</summary>
        private bool Ignore() => wiring || SettingsManager.Instance == null;

        // --------------------------------------------------------------------- text

        private static string OnOff(bool value) =>
            Strings.Get(value ? StringKeys.MenuLabelOn : StringKeys.MenuLabelOff);

        private static string Percent(float fraction) => Mathf.RoundToInt(fraction * 100f) + "%";

        /// <summary>
        /// A difficulty's name in the player's language. Built from the enum rather than a
        /// switch, so a new mode shows a missing key — which is visible and greppable —
        /// rather than silently showing its C# name to a player who does not read English.
        /// </summary>
        public static string DifficultyName(DifficultyMode mode) =>
            Strings.Get(StringKeys.MenuDifficultyPrefix + mode.ToString().ToLowerInvariant());

        /// <inheritdoc cref="DifficultyName"/>
        public static string DifficultyDescription(DifficultyMode mode) =>
            Strings.Get(StringKeys.MenuDifficultyPrefix + mode.ToString().ToLowerInvariant() + ".description");

        /// <summary>
        /// The chosen language's name in its own language, which is what a language picker
        /// has to show: someone who has accidentally selected a language they cannot read
        /// needs to recognise the one they want, and "Hindi" written in English is no help
        /// to a reader of Devanagari.
        /// </summary>
        private static string CurrentLanguageName()
        {
            var active = Strings.Active;
            return active != null && !string.IsNullOrWhiteSpace(active.LanguageName)
                ? active.LanguageName
                : Strings.DefaultLanguage;
        }

        /// <summary>
        /// Builds the panel without running the game, so <c>UiCapture</c> can render it.
        ///
        /// A MonoBehaviour's OnEnable does not fire in the Editor outside play mode, so a
        /// screen that builds itself is invisible to the tool that checks screens — which
        /// would have left the Settings panel, the one with the layout defect that started
        /// this task, as the one screen nobody could look at.
        /// </summary>
        public void BuildForPreview()
        {
            EnsureBuilt();
            Refresh();
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(RectTransform host, bool background, bool display)
        {
            container = host;
            drawBackground = background;
            includeDisplay = display;
        }

        /// <summary>Gives the panel's first control controller focus, so a gamepad can move from it.</summary>
        public void SelectFirst()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            foreach (var selectable in GetComponentsInChildren<Selectable>())
            {
                if (selectable.IsActive() && selectable.IsInteractable())
                {
                    EventSystem.current.SetSelectedGameObject(selectable.gameObject);
                    return;
                }
            }
        }
    }
}

using System.Collections.Generic;
using Game.Core;
using Game.Save;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The Main Menu and everything reachable from it without a running game (SPEC.md
    /// section 42): New Game, Continue, Save/Load, Settings, Controls, Credits.
    ///
    /// Lives in its own scene, listed first in Build Settings. New Game and Load both
    /// hand off to <see cref="GameSceneManager"/> to load a gameplay scene; Load also
    /// sets <see cref="SaveManager.PendingLoad"/> first, so the <see cref="SaveManager"/>
    /// that wakes up in that scene applies the save once everything in it exists. This
    /// menu never touches gameplay systems directly — it does not know Combat, AI or
    /// Quests exist — because a save's data, not this controller, is what a level reads.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Tooltip("The scene New Game loads. SPEC.md section 60: the vertical slice starts in Avarsha.")]
        [SerializeField] private string startingScene = "Avarsha";

        [Header("Panels")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private GameObject newGamePanel;
        [SerializeField] private GameObject loadPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private GameObject creditsPanel;

        [Header("Root buttons")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button controlsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button quitButton;

        [Header("New game")]
        [Tooltip("Indexed by DifficultyMode: Story, Normal, Warrior, Mythic.")]
        [SerializeField] private Button[] difficultyButtons;
        [SerializeField] private Button newGameBackButton;

        [System.Serializable]
        public class SlotRow
        {
            public SaveSlot Slot;
            public Text Label;
            public Button LoadRowButton;
        }

        [Header("Save / Load")]
        [SerializeField] private SlotRow[] slotRows;
        [SerializeField] private Button loadBackButton;

        [Header("Settings")]
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Toggle invertYToggle;
        [Tooltip("Indexed by DifficultyMode: Story, Normal, Warrior, Mythic.")]
        [SerializeField] private Button[] settingsDifficultyButtons;
        [SerializeField] private Text settingsDifficultyLabel;
        [SerializeField] private Button settingsBackButton;

        [Header("Accessibility (SPEC.md section 43)")]
        [SerializeField] private Slider textScaleSlider;
        [SerializeField] private Toggle subtitleBackgroundToggle;
        [SerializeField] private Toggle cameraShakeToggle;
        [SerializeField] private Toggle screenEffectsToggle;
        [SerializeField] private Toggle aimAssistToggle;

        [Header("Display (SPEC.md section 54, edge cases 20 and 24)")]
        [SerializeField] private Dropdown resolutionDropdown;
        [SerializeField] private Dropdown qualityDropdown;
        [SerializeField] private Toggle fullscreenToggle;

        [Header("Controls")]
        [SerializeField] private RemapUI remapUI;

        [Header("Static screens")]
        [SerializeField] private Button controlsBackButton;
        [SerializeField] private Button creditsBackButton;

        private bool wiringSettingsControls;

        private void Awake()
        {
            newGameButton?.onClick.AddListener(OpenNewGame);
            continueButton?.onClick.AddListener(Continue);
            loadButton?.onClick.AddListener(OpenLoad);
            settingsButton?.onClick.AddListener(OpenSettings);
            controlsButton?.onClick.AddListener(OpenControls);
            creditsButton?.onClick.AddListener(OpenCredits);
            quitButton?.onClick.AddListener(Quit);

            newGameBackButton?.onClick.AddListener(OpenRoot);
            loadBackButton?.onClick.AddListener(OpenRoot);
            settingsBackButton?.onClick.AddListener(OpenRoot);
            controlsBackButton?.onClick.AddListener(OpenRoot);
            creditsBackButton?.onClick.AddListener(OpenRoot);

            if (difficultyButtons != null)
            {
                for (var i = 0; i < difficultyButtons.Length; i++)
                {
                    var mode = (DifficultyMode)i;
                    difficultyButtons[i]?.onClick.AddListener(() => StartNewGame(mode));
                }
            }

            if (settingsDifficultyButtons != null)
            {
                for (var i = 0; i < settingsDifficultyButtons.Length; i++)
                {
                    var mode = (DifficultyMode)i;
                    settingsDifficultyButtons[i]?.onClick.AddListener(() => SetDifficulty(mode));
                }
            }

            if (slotRows != null)
            {
                foreach (var row in slotRows)
                {
                    if (row?.LoadRowButton == null)
                    {
                        continue;
                    }

                    var slot = row.Slot;
                    row.LoadRowButton.onClick.AddListener(() => LoadSlot(slot));
                }
            }

            volumeSlider?.onValueChanged.AddListener(OnVolumeChanged);
            sensitivitySlider?.onValueChanged.AddListener(OnSensitivityChanged);
            invertYToggle?.onValueChanged.AddListener(OnInvertYChanged);

            resolutionDropdown?.onValueChanged.AddListener(OnResolutionChanged);
            qualityDropdown?.onValueChanged.AddListener(OnQualityChanged);
            fullscreenToggle?.onValueChanged.AddListener(OnFullscreenChanged);

            textScaleSlider?.onValueChanged.AddListener(OnTextScaleChanged);
            subtitleBackgroundToggle?.onValueChanged.AddListener(OnSubtitleBackgroundChanged);
            cameraShakeToggle?.onValueChanged.AddListener(OnCameraShakeChanged);
            screenEffectsToggle?.onValueChanged.AddListener(OnScreenEffectsChanged);
            aimAssistToggle?.onValueChanged.AddListener(OnAimAssistChanged);
        }

        private void OnEnable()
        {
            OpenRoot();
            RefreshContinueButton();
        }

        // ------------------------------------------------------------------ navigation

        private void OpenRoot() => ShowPanel(rootPanel);
        private void OpenNewGame() => ShowPanel(newGamePanel);
        private void OpenControls()
        {
            ShowPanel(controlsPanel);
            remapUI?.Refresh();
        }
        private void OpenCredits() => ShowPanel(creditsPanel);

        private void OpenSettings()
        {
            RefreshSettingsPanel();
            ShowPanel(settingsPanel);
        }

        private void OpenLoad()
        {
            RefreshLoadPanel();
            ShowPanel(loadPanel);
        }

        private void ShowPanel(GameObject panel)
        {
            SetActiveIfAssigned(rootPanel, panel == rootPanel);
            SetActiveIfAssigned(newGamePanel, panel == newGamePanel);
            SetActiveIfAssigned(loadPanel, panel == loadPanel);
            SetActiveIfAssigned(settingsPanel, panel == settingsPanel);
            SetActiveIfAssigned(controlsPanel, panel == controlsPanel);
            SetActiveIfAssigned(creditsPanel, panel == creditsPanel);
        }

        private static void SetActiveIfAssigned(GameObject go, bool active)
        {
            if (go != null)
            {
                go.SetActive(active);
            }
        }

        private void Quit()
        {
            GameLogger.Log(LogCategory.UI, "Quit requested from the Main Menu.", this);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // -------------------------------------------------------- new game / continue / load

        private void StartNewGame(DifficultyMode mode)
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SetDifficulty(mode);
            }
            else
            {
                Difficulty.Set(mode);
            }

            ResetPersistentWorldState();
            SaveManager.PendingLoad = null;
            LoadGameplayScene(startingScene);
        }

        private void Continue()
        {
            if (SaveBrowser.TryFindMostRecent(SaveRoot(), out var slot))
            {
                LoadSlot(slot);
            }
        }

        private void LoadSlot(SaveSlot slot)
        {
            var outcome = SaveBrowser.Peek(SaveRoot(), slot);
            if (!outcome.Loaded)
            {
                return;
            }

            SaveManager.PendingLoad = slot;
            var sceneName = string.IsNullOrEmpty(outcome.Data.SceneName) ? startingScene : outcome.Data.SceneName;
            LoadGameplayScene(sceneName);
        }

        private void LoadGameplayScene(string sceneName)
        {
            if (GameSceneManager.Instance == null)
            {
                GameLogger.LogError(LogCategory.UI, "No GameSceneManager in the Main Menu scene.", this);
                return;
            }

            GameSceneManager.Instance.LoadScene(sceneName);
        }

        /// <summary>
        /// A genuine new game must not inherit flags from a previous playthrough.
        /// <see cref="WorldState"/> survives scene loads on purpose (SPEC.md section 71
        /// needs it alive across a normal level transition), so returning to this menu
        /// and starting over is the one place that has to end its life deliberately.
        /// </summary>
        private void ResetPersistentWorldState()
        {
            var world = FindAnyObjectByType<WorldState>(FindObjectsInactive.Include);
            if (world != null)
            {
                Destroy(world.gameObject);
            }
        }

        private string SaveRoot() => SaveManager.Instance != null ? SaveManager.Instance.Root : SaveStorage.DefaultRoot;

        private void RefreshContinueButton()
        {
            if (continueButton != null)
            {
                continueButton.interactable = SaveBrowser.TryFindMostRecent(SaveRoot(), out _);
            }
        }

        private void RefreshLoadPanel()
        {
            if (slotRows == null)
            {
                return;
            }

            var root = SaveRoot();
            foreach (var row in slotRows)
            {
                if (row == null)
                {
                    continue;
                }

                var outcome = SaveBrowser.Peek(root, row.Slot);
                if (outcome.Loaded)
                {
                    SetText(row.Label, $"{row.Slot}\n{outcome.Data.SceneName}  -  {outcome.Data.Timestamp.ToLocalTime():g}");
                }
                else
                {
                    SetText(row.Label, $"{row.Slot}\n(empty)");
                }

                if (row.LoadRowButton != null)
                {
                    row.LoadRowButton.interactable = outcome.Loaded;
                }
            }
        }

        // -------------------------------------------------------------------- settings

        private void RefreshSettingsPanel()
        {
            var settings = SettingsManager.Instance?.Current;
            wiringSettingsControls = true;

            if (settings != null)
            {
                if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(settings.MasterVolume);
                if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(settings.MouseSensitivity);
                if (invertYToggle != null) invertYToggle.SetIsOnWithoutNotify(settings.InvertY);

                if (textScaleSlider != null) textScaleSlider.SetValueWithoutNotify(settings.TextScale);
                if (subtitleBackgroundToggle != null) subtitleBackgroundToggle.SetIsOnWithoutNotify(settings.SubtitleBackground);
                if (cameraShakeToggle != null) cameraShakeToggle.SetIsOnWithoutNotify(settings.CameraShakeEnabled);
                if (screenEffectsToggle != null) screenEffectsToggle.SetIsOnWithoutNotify(settings.ScreenEffectsEnabled);
                if (aimAssistToggle != null) aimAssistToggle.SetIsOnWithoutNotify(settings.AimAssistEnabled);
            }

            RefreshDisplayControls(settings);

            wiringSettingsControls = false;
            SetText(settingsDifficultyLabel, $"Difficulty: {Difficulty.Current}");
        }

        // --------------------------------------------------------------------- display

        /// <summary>
        /// The resolutions this screen actually supports, newest-first and
        /// de-duplicated. Rebuilt each time the panel opens rather than cached,
        /// because the list changes when a monitor is plugged in or the player drags
        /// the window to a different one.
        /// </summary>
        private readonly List<Resolution> offeredResolutions = new();

        private void RefreshDisplayControls(GameSettings settings)
        {
            if (fullscreenToggle != null && settings != null)
            {
                fullscreenToggle.SetIsOnWithoutNotify(settings.Fullscreen);
            }

            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new List<string>(QualitySettings.names));

                var level = settings != null && settings.QualityLevel >= 0
                    ? settings.QualityLevel
                    : QualitySettings.GetQualityLevel();
                qualityDropdown.SetValueWithoutNotify(Mathf.Clamp(level, 0, Mathf.Max(0, QualitySettings.names.Length - 1)));
                qualityDropdown.RefreshShownValue();
            }

            if (resolutionDropdown == null)
            {
                return;
            }

            offeredResolutions.Clear();
            var labels = new List<string>();
            var selected = 0;

            foreach (var resolution in Screen.resolutions)
            {
                var label = $"{resolution.width} x {resolution.height}";
                if (labels.Contains(label))
                {
                    // Screen.resolutions lists the same size once per refresh rate.
                    // The player is choosing a size, not a refresh rate.
                    continue;
                }

                if (settings != null && settings.HasResolution
                    && resolution.width == settings.ResolutionWidth
                    && resolution.height == settings.ResolutionHeight)
                {
                    selected = labels.Count;
                }
                else if ((settings == null || !settings.HasResolution)
                         && resolution.width == Screen.width && resolution.height == Screen.height)
                {
                    selected = labels.Count;
                }

                offeredResolutions.Add(resolution);
                labels.Add(label);
            }

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(labels);
            resolutionDropdown.SetValueWithoutNotify(selected);
            resolutionDropdown.RefreshShownValue();
        }

        private void OnResolutionChanged(int index)
        {
            if (wiringSettingsControls || SettingsManager.Instance == null
                || index < 0 || index >= offeredResolutions.Count)
            {
                return;
            }

            var chosen = offeredResolutions[index];
            SettingsManager.Instance.SetDisplay(
                chosen.width,
                chosen.height,
                SettingsManager.Instance.Current.Fullscreen,
                SettingsManager.Instance.Current.QualityLevel);
        }

        private void OnQualityChanged(int index)
        {
            if (wiringSettingsControls || SettingsManager.Instance == null)
            {
                return;
            }

            var current = SettingsManager.Instance.Current;
            SettingsManager.Instance.SetDisplay(current.ResolutionWidth, current.ResolutionHeight, current.Fullscreen, index);
        }

        private void OnFullscreenChanged(bool value)
        {
            if (wiringSettingsControls || SettingsManager.Instance == null)
            {
                return;
            }

            var current = SettingsManager.Instance.Current;
            SettingsManager.Instance.SetDisplay(current.ResolutionWidth, current.ResolutionHeight, value, current.QualityLevel);
        }

        private void SetDifficulty(DifficultyMode mode)
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SetDifficulty(mode);
            }
            else
            {
                Difficulty.Set(mode);
            }

            SetText(settingsDifficultyLabel, $"Difficulty: {Difficulty.Current}");
        }

        private void OnVolumeChanged(float value)
        {
            if (wiringSettingsControls || SettingsManager.Instance == null)
            {
                return;
            }

            SettingsManager.Instance.Current.MasterVolume = value;
            SettingsManager.Instance.Apply();
            SettingsManager.Instance.Save();
        }

        private void OnSensitivityChanged(float value)
        {
            if (wiringSettingsControls || SettingsManager.Instance == null)
            {
                return;
            }

            SettingsManager.Instance.Current.MouseSensitivity = value;
            SettingsManager.Instance.Save();
        }

        private void OnInvertYChanged(bool value)
        {
            if (wiringSettingsControls || SettingsManager.Instance == null)
            {
                return;
            }

            SettingsManager.Instance.Current.InvertY = value;
            SettingsManager.Instance.Save();
        }

        private void OnTextScaleChanged(float value)
        {
            if (wiringSettingsControls || SettingsManager.Instance == null)
            {
                return;
            }

            SettingsManager.Instance.Current.TextScale = value;
            SettingsManager.Instance.Save();
        }

        private void OnSubtitleBackgroundChanged(bool value)
        {
            if (wiringSettingsControls || SettingsManager.Instance == null)
            {
                return;
            }

            SettingsManager.Instance.Current.SubtitleBackground = value;
            SettingsManager.Instance.Save();
        }

        private void OnCameraShakeChanged(bool value)
        {
            if (wiringSettingsControls || SettingsManager.Instance == null)
            {
                return;
            }

            SettingsManager.Instance.Current.CameraShakeEnabled = value;
            SettingsManager.Instance.Save();
        }

        private void OnScreenEffectsChanged(bool value)
        {
            if (wiringSettingsControls || SettingsManager.Instance == null)
            {
                return;
            }

            SettingsManager.Instance.Current.ScreenEffectsEnabled = value;
            SettingsManager.Instance.Save();
        }

        private void OnAimAssistChanged(bool value)
        {
            if (wiringSettingsControls || SettingsManager.Instance == null)
            {
                return;
            }

            SettingsManager.Instance.Current.AimAssistEnabled = value;
            SettingsManager.Instance.Save();
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

using Game.Core;
using Game.Core.Localization;
using Game.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The menu behind Pause: everything the Main Menu offers that still makes sense with a
    /// run loaded (STANDALONE_RELEASE_ROADMAP.md TASK 054).
    ///
    /// **What was there before.** Three buttons — Resume, Save, Main Menu — laid out so that
    /// the word PAUSED was drawn across the Resume and Save buttons, on top of a world that
    /// was not dimmed, so a paused game and a running one were hard to tell apart. There was
    /// no route to Settings or to remapping from inside a run at all: KNOWN_ISSUES.md has
    /// recorded since TASK 017 that a player who wants to invert the camera has to quit to
    /// the Main Menu, and text size — the setting someone discovers they need while actually
    /// reading subtitles — was behind the same wall.
    ///
    /// This builds the whole thing at runtime from <see cref="UiBuilder"/> rather than
    /// authoring it into both gameplay scenes, for the reason the overlap above illustrates:
    /// hand-placed positions drift, and two hand-placed copies drift independently. The
    /// panels are the same <see cref="SettingsPanel"/>, <see cref="RemapUI"/> and
    /// <see cref="HelpPanel"/> the Main Menu uses, so a setting added once appears in both.
    ///
    /// **State stays with <see cref="PauseMenu"/>.** This is presentation: it does not pause,
    /// resume, save or load, it calls the methods that do. There is exactly one place that
    /// knows how to stop and start the game, and a second one would be a bug waiting for the
    /// day the two disagree about <see cref="GameManager"/>'s state.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class GameMenu : MonoBehaviour
    {
        [Tooltip("Owns the pause state. Found in the scene when not assigned.")]
        [SerializeField] private PauseMenu pauseMenu;

        [Tooltip("Drives the Controls screen's rows and the How to Play list.")]
        [SerializeField] private InputActionAsset inputActions;

        [SerializeField] private float panelWidth = 460f;

        private bool built;

        private RectTransform rootPanel;
        private RectTransform settingsHost;
        private RectTransform controlsHost;
        private RectTransform helpHost;

        private SettingsPanel settings;
        private HelpPanel help;
        private RemapUI remap;

        private Text unsavedWarning;
        private Button resumeButton;

        /// <summary>The button a gamepad should land on when the menu opens.</summary>
        public Button ResumeButton => resumeButton;

        private void OnEnable()
        {
            EnsureBuilt();
            ShowRoot();
        }

        /// <summary>
        /// Builds the menu once, on first open rather than at scene load.
        ///
        /// Deferred because the great majority of a run never opens it, and building four
        /// panels' worth of controls — including the remap list — costs a frame nobody
        /// should pay at the moment a scene starts. Paying it on the first pause is paying
        /// it while the game is already stopped.
        /// </summary>
        public void EnsureBuilt()
        {
            if (built)
            {
                return;
            }

            built = true;

            pauseMenu = pauseMenu != null ? pauseMenu : FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
            if (pauseMenu == null)
            {
                GameLogger.LogError(LogCategory.UI,
                    "GameMenu found no PauseMenu; its buttons would have nothing to call.", this);
            }

            UiBuilder.Scrim(transform);
            BuildRoot();
            BuildSettings();
            BuildControls();
            BuildHelp();
        }

        private void BuildRoot()
        {
            rootPanel = UiBuilder.FullScreen(transform, "PauseRoot");
            var panel = UiBuilder.Panel(rootPanel, "PauseContent", panelWidth);

            UiBuilder.Label(panel, StringKeys.MenuLabelPaused, UiTheme.TextRole.Title);

            // Shown only when it is true, and it is the reason the player is being warned
            // rather than a permanent caption: a warning that is always there is one that
            // is never read (TASK 040 left this written down and unshown).
            unsavedWarning = UiBuilder.Paragraph(panel, null, UiTheme.TextRole.Body);
            unsavedWarning.color = UiTheme.Caution;
            unsavedWarning.alignment = TextAnchor.MiddleCenter;

            UiBuilder.Spacer(panel, UiTheme.Spacing);

            resumeButton = UiBuilder.Button(panel, StringKeys.MenuLabelResume, confirm: true);
            var saveButton = UiBuilder.Button(panel, StringKeys.MenuLabelSave);
            var settingsButton = UiBuilder.Button(panel, StringKeys.MenuLabelSettings);
            var controlsButton = UiBuilder.Button(panel, StringKeys.MenuLabelControls);
            var helpButton = UiBuilder.Button(panel, StringKeys.MenuLabelHelp);

            UiBuilder.Spacer(panel, UiTheme.Spacing);
            var quitButton = UiBuilder.Button(panel, StringKeys.MenuLabelMainMenu);

            resumeButton.onClick.AddListener(() => pauseMenu?.OnResumeButtonPressed());
            saveButton.onClick.AddListener(OnSavePressed);
            settingsButton.onClick.AddListener(ShowSettings);
            controlsButton.onClick.AddListener(ShowControls);
            helpButton.onClick.AddListener(ShowHelp);
            quitButton.onClick.AddListener(() => pauseMenu?.OnMainMenuButtonPressed());
        }

        private void BuildSettings()
        {
            settingsHost = UiBuilder.FullScreen(transform, "PauseSettings");
            settings = settingsHost.gameObject.AddComponent<SettingsPanel>();

            // No Display section from inside a run. Changing resolution reloads the screen
            // under a live game, and SPEC.md section 54's edge case 24 already treats that
            // as disruptive; it belongs on the Main Menu, where nothing is in progress.
            settings.Configure(settingsHost, background: true, display: false);
            settings.BackRequested += ShowRoot;
            settingsHost.gameObject.SetActive(false);
        }

        private void BuildControls()
        {
            controlsHost = UiBuilder.FullScreen(transform, "PauseControls");
            var panel = UiBuilder.Panel(controlsHost, "ControlsContent", 820f);

            UiBuilder.Label(panel, StringKeys.MenuLabelControlsTitle, UiTheme.TextRole.Title);
            var content = UiBuilder.Scroll(panel, "RemapScroll", 420f);

            var back = UiBuilder.Button(panel, StringKeys.MenuLabelBack);
            back.onClick.AddListener(ShowRoot);

            remap = controlsHost.gameObject.AddComponent<RemapUI>();
            remap.Configure(inputActions, content);

            controlsHost.gameObject.SetActive(false);
        }

        private void BuildHelp()
        {
            helpHost = UiBuilder.FullScreen(transform, "PauseHelp");
            help = helpHost.gameObject.AddComponent<HelpPanel>();
            help.Configure(inputActions, helpHost);
            help.BackRequested += ShowRoot;
            helpHost.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ navigation

        /// <summary>Shows the pause root, refreshing what it says about the run.</summary>
        public void ShowRoot()
        {
            Show(rootPanel);
            RefreshUnsavedWarning();
            Select(resumeButton);
        }

        public void ShowSettings()
        {
            Show(settingsHost);
            settings.Refresh();
            settings.SelectFirst();
        }

        public void ShowControls()
        {
            Show(controlsHost);
            remap.Refresh();
        }

        public void ShowHelp()
        {
            Show(helpHost);
            help.RefreshBindings();
            Select(help.BackButton);
        }

        private void Show(RectTransform panel)
        {
            SetActive(rootPanel, panel == rootPanel);
            SetActive(settingsHost, panel == settingsHost);
            SetActive(controlsHost, panel == controlsHost);
            SetActive(helpHost, panel == helpHost);
        }

        private static void SetActive(RectTransform rect, bool active)
        {
            if (rect != null)
            {
                rect.gameObject.SetActive(active);
            }
        }

        private void OnSavePressed()
        {
            pauseMenu?.OnSaveButtonPressed();

            // The warning is the feedback. A manual save with no visible result is
            // indistinguishable from a button that does nothing, and this one has
            // something true to say either way.
            RefreshUnsavedWarning();
        }

        /// <summary>
        /// Says, on the screen the player quits from, that quitting now costs them
        /// something.
        ///
        /// TASK 040 decided a mid-run difficulty change marks the run as differing from its
        /// save, and <see cref="SaveManager.HasUnsavedRunChanges"/> has recorded it ever
        /// since — but KNOWN_ISSUES.md noted that nothing ever asked. A flag no screen reads
        /// protects nobody.
        /// </summary>
        private void RefreshUnsavedWarning()
        {
            if (unsavedWarning == null)
            {
                return;
            }

            var dirty = SaveManager.Instance != null && SaveManager.Instance.HasUnsavedRunChanges;
            unsavedWarning.text = dirty ? Strings.Get(StringKeys.MenuUnsavedWarning) : string.Empty;
            unsavedWarning.gameObject.SetActive(dirty);
        }

        private static void Select(Selectable selectable)
        {
            if (EventSystem.current != null && selectable != null && selectable.IsActive())
            {
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            }
        }

        /// <inheritdoc cref="SettingsPanel.BuildForPreview"/>
        public void BuildForPreview()
        {
            EnsureBuilt();
            ShowRoot();
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(PauseMenu owner, InputActionAsset actions)
        {
            pauseMenu = owner;
            inputActions = actions;
        }
    }
}

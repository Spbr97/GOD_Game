using Game.Core;
using Game.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Game.Core.Localization;

namespace Game.UI
{
    /// <summary>
    /// The Main Menu and everything reachable from it without a running game (SPEC.md
    /// section 42): New Game, Continue, Save/Load, Settings, Controls, How to Play,
    /// Credits.
    ///
    /// Lives in its own scene, listed first in Build Settings. New Game and Load both
    /// hand off to <see cref="GameSceneManager"/> to load a gameplay scene; Load also
    /// sets <see cref="SaveManager.PendingLoad"/> first, so the <see cref="SaveManager"/>
    /// that wakes up in that scene applies the save once everything in it exists. This
    /// menu never touches gameplay systems directly — it does not know Combat, AI or
    /// Quests exist — because a save's data, not this controller, is what a level reads.
    ///
    /// **It no longer owns the Settings screen.** Until TASK 054 this class carried every
    /// settings control as a serialized field and every handler as a method, which is why
    /// settings were unreachable from a running game: the screen was a thing in
    /// <c>MainMenu.unity</c>, not a thing the game had. <see cref="SettingsPanel"/>,
    /// <see cref="RemapUI"/> and <see cref="HelpPanel"/> now build themselves wherever they
    /// are mounted, and this menu hosts the same three components the pause menu does.
    /// Roughly 250 lines of duplicated settings handling left this file with them.
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
        [SerializeField] private GameObject helpPanel;
        [SerializeField] private GameObject creditsPanel;

        [Header("Root buttons")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button controlsButton;
        [SerializeField] private Button helpButton;
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

        [Header("Shared screens")]
        [Tooltip("Builds and owns the Settings controls. The same component the pause menu mounts.")]
        [SerializeField] private SettingsPanel settings;

        [SerializeField] private RemapUI remapUI;
        [SerializeField] private HelpPanel help;

        [Header("Static screens")]
        [SerializeField] private Button controlsBackButton;
        [SerializeField] private Button creditsBackButton;

        private void Awake()
        {
            // Being at the Main Menu ends any journey (TASK 041). A pending arrival is a
            // static, so one left behind by a failed load, or by a player who quit to the
            // menu mid-transition, would otherwise be honoured by whichever gameplay scene
            // is entered next — placing them at a door they never walked through, in a
            // scene they did not travel to. The same goes for this session's per-scene
            // memory: Continue adopts the file's records, and New Game must start with none.
            SceneTravel.Clear();
            SceneMemory.Clear();

            // And the disk half of it. The journal exists so a crossing survives losing
            // its statics, which means a stale one would deposit a previous run's
            // progression into a New Game that happens to start in the same scene.
            // Reaching this menu is the one unambiguous signal that the journey is over.
            Game.Save.TravelJournal.Clear(Game.Save.SaveManager.Instance != null
                ? Game.Save.SaveManager.Instance.Root
                : Game.Save.SaveStorage.DefaultRoot);

            newGameButton?.onClick.AddListener(OpenNewGame);
            continueButton?.onClick.AddListener(Continue);
            loadButton?.onClick.AddListener(OpenLoad);
            settingsButton?.onClick.AddListener(OpenSettings);
            controlsButton?.onClick.AddListener(OpenControls);
            helpButton?.onClick.AddListener(OpenHelp);
            creditsButton?.onClick.AddListener(OpenCredits);
            quitButton?.onClick.AddListener(Quit);

            newGameBackButton?.onClick.AddListener(OpenRoot);
            loadBackButton?.onClick.AddListener(OpenRoot);
            controlsBackButton?.onClick.AddListener(OpenRoot);
            creditsBackButton?.onClick.AddListener(OpenRoot);

            if (settings != null)
            {
                settings.BackRequested += OpenRoot;
            }

            if (help != null)
            {
                help.BackRequested += OpenRoot;
            }

            if (difficultyButtons != null)
            {
                for (var i = 0; i < difficultyButtons.Length; i++)
                {
                    var mode = (DifficultyMode)i;
                    difficultyButtons[i]?.onClick.AddListener(() => StartNewGame(mode));
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
        }

        private void OnEnable()
        {
            OpenRoot();
            RefreshContinueButton();
        }

        private void Start()
        {
            // EventSystem may enable after this component. All scene objects have
            // enabled by Start, so the initial screen also gets controller focus.
            SelectFirstUsable(rootPanel);
        }

        // ------------------------------------------------------------------ navigation

        private void OpenRoot() => ShowPanel(rootPanel);
        private void OpenNewGame() => ShowPanel(newGamePanel);
        private void OpenCredits() => ShowPanel(creditsPanel);

        private void OpenControls()
        {
            ShowPanel(controlsPanel);
            remapUI?.Refresh();
        }

        private void OpenHelp()
        {
            ShowPanel(helpPanel);
            help?.RefreshBindings();
        }

        private void OpenSettings()
        {
            ShowPanel(settingsPanel);
            settings?.Refresh();
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
            SetActiveIfAssigned(helpPanel, panel == helpPanel);
            SetActiveIfAssigned(creditsPanel, panel == creditsPanel);

            // A controller cannot navigate a newly opened screen if focus still
            // belongs to a button on the panel we just hid.
            SelectFirstUsable(panel);
        }

        private static void SelectFirstUsable(GameObject panel)
        {
            if (EventSystem.current != null && panel != null && panel.activeInHierarchy)
            {
                foreach (var selectable in panel.GetComponentsInChildren<Selectable>())
                {
                    if (selectable.IsActive() && selectable.IsInteractable())
                    {
                        EventSystem.current.SetSelectedGameObject(selectable.gameObject);
                        break;
                    }
                }
            }
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

#if UNITY_EDITOR || GAME_DEVELOPER_TOOLS
        /// <summary>
        /// Lets <c>StandaloneSmokeTest</c> take the same two paths the New Game and
        /// Continue buttons take, inside a built player where nothing can click them
        /// (STANDALONE_RELEASE_ROADMAP.md TASK 039).
        ///
        /// Deliberately a seam onto the real methods rather than a second copy of them:
        /// the point of the smoke test is to exercise difficulty application, world
        /// state reset and save precedence as shipped, so a bug in any of them must
        /// fail the test instead of living in a parallel implementation. Compiled out
        /// of a release player along with the harness that calls it (SPEC.md section 52).
        /// </summary>
        public void BeginNewGameForSmokeTest(DifficultyMode mode) => StartNewGame(mode);

        /// <inheritdoc cref="BeginNewGameForSmokeTest"/>
        public void ContinueForSmokeTest() => Continue();
#endif

#if UNITY_EDITOR || GAME_DEVELOPER_TOOLS
        /// <summary>Which screen to show. Named MenuScreen so it does not collide with UnityEngine.Screen.</summary>
        public enum MenuScreen
        {
            Root,
            NewGame,
            Load,
            Settings,
            Controls,
            Help,
            Credits
        }

        /// <summary>
        /// Opens a screen from outside, so <c>StandaloneSmokeTest</c> can photograph each
        /// one **in the built player** rather than in the Editor.
        ///
        /// That distinction is the whole reason this exists. The editor capture tool can
        /// show what a canvas looks like at a size somebody chose; it cannot show the real
        /// resolution list this machine offers, a save row written from a real file, or a
        /// checkbox whose tick is drawn by a Toggle that has actually run. Those were three
        /// open questions in UI_BASELINE_REVIEW.md that did not need an owner's opinion —
        /// they needed a screenshot of the running game, which nothing could take.
        ///
        /// Compiled out of a Release player with the harness that calls it.
        /// </summary>
        public void ShowScreenForSmokeTest(MenuScreen screen)
        {
            switch (screen)
            {
                case MenuScreen.NewGame: OpenNewGame(); break;
                case MenuScreen.Load: OpenLoad(); break;
                case MenuScreen.Settings: OpenSettings(); break;
                case MenuScreen.Controls: OpenControls(); break;
                case MenuScreen.Help: OpenHelp(); break;
                case MenuScreen.Credits: OpenCredits(); break;
                default: OpenRoot(); break;
            }
        }
#endif

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
                    SetText(row.Label, Strings.Format(StringKeys.MenuSaveRow, row.Slot, outcome.Data.SceneName,
                        outcome.Data.Timestamp.ToLocalTime().ToString("g")));
                }
                else
                {
                    SetText(row.Label, Strings.Format(StringKeys.MenuSaveRowEmpty, row.Slot));
                }

                if (row.LoadRowButton != null)
                {
                    row.LoadRowButton.interactable = outcome.Loaded;
                }
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

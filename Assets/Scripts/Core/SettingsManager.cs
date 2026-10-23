using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core
{
    /// <summary>
    /// Raised when the display language changes, so screens already on screen redraw.
    ///
    /// Without it, a language change lands in the string table and the panel the player
    /// is looking at keeps its old words until it is closed and reopened — which reads as
    /// the setting being broken rather than as a refresh problem.
    /// </summary>
    public readonly struct LanguageChangedEvent
    {
        public readonly string Previous;
        public readonly string Current;

        public LanguageChangedEvent(string previous, string current)
        {
            Previous = previous;
            Current = current;
        }
    }

    /// <summary>
    /// Raised when the **player** changes difficulty, and never when the game merely
    /// discovers what the difficulty already is.
    ///
    /// The distinction is the whole point, and it is the same one that separates
    /// <c>SettingsManager.SetDifficulty</c> from <c>AdoptDifficultyFromSave</c>. A save
    /// being loaded tells the global settings what that run is being played on; that is
    /// not a change and must not mark anything dirty. A player moving the control in the
    /// Settings panel mid-run is a change, and the run they are playing no longer matches
    /// the file on disk.
    ///
    /// SPEC.md section 44 permits changing difficulty mid-run but does not say whether
    /// the change should survive a reload without a save. TASK 040 settled it: the run is
    /// marked dirty, so quitting without saving cannot silently discard it.
    /// </summary>
    public readonly struct DifficultyChangedByPlayerEvent
    {
        public readonly DifficultyMode Previous;
        public readonly DifficultyMode Current;

        public DifficultyChangedByPlayerEvent(DifficultyMode previous, DifficultyMode current)
        {
            Previous = previous;
            Current = current;
        }
    }

    /// <summary>
    /// Raised when the player changes UI text size, so canvases already on screen rescale.
    ///
    /// <see cref="Game.UI.UIScaleApplier"/> used to apply the setting once on enable, which
    /// was enough while settings existed only in the Main Menu — every gameplay canvas was
    /// created after the change. Now that the Settings panel opens from Pause, the canvas
    /// the player is looking at is already enabled, and a text-size control that does
    /// nothing until the next scene load is one the player reports as broken.
    /// </summary>
    public readonly struct TextScaleChangedEvent
    {
        public readonly float Scale;

        public TextScaleChangedEvent(float scale)
        {
            Scale = scale;
        }
    }

    [Serializable]
    public class GameSettings
    {
        public float MasterVolume = 1f;
        public float MouseSensitivity = 1f;
        public bool InvertY;

        /// <summary>SPEC.md section 44. Applied to the static <see cref="Difficulty"/> on load.</summary>
        public DifficultyMode Difficulty = DifficultyMode.Normal;

        /// <summary>
        /// The language code the player chose, e.g. <c>en</c> (SPEC.md section 73).
        ///
        /// A global setting rather than per-save: it describes the person at the keyboard,
        /// not the run. Two playthroughs do not get different languages, and a save
        /// carried to another machine should read in that machine's chosen language
        /// rather than overriding it — which is the opposite of how difficulty works, and
        /// deliberately so.
        /// </summary>
        public string Language = Game.Core.Localization.Strings.DefaultLanguage;

        // ---------------------------------------------------- SPEC.md section 43

        /// <summary>
        /// Scales UI text size, applied to a scene's Canvas via <see cref="Game.UI.UIScaleApplier"/>.
        /// Covers both "text scaling" and "subtitle size" — subtitles are UI text like
        /// anything else, and section 43 does not ask for them to scale independently.
        /// </summary>
        [Range(0.75f, 1.5f)]
        public float TextScale = 1f;

        /// <summary>Whether dialogue/cinematic subtitles show on a solid backing rather than directly over the scene.</summary>
        public bool SubtitleBackground = true;

        /// <summary>Gates <see cref="Game.Player.PlayerCamera.Shake"/>.</summary>
        public bool CameraShakeEnabled = true;

        /// <summary>Gates <see cref="Game.UI.ScreenEffectsUI"/>'s damage flash.</summary>
        public bool ScreenEffectsEnabled = true;

        /// <summary>Widens <see cref="Game.Combat.LockOnController"/>'s acquisition cone when true.</summary>
        public bool AimAssistEnabled = true;

        /// <summary>
        /// Whether the HUD is drawn (SPEC.md section 42: "HUD must be hideable").
        ///
        /// Persisted rather than reset per scene, because a player who turns the HUD off —
        /// for a screenshot, for immersion, or because a permanent overlay is a problem for
        /// them — wants it off, not off until the next door they walk through.
        /// </summary>
        public bool HudVisible = true;

        // ------------------------------------- SPEC.md section 54, edge cases 20 and 24

        /// <summary>
        /// Index into <see cref="QualitySettings.names"/>. -1 means "leave the
        /// project's own default alone", which is what a fresh install wants: the
        /// build was configured with a sensible level and a saved -1 says the player
        /// has never chosen otherwise.
        /// </summary>
        public int QualityLevel = -1;

        /// <summary>Chosen screen width. 0 means "leave the current resolution alone".</summary>
        public int ResolutionWidth;

        /// <summary>Chosen screen height. 0 means "leave the current resolution alone".</summary>
        public int ResolutionHeight;

        public bool Fullscreen = true;

        /// <summary>Whether a stored resolution exists to apply at all.</summary>
        public bool HasResolution => ResolutionWidth > 0 && ResolutionHeight > 0;
    }

    /// <summary>
    /// Raised after display settings were actually applied, so anything laid out
    /// against the screen can re-measure (SPEC.md section 54's edge case 24). Not
    /// raised for a change that was deferred because a scene was loading.
    /// </summary>
    public readonly struct DisplaySettingsAppliedEvent
    {
        public readonly int Width;
        public readonly int Height;
        public readonly bool Fullscreen;
        public readonly int QualityLevel;

        public DisplaySettingsAppliedEvent(int width, int height, bool fullscreen, int qualityLevel)
        {
            Width = width;
            Height = height;
            Fullscreen = fullscreen;
            QualityLevel = qualityLevel;
        }
    }

    /// <summary>
    /// Holds and persists runtime settings (SPEC.md section 42, 43). Uses
    /// PlayerPrefs+JSON for TASK 001; a full save profile lives in the future
    /// Save system (section 31), which is separate from user settings.
    /// </summary>
    [DisallowMultipleComponent]
    public class SettingsManager : MonoBehaviour
    {
        private const string PrefsKey = "GameSettings";

        /// <summary>SPEC.md section 43's controller/keyboard remapping, saved separately from <see cref="GameSettings"/> since it is Input System data, not a plain value.</summary>
        private const string RebindPrefsKey = "InputBindingOverrides";

        [Tooltip("The same asset every gameplay controller reads actions from. Binding overrides are loaded onto this one shared instance, once, here, before anything else reads a binding.")]
        [SerializeField] private InputActionAsset inputActions;

        public static SettingsManager Instance { get; private set; }

        public GameSettings Current { get; private set; } = new();

        public InputActionAsset InputActions => inputActions;

        /// <summary>A display change arrived while a scene was loading and is waiting for it to finish.</summary>
        private bool displayApplyPending;

        /// <summary>Exposed for tests and the debug overlay.</summary>
        public bool DisplayApplyPending => displayApplyPending;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // The component, not the GameObject: see GameManager.Awake for why.
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadBindingOverrides();
            Load();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Applies any remembered rebinds onto the shared asset. Safe to call more than once.</summary>
        public void LoadBindingOverrides()
        {
            if (inputActions == null || !PlayerPrefs.HasKey(RebindPrefsKey))
            {
                return;
            }

            try
            {
                inputActions.LoadBindingOverridesFromJson(PlayerPrefs.GetString(RebindPrefsKey));
            }
            catch (Exception e)
            {
                GameLogger.LogFallback(LogCategory.Error, "Loading control bindings", "SettingsManager.LoadBindingOverrides", e.Message, "Falling back to default bindings.", this);
            }
        }

        /// <summary>Persists the asset's current overrides. Called after each successful rebind.</summary>
        public void SaveBindingOverrides()
        {
            if (inputActions == null)
            {
                return;
            }

            PlayerPrefs.SetString(RebindPrefsKey, inputActions.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        /// <summary>SPEC.md section 42's "Controls" screen needs a way back to the defaults.</summary>
        public void ResetBindingOverrides()
        {
            if (inputActions == null)
            {
                return;
            }

            inputActions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(RebindPrefsKey);
        }

        /// <summary>Test and tooling seam for assigning the shared asset without the Inspector.</summary>
        public void ConfigureInputActions(InputActionAsset actions)
        {
            inputActions = actions;
        }

        public void Save()
        {
            try
            {
                var json = JsonUtility.ToJson(Current);
                PlayerPrefs.SetString(PrefsKey, json);
                PlayerPrefs.Save();
                GameLogger.Log(LogCategory.Game, "Settings saved.");
            }
            catch (Exception e)
            {
                GameLogger.LogFallback(LogCategory.Error, "Settings save", "SettingsManager.Save", e.Message, "Settings kept in memory only for this session.", this);
            }
        }

        public void Load()
        {
            if (!PlayerPrefs.HasKey(PrefsKey))
            {
                Current = new GameSettings();
                Apply();
                return;
            }

            try
            {
                var json = PlayerPrefs.GetString(PrefsKey);
                Current = JsonUtility.FromJson<GameSettings>(json) ?? new GameSettings();
            }
            catch (Exception e)
            {
                GameLogger.LogFallback(LogCategory.Error, "Settings load", "SettingsManager.Load", e.Message, "Falling back to default settings.", this);
                Current = new GameSettings();
            }

            Apply();
        }

        /// <summary>
        /// Pushes loaded settings into the systems that read them without going
        /// through this component. Difficulty is static (see <see cref="Game.Core.Difficulty"/>)
        /// because every enemy reads it, so it has to be pushed rather than pulled.
        /// </summary>
        public void Apply()
        {
            Game.Core.Difficulty.Set(Current.Difficulty);
            ApplyLanguage();

            // SPEC.md section 43's "audio volume controls". Pushed the same way
            // Difficulty is: AudioListener's volume is a global the emitting sources
            // never read from here (TASK 018's SfxSpawner builds one per cue).
            AudioListener.volume = Mathf.Clamp01(Current.MasterVolume);

            ApplyDisplaySettings();
        }

        /// <summary>
        /// Points <see cref="Game.Core.Localization.Strings"/> at the chosen language,
        /// falling back to English when the build has no table for it.
        ///
        /// The fallback is not an error path: a save or a settings file carried from a
        /// build that shipped more languages than this one names a language this build
        /// does not have, and the right answer is English text rather than a refusal or a
        /// screen of keys. The setting is left as the player chose it, so the language
        /// reappears if they move back to a build that has it.
        /// </summary>
        public void ApplyLanguage()
        {
            var requested = string.IsNullOrWhiteSpace(Current.Language)
                ? Game.Core.Localization.Strings.DefaultLanguage
                : Current.Language;

            if (!Game.Core.Localization.Strings.SetLanguage(requested))
            {
                GameLogger.LogFallback(LogCategory.UI,
                    $"could not use language '{requested}'",
                    "SettingsManager.ApplyLanguage",
                    "this build has no string table for it",
                    "English text is shown and the setting is left alone", this);
            }
        }

        /// <summary>
        /// Changes language and tells the game to redraw.
        ///
        /// Every screen reads its text in <c>OnEnable</c> or per frame, so a language
        /// change has to announce itself or the panel the player is looking at keeps the
        /// old words until it is closed and reopened — which reads as the setting not
        /// working. <see cref="LanguageChangedEvent"/> is what the open UI listens to.
        /// </summary>
        public void SetLanguage(string languageCode)
        {
            var previous = Current.Language;

            Current.Language = string.IsNullOrWhiteSpace(languageCode)
                ? Game.Core.Localization.Strings.DefaultLanguage
                : languageCode;

            ApplyLanguage();
            Save();

            if (previous != Current.Language)
            {
                EventBus.Publish(new LanguageChangedEvent(previous, Current.Language));
            }
        }

        /// <summary>
        /// Every language this build can actually show, newest-found order, English first.
        ///
        /// Discovered by loading the tables out of Resources rather than from a list
        /// written here, so adding a translation is adding one asset. The pseudo-locale
        /// is included on purpose: it is how the text is checked, and hiding it would
        /// mean the only way to see it is to edit a settings file by hand.
        /// </summary>
        public static System.Collections.Generic.List<Game.Core.Localization.StringTable> AvailableLanguages()
        {
            var tables = new System.Collections.Generic.List<Game.Core.Localization.StringTable>(
                Resources.LoadAll<Game.Core.Localization.StringTable>("Localization"));

            tables.RemoveAll(table => table == null || string.IsNullOrWhiteSpace(table.LanguageCode));

            // The pseudo-locale is a test instrument, not a language. It is how long text
            // and non-ASCII glyphs get checked, so it has to be reachable in the Editor
            // and in a Development player -- but a player who picks it in a Release build
            // gets accented, padded nonsense and has to navigate back out of a menu they
            // can no longer read. Gated on the same define as the rest of the developer
            // tools, so it cannot reach a release by being forgotten.
#if !UNITY_EDITOR && !GAME_DEVELOPER_TOOLS
            tables.RemoveAll(table =>
                table.LanguageCode == Game.Core.Localization.StringTable.PseudoLanguageCode);
#endif
            tables.Sort((a, b) =>
            {
                if (a.LanguageCode == Game.Core.Localization.Strings.DefaultLanguage) { return -1; }
                if (b.LanguageCode == Game.Core.Localization.Strings.DefaultLanguage) { return 1; }
                return string.Compare(a.LanguageName, b.LanguageName, System.StringComparison.Ordinal);
            });

            return tables;
        }

        /// <summary>
        /// Applies quality level and resolution (SPEC.md section 54's edge cases 20
        /// and 24).
        ///
        /// Refused outright while a scene is loading, and retried once it finishes.
        /// That is edge case 20: changing the resolution mid-load resizes the screen
        /// underneath a scene that is halfway through building its canvas, and what
        /// comes out is a layout measured against a screen that no longer exists.
        /// Deferring costs the player nothing — the change lands a moment later.
        /// </summary>
        public void ApplyDisplaySettings()
        {
            if (GameSceneManager.IsLoading)
            {
                displayApplyPending = true;
                GameLogger.Log(LogCategory.UI, "Display settings deferred: a scene is loading.", this);
                return;
            }

            displayApplyPending = false;

            var quality = Current.QualityLevel;
            if (quality >= 0 && quality < QualitySettings.names.Length && quality != QualitySettings.GetQualityLevel())
            {
                // applyExpensiveChanges: false — the expensive half is texture and
                // shader reloading, and forcing it mid-session is a visible hitch for
                // no benefit the player asked for.
                QualitySettings.SetQualityLevel(quality, false);
            }

            if (Current.HasResolution
                && (Screen.width != Current.ResolutionWidth
                    || Screen.height != Current.ResolutionHeight
                    || Screen.fullScreen != Current.Fullscreen))
            {
                Screen.SetResolution(Current.ResolutionWidth, Current.ResolutionHeight, Current.Fullscreen);
            }

            EventBus.Publish(new DisplaySettingsAppliedEvent(
                Current.HasResolution ? Current.ResolutionWidth : Screen.width,
                Current.HasResolution ? Current.ResolutionHeight : Screen.height,
                Current.Fullscreen,
                quality));
        }

        /// <summary>
        /// Lands a display change that arrived mid-load. A per-frame bool test rather
        /// than a callback on <see cref="GameSceneManager"/>, because the load that
        /// deferred it may be the one that destroys whatever registered the callback.
        /// </summary>
        private void Update()
        {
            if (displayApplyPending && !GameSceneManager.IsLoading)
            {
                ApplyDisplaySettings();
            }
        }

        /// <summary>Sets and persists the display settings together, since changing one usually means changing the other.</summary>
        public void SetDisplay(int width, int height, bool fullscreen, int qualityLevel)
        {
            Current.ResolutionWidth = width;
            Current.ResolutionHeight = height;
            Current.Fullscreen = fullscreen;
            Current.QualityLevel = qualityLevel;

            ApplyDisplaySettings();
            Save();
        }

        /// <summary>Changes difficulty and persists it. SPEC.md section 44 allows this mid-run.</summary>
        public void SetDifficulty(DifficultyMode mode)
        {
            var previous = Current.Difficulty;

            Current.Difficulty = mode;
            Apply();
            Save();

            if (previous != mode)
            {
                // Announced rather than acted on, because what a loaded run should do
                // about it is the save system's business and nothing may depend on Save
                // (SPEC.md section 58). SaveManager marks the run dirty; see
                // DifficultyChangedByPlayerEvent.
                EventBus.Publish(new DifficultyChangedByPlayerEvent(previous, mode));
            }
        }

        /// <summary>
        /// Takes the difficulty a loaded save was being played on (TASK 041).
        ///
        /// Difficulty is the one value that both a save slot and the global settings
        /// hold, and the two can disagree the moment a player has more than one
        /// playthrough. The slot wins: difficulty is chosen at New Game and belongs to
        /// that run. This exists so the global copy is told, rather than being left
        /// describing a run that is no longer loaded — the Settings panel reads
        /// <see cref="Current"/>, and a panel that says Normal while the game is running
        /// Hard is a bug report waiting to happen.
        ///
        /// Distinct from <see cref="SetDifficulty"/> even though the body is nearly the
        /// same, because the two mean opposite things: that one is the player changing
        /// difficulty, this one is the game reporting what the difficulty already is.
        /// Only <see cref="SetDifficulty"/> should ever be reachable from a UI control.
        /// </summary>
        public void AdoptDifficultyFromSave(DifficultyMode mode)
        {
            if (Current.Difficulty == mode)
            {
                return;
            }

            Current.Difficulty = mode;
            Difficulty.Set(mode);
            Save();
        }
    }
}

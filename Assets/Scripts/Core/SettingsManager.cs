using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core
{
    [Serializable]
    public class GameSettings
    {
        public float MasterVolume = 1f;
        public float MouseSensitivity = 1f;
        public bool InvertY;

        /// <summary>SPEC.md section 44. Applied to the static <see cref="Difficulty"/> on load.</summary>
        public DifficultyMode Difficulty = DifficultyMode.Normal;

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

            // SPEC.md section 43's "audio volume controls". Pushed the same way
            // Difficulty is: AudioListener's volume is a global the emitting sources
            // never read from here (TASK 018's SfxSpawner builds one per cue).
            AudioListener.volume = Mathf.Clamp01(Current.MasterVolume);

            ApplyDisplaySettings();
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
            Current.Difficulty = mode;
            Apply();
            Save();
        }
    }
}

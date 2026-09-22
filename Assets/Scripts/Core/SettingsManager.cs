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

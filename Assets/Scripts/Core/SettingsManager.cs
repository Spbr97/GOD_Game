using System;
using UnityEngine;

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

        public static SettingsManager Instance { get; private set; }

        public GameSettings Current { get; private set; } = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
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

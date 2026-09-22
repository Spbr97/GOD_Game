using UnityEngine;

namespace Game.Core
{
    public enum GameState
    {
        Boot,
        Playing,
        Paused,
        Cutscene
    }

    public readonly struct GameStateChangedEvent
    {
        public readonly GameState PreviousState;
        public readonly GameState NewState;

        public GameStateChangedEvent(GameState previousState, GameState newState)
        {
            PreviousState = previousState;
            NewState = newState;
        }
    }

    /// <summary>
    /// Top-level game state singleton (SPEC.md section 47). Persists across scene
    /// loads. Does not reference Combat/AI/Quest systems directly — those react to
    /// state changes via EventBus.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState CurrentState { get; private set; } = GameState.Boot;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // The component, not the GameObject: this sits on a shared "GameSystems"
                // object alongside scene-scoped managers (WorldState, QuestManager, ...)
                // that must survive even when this one is a stale duplicate from a scene
                // loaded earlier in the session (e.g. the Main Menu).
                GameLogger.LogWarning(LogCategory.Game, "Duplicate GameManager found, destroying the new instance.", this);
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            GameLogger.Log(LogCategory.Game, "GameManager initialized.");
        }

        private void Start()
        {
            if (Instance != this)
            {
                return;
            }

            SetState(GameState.Playing);
        }

        public void Pause()
        {
            if (CurrentState != GameState.Playing)
            {
                return;
            }

            Time.timeScale = 0f;
            SetState(GameState.Paused);
        }

        public void Resume()
        {
            if (CurrentState != GameState.Paused)
            {
                return;
            }

            Time.timeScale = 1f;
            SetState(GameState.Playing);
        }

        /// <summary>
        /// Hands control to a cinematic (SPEC.md section 69, TASK 014): freezes the
        /// world the same way <see cref="Pause"/> does, so nothing needs its own
        /// "am I in a cutscene" check to stop moving or swinging while one plays.
        /// Refused from any state but <see cref="GameState.Playing"/>, which is what
        /// stops the Pause menu and a cinematic from ever being active together.
        /// </summary>
        public void EnterCutscene()
        {
            if (CurrentState != GameState.Playing)
            {
                return;
            }

            Time.timeScale = 0f;
            SetState(GameState.Cutscene);
        }

        /// <summary>Hands control back after a cinematic finishes or is skipped.</summary>
        public void ExitCutscene()
        {
            if (CurrentState != GameState.Cutscene)
            {
                return;
            }

            Time.timeScale = 1f;
            SetState(GameState.Playing);
        }

        public void SetState(GameState newState)
        {
            if (newState == CurrentState)
            {
                return;
            }

            var previous = CurrentState;
            CurrentState = newState;
            GameLogger.Log(LogCategory.Game, $"State changed: {previous} -> {newState}");
            EventBus.Publish(new GameStateChangedEvent(previous, newState));
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}

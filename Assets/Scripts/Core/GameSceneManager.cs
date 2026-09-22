using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// Wraps UnityEngine.SceneManagement so gameplay code goes through one place
    /// for scene transitions (SPEC.md section 47). Named GameSceneManager to avoid
    /// colliding with UnityEngine.SceneManagement.SceneManager.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameSceneManager : MonoBehaviour
    {
        public static GameSceneManager Instance { get; private set; }

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
        }

        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                GameLogger.LogError(LogCategory.Game, "LoadScene called with a null or empty scene name.", this);
                return;
            }

            GameLogger.Log(LogCategory.Game, $"Loading scene '{sceneName}' (sync).");
            SceneManager.LoadScene(sceneName);
        }

        public void LoadSceneAsync(string sceneName, Action onComplete = null)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                GameLogger.LogError(LogCategory.Game, "LoadSceneAsync called with a null or empty scene name.", this);
                return;
            }

            StartCoroutine(LoadSceneAsyncRoutine(sceneName, onComplete));
        }

        private System.Collections.IEnumerator LoadSceneAsyncRoutine(string sceneName, Action onComplete)
        {
            GameLogger.Log(LogCategory.Game, $"Loading scene '{sceneName}' (async).");
            var operation = SceneManager.LoadSceneAsync(sceneName);
            if (operation == null)
            {
                GameLogger.LogFallback(LogCategory.Game, $"LoadSceneAsync for '{sceneName}'", "GameSceneManager.LoadSceneAsyncRoutine",
                    "Scene is not in Build Settings or the name is invalid.", "Aborting load, staying on current scene.", this);
                yield break;
            }

            while (!operation.isDone)
            {
                yield return null;
            }

            GameLogger.Log(LogCategory.Game, $"Scene '{sceneName}' loaded.");
            onComplete?.Invoke();
        }
    }
}

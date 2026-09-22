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

        /// <summary>
        /// True while a scene load is in flight. Static because the things that need
        /// to know are not holding a reference to this and must not have to: SPEC.md
        /// section 54's edge case 20 (changing graphics settings during loading) is
        /// answered by <see cref="SettingsManager"/> deferring the resolution change
        /// until this goes false, and a resolution change mid-load is precisely the
        /// one that leaves the new scene's canvas laid out for the old screen.
        /// </summary>
        public static bool IsLoading { get; private set; }

        /// <summary>
        /// Test seam. A test for "what happens during a load" cannot start a real
        /// scene load without taking the test runner's own scene with it, so it says
        /// a load is in flight instead.
        /// </summary>
        public static void SetLoadingForTests(bool loading) => IsLoading = loading;

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

            // A synchronous load blocks, so nothing can read this in between — but a
            // component's OnDestroy and the new scene's Awake both run inside the
            // call, and those can ask.
            IsLoading = true;
            try
            {
                SceneManager.LoadScene(sceneName);
            }
            finally
            {
                IsLoading = false;
            }
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

            IsLoading = true;

            while (!operation.isDone)
            {
                yield return null;
            }

            IsLoading = false;

            GameLogger.Log(LogCategory.Game, $"Scene '{sceneName}' loaded.");
            onComplete?.Invoke();
        }
    }
}

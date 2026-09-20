using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// Toggles a pause panel on the Pause input action and routes state changes
    /// through GameManager (SPEC.md TASK 001).
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private GameObject pausePanel;

        private InputAction pauseAction;

        private void Awake()
        {
            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }

            if (inputActions == null)
            {
                GameLogger.LogError(LogCategory.UI, "No InputActionAsset assigned to PauseMenu.", this);
                enabled = false;
                return;
            }

            var gameplayMap = inputActions.FindActionMap("Gameplay");
            pauseAction = gameplayMap?.FindAction("Pause");
        }

        private void OnEnable()
        {
            pauseAction?.Enable();
            if (pauseAction != null)
            {
                pauseAction.performed += OnPausePressed;
            }
        }

        private void OnDisable()
        {
            if (pauseAction != null)
            {
                pauseAction.performed -= OnPausePressed;
            }

            pauseAction?.Disable();
        }

        private void OnPausePressed(InputAction.CallbackContext context)
        {
            if (GameManager.Instance == null)
            {
                GameLogger.LogWarning(LogCategory.UI, "Pause pressed but no GameManager instance exists.", this);
                return;
            }

            if (GameManager.Instance.CurrentState == GameState.Paused)
            {
                GameManager.Instance.Resume();
                SetPanelActive(false);
            }
            else if (GameManager.Instance.CurrentState == GameState.Playing)
            {
                GameManager.Instance.Pause();
                SetPanelActive(true);
            }
        }

        private void SetPanelActive(bool active)
        {
            if (pausePanel != null)
            {
                pausePanel.SetActive(active);
            }
        }

        /// <summary>Wired to a Resume button's OnClick in the Inspector.</summary>
        public void OnResumeButtonPressed()
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Paused)
            {
                return;
            }

            GameManager.Instance.Resume();
            SetPanelActive(false);
        }
    }
}

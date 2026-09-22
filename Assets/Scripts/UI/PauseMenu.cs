using Game.Core;
using Game.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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

        [Tooltip("Selected automatically when the panel opens, so a gamepad (with no cursor) has a button to move from (SPEC.md section 43).")]
        [SerializeField] private Button resumeButton;

        [Tooltip("Scene the 'Main Menu' button loads (SPEC.md section 42's Main Menu screen).")]
        [SerializeField] private string mainMenuScene = "MainMenu";

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

            EventBus.Subscribe<AutoPauseRequestedEvent>(OnAutoPauseRequested);
        }

        private void OnDisable()
        {
            if (pauseAction != null)
            {
                pauseAction.performed -= OnPausePressed;
            }

            pauseAction?.Disable();
            EventBus.Unsubscribe<AutoPauseRequestedEvent>(OnAutoPauseRequested);
        }

        /// <summary>
        /// A pause the player did not ask for: the controller was unplugged, or the
        /// window lost focus (SPEC.md section 54's edge cases 21 and 23). It opens the
        /// ordinary menu, so there is always something on screen explaining why the
        /// game stopped and a button to carry on with.
        /// </summary>
        private void OnAutoPauseRequested(AutoPauseRequestedEvent request) => OpenPause();

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
                OpenPause();
            }
        }

        /// <summary>
        /// Pauses and shows the menu. Public so anything that needs to stop the game
        /// with something on screen goes through one path — see
        /// <see cref="OnAutoPauseRequested"/> — rather than setting the state and
        /// leaving a frozen game with no menu on it.
        /// </summary>
        public void OpenPause()
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            GameManager.Instance.Pause();
            SetPanelActive(true);

            if (EventSystem.current != null && resumeButton != null)
            {
                EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
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

        /// <summary>Wired to a Save button's OnClick in the Inspector (SPEC.md section 31's manual save).</summary>
        public void OnSaveButtonPressed()
        {
            SaveManager.Instance?.Save(SaveSlot.Manual);
        }

        /// <summary>Wired to a Main Menu button's OnClick in the Inspector.</summary>
        public void OnMainMenuButtonPressed()
        {
            Time.timeScale = 1f;

            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            {
                GameManager.Instance.Resume();
            }

            SetPanelActive(false);
            GameSceneManager.Instance?.LoadScene(mainMenuScene);
        }
    }
}

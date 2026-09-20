using Game.Core;
using Game.Dialogue;
using Game.World;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Takes control away from the player while a conversation is running and gives it
    /// back afterwards (SPEC.md TASK 003).
    ///
    /// This lives with the player rather than in the dialogue UI because it is a
    /// gameplay concern: the UI should be replaceable without changing who can move.
    /// It restores each component to the state it was actually in, so it cannot
    /// re-enable a controller that something else — death, a cutscene — had disabled.
    /// </summary>
    public class PlayerDialogueLock : MonoBehaviour
    {
        [SerializeField] private PlayerController locomotion;
        [SerializeField] private PlayerCamera cameraRig;
        [SerializeField] private Game.Combat.CombatController combat;
        [SerializeField] private PlayerInteractor interactor;

        [Tooltip("Show and unlock the cursor during dialogue so choices can be clicked.")]
        [SerializeField] private bool releaseCursor = true;

        private bool locked;
        private bool locomotionWasEnabled;
        private bool cameraWasEnabled;
        private bool combatWasEnabled;
        private CursorLockMode previousLockState;
        private bool previousCursorVisible;

        private void Awake()
        {
            if (locomotion == null)
            {
                locomotion = GetComponent<PlayerController>();
            }

            if (combat == null)
            {
                combat = GetComponent<Game.Combat.CombatController>();
            }

            if (interactor == null)
            {
                interactor = GetComponent<PlayerInteractor>();
            }

            if (cameraRig == null)
            {
                cameraRig = FindAnyObjectByType<PlayerCamera>();
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Subscribe<DialogueCompletedEvent>(OnDialogueCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Unsubscribe<DialogueCompletedEvent>(OnDialogueCompleted);

            // Leaving the player frozen because this component was disabled mid-
            // conversation would be a softlock (SPEC.md section 55).
            Unlock();
        }

        private void OnDialogueStarted(DialogueStartedEvent started)
        {
            Lock();
        }

        private void OnDialogueCompleted(DialogueCompletedEvent completed)
        {
            Unlock();
        }

        public void Lock()
        {
            if (locked)
            {
                return;
            }

            locked = true;

            locomotionWasEnabled = locomotion != null && locomotion.enabled;
            cameraWasEnabled = cameraRig != null && cameraRig.enabled;
            combatWasEnabled = combat != null && combat.enabled;

            if (locomotion != null)
            {
                locomotion.enabled = false;
            }

            if (cameraRig != null)
            {
                cameraRig.enabled = false;
            }

            if (combat != null)
            {
                combat.enabled = false;
            }

            if (interactor != null)
            {
                interactor.InteractionSuspended = true;
            }

            if (releaseCursor)
            {
                previousLockState = Cursor.lockState;
                previousCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            GameLogger.Log(LogCategory.Player, "Player control suspended for dialogue.", this);
        }

        public void Unlock()
        {
            if (!locked)
            {
                return;
            }

            locked = false;

            if (locomotion != null)
            {
                locomotion.enabled = locomotionWasEnabled;
            }

            if (cameraRig != null)
            {
                cameraRig.enabled = cameraWasEnabled;
            }

            if (combat != null)
            {
                combat.enabled = combatWasEnabled;
            }

            if (interactor != null)
            {
                interactor.InteractionSuspended = false;
            }

            if (releaseCursor)
            {
                Cursor.lockState = previousLockState;
                Cursor.visible = previousCursorVisible;
            }

            GameLogger.Log(LogCategory.Player, "Player control restored after dialogue.", this);
        }
    }
}

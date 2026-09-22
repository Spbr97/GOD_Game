using Game.Core;
using Game.Save;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Shows the exact sentence SPEC.md section 32 requires when a corrupt save was
    /// replaced by its backup, and a shorter one when a save could not be written or
    /// read at all. Lives in a gameplay scene's canvas next to <see cref="HudUI"/>,
    /// because the event it reacts to is published by the <see cref="SaveManager"/>
    /// that just woke up in that scene.
    ///
    /// Also carries the out-of-world recovery message (SPEC.md section 50's invalid
    /// player position). That is the same kind of sentence — "something went wrong
    /// and the game put it right" — so it belongs in the same place rather than in a
    /// second banner that could overlap this one.
    /// </summary>
    public class RecoveryMessageUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text label;
        [SerializeField] private float visibleSeconds = 6f;

        private float hideAt;

        private void OnEnable()
        {
            EventBus.Subscribe<SaveRecoveredFromBackupEvent>(OnRecovered);
            EventBus.Subscribe<SaveFailedEvent>(OnFailed);
            EventBus.Subscribe<Game.World.OutOfWorldRecoveryEvent>(OnOutOfWorld);
            EventBus.Subscribe<InputDeviceChangedEvent>(OnDeviceChanged);
            SetVisible(false);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SaveRecoveredFromBackupEvent>(OnRecovered);
            EventBus.Unsubscribe<SaveFailedEvent>(OnFailed);
            EventBus.Unsubscribe<Game.World.OutOfWorldRecoveryEvent>(OnOutOfWorld);
            EventBus.Unsubscribe<InputDeviceChangedEvent>(OnDeviceChanged);
        }

        private void Update()
        {
            if (hideAt > 0f && Time.unscaledTime >= hideAt)
            {
                hideAt = 0f;
                SetVisible(false);
            }
        }

        private void OnRecovered(SaveRecoveredFromBackupEvent recovered) => Show(recovered.PlayerMessage);

        private void OnFailed(SaveFailedEvent failed) => Show(failed.PlayerMessage);

        /// <summary>
        /// Only the player's own recovery is worth a banner. An enemy that fell
        /// through the floor and was put back publishes the same event with an empty
        /// message, because the player neither caused it nor can act on it.
        /// </summary>
        private void OnOutOfWorld(Game.World.OutOfWorldRecoveryEvent recovery)
        {
            if (recovery.IsPlayer && !string.IsNullOrEmpty(recovery.PlayerMessage))
            {
                Show(recovery.PlayerMessage);
            }
        }

        /// <summary>A controller came or went (SPEC.md section 54's edge cases 21 and 22).</summary>
        private void OnDeviceChanged(InputDeviceChangedEvent changed) => Show(changed.PlayerMessage);

        private void Show(string message)
        {
            if (label != null)
            {
                label.text = message;
            }

            SetVisible(true);
            hideAt = Time.unscaledTime + visibleSeconds;
        }

        private void SetVisible(bool visible)
        {
            if (root != null)
            {
                root.SetActive(visible);
            }
        }
    }
}

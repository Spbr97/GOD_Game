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
            SetVisible(false);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SaveRecoveredFromBackupEvent>(OnRecovered);
            EventBus.Unsubscribe<SaveFailedEvent>(OnFailed);
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

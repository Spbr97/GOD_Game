using Game.Core;
using Game.World;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Shows "Speak to Queen Amara" when something is in range (SPEC.md section 27).
    /// Driven entirely by <see cref="InteractionTargetChangedEvent"/>, so it does no
    /// per-frame work when nothing is nearby.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private Text promptLabel;

        [Tooltip("Key shown alongside the prompt. Cosmetic; rebinding is not implemented.")]
        [SerializeField] private string keyHint = "E";

        private void Awake()
        {
            Show(null);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<InteractionTargetChangedEvent>(OnTargetChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InteractionTargetChangedEvent>(OnTargetChanged);
            Show(null);
        }

        private void OnTargetChanged(InteractionTargetChangedEvent changed)
        {
            Show(changed.Target);
        }

        private void Show(Interactable target)
        {
            var visible = target != null;

            if (promptRoot != null)
            {
                promptRoot.SetActive(visible);
            }

            if (visible && promptLabel != null)
            {
                promptLabel.text = string.IsNullOrEmpty(keyHint)
                    ? target.Prompt
                    : $"[{keyHint}]  {target.Prompt}";
            }
        }
    }
}

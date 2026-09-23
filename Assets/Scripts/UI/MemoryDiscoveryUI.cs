using Game.Core;
using Game.Memory;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The banner shown when a memory fragment is recovered (SPEC.md sections 19 and
    /// 70). A flat panel standing in for the memory visualization described in section
    /// 70 — the text is the content, the presentation is placeholder.
    /// </summary>
    public class MemoryDiscoveryUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text bodyLabel;
        [SerializeField] private Image accent;

        [SerializeField] private float displaySeconds = 6f;

        private float hideAt;

        private void Awake()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<MemoryDiscoveredEvent>(OnMemoryDiscovered);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<MemoryDiscoveredEvent>(OnMemoryDiscovered);
        }

        private void Update()
        {
            if (hideAt > 0f && Time.unscaledTime >= hideAt)
            {
                hideAt = 0f;
                if (panel != null)
                {
                    panel.SetActive(false);
                }
            }
        }

        private void OnMemoryDiscovered(MemoryDiscoveredEvent discovered)
        {
            var memory = discovered.Memory;

            if (titleLabel != null)
            {
                titleLabel.text = $"Memory recovered — {memory.Title}";
            }

            if (bodyLabel != null)
            {
                var integrity = MemoryManager.Instance?.Integrity ?? 1f;
                var text = memory.Description;
                if (!memory.IsProtected && integrity < 0.5f && !string.IsNullOrEmpty(text))
                {
                    text = text.Substring(0, Mathf.Max(1, text.Length / 2)) + "… [memory incomplete]";
                }
                bodyLabel.text = text;
            }

            if (accent != null)
            {
                accent.color = memory.VisualTint;
            }

            if (panel != null)
            {
                panel.SetActive(true);
            }

            hideAt = Time.unscaledTime + displaySeconds;
        }
    }
}

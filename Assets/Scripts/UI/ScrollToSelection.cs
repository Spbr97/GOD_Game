using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Keeps the focused control inside the visible part of a scrolling list
    /// (STANDALONE_RELEASE_ROADMAP.md TASK 054).
    ///
    /// Unity's navigation happily moves selection onto a control that is scrolled out of
    /// sight, and the <see cref="ScrollRect"/> does not follow. On a mouse that is merely
    /// untidy; on a gamepad it is the list going dead — the player presses down, nothing
    /// appears to happen, and the thing that moved is off-screen. That is the shape of the
    /// "gamepad navigation onto dynamically built rows is unwired" entry KNOWN_ISSUES.md
    /// has carried since TASK 010.
    ///
    /// Polling the EventSystem rather than listening: selection changes come from the mouse,
    /// from navigation, and from scripts calling <c>SetSelectedGameObject</c>, and there is
    /// no one event that covers all three. The check is two comparisons on a panel that is
    /// only open while the game is paused.
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public class ScrollToSelection : MonoBehaviour
    {
        [Tooltip("Extra space kept above and below the focused row, in canvas units.")]
        [SerializeField] private float margin = 24f;

        private ScrollRect scroll;
        private GameObject lastSelected;

        private void Awake() => scroll = GetComponent<ScrollRect>();

        private void OnDisable() => lastSelected = null;

        private void Update()
        {
            var current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (current == null || current == lastSelected)
            {
                return;
            }

            lastSelected = current;
            Reveal(current.transform as RectTransform);
        }

        /// <summary>
        /// Scrolls the minimum distance that brings <paramref name="target"/> fully into
        /// view. Public so a test can ask for it without driving the EventSystem.
        /// </summary>
        public void Reveal(RectTransform target)
        {
            if (scroll == null)
            {
                scroll = GetComponent<ScrollRect>();
            }

            if (target == null || scroll == null || scroll.content == null || scroll.viewport == null
                || !target.IsChildOf(scroll.content))
            {
                return;
            }

            var contentHeight = scroll.content.rect.height;
            var viewHeight = scroll.viewport.rect.height;
            if (contentHeight <= viewHeight)
            {
                // Nothing to scroll. Guarded because the division below would be by zero.
                return;
            }

            // Distance from the top of the content down to the top and bottom of the row.
            var offset = scroll.content.InverseTransformPoint(target.position).y
                         - scroll.content.rect.yMax
                         + target.rect.yMax;
            var top = -offset + margin;
            var bottom = top + target.rect.height + margin * 2f;

            var scrollable = contentHeight - viewHeight;
            var viewTop = (1f - scroll.verticalNormalizedPosition) * scrollable;

            float newTop;
            if (top < viewTop)
            {
                newTop = top;
            }
            else if (bottom > viewTop + viewHeight)
            {
                newTop = bottom - viewHeight;
            }
            else
            {
                return;
            }

            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(newTop / scrollable);
        }
    }
}

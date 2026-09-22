using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// SPEC.md section 43's "text scaling" (and, since a subtitle is UI text like any
    /// other, its "subtitle size" too): scales this Canvas's <see cref="CanvasScaler"/>
    /// uniformly rather than every individual <c>Text</c> component's font size, so
    /// one setting affects the whole screen consistently, icons included.
    ///
    /// Settings only ever change in the Main Menu today (no gameplay scene offers a
    /// Settings screen yet — see KNOWN_ISSUES.md), so applying once on enable is
    /// enough; there is no live value to re-poll while a gameplay Canvas is loaded.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public class UIScaleApplier : MonoBehaviour
    {
        private CanvasScaler scaler;
        private float baseScaleFactor;

        private void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            baseScaleFactor = scaler.scaleFactor;
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Apply()
        {
            var textScale = SettingsManager.Instance != null ? SettingsManager.Instance.Current.TextScale : 1f;
            scaler.scaleFactor = baseScaleFactor * Mathf.Max(0.1f, textScale);
        }
    }
}

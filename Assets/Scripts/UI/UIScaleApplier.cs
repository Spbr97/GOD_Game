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
    /// Applied on enable and again on <see cref="TextScaleChangedEvent"/>. The event half
    /// was added with the in-game Settings panel (TASK 054): until then every settings
    /// change happened in the Main Menu, so every gameplay canvas was created after it and
    /// enable alone was enough. Now the canvas being rescaled is the one the player is
    /// looking at while they move the slider.
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
            EventBus.Subscribe<TextScaleChangedEvent>(OnTextScaleChanged);
            Apply();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TextScaleChangedEvent>(OnTextScaleChanged);
        }

        private void OnTextScaleChanged(TextScaleChangedEvent changed)
        {
            scaler.scaleFactor = baseScaleFactor * Mathf.Max(0.1f, changed.Scale);
        }

        private void Apply()
        {
            var textScale = SettingsManager.Instance != null ? SettingsManager.Instance.Current.TextScale : 1f;
            scaler.scaleFactor = baseScaleFactor * Mathf.Max(0.1f, textScale);
        }
    }
}

using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// SPEC.md section 43's "text scaling" (and, since a subtitle is UI text like any
    /// other, its "subtitle size" too): scales this Canvas's <see cref="CanvasScaler"/>
    /// uniformly rather than every individual <c>Text</c> component's font size, so one
    /// setting affects the whole screen consistently, icons included.
    ///
    /// **This did nothing at all until 24 October 2026.** It set
    /// <see cref="CanvasScaler.scaleFactor"/>, which Unity only reads in
    /// <see cref="CanvasScaler.ScaleMode.ConstantPixelSize"/> mode — and both canvases in
    /// this game are <see cref="CanvasScaler.ScaleMode.ScaleWithScreenSize"/>, where the
    /// scaler recomputes the factor from the reference resolution on every enable and
    /// overwrites whatever was assigned. Measured rather than deduced: asking for 1.5×
    /// left <c>canvas.scaleFactor</c> at 0.4000813 in the Main Menu and in Avarsha alike.
    ///
    /// The setting had existed since TASK 017, was listed as delivered, and appeared in the
    /// Settings panel with a working slider that saved a value nothing could use. Nothing
    /// caught it because every check asked whether the value was stored, and storing it was
    /// the half that worked.
    ///
    /// Under ScaleWithScreenSize the way to make the UI bigger is to tell the scaler the
    /// screen is *smaller* than it is: halve the reference resolution and everything on the
    /// canvas doubles. So that is what this does, and which branch it takes depends on the
    /// mode rather than on an assumption about it.
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
        private Vector2 baseReferenceResolution;
        private bool captured;

        private void Awake() => Capture();

        private void OnEnable()
        {
            EventBus.Subscribe<TextScaleChangedEvent>(OnTextScaleChanged);
            Apply(SettingsManager.Instance != null ? SettingsManager.Instance.Current.TextScale : 1f);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TextScaleChangedEvent>(OnTextScaleChanged);
        }

        private void OnTextScaleChanged(TextScaleChangedEvent changed) => Apply(changed.Scale);

        /// <summary>
        /// Remembers the authored scale so every later change is measured from it rather
        /// than compounding on the last one — applying 1.5 twice must not give 2.25.
        /// </summary>
        private void Capture()
        {
            if (captured)
            {
                return;
            }

            scaler = GetComponent<CanvasScaler>();
            baseScaleFactor = scaler.scaleFactor;
            baseReferenceResolution = scaler.referenceResolution;
            captured = true;
        }

        /// <summary>
        /// Applies a scale. Public and callable without a <see cref="SettingsManager"/>, so
        /// a test and the editor capture tool can ask for a size and see what the player
        /// would see.
        /// </summary>
        public void Apply(float textScale)
        {
            Capture();
            var scale = Mathf.Max(0.1f, textScale);

            if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                // A smaller reference resolution means "pretend the screen is smaller",
                // and everything laid out against it grows to fill the real one.
                scaler.referenceResolution = baseReferenceResolution / scale;
            }
            else
            {
                scaler.scaleFactor = baseScaleFactor * scale;
            }
        }

        /// <summary>
        /// What the canvas is actually scaled by right now. Exposed because the bug this
        /// component carried for four tasks was invisible from the outside: the setting was
        /// stored, the slider moved, and nothing could tell you the screen had not changed.
        /// </summary>
        public float EffectiveScale
        {
            get
            {
                Capture();
                return scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
                    ? baseReferenceResolution.x / Mathf.Max(1f, scaler.referenceResolution.x)
                    : scaler.scaleFactor / Mathf.Max(0.0001f, baseScaleFactor);
            }
        }
    }
}

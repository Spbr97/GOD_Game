using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// What every screen in the game is allowed to look like (STANDALONE_RELEASE_ROADMAP.md
    /// TASK 054).
    ///
    /// Before this existed, four screens each carried their own copy of
    /// <c>Resources.GetBuiltinResource&lt;Font&gt;("LegacyRuntime.ttf")</c> and about forty
    /// colour literals were spread across the UI folder — four greys that were meant to be
    /// the same grey, two greens that were meant to be the same green. That is not a
    /// styling complaint: it means a change to "the muted colour" is a change to a dozen
    /// call sites, so in practice nobody makes it and the screens drift apart. One table
    /// makes a palette decision a one-line edit and makes it testable.
    ///
    /// The colours carry **meaning**, not decoration, and the names say which meaning:
    /// <see cref="Positive"/> is "done, owned, reached", <see cref="Negative"/> is "failed",
    /// <see cref="Caution"/> is "altered, unreliable". Nothing in the game may rely on the
    /// colour alone (SPEC.md section 43) — every one of these is paired with a word or a
    /// glyph at its call site — but a palette that collapses under colour blindness still
    /// costs a player the quick read, so <c>UiThemeTests</c> runs these pairs through
    /// simulated dichromacy and holds them apart on luminance, which no dichromacy removes.
    ///
    /// Deliberately a static class and not a ScriptableObject. Half the UI that needs it is
    /// built at runtime by code that has no serialized fields to hang an asset reference
    /// on (<see cref="JournalUI"/>'s rows, <see cref="MapUI"/>'s markers), and a theme asset
    /// that one screen forgets to reference is exactly the drift this replaces.
    /// </summary>
    public static class UiTheme
    {
        // ------------------------------------------------------------------- surfaces

        /// <summary>The dim laid over the world behind a full-screen panel.</summary>
        public static readonly Color Scrim = new(0.04f, 0.04f, 0.06f, 0.78f);

        /// <summary>A panel's own background.</summary>
        public static readonly Color Panel = new(0.08f, 0.075f, 0.10f, 0.95f);

        /// <summary>A block inside a panel: a header strip, a selected row, a tab bar.</summary>
        public static readonly Color PanelRaised = new(0.13f, 0.12f, 0.16f, 0.95f);

        /// <summary>A hairline between sections. Thin enough that it reads as structure, not as a box.</summary>
        public static readonly Color Divider = new(0.34f, 0.29f, 0.20f, 0.55f);

        // ----------------------------------------------------------------------- text

        /// <summary>Screen and section titles. The gold the rest of the game's fire imagery already uses.</summary>
        public static readonly Color Heading = new(0.95f, 0.82f, 0.45f);

        /// <summary>Ordinary readable text.</summary>
        public static readonly Color TextPrimary = new(0.93f, 0.93f, 0.95f);

        /// <summary>Supporting text: descriptions, hints, "nothing here yet".</summary>
        public static readonly Color TextMuted = new(0.63f, 0.63f, 0.67f);

        // ------------------------------------------------------------------- meaning

        /// <summary>Done, owned, unlocked, reached.</summary>
        public static readonly Color Positive = new(0.52f, 0.88f, 0.56f);

        /// <summary>Failed, lost, refused.</summary>
        public static readonly Color Negative = new(0.92f, 0.42f, 0.42f);

        /// <summary>Altered or unreliable — a false memory, an unsaved run.</summary>
        public static readonly Color Caution = new(0.96f, 0.72f, 0.32f);

        /// <summary>The player themselves, and the thing the player is being pointed at.</summary>
        public static readonly Color Accent = new(0.45f, 0.85f, 1f);

        /// <summary>A memory that was taken and changed (SPEC.md section 70).</summary>
        public static readonly Color MemoryCorrupted = new(0.78f, 0.45f, 0.92f);

        /// <summary>A memory that is gone.</summary>
        public static readonly Color MemoryForgotten = new(0.50f, 0.50f, 0.54f);

        /// <summary>A memory that was put back.</summary>
        public static readonly Color MemoryRestored = new(0.45f, 0.85f, 0.95f);

        // -------------------------------------------------------------------- buttons

        public static readonly Color ButtonNormal = new(0.16f, 0.15f, 0.20f, 0.95f);

        /// <summary>
        /// Where the keyboard or pad focus is. Bright enough to find without hunting, and
        /// no brighter: at (0.30, 0.26, 0.15) it put muted text at 3.8:1 against it, which
        /// <c>UiLayoutTests</c> caught. Focus has to be findable and the words on it still
        /// have to be readable.
        /// </summary>
        public static readonly Color ButtonFocused = new(0.26f, 0.22f, 0.12f, 1f);

        /// <summary>A button that cannot be pressed, and visibly cannot.</summary>
        public static readonly Color ButtonDisabled = new(0.13f, 0.13f, 0.15f, 0.55f);

        /// <summary>A button that spends something or commits: Unlock, New Game.</summary>
        public static readonly Color ButtonConfirm = new(0.16f, 0.34f, 0.19f, 0.95f);

        // -------------------------------------------------------------------- metrics

        // Sized against the 1920x1080 reference the Canvas scaler uses, then checked by
        // rendering the screens rather than by reasoning about the numbers. The first pass
        // used a 34px title and a 36px button, which read as correct in the Inspector and
        // came out looking like a debug overlay: at the reference resolution a menu button
        // a player is meant to hit with a thumbstick cursor wants to be around 50px tall,
        // not the height of a line of body text.
        public const int FontSizeTitle = 52;
        public const int FontSizeHeading = 28;
        public const int FontSizeBody = 22;
        public const int FontSizeSmall = 18;

        /// <summary>Vertical space one list row occupies.</summary>
        public const float RowHeight = 34f;

        /// <summary>The gap between two related controls. Everything else is a multiple of it.</summary>
        public const float Spacing = 10f;

        /// <summary>The breathing room between a panel's edge and its content.</summary>
        public const float PanelPadding = 32f;

        /// <summary>How far an indented child row sits from its parent's text.</summary>
        public const float Indent = 24f;

        public const float ButtonHeight = 52f;
        public const float ButtonWidth = 340f;

        // ----------------------------------------------------------------------- font

        private static Font cachedFont;

        /// <summary>
        /// The font every screen uses.
        ///
        /// Lazy rather than a field initializer because <c>Resources.GetBuiltinResource</c>
        /// refuses to run during static construction on a serialization thread, which is
        /// where a field initializer can end up being evaluated. Each screen used to carry
        /// its own copy of this property and its own copy of this comment.
        /// </summary>
        public static Font Font =>
            cachedFont != null ? cachedFont : (cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        /// <summary>What a piece of text is for, which decides its size and weight.</summary>
        public enum TextRole
        {
            /// <summary>The name of a screen.</summary>
            Title,

            /// <summary>The name of a section within a screen.</summary>
            Heading,

            /// <summary>Ordinary text.</summary>
            Body,

            /// <summary>Supporting text, one step quieter than body.</summary>
            Muted,

            /// <summary>A button label or a tight annotation.</summary>
            Small
        }

        /// <summary>
        /// Applies a role's font, size, weight and colour to an existing label, leaving its
        /// alignment and its <c>RectTransform</c> alone — a caller that has already placed
        /// a label is not asking for it to be moved.
        /// </summary>
        public static void Apply(Text label, TextRole role)
        {
            if (label == null)
            {
                return;
            }

            label.font = Font;
            label.fontSize = SizeFor(role);
            label.fontStyle = role == TextRole.Title || role == TextRole.Heading ? FontStyle.Bold : FontStyle.Normal;
            label.color = ColourFor(role);
        }

        public static int SizeFor(TextRole role) => role switch
        {
            TextRole.Title => FontSizeTitle,
            TextRole.Heading => FontSizeHeading,
            TextRole.Small => FontSizeSmall,
            _ => FontSizeBody
        };

        public static Color ColourFor(TextRole role) => role switch
        {
            TextRole.Title => Heading,
            TextRole.Heading => Heading,
            TextRole.Muted => TextMuted,
            _ => TextPrimary
        };

        /// <summary>
        /// Gives a <see cref="Button"/> the palette's four states.
        ///
        /// Worth doing explicitly rather than leaving Unity's defaults: the default
        /// highlighted colour is a slight tint of white, which on a dark panel is nearly
        /// invisible — and a player on a gamepad, with no cursor, has nothing but that tint
        /// to tell them where they are. <see cref="ButtonFocused"/> is deliberately loud.
        /// </summary>
        public static void Apply(Button button, bool confirm = false)
        {
            if (button == null)
            {
                return;
            }

            var normal = confirm ? ButtonConfirm : ButtonNormal;
            var colours = button.colors;
            colours.normalColor = normal;
            colours.highlightedColor = ButtonFocused;
            colours.selectedColor = ButtonFocused;
            colours.pressedColor = normal * 0.8f;
            colours.disabledColor = ButtonDisabled;
            colours.fadeDuration = 0.08f;
            button.colors = colours;

            if (button.targetGraphic is Image image)
            {
                // Transition.ColorTint multiplies the target graphic's own colour, so a
                // graphic left at some other tint silently shifts every state with it.
                image.color = Color.white;
            }
        }

        /// <summary>
        /// Luminance as a human eye weights it (Rec. 709). The one channel of a colour that
        /// no form of colour blindness takes away, which is why the theme's tests assert on
        /// it rather than on how far apart two hues look to someone with full colour vision.
        /// </summary>
        public static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
    }
}

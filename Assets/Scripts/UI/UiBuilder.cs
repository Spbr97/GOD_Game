using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Builds themed controls at runtime (STANDALONE_RELEASE_ROADMAP.md TASK 054).
    ///
    /// **Why the menus are built in code rather than authored in a scene.** The Settings
    /// and Pause screens were laid out by hand, by a one-off editor script nobody kept, and
    /// the result had two defects that survived every check this project has: the pause
    /// title was drawn on top of its own Resume and Save buttons, and a slider's fill had
    /// escaped its track into a green rectangle covering the Normal difficulty button. Both
    /// are invisible in C#, invisible in scene YAML, and invisible to a test that asks
    /// whether a reference is wired — they are only visible if you look, and nobody could.
    ///
    /// The cause in both cases was a hand-written <c>anchoredPosition</c>. So the fix is not
    /// to correct those two numbers, it is to stop writing them: everything here is placed
    /// by a <see cref="VerticalLayoutGroup"/> or a <see cref="HorizontalLayoutGroup"/>, and
    /// a layout group cannot overlap its own children. A whole class of defect goes away
    /// rather than one instance of it.
    ///
    /// The second reason is duplication. The Settings screen has to exist in the Main Menu
    /// *and* behind Pause in two gameplay scenes, and three authored copies would need
    /// three edits every time a setting is added — which is how two of them end up stale.
    /// One builder, called from wherever the panel is needed, cannot drift from itself.
    ///
    /// Nothing here loads a sprite. Images are untextured quads tinted from
    /// <see cref="UiTheme"/>, which is honest about where this project is: TASK 053 owns
    /// the art, and a flat panel that is laid out correctly is a better starting point for
    /// it than a textured one that is not.
    /// </summary>
    public static class UiBuilder
    {
        /// <summary>A full-screen stretched child, which is what every panel root wants.</summary>
        public static RectTransform FullScreen(Transform parent, string name)
        {
            var rect = NewRect(parent, name);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>
        /// The dim over the world behind an in-game panel.
        ///
        /// Not decoration: a paused game and a running game look identical in a screenshot
        /// without it, and "is this frozen or did it crash" is a real question a player asks
        /// of a game that stops. It also raises the contrast under the text, which is the
        /// difference between a readable menu and one competing with whatever happens to be
        /// on screen behind it.
        /// </summary>
        public static Image Scrim(Transform parent)
        {
            var rect = FullScreen(parent, "Scrim");
            var image = rect.gameObject.AddComponent<Image>();
            image.color = UiTheme.Scrim;

            // Eats clicks aimed at the world behind the panel.
            image.raycastTarget = true;
            return image;
        }

        /// <summary>
        /// A centred panel that sizes itself to its contents and lays them out in a column.
        /// <paramref name="width"/> is fixed because a settings row that reflows as its
        /// longest translation changes is a layout that has to be re-checked per language.
        /// </summary>
        public static RectTransform Panel(Transform parent, string name, float width, bool background = true)
        {
            var rect = NewRect(parent, name);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, 0f);

            if (background)
            {
                var image = rect.gameObject.AddComponent<Image>();
                image.color = UiTheme.Panel;
            }

            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(
                (int)UiTheme.PanelPadding, (int)UiTheme.PanelPadding,
                (int)UiTheme.PanelPadding, (int)UiTheme.PanelPadding);
            layout.spacing = UiTheme.Spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }

        /// <summary>
        /// A panel that fills the screen's height instead of sizing to its contents, so a
        /// flexible child inside it absorbs whatever is left over.
        ///
        /// **Why a screen needs this.** <see cref="Panel"/> grows to fit, which is right
        /// for a short screen and wrong for a long one: at the largest text size the
        /// Settings panel grew past the top and bottom of a 1080 screen and took the Back
        /// button with it, so a player who turned the text up could not leave the screen
        /// they had turned it up on. Found by rendering it at 1.5x rather than by reasoning
        /// about it.
        ///
        /// Because the canvas scaler shrinks the reference resolution as text grows, a
        /// panel pinned to the screen's height is pinned to the *right* height at every
        /// scale, with no arithmetic about font sizes anywhere.
        /// </summary>
        public static RectTransform FittedPanel(Transform parent, string name, float width,
            float verticalMargin = 24f)
        {
            var rect = NewRect(parent, name);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, -verticalMargin * 2f);
            rect.anchoredPosition = Vector2.zero;

            rect.gameObject.AddComponent<Image>().color = UiTheme.Panel;

            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(
                (int)UiTheme.PanelPadding, (int)UiTheme.PanelPadding,
                (int)UiTheme.PanelPadding, (int)UiTheme.PanelPadding);
            layout.spacing = UiTheme.Spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        /// <summary>A column of controls inside a panel, with no background of its own.</summary>
        public static RectTransform Column(Transform parent, string name, float spacing = UiTheme.Spacing)
        {
            var rect = NewRect(parent, name);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        /// <summary>
        /// Two or more columns side by side, each laid out as its own vertical column.
        ///
        /// The Settings screen needs this and not a single list: thirteen rows at a size a
        /// player can actually read come to roughly 1180 reference pixels, which does not
        /// fit on a 1080-tall screen. The alternatives were a scroll view — which a gamepad
        /// does not follow focus into without extra work — or smaller text, which is the
        /// wrong thing to shrink on the screen that holds the accessibility options.
        /// </summary>
        public static RectTransform[] Columns(Transform parent, int count, float spacing = UiTheme.Spacing * 3f)
        {
            var host = NewRect(parent, "Columns");
            var layout = host.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = host.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var columns = new RectTransform[count];
            for (var i = 0; i < count; i++)
            {
                columns[i] = Column(host, "Column" + i);
                var size = columns[i].gameObject.AddComponent<LayoutElement>();
                size.flexibleWidth = 1f;
                size.flexibleHeight = 0f;
            }

            return columns;
        }

        /// <summary>
        /// A labelled row: its caption on the left, its control on the right. Returns the
        /// right-hand half for the caller to put a control in.
        ///
        /// The caption is a <see cref="LocalizedLabel"/> rather than typed text, so a
        /// settings row cannot be added without a key — which is the gap that left the
        /// whole Main Menu in English through a task that claimed the text was externalized.
        /// </summary>
        public static RectTransform Row(Transform parent, string captionKey, float captionWidth = 260f)
        {
            var rect = NewRect(parent, "Row_" + captionKey);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = UiTheme.Spacing * 2f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var height = rect.gameObject.AddComponent<LayoutElement>();
            height.minHeight = UiTheme.ButtonHeight;
            height.preferredHeight = UiTheme.ButtonHeight;

            // Explicitly zero, not left at the default -1 "ignore me". A column with fewer
            // rows than its neighbour is stretched to match, and the spare height is handed
            // to whichever children will take it: the first build of this screen had the
            // three-row column's rows at 89px against the eight-row column's 52px, for no
            // reason visible in the code.
            height.flexibleHeight = 0f;

            var caption = Label(rect, captionKey, UiTheme.TextRole.Body, TextAnchor.MiddleLeft);
            var captionSize = caption.gameObject.AddComponent<LayoutElement>();
            captionSize.preferredWidth = captionWidth;
            captionSize.flexibleWidth = 0f;

            var control = NewRect(rect, "Control");
            var controlLayout = control.gameObject.AddComponent<HorizontalLayoutGroup>();
            controlLayout.spacing = UiTheme.Spacing;
            controlLayout.childAlignment = TextAnchor.MiddleRight;
            controlLayout.childControlWidth = true;
            controlLayout.childControlHeight = true;

            // Force-expand divides the width evenly and ignores what each child asked for,
            // which gave a slider the same width as the number beside it. With it off, the
            // readout takes its preferred width and the slider takes the rest.
            controlLayout.childForceExpandWidth = false;
            controlLayout.childForceExpandHeight = false;

            var controlSize = control.gameObject.AddComponent<LayoutElement>();
            controlSize.flexibleWidth = 1f;
            return control;
        }

        /// <summary>A section heading with a hairline under it, so a long panel reads as groups.</summary>
        public static Text SectionHeader(Transform parent, string key)
        {
            var label = Label(parent, key, UiTheme.TextRole.Heading, TextAnchor.MiddleLeft);
            var size = label.gameObject.AddComponent<LayoutElement>();
            size.minHeight = UiTheme.FontSizeHeading + UiTheme.Spacing * 2f;
            size.preferredHeight = UiTheme.FontSizeHeading + UiTheme.Spacing * 2f;

            var rule = NewRect(parent, "Divider");
            var image = rule.gameObject.AddComponent<Image>();
            image.color = UiTheme.Divider;
            image.raycastTarget = false;
            var ruleSize = rule.gameObject.AddComponent<LayoutElement>();
            ruleSize.minHeight = 2f;
            ruleSize.preferredHeight = 2f;

            return label;
        }

        /// <summary>
        /// A label whose text comes from <paramref name="key"/>. Pass a null key for a label
        /// a script will write into (a value readout, a row built from live data).
        /// </summary>
        public static Text Label(Transform parent, string key, UiTheme.TextRole role = UiTheme.TextRole.Body,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var rect = NewRect(parent, string.IsNullOrEmpty(key) ? "Label" : "Label_" + key);
            var text = rect.gameObject.AddComponent<Text>();
            UiTheme.Apply(text, role);
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            if (!string.IsNullOrEmpty(key))
            {
                rect.gameObject.AddComponent<LocalizedLabel>().Configure(key);
            }

            return text;
        }

        /// <summary>
        /// A paragraph that grows to fit its text. Separate from <see cref="Label"/> because
        /// a fixed-height label silently clips a translation that runs longer than English,
        /// which is the most common way a localized layout breaks.
        /// </summary>
        public static Text Paragraph(Transform parent, string key, UiTheme.TextRole role = UiTheme.TextRole.Body)
        {
            var text = Label(parent, key, role, TextAnchor.UpperLeft);
            var fitter = text.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return text;
        }

        /// <summary>A themed button with a localized caption.</summary>
        public static Button Button(Transform parent, string captionKey, bool confirm = false,
            float width = UiTheme.ButtonWidth)
        {
            var rect = NewRect(parent, "Button_" + captionKey);
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            UiTheme.Apply(button, confirm);

            var size = rect.gameObject.AddComponent<LayoutElement>();
            size.minHeight = UiTheme.ButtonHeight;
            size.preferredHeight = UiTheme.ButtonHeight;
            size.preferredWidth = width;
            size.flexibleWidth = 0f;

            var caption = Label(rect, captionKey, UiTheme.TextRole.Body);
            Stretch(caption.rectTransform);
            return button;
        }

        /// <summary>
        /// A slider with a readout of its own value beside it.
        ///
        /// The readout is the point. SPEC.md section 43 forbids communicating state by
        /// appearance alone, and a bare handle on a track tells a player nothing about
        /// whether they are at 40% or 60% — the number does, and it is also the only way
        /// someone can put a setting back where it was.
        /// </summary>
        public static Slider Slider(Transform parent, float min, float max, out Text readout)
        {
            var rect = NewRect(parent, "Slider");
            var slider = rect.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.minValue = min;
            slider.maxValue = max;

            var size = rect.gameObject.AddComponent<LayoutElement>();
            size.minHeight = 24f;
            size.preferredHeight = 24f;
            size.flexibleHeight = 0f;
            size.minWidth = 120f;
            size.flexibleWidth = 1f;

            var background = NewRect(rect, "Background");
            Stretch(background, new Vector2(0f, 0.3f), new Vector2(1f, 0.7f));
            var backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = UiTheme.PanelRaised;

            var fillArea = NewRect(rect, "Fill Area");
            Stretch(fillArea, new Vector2(0f, 0.3f), new Vector2(1f, 0.7f));
            var fill = NewRect(fillArea, "Fill");
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.sizeDelta = new Vector2(10f, 0f);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = UiTheme.Heading;

            var handleArea = NewRect(rect, "Handle Slide Area");
            Stretch(handleArea);
            var handle = NewRect(handleArea, "Handle");
            handle.sizeDelta = new Vector2(14f, 0f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = UiTheme.TextPrimary;

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;

            var colours = slider.colors;
            colours.normalColor = UiTheme.TextPrimary;
            colours.highlightedColor = UiTheme.Heading;
            colours.selectedColor = UiTheme.Heading;
            colours.pressedColor = UiTheme.Heading;
            slider.colors = colours;

            readout = Label(parent, null, UiTheme.TextRole.Small);
            var readoutSize = readout.gameObject.AddComponent<LayoutElement>();
            readoutSize.preferredWidth = 76f;
            readoutSize.flexibleWidth = 0f;
            return slider;
        }

        /// <summary>
        /// A checkbox that also says the word "On" or "Off".
        ///
        /// The word is not redundant. A tick and an empty box differ only in a small shape
        /// at low contrast; the state of every accessibility option on this screen should
        /// be readable at a glance by the players those options exist for.
        /// </summary>
        public static Toggle Toggle(Transform parent, out Text state)
        {
            var rect = NewRect(parent, "Toggle");
            var toggle = rect.gameObject.AddComponent<UnityEngine.UI.Toggle>();

            var size = rect.gameObject.AddComponent<LayoutElement>();
            size.minHeight = 28f;
            size.preferredHeight = 28f;
            size.flexibleHeight = 0f;
            size.preferredWidth = 28f;
            size.flexibleWidth = 0f;

            var background = NewRect(rect, "Background");
            Stretch(background);
            var backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = UiTheme.PanelRaised;

            var checkmark = NewRect(background, "Checkmark");
            Stretch(checkmark, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f));
            var checkImage = checkmark.gameObject.AddComponent<Image>();
            checkImage.color = UiTheme.Positive;

            toggle.targetGraphic = backgroundImage;
            toggle.graphic = checkImage;

            var colours = toggle.colors;
            colours.normalColor = Color.white;
            colours.highlightedColor = UiTheme.ButtonFocused;
            colours.selectedColor = UiTheme.ButtonFocused;
            toggle.colors = colours;

            state = Label(parent, null, UiTheme.TextRole.Small, TextAnchor.MiddleLeft);
            var stateSize = state.gameObject.AddComponent<LayoutElement>();
            stateSize.preferredWidth = 76f;
            stateSize.flexibleWidth = 0f;
            return toggle;
        }

        /// <summary>
        /// A value the player steps through with one button, with the current value shown
        /// beside it.
        ///
        /// Used instead of a <see cref="Dropdown"/> for resolution, quality and language.
        /// A dropdown opens a floating list that a gamepad has to be taught to navigate and
        /// that can open off the bottom of the screen; a step button is one control, always
        /// in the same place, already reachable by whatever is navigating the panel. The
        /// language picker added in TASK 042 made the same choice for the same reason, and
        /// this makes the whole screen consistent with it.
        /// </summary>
        public static Button Cycler(Transform parent, out Text value)
        {
            value = Label(parent, null, UiTheme.TextRole.Body, TextAnchor.MiddleRight);
            var valueSize = value.gameObject.AddComponent<LayoutElement>();
            valueSize.preferredWidth = 160f;
            valueSize.flexibleWidth = 1f;

            return Button(parent, Game.Core.Localization.StringKeys.MenuLabelChange, width: 150f);
        }

        /// <summary>
        /// A value with a step-back and a step-forward button either side of it.
        ///
        /// Used where the list is long. A single forward-only button is fine for four
        /// difficulties and silly for twenty resolutions: overshoot the one you wanted and
        /// the only way back is nineteen more presses. Two buttons cost one more control
        /// and remove that entirely.
        ///
        /// Still buttons rather than a dropdown, for the reason given on
        /// <see cref="Cycler"/>: a floating list has to be taught to a gamepad and can open
        /// off the bottom of the screen.
        /// </summary>
        public static void Stepper(Transform parent, out Text value, out Button back, out Button forward)
        {
            back = Button(parent, Game.Core.Localization.StringKeys.MenuLabelPrevious, width: 60f);

            value = Label(parent, null, UiTheme.TextRole.Body, TextAnchor.MiddleCenter);
            var valueSize = value.gameObject.AddComponent<LayoutElement>();
            valueSize.preferredWidth = 190f;
            valueSize.flexibleWidth = 1f;

            forward = Button(parent, Game.Core.Localization.StringKeys.MenuLabelNext, width: 60f);
        }

        /// <summary>
        /// A scrolling viewport, for the two screens whose contents are as long as the
        /// player's progress: the remap list and the journal. Returns the content rect to
        /// fill, which grows downwards as rows are added.
        /// </summary>
        public static RectTransform Scroll(Transform parent, string name, float height)
        {
            var viewport = NewRect(parent, name);
            var size = viewport.gameObject.AddComponent<LayoutElement>();
            size.minHeight = 0f;
            size.preferredHeight = height;

            // Takes whatever the fixed parts of the panel leave, which is what keeps the
            // Back button on screen when the text is turned up.
            size.flexibleHeight = 1f;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            // Otherwise navigation moves focus to a row that is scrolled out of sight and
            // the list looks dead to anyone on a gamepad.
            viewport.gameObject.AddComponent<ScrollToSelection>();

            var mask = NewRect(viewport, "Viewport");
            Stretch(mask);
            var maskImage = mask.gameObject.AddComponent<Image>();
            maskImage.color = new Color(0f, 0f, 0f, 0.25f);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var content = NewRect(mask, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 0f);

            // The content grows downwards as rows are added; without a fitter the mask
            // would clip everything past the viewport's own height and never scroll.
            var contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            scroll.viewport = mask;
            scroll.content = content;
            return content;
        }

        /// <summary>
        /// Wires explicit up/down/left/right between the controls of several columns, plus
        /// a footer button that both columns can reach.
        ///
        /// **Unity's automatic navigation is not enough here, and it fails silently.** It
        /// picks the nearest candidate in the pressed direction by projecting rectangle
        /// edges, which works down a single list and does not reliably cross from one
        /// column to another: on the Settings screen it left the entire Accessibility and
        /// Display column — seven controls, including every accessibility option — with no
        /// sequence of presses that reached them. A player on a gamepad could not change
        /// subtitle background, camera shake, screen effects or aim assist at all.
        ///
        /// Nothing about that is visible to someone building the screen with a mouse, which
        /// is why KNOWN_ISSUES.md has carried "gamepad navigation onto dynamically built
        /// rows is unwired" since TASK 010 without anyone being able to say what it cost.
        ///
        /// Explicit links are deterministic: down goes to the next control in the column,
        /// right goes to the column beside it, and the ends wrap to the footer so there is
        /// always a way out of the screen.
        /// </summary>
        public static void WireColumnNavigation(RectTransform[] columns, Selectable footer = null)
        {
            if (columns == null || columns.Length == 0)
            {
                return;
            }

            var lists = new List<List<Selectable>>();
            foreach (var column in columns)
            {
                var found = new List<Selectable>();
                if (column != null)
                {
                    foreach (var selectable in column.GetComponentsInChildren<Selectable>(true))
                    {
                        found.Add(selectable);
                    }
                }

                lists.Add(found);
            }

            for (var c = 0; c < lists.Count; c++)
            {
                var column = lists[c];
                for (var i = 0; i < column.Count; i++)
                {
                    var navigation = new Navigation { mode = Navigation.Mode.Explicit };

                    navigation.selectOnUp = i > 0 ? column[i - 1] : footer;
                    navigation.selectOnDown = i < column.Count - 1 ? column[i + 1] : footer;
                    navigation.selectOnLeft = Neighbour(lists, c - 1, i);
                    navigation.selectOnRight = Neighbour(lists, c + 1, i);

                    column[i].navigation = navigation;
                }
            }

            if (footer == null)
            {
                return;
            }

            var first = lists[0].Count > 0 ? lists[0][0] : null;
            var last = lists[0].Count > 0 ? lists[0][lists[0].Count - 1] : null;
            footer.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = last,
                selectOnDown = first
            };
        }

        /// <summary>
        /// The control beside this one in a neighbouring column. A shorter column is
        /// entered at its last control rather than nowhere, so moving sideways from the
        /// bottom of a long column never does nothing.
        /// </summary>
        private static Selectable Neighbour(List<List<Selectable>> columns, int column, int index)
        {
            if (column < 0 || column >= columns.Count || columns[column].Count == 0)
            {
                return null;
            }

            return columns[column][Mathf.Min(index, columns[column].Count - 1)];
        }

        /// <summary>Makes a rect fill its parent.</summary>
        public static void Stretch(RectTransform rect) => Stretch(rect, Vector2.zero, Vector2.one);

        public static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>A fixed gap between groups, for where spacing alone reads as too tight.</summary>
        public static void Spacer(Transform parent, float height)
        {
            var rect = NewRect(parent, "Spacer");
            var size = rect.gameObject.AddComponent<LayoutElement>();
            size.minHeight = height;
            size.preferredHeight = height;
        }

        private static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
    }
}

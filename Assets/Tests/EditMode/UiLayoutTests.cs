using System.Collections.Generic;
using System.Linq;
using Game.Core.Localization;
using Game.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests
{
    /// <summary>
    /// The rules the UI pass established (STANDALONE_RELEASE_ROADMAP.md TASK 054).
    ///
    /// These exist because of what TASK 054 found when the menus were rendered for the
    /// first time: the pause menu's title drawn across its own Resume and Save buttons,
    /// and a slider's fill covering the Normal difficulty button and the whole Text Size
    /// row. Both had survived every check this project has, because every check read code
    /// or scene YAML, and a layout is the one thing that cannot be verified by reading it.
    ///
    /// So these tests do not check that the screens look nice — nothing here can. They
    /// check the properties that, had they held, would have made those two defects
    /// impossible: that rows do not overlap, that menu words come from the string table,
    /// and that the palette still carries meaning for a player who cannot see colour.
    /// </summary>
    public class UiLayoutTests
    {
        private UnityEditor.SceneManagement.SceneSetup[] previousScenes;

        [SetUp]
        public void SaveOpenScenes() => previousScenes = EditorSceneManager.GetSceneManagerSetup();

        [TearDown]
        public void RestoreOpenScenes()
        {
            if (previousScenes == null)
            {
                return;
            }

            foreach (var setup in previousScenes)
            {
                if (setup.isLoaded && setup.isActive)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
                    return;
                }
            }
        }

        // ------------------------------------------------------------------- layout

        /// <summary>
        /// **The pause menu's defect, as a rule.**
        ///
        /// The title sat on top of the buttons because its position was a number somebody
        /// typed. Everything <see cref="UiBuilder"/> makes is placed by a layout group
        /// instead, and a layout group cannot overlap its own children — so this builds a
        /// panel the way the game does and proves the property rather than trusting it.
        ///
        /// Asserting on the built result and not on "we used a VerticalLayoutGroup" is the
        /// point: the second would pass just as happily if a later change added a
        /// hand-placed child to the same panel.
        /// </summary>
        [Test]
        public void NothingAPanelBuildsOverlapsAnythingElseInIt()
        {
            var host = NewCanvas(out var canvas);

            try
            {
                var panel = UiBuilder.Panel(host, "Probe", 800f);
                UiBuilder.Label(panel, StringKeys.MenuLabelSettingsTitle, UiTheme.TextRole.Title);
                UiBuilder.SectionHeader(panel, StringKeys.MenuSectionAudio);
                UiBuilder.Slider(UiBuilder.Row(panel, StringKeys.MenuLabelMasterVolume), 0f, 1f, out _);
                UiBuilder.Toggle(UiBuilder.Row(panel, StringKeys.MenuLabelInvertY), out _);
                UiBuilder.Cycler(UiBuilder.Row(panel, StringKeys.MenuLabelDifficulty), out _);
                UiBuilder.Button(panel, StringKeys.MenuLabelBack);

                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

                var siblings = new List<RectTransform>();
                for (var i = 0; i < panel.childCount; i++)
                {
                    var child = (RectTransform)panel.GetChild(i);
                    if (child.rect.height > 0.5f)
                    {
                        siblings.Add(child);
                    }
                }

                Assert.Greater(siblings.Count, 4, "the probe panel built nothing to check");

                for (var i = 0; i < siblings.Count; i++)
                {
                    for (var j = i + 1; j < siblings.Count; j++)
                    {
                        Assert.IsFalse(Overlaps(siblings[i], siblings[j]),
                            $"'{siblings[i].name}' and '{siblings[j].name}' overlap. A layout group "
                            + "cannot do this, so something in the panel is placed by hand — which is "
                            + "how the pause menu came to draw its title across its own buttons.");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        /// <summary>
        /// **The slider's defect, as a rule.** Its fill escaped its track and covered two
        /// other controls. A child that is larger than the control it belongs to is the
        /// shape of that bug, whatever caused it.
        /// </summary>
        [Test]
        public void NoSliderPartEscapesItsSlider()
        {
            var host = NewCanvas(out var canvas);

            try
            {
                var panel = UiBuilder.Panel(host, "Probe", 800f);
                var slider = UiBuilder.Slider(UiBuilder.Row(panel, StringKeys.MenuLabelMasterVolume), 0f, 1f, out _);
                slider.value = 0.5f;

                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

                var track = ((RectTransform)slider.transform).rect;
                foreach (var part in slider.GetComponentsInChildren<RectTransform>(true))
                {
                    if (part == slider.transform)
                    {
                        continue;
                    }

                    Assert.LessOrEqual(part.rect.width, track.width + 1f,
                        $"the slider's '{part.name}' is wider than the slider itself");
                    Assert.LessOrEqual(part.rect.height, track.height + 1f,
                        $"the slider's '{part.name}' is taller than the slider itself");
                }
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        // ---------------------------------------------------------------------- text

        /// <summary>
        /// **No English typed into a menu scene.**
        ///
        /// This is the check that would have caught TASK 042's blind spot. That task
        /// externalized every string a *script* shows a player and every test written for
        /// it passed, because every test looked at scripts — while "New Game", "Continue",
        /// "Settings" and every accessibility caption sat in <c>MainMenu.unity</c> as
        /// authored text belonging to no key. The language picker then switched the handful
        /// of labels a script writes and left the menu in English, which looks like support
        /// and is not.
        ///
        /// A label is allowed to carry text only if something owns it: a
        /// <see cref="LocalizedLabel"/> with a key, or a component that writes it at
        /// runtime.
        /// </summary>
        [Test]
        public void NoMenuLabelHasTextTypedIntoTheScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
            var authored = new List<string>();

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var label in root.GetComponentsInChildren<Text>(true))
                {
                    if (string.IsNullOrWhiteSpace(label.text))
                    {
                        continue;
                    }

                    if (label.GetComponent<LocalizedLabel>() != null || label.GetComponent<VersionLabel>() != null)
                    {
                        continue;
                    }

                    authored.Add($"{Path(label.transform)} = \"{label.text}\"");
                }
            }

            Assert.IsEmpty(authored,
                "These menu labels carry text that belongs to no string key, so no translation can "
                + "reach them and a language change leaves them in English:\n  " + string.Join("\n  ", authored));
        }

        /// <summary>Every key authored on a scene label has text, or the menu shows the key.</summary>
        [TestCase("Assets/Scenes/MainMenu.unity")]
        [TestCase("Assets/Scenes/Avarsha.unity")]
        [TestCase("Assets/Scenes/Agniya.unity")]
        public void EveryLocalizedLabelNamesAKeyTheTableHas(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var english = Resources.Load<StringTable>("Localization/Strings_en");
            Assert.IsNotNull(english, "no English string table");

            var broken = new List<string>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var label in root.GetComponentsInChildren<LocalizedLabel>(true))
                {
                    if (string.IsNullOrWhiteSpace(label.Key))
                    {
                        broken.Add(Path(label.transform) + " (no key)");
                    }
                    else if (english.Find(label.Key) == null)
                    {
                        broken.Add($"{Path(label.transform)} -> {label.Key}");
                    }
                }
            }

            Assert.IsEmpty(broken, "Scene labels whose key has no English text:\n  " + string.Join("\n  ", broken));
        }

        // ------------------------------------------------------------------ palette

        /// <summary>
        /// **The palette still says something to a player who cannot see colour.**
        ///
        /// The same question <c>CombatRulesTests</c> asks of the enemy telegraphs, asked of
        /// the UI. Every one of these colours is paired with a word at its call site, so
        /// nothing here is load-bearing on its own — but a palette whose "done" and "failed"
        /// collapse into the same grey costs a large minority of players the quick read, and
        /// the whole value of a colour in a list is that it is quicker than reading.
        ///
        /// Asserted on luminance, which no form of colour blindness removes.
        /// </summary>
        [Test]
        public void TheMeaningfulColoursStayApartWithoutColourVision()
        {
            var pairs = new (string Name, Color A, Color B)[]
            {
                ("positive vs negative", UiTheme.Positive, UiTheme.Negative),
                ("positive vs caution", UiTheme.Positive, UiTheme.Caution),
                ("negative vs muted text", UiTheme.Negative, UiTheme.TextMuted),
                ("primary text vs muted text", UiTheme.TextPrimary, UiTheme.TextMuted),
                ("heading vs primary text", UiTheme.Heading, UiTheme.TextPrimary),
                ("a forgotten memory vs a restored one", UiTheme.MemoryForgotten, UiTheme.MemoryRestored),
                ("a corrupted memory vs a restored one", UiTheme.MemoryCorrupted, UiTheme.MemoryRestored)
            };

            foreach (var (name, a, b) in pairs)
            {
                var gap = Mathf.Abs(UiTheme.Luminance(a) - UiTheme.Luminance(b));
                var simulated = new System.Func<Color, Color>[] { Deuteranope, Protanope, Tritanope }
                    .Min(simulate => Distance(simulate(a), simulate(b)));

                Assert.IsTrue(gap > 0.12f || simulated > 0.25f,
                    $"{name}: {gap:0.00} apart in luminance and {simulated:0.00} apart once simulated for "
                    + "colour blindness. They may look different to you and they will not to everyone.");
            }
        }

        /// <summary>
        /// Text has to be readable on the surface it is drawn on. A 4.5:1 contrast ratio is
        /// WCAG's bar for body text; this asks for 4.0, because the UI font is larger than
        /// body copy and holding the exact web figure would be false precision.
        /// </summary>
        [Test]
        public void EveryTextColourIsReadableOnThePanelBehindIt()
        {
            var surfaces = new (string Name, Color Colour)[]
            {
                ("panel", UiTheme.Panel),
                ("raised panel", UiTheme.PanelRaised),
                ("a button", UiTheme.ButtonNormal),
                ("a focused button", UiTheme.ButtonFocused)
            };

            var inks = new (string Name, Color Colour)[]
            {
                ("primary text", UiTheme.TextPrimary),
                ("muted text", UiTheme.TextMuted),
                ("a heading", UiTheme.Heading)
            };

            foreach (var (surfaceName, surface) in surfaces)
            {
                foreach (var (inkName, ink) in inks)
                {
                    var ratio = ContrastRatio(ink, surface);
                    Assert.Greater(ratio, 4.0f,
                        $"{inkName} on {surfaceName} is {ratio:0.0}:1. Below about 4:1 it stops being "
                        + "readable for players with low vision, and on a dim laptop panel for everyone.");
                }
            }
        }

        // ---------------------------------------------------------------- utilities

        private static RectTransform NewCanvas(out GameObject canvasObject)
        {
            canvasObject = new GameObject("ProbeCanvas", typeof(RectTransform), typeof(Canvas));
            var rect = (RectTransform)canvasObject.transform;
            rect.sizeDelta = new Vector2(1920f, 1080f);
            return rect;
        }

        private static bool Overlaps(RectTransform a, RectTransform b)
        {
            var one = WorldRect(a);
            var two = WorldRect(b);

            // A one-pixel touch is two rows sitting against each other, not an overlap.
            return one.xMin < two.xMax - 1f && two.xMin < one.xMax - 1f
                   && one.yMin < two.yMax - 1f && two.yMin < one.yMax - 1f;
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return new Rect(corners[0].x, corners[0].y,
                corners[2].x - corners[0].x, corners[2].y - corners[0].y);
        }

        private static string Path(Transform transform)
        {
            var path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return path;
        }

        /// <summary>
        /// Rough but standard dichromacy simulations, the same ones
        /// <c>CombatRulesTests</c> uses. They are not clinically exact; they collapse the
        /// axis the real condition collapses, which is all that is needed to catch a
        /// palette leaning on it.
        /// </summary>
        private static Color Deuteranope(Color c) => new(
            0.625f * c.r + 0.375f * c.g,
            0.700f * c.r + 0.300f * c.g,
            0.300f * c.g + 0.700f * c.b, 1f);

        private static Color Protanope(Color c) => new(
            0.567f * c.r + 0.433f * c.g,
            0.558f * c.r + 0.442f * c.g,
            0.242f * c.g + 0.758f * c.b, 1f);

        private static Color Tritanope(Color c) => new(
            0.950f * c.r + 0.050f * c.g,
            0.433f * c.g + 0.567f * c.b,
            0.475f * c.g + 0.525f * c.b, 1f);

        private static float Distance(Color a, Color b) =>
            Mathf.Sqrt((a.r - b.r) * (a.r - b.r) + (a.g - b.g) * (a.g - b.g) + (a.b - b.b) * (a.b - b.b));

        /// <summary>WCAG relative luminance, which is gamma-corrected — not the same as <see cref="UiTheme.Luminance"/>.</summary>
        private static float RelativeLuminance(Color c)
        {
            static float Channel(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Channel(c.r) + 0.7152f * Channel(c.g) + 0.0722f * Channel(c.b);
        }

        private static float ContrastRatio(Color ink, Color surface)
        {
            var a = RelativeLuminance(ink);
            var b = RelativeLuminance(surface);
            return (Mathf.Max(a, b) + 0.05f) / (Mathf.Min(a, b) + 0.05f);
        }
    }
}

using System.Collections.Generic;
using Game.Core;
using Game.Core.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// "How to Play" (STANDALONE_RELEASE_ROADMAP.md TASK 054).
    ///
    /// TASK 054's gate is that **a first-time tester can play without instructions**, and
    /// the game did not meet it for a simple reason: nothing in it ever said what the
    /// buttons do. The controls were written down in <c>CONTROLS.txt</c> inside the release
    /// ZIP, which is a file beside the game rather than a screen in it, and a player who
    /// has already double-clicked the exe is not going back to a text file to find out how
    /// to dodge.
    ///
    /// **The list is generated from the live <see cref="InputActionAsset"/>, never typed.**
    /// That is the whole point of generating it: the game lets the player rebind, so a
    /// hand-written help screen is wrong the moment they do, and wrong in the most damaging
    /// way — it tells them to press a key that no longer does anything. Reading the asset
    /// means the screen shows what is actually bound, including their own changes, with no
    /// second list to keep in step.
    ///
    /// It carries three sentences that are not controls, because they are the three things
    /// a new player gets wrong and cannot deduce from a key list: that stamina is shared
    /// between attacking, dodging and sprinting; that divine abilities are paid for in
    /// memory; and when the game saves.
    /// </summary>
    public class HelpPanel : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

        [Tooltip("Where the screen is built. Defaults to this object's own RectTransform.")]
        [SerializeField] private RectTransform container;

        [SerializeField] private float panelWidth = 820f;
        [SerializeField] private bool drawBackground = true;

        /// <summary>Raised when the player presses Back.</summary>
        public event System.Action BackRequested;

        /// <summary>
        /// Which actions appear under which heading, and in what order.
        ///
        /// Ordered for someone who has never played rather than alphabetically or by the
        /// order the asset happens to list them: move before fight, and the screens last,
        /// because that is the sequence in which a new player needs them.
        /// </summary>
        private static readonly string[] MovingActions = { "Move", "Look", "Jump", "Sprint" };

        // Dodge is listed here and not under Moving. It is both, but a player meets it as
        // the answer to an attack, and the same row printed under two headings reads as a
        // mistake rather than as emphasis.
        private static readonly string[] FightingActions =
            { "LightAttack", "HeavyAttack", "Guard", "Dodge", "LockOn", "Ability" };

        private static readonly string[] ScreenActions =
            { "Interact", "Journal", "Map", "Progression", "ToggleHud", "Pause", "Skip" };

        /// <summary>
        /// Every action this screen can list, in one place so the localization test can
        /// check that each one has a name without duplicating the list. A help screen whose
        /// rows read <c>help.action.dodge</c> would be worse than no help screen.
        /// </summary>
        public static IEnumerable<string> ListedActions
        {
            get
            {
                var seen = new HashSet<string>();
                foreach (var group in new[] { MovingActions, FightingActions, ScreenActions })
                {
                    foreach (var action in group)
                    {
                        if (seen.Add(action))
                        {
                            yield return action;
                        }
                    }
                }
            }
        }

        private bool built;
        private RectTransform bindingsColumn;
        private RectTransform screensColumn;
        private Button backButton;

        public Button BackButton => backButton;

        private void OnEnable()
        {
            EnsureBuilt();
            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);

            // Rebuilt on every open rather than once: the player may have just come from
            // the Controls screen, and a help screen showing the bindings they replaced is
            // worse than no help screen.
            RefreshBindings();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        private void OnLanguageChanged(LanguageChangedEvent changed) => RefreshBindings();

        public void EnsureBuilt()
        {
            if (built)
            {
                return;
            }

            built = true;

            var host = container != null ? container : (RectTransform)transform;
            var panel = UiBuilder.Panel(host, "HelpContent", panelWidth, drawBackground);

            UiBuilder.Label(panel, StringKeys.MenuLabelHelpTitle, UiTheme.TextRole.Title);
            UiBuilder.Paragraph(panel, StringKeys.HelpIntro);
            UiBuilder.Paragraph(panel, StringKeys.HelpBindingNote, UiTheme.TextRole.Muted);

            // Two columns. In one, the control list plus three lines of advice runs past
            // the bottom of a 1080 screen, which cut off both the title and the Back button
            // — a help screen you cannot leave being a particularly unhelpful one.
            var columns = UiBuilder.Columns(panel, 2);
            bindingsColumn = columns[0];
            screensColumn = columns[1];

            UiBuilder.Spacer(panel, UiTheme.Spacing);
            UiBuilder.Paragraph(panel, StringKeys.HelpTipStamina, UiTheme.TextRole.Muted);
            UiBuilder.Paragraph(panel, StringKeys.HelpTipMemory, UiTheme.TextRole.Muted);
            UiBuilder.Paragraph(panel, StringKeys.HelpTipSaving, UiTheme.TextRole.Muted);

            UiBuilder.Spacer(panel, UiTheme.Spacing * 2f);
            backButton = UiBuilder.Button(panel, StringKeys.MenuLabelBack);
            backButton.onClick.AddListener(() => BackRequested?.Invoke());
        }

        /// <summary>Rebuilds the control list from whatever is bound right now.</summary>
        public void RefreshBindings()
        {
            if (bindingsColumn == null)
            {
                return;
            }

            Clear(bindingsColumn);
            Clear(screensColumn);

            if (inputActions == null)
            {
                GameLogger.LogWarning(LogCategory.UI, "HelpPanel has no InputActionAsset; the control list is empty.", this);
                return;
            }

            var map = inputActions.FindActionMap("Gameplay");
            if (map == null)
            {
                GameLogger.LogWarning(LogCategory.UI, "HelpPanel found no Gameplay action map.", this);
                return;
            }

            AddSection(bindingsColumn, map, StringKeys.HelpSectionMoving, MovingActions);
            AddSection(bindingsColumn, map, StringKeys.HelpSectionFighting, FightingActions);
            AddSection(screensColumn, map, StringKeys.HelpSectionScreens, ScreenActions);
        }

        /// <summary>
        /// Empties a column. <c>DestroyImmediate</c> rather than <c>Destroy</c> because this
        /// also runs from the editor capture tool, where a deferred destroy never happens
        /// and the rows would be rebuilt on top of the ones already there.
        /// </summary>
        private static void Clear(RectTransform column)
        {
            if (column == null)
            {
                return;
            }

            for (var i = column.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(column.GetChild(i).gameObject);
            }
        }

        private void AddSection(RectTransform column, InputActionMap map, string headingKey, string[] actionNames)
        {
            var wroteHeading = false;

            foreach (var actionName in actionNames)
            {
                var action = map.FindAction(actionName);
                if (action == null)
                {
                    // Not an error: ToggleHud is bound on some builds and not others, and
                    // a help screen should list what exists rather than what was planned.
                    continue;
                }

                if (!wroteHeading)
                {
                    UiBuilder.SectionHeader(column, headingKey);
                    wroteHeading = true;
                }

                var label = UiBuilder.Label(column, null, UiTheme.TextRole.Body, TextAnchor.MiddleLeft);
                label.text = Strings.Format(StringKeys.HelpBindingRow,
                    Strings.Get(StringKeys.HelpActionPrefix + actionName.ToLowerInvariant()),
                    BindingsFor(action));

                var size = label.gameObject.AddComponent<LayoutElement>();
                size.minHeight = UiTheme.RowHeight - 4f;
                size.preferredHeight = UiTheme.RowHeight - 4f;
            }
        }

        /// <summary>
        /// Every binding on an action, joined — not just the first.
        ///
        /// The first binding is the keyboard one, and a player holding a controller would
        /// be told to press E. Showing both is what makes this screen true for "both input
        /// methods", which is what TASK 054 asks for.
        /// </summary>
        private static string BindingsFor(InputAction action)
        {
            var display = action.GetBindingDisplayString(InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
            return string.IsNullOrWhiteSpace(display) ? "-" : display.Replace(" | ", " / ");
        }

        /// <inheritdoc cref="SettingsPanel.BuildForPreview"/>
        public void BuildForPreview()
        {
            EnsureBuilt();
            RefreshBindings();
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(InputActionAsset actions, RectTransform host = null, bool background = true)
        {
            inputActions = actions;
            if (host != null)
            {
                container = host;
            }

            drawBackground = background;
        }
    }
}

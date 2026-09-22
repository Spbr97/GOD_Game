using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.DevTools
{
    /// <summary>
    /// The developer console: a text box that runs <see cref="DebugCommands"/> and an
    /// on/off switch for <see cref="DebugOverlay"/>'s readouts (SPEC.md section 52).
    ///
    /// Gated twice over. The whole component switches itself off in
    /// <see cref="Awake"/> when <see cref="DebugMode.IsAvailableInThisBuild"/> is
    /// false, and every command re-checks <see cref="DebugMode.IsEnabled"/> on its own
    /// — so a release build has no console, and a development build has one that does
    /// nothing until a developer deliberately opens it.
    ///
    /// The opening key is read through the keyboard device rather than an input
    /// action, on purpose: the console has to be reachable when the input asset is
    /// what is broken, and adding a binding to the shipped asset would put a
    /// developer tool in the player's rebinding screen.
    ///
    /// IMGUI for the same reason <see cref="DebugOverlay"/> uses it — no canvas, no
    /// prefab, no Inspector wiring, and it draws over anything.
    /// </summary>
    [DisallowMultipleComponent]
    public class DebugConsole : MonoBehaviour
    {
        private const int HistoryLimit = 200;

        [Tooltip("Key that opens and closes the console. Backquote is the usual one and is not bound to anything in the game.")]
        [SerializeField] private Key toggleKey = Key.Backquote;

        [Tooltip("Fraction of the screen height the console covers.")]
        [Range(0.2f, 0.9f)]
        [SerializeField] private float heightFraction = 0.4f;

        [SerializeField] private DebugOverlay overlay;

        private readonly List<string> output = new();
        private readonly List<string> entered = new();
        private int recalledIndex = -1;
        private string input = string.Empty;
        private Vector2 scroll;
        private bool focusQueued;

        public bool IsOpen { get; private set; }

        /// <summary>Everything printed so far. Read by tests.</summary>
        public IReadOnlyList<string> Output => output;

        private void Awake()
        {
            if (!DebugMode.IsAvailableInThisBuild)
            {
                // SPEC.md section 52: "Debug mode must be disabled in release builds."
                // The component removes itself rather than merely hiding, so nothing
                // is left listening for a key in a shipped game.
                Destroy(this);
                return;
            }

            if (overlay == null)
            {
                overlay = GetComponent<DebugOverlay>();
            }

            Print("Developer console. Type 'help'. SPEC.md section 52.");
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard[toggleKey].wasPressedThisFrame)
            {
                SetOpen(!IsOpen);
            }

            if (IsOpen && keyboard.escapeKey.wasPressedThisFrame)
            {
                SetOpen(false);
            }
        }

        /// <summary>Opens or closes the console. Public so a test drives it without a keypress.</summary>
        public void SetOpen(bool open)
        {
            if (open && !DebugMode.Enable())
            {
                return;
            }

            IsOpen = open;
            focusQueued = open;

            if (!open)
            {
                return;
            }

            // Opening pauses, so typing a command does not mean fighting whatever is
            // attacking while both hands are on the keyboard.
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                GameManager.Instance.Pause();
            }
        }

        /// <summary>
        /// Runs a line and prints the answer. Public and returning the same string it
        /// prints, so a test can assert on a command's result without reading the UI.
        /// </summary>
        public string Submit(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return string.Empty;
            }

            Print($"> {line}");
            entered.Add(line);
            recalledIndex = entered.Count;

            var result = line.TrimStart().StartsWith("show", System.StringComparison.OrdinalIgnoreCase)
                ? RunShow(line)
                : DebugCommands.Run(line);

            Print(result);
            return result;
        }

        /// <summary>
        /// The five "show ..." tools of section 52. Handled here rather than in
        /// <see cref="DebugCommands"/> because they toggle a component's state, and
        /// DebugCommands deliberately holds no scene references.
        /// </summary>
        private string RunShow(string line)
        {
            if (overlay == null)
            {
                return "No DebugOverlay on this object.";
            }

            var parts = line.Trim().Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return "Usage: show fps|ai|nav|flags|save|none";
            }

            switch (parts[1].ToLowerInvariant())
            {
                case "fps": return overlay.Toggle(OverlayPanels.Fps);
                case "ai": return overlay.Toggle(OverlayPanels.Ai);
                case "nav" or "navigation": return overlay.Toggle(OverlayPanels.Navigation);
                case "flags" or "quest" or "quests": return overlay.Toggle(OverlayPanels.QuestFlags);
                case "save": return overlay.Toggle(OverlayPanels.SaveVersion);

                case "all":
                    overlay.Panels = OverlayPanels.Fps | OverlayPanels.Ai | OverlayPanels.Navigation
                                     | OverlayPanels.QuestFlags | OverlayPanels.SaveVersion;
                    return "All overlays on.";

                case "none":
                    overlay.Panels = OverlayPanels.None;
                    return "All overlays off.";

                default:
                    return "Usage: show fps|ai|nav|flags|save|all|none";
            }
        }

        private void Print(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            foreach (var line in text.Split('\n'))
            {
                output.Add(line.TrimEnd());
            }

            // Bounded: a console left open through a long session would otherwise grow
            // without limit, and only the recent lines are ever read.
            if (output.Count > HistoryLimit)
            {
                output.RemoveRange(0, output.Count - HistoryLimit);
            }

            scroll.y = float.MaxValue;
        }

        private void OnGUI()
        {
            if (!IsOpen || !DebugMode.IsEnabled)
            {
                return;
            }

            var height = Screen.height * heightFraction;
            var area = new Rect(0f, 0f, Screen.width, height);

            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(8f, 4f, Screen.width - 16f, height - 8f));

            scroll = GUILayout.BeginScrollView(scroll);
            for (var i = 0; i < output.Count; i++)
            {
                GUILayout.Label(output[i]);
            }

            GUILayout.EndScrollView();

            HandleEditingKeys();

            GUI.SetNextControlName("DebugConsoleInput");
            input = GUILayout.TextField(input ?? string.Empty);

            if (focusQueued && Event.current.type == EventType.Repaint)
            {
                // Focus has to be claimed during Repaint: the control does not exist
                // yet in the Layout pass, and naming it before it is drawn does
                // nothing.
                GUI.FocusControl("DebugConsoleInput");
                focusQueued = false;
            }

            GUILayout.EndArea();
        }

        /// <summary>Return submits; up and down walk back through what was typed before.</summary>
        private void HandleEditingKeys()
        {
            if (Event.current.type != EventType.KeyDown)
            {
                return;
            }

            switch (Event.current.keyCode)
            {
                case KeyCode.Return or KeyCode.KeypadEnter:
                    Submit(input);
                    input = string.Empty;
                    Event.current.Use();
                    break;

                case KeyCode.UpArrow when entered.Count > 0:
                    recalledIndex = Mathf.Max(0, recalledIndex - 1);
                    input = entered[recalledIndex];
                    Event.current.Use();
                    break;

                case KeyCode.DownArrow when entered.Count > 0:
                    recalledIndex = Mathf.Min(entered.Count, recalledIndex + 1);
                    input = recalledIndex >= entered.Count ? string.Empty : entered[recalledIndex];
                    Event.current.Use();
                    break;
            }
        }
    }
}

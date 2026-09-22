using System;
using System.Text;
using Game.AI;
using Game.Core;
using Game.Memory;
using Game.Quests;
using Game.Save;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>Which readouts <see cref="DebugOverlay"/> is currently drawing.</summary>
    [Flags]
    public enum OverlayPanels
    {
        None = 0,

        /// <summary>SPEC.md section 52: show FPS.</summary>
        Fps = 1 << 0,

        /// <summary>SPEC.md section 52: show AI state.</summary>
        Ai = 1 << 1,

        /// <summary>SPEC.md section 52: show navigation.</summary>
        Navigation = 1 << 2,

        /// <summary>SPEC.md section 52: show quest flags.</summary>
        QuestFlags = 1 << 3,

        /// <summary>SPEC.md section 52: show save version.</summary>
        SaveVersion = 1 << 4
    }

    /// <summary>
    /// The five read-only developer readouts of SPEC.md section 52 — FPS, AI state,
    /// navigation, quest flags and save version.
    ///
    /// IMGUI rather than a canvas: this must be able to appear over any scene,
    /// including one whose UI is what is being debugged, and it must never need
    /// wiring in the Inspector to work. The cost is IMGUI's per-frame allocation,
    /// which is why every panel is off by default and the whole component returns
    /// immediately when debug mode is not enabled.
    ///
    /// Everything here reads; nothing writes. The tools that change the game are in
    /// <see cref="DebugCommands"/>, and keeping the two apart means a developer can
    /// leave a readout up during a playtest without any risk of it altering what they
    /// are watching.
    /// </summary>
    [DisallowMultipleComponent]
    public class DebugOverlay : MonoBehaviour
    {
        [Tooltip("Panels shown when debug mode is switched on. Changed at runtime with the console's 'show' command.")]
        [SerializeField] private OverlayPanels panels = OverlayPanels.None;

        [Tooltip("Seconds the frame-time average is taken over. Long enough not to flicker, short enough to catch a hitch.")]
        [SerializeField] private float fpsWindow = 0.5f;

        [Tooltip("Enemies further than this from the camera are left out of the AI and navigation panels, so a large scene stays readable.")]
        [SerializeField] private float readoutRange = 60f;

        private float fpsAccumulator;
        private int fpsFrames;
        private float fpsNextSample;
        private float displayedFps;
        private float worstFrameMs;

        private GUIStyle style;

        public OverlayPanels Panels
        {
            get => panels;
            set => panels = value;
        }

        /// <summary>Turns one panel on or off. Returns the sentence the console prints.</summary>
        public string Toggle(OverlayPanels panel)
        {
            var on = (panels & panel) == 0;
            panels = on ? panels | panel : panels & ~panel;
            return $"{panel} overlay {(on ? "on" : "off")}.";
        }

        private void Update()
        {
            if (!DebugMode.IsEnabled || (panels & OverlayPanels.Fps) == 0)
            {
                return;
            }

            // unscaledDeltaTime: a paused game has a timeScale of zero, and an FPS
            // readout that reads zero while the developer is looking at a pause menu
            // is the opposite of useful.
            var delta = Time.unscaledDeltaTime;
            fpsAccumulator += delta;
            fpsFrames++;
            worstFrameMs = Mathf.Max(worstFrameMs, delta * 1000f);

            if (Time.unscaledTime < fpsNextSample)
            {
                return;
            }

            displayedFps = fpsFrames > 0 && fpsAccumulator > 0f ? fpsFrames / fpsAccumulator : 0f;
            fpsAccumulator = 0f;
            fpsFrames = 0;
            fpsNextSample = Time.unscaledTime + fpsWindow;
        }

        private void OnGUI()
        {
            if (!DebugMode.IsEnabled || panels == OverlayPanels.None)
            {
                return;
            }

            style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                richText = false,
                normal = { textColor = Color.white }
            };

            var text = new StringBuilder();

            if ((panels & OverlayPanels.Fps) != 0)
            {
                AppendFps(text);
            }

            if ((panels & OverlayPanels.Ai) != 0)
            {
                AppendAi(text);
            }

            if ((panels & OverlayPanels.Navigation) != 0)
            {
                AppendNavigation(text);
            }

            if ((panels & OverlayPanels.QuestFlags) != 0)
            {
                AppendQuestFlags(text);
            }

            if ((panels & OverlayPanels.SaveVersion) != 0)
            {
                AppendSaveVersion(text);
            }

            var body = text.ToString();
            var size = style.CalcSize(new GUIContent(body));
            var rect = new Rect(8f, 8f, Mathf.Min(size.x + 16f, Screen.width - 16f), size.y + 12f);

            // A backing box: white text over a bright scene is unreadable, and this
            // gets used most on the outdoor scenes.
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width, rect.height), body, style);
        }

        private void AppendFps(StringBuilder text)
        {
            var target = 60f;
            var verdict = displayedFps >= target ? "ok" : displayedFps >= target * 0.5f ? "below target" : "poor";

            // The number and the word, not a colour: SPEC.md section 43 says important
            // information is never carried by colour alone, and that applies to the
            // tools as much as to the game.
            text.AppendLine($"FPS {displayedFps:0}  ({1000f / Mathf.Max(displayedFps, 0.01f):0.0} ms, worst {worstFrameMs:0.0} ms) — {verdict}");
            text.AppendLine($"  target {target:0} (SPEC.md section 45)");
        }

        private void AppendAi(StringBuilder text)
        {
            text.AppendLine("AI:");

            var camera = Camera.main;
            var origin = camera != null ? camera.transform.position : Vector3.zero;
            var shown = 0;

            foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (camera != null && Vector3.Distance(origin, enemy.transform.position) > readoutRange)
                {
                    continue;
                }

                var health = enemy.GetComponent<Game.Combat.HealthComponent>();
                var stagger = enemy.GetComponent<EnemyStagger>();

                text.AppendLine(
                    $"  {enemy.name}: {enemy.State} for {enemy.TimeInState:0.0}s" +
                    (health != null ? $", hp {health.CurrentHealth:0}/{health.MaxHealth:0}" : string.Empty) +
                    (stagger != null && stagger.IsStaggered ? ", STAGGERED" : string.Empty));

                shown++;
            }

            if (shown == 0)
            {
                text.AppendLine("  (no enemies within range)");
            }
        }

        private void AppendNavigation(StringBuilder text)
        {
            text.AppendLine("Navigation:");

            var camera = Camera.main;
            var origin = camera != null ? camera.transform.position : Vector3.zero;
            var shown = 0;

            foreach (var navigator in FindObjectsByType<EnemyNavigator>(FindObjectsSortMode.None))
            {
                if (camera != null && Vector3.Distance(origin, navigator.transform.position) > readoutRange)
                {
                    continue;
                }

                var problem = navigator.LastMoveFailed ? "PATH FAILED"
                    : navigator.IsUsingFallbackSteering ? "off-mesh, steering directly"
                    : navigator.HasArrived ? "arrived" : "moving";

                text.AppendLine($"  {navigator.name}: {problem}, destination {navigator.Destination}");

                // Also drawn in the world, where the shape of a bad path is obvious in
                // a way a printed coordinate never is. Scene view always; Game view
                // with gizmos on.
                UnityEngine.Debug.DrawLine(
                    navigator.transform.position,
                    navigator.Destination,
                    navigator.LastMoveFailed ? Color.red : Color.cyan);

                shown++;
            }

            if (shown == 0)
            {
                text.AppendLine("  (no navigators within range)");
            }
        }

        private void AppendQuestFlags(StringBuilder text)
        {
            text.AppendLine("Quests:");

            var quests = QuestManager.Instance;
            if (quests == null)
            {
                text.AppendLine("  (no QuestManager)");
            }
            else
            {
                foreach (var progress in quests.ActiveQuests.Values)
                {
                    var objective = progress.CurrentObjective;
                    text.AppendLine($"  {progress.Definition.QuestId}: {progress.Status}" +
                                    (objective != null ? $" — {objective.ObjectiveId}" : string.Empty));
                }

                foreach (var progress in quests.FinishedQuests.Values)
                {
                    text.AppendLine($"  {progress.Definition.QuestId}: {progress.Status}");
                }

                if (quests.ActiveQuests.Count == 0 && quests.FinishedQuests.Count == 0)
                {
                    text.AppendLine("  (none started)");
                }
            }

            text.AppendLine("World flags set:");

            var state = WorldState.Instance;
            if (state == null)
            {
                text.AppendLine("  (no WorldState)");
                return;
            }

            var any = false;
            foreach (var pair in state.Flags)
            {
                if (!pair.Value)
                {
                    // Only the set ones. An unset flag is the default and listing every
                    // one buries the handful that matter.
                    continue;
                }

                text.AppendLine($"  {pair.Key}");
                any = true;
            }

            if (!any)
            {
                text.AppendLine("  (none)");
            }

            foreach (var pair in state.Counters)
            {
                text.AppendLine($"  {pair.Key} = {pair.Value}");
            }

            var memories = MemoryManager.Instance;
            if (memories != null)
            {
                text.AppendLine($"Memory integrity {memories.Integrity:P0}, {memories.DiscoveredCount} discovered");
            }
        }

        private void AppendSaveVersion(StringBuilder text)
        {
            text.AppendLine($"Save format version {SaveData.CurrentVersion} (SPEC.md section 31)");

            var manager = SaveManager.Instance;
            var root = manager != null ? manager.Root : SaveStorage.DefaultRoot;

            foreach (SaveSlot slot in Enum.GetValues(typeof(SaveSlot)))
            {
                var outcome = SaveBrowser.Peek(root, slot);

                if (!outcome.Loaded)
                {
                    text.AppendLine($"  {slot}: empty or unreadable ({outcome.PrimaryResult})");
                    continue;
                }

                var stale = outcome.Data.Version != SaveData.CurrentVersion
                    ? $"  MIGRATES from v{outcome.Data.Version}"
                    : string.Empty;

                text.AppendLine(
                    $"  {slot}: v{outcome.Data.Version} from {outcome.Source}, scene '{outcome.Data.SceneName}', " +
                    $"{outcome.Data.Timestamp.ToLocalTime():g}{stale}");
            }
        }
    }
}

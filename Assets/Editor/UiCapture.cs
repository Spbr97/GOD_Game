using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Renders a screen's UI to a PNG without entering play mode
    /// (STANDALONE_RELEASE_ROADMAP.md TASK 054).
    ///
    /// **Why this exists at all.** Every UI judgement in this project so far has been made
    /// by reading C# and scene YAML, and KNOWN_ISSUES.md has carried "Scene-view UI
    /// placement was not visually verified" since TASK 007 for exactly that reason. A
    /// layout is the one thing that cannot be verified by reasoning about it: overlapping
    /// labels, text clipped by its panel, a button off the bottom of the screen and a
    /// perfectly tidy hierarchy all read the same in YAML. Restyling menus without being
    /// able to look at them would be guessing, and guessing is how the braziers came to
    /// say "Light Brazier_0" through four tasks that each believed they had checked.
    ///
    /// So this is the eyes. It opens a scene, points a throwaway camera at one canvas,
    /// renders it to a <see cref="RenderTexture"/> and writes a PNG. It is checked in
    /// rather than thrown away because the next person to move a panel needs it too, and
    /// because the manual pass will want before/after shots of the same screen.
    ///
    /// **What it does not prove.** This is the Editor's render of a canvas at one size, not
    /// the built player at the player's resolution. It catches layout — overlap, clipping,
    /// contrast, a label that is still the GameObject's name — and it cannot catch anything
    /// that only happens once the game is running. The manual pass still owns that.
    /// </summary>
    public static class UiCapture
    {
        /// <summary>
        /// Queues a capture to run on the next editor tick and writes the outcome beside
        /// the PNG as <c>&lt;output&gt;.log</c>.
        ///
        /// The editor's command pipeline gives a call five seconds on the main thread, and
        /// opening a scene costs more than that, so a capture driven straight from
        /// <see cref="Capture"/> times out and reports a failure for work that was about to
        /// succeed. Deferring hands the pipeline its answer immediately and does the slow
        /// part afterwards; the caller waits for the log file rather than for the call.
        /// </summary>
        public static string CaptureDeferred(string scenePath, string outputPath, int width, int height,
            params string[] showPanels)
        {
            var language = Language;
            var textScale = TextScale;
            var logPath = outputPath + ".log";
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }

            EditorApplication.CallbackFunction run = null;
            run = () =>
            {
                EditorApplication.update -= run;

                string result;
                try
                {
                    // Captured when the call was made, not when the tick fires: a sweep
                    // queues several of these and the static would otherwise hold whatever
                    // the last caller set.
                    Language = language;
                    TextScale = textScale;
                    result = Capture(scenePath, outputPath, width, height, showPanels);
                }
                catch (Exception error)
                {
                    result = "FAILED: " + error;
                }

                File.WriteAllText(logPath, result);
            };

            EditorApplication.update += run;
            return "queued; poll " + logPath;
        }

        /// <summary>
        /// Opens <paramref name="scenePath"/>, shows the panels named in
        /// <paramref name="showPanels"/> (hiding their siblings so one screen is captured
        /// at a time), and writes a PNG to <paramref name="outputPath"/>.
        ///
        /// The scene is reopened from disk afterwards, so nothing this does can be saved by
        /// accident — the panel activations it performs are exactly the kind of change that
        /// would otherwise end up committed.
        /// </summary>
        /// <param name="showPanels">
        /// GameObject names to activate before rendering. Each one's siblings are
        /// deactivated, which is how a menu's panels are mutually exclusive at runtime.
        /// Empty means "capture the scene as it is on disk".
        /// </param>
        /// <summary>
        /// The language to render in, and the text size to render at, for the next capture.
        ///
        /// A baseline pass needs both: a layout only breaks under conditions the author did
        /// not type into it, and the two that break a menu are a translation longer than
        /// English and a player who has turned the text up. The pseudo-locale exists to
        /// supply the first and had never been looked at.
        /// </summary>
        public static string Language = "en";

        public static float TextScale = 1f;

        public static string Capture(string scenePath, string outputPath, int width, int height,
            params string[] showPanels)
        {
            var originalScene = EditorSceneManager.GetActiveScene().path;
            var log = new List<string>();

            try
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                // Before anything builds, not after. A panel writes its values — the
                // difficulty name, "On"/"Off", the language's own name — while it is being
                // constructed, so a language set afterwards leaves those in English and the
                // capture quietly understates how much of the screen is translated.
                Game.Core.Localization.Strings.SetLanguage(Language);

                // Two passes, because half these screens do not exist until something
                // builds them: a panel named here may be a child the pause menu creates in
                // code, which cannot be shown before the thing that makes it has run.
                var pending = new List<string>();
                foreach (var panelName in showPanels ?? Array.Empty<string>())
                {
                    if (!ShowOnly(panelName, log))
                    {
                        pending.Add(panelName);
                    }
                }

                BuildRuntimePanels(log);

                foreach (var panelName in pending)
                {
                    if (!ShowOnly(panelName, log))
                    {
                        return $"FAILED: no GameObject named '{panelName}' in {scenePath}";
                    }
                }

                // Again, because a panel that was just revealed may itself be one that
                // builds on enable — the in-game Settings screen is built by the pause menu
                // and then builds its own rows, which is two levels of "not there yet".
                // EnsureBuilt makes the repeat free for anything already built.
                if (pending.Count > 0)
                {
                    BuildRuntimePanels(log);
                }

                var canvas = FindCanvas();
                if (canvas == null)
                {
                    return $"FAILED: no Canvas in {scenePath}";
                }

                ApplyConditions(canvas, log);

                var file = Render(canvas, outputPath, width, height, log);
                return $"OK {file}\n{string.Join("\n", log)}";
            }
            finally
            {
                // Discards the panel activations above: OpenScene from code does not
                // prompt and does not save, which is the behaviour wanted here.
                if (!string.IsNullOrEmpty(originalScene))
                {
                    EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
                }
            }
        }

        /// <summary>
        /// Asks every screen that builds itself at runtime to build now.
        ///
        /// Half the UI this tool exists to check does not exist until the game runs — the
        /// Settings panel, How to Play, the pause menu and the remap list are all
        /// constructed in OnEnable, which the Editor does not call outside play mode. Without
        /// this the tool would render those screens as an empty rectangle and report success,
        /// which is the most useless result a verification tool can produce.
        ///
        /// Nothing built here is saved: the caller reopens the scene from disk afterwards.
        /// </summary>
        private static void BuildRuntimePanels(List<string> log)
        {
            foreach (var panel in Resources.FindObjectsOfTypeAll<Game.UI.SettingsPanel>())
            {
                if (panel.gameObject.scene.IsValid() && panel.gameObject.activeInHierarchy)
                {
                    panel.BuildForPreview();
                    log.Add("built SettingsPanel");
                }
            }

            foreach (var panel in Resources.FindObjectsOfTypeAll<Game.UI.HelpPanel>())
            {
                if (panel.gameObject.scene.IsValid() && panel.gameObject.activeInHierarchy)
                {
                    panel.BuildForPreview();
                    log.Add("built HelpPanel");
                }
            }

            foreach (var menu in Resources.FindObjectsOfTypeAll<Game.UI.GameMenu>())
            {
                if (menu.gameObject.scene.IsValid() && menu.gameObject.activeInHierarchy)
                {
                    menu.BuildForPreview();
                    log.Add("built GameMenu");
                }
            }

            foreach (var remap in Resources.FindObjectsOfTypeAll<Game.UI.RemapUI>())
            {
                if (remap.gameObject.scene.IsValid() && remap.gameObject.activeInHierarchy)
                {
                    remap.Refresh();
                    log.Add("built RemapUI");
                }
            }
        }

        /// <summary>
        /// Activates the named object and deactivates its siblings. Returns false when no
        /// such object exists, so a typo in a panel name fails loudly instead of quietly
        /// capturing whichever screen happened to be left on.
        /// </summary>
        private static bool ShowOnly(string panelName, List<string> log)
        {
            var target = FindByName(panelName);
            if (target == null)
            {
                return false;
            }

            var parent = target.transform.parent;
            if (parent != null)
            {
                for (var i = 0; i < parent.childCount; i++)
                {
                    var sibling = parent.GetChild(i).gameObject;
                    sibling.SetActive(sibling == target);
                }
            }

            // A panel inside a panel is no use if its parent is off.
            for (var t = target.transform.parent; t != null; t = t.parent)
            {
                t.gameObject.SetActive(true);
            }

            target.SetActive(true);
            log.Add($"showing {panelName}");
            return true;
        }

        private static GameObject FindByName(string name)
        {
            foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (transform.name != name || transform.gameObject.scene.IsValid() == false)
                {
                    continue;
                }

                return transform.gameObject;
            }

            return null;
        }

        /// <summary>
        /// Puts the canvas into the language and text size being tested, then rebuilds the
        /// labels so they are re-read. Both are reverted when the scene is reopened.
        /// </summary>
        private static void ApplyConditions(Canvas canvas, List<string> log)
        {
            Game.Core.Localization.Strings.SetLanguage(Language);
            log.Add($"language {Language}, text scale {TextScale:0.00}");

            var applier = canvas.GetComponent<Game.UI.UIScaleApplier>();
            if (applier != null)
            {
                applier.Apply(TextScale);
                log.Add($"effective scale {applier.EffectiveScale:0.00}");
            }
            else if (TextScale != 1f)
            {
                log.Add("WARN this canvas has no UIScaleApplier, so text size does nothing on it");
            }

            // Every label reads its key on enable, which has not happened here.
            foreach (var label in Resources.FindObjectsOfTypeAll<Game.UI.LocalizedLabel>())
            {
                if (label.gameObject.scene.IsValid())
                {
                    label.Apply();
                }
            }
        }

        private static Canvas FindCanvas()
        {
            Canvas best = null;
            foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (!canvas.gameObject.scene.IsValid() || canvas.transform.parent != null)
                {
                    continue;
                }

                // The one with the most under it, so a scene with a second small canvas
                // (a world-space label, a debug overlay) still captures the real screen.
                if (best == null || canvas.transform.childCount > best.transform.childCount)
                {
                    best = canvas;
                }
            }

            return best;
        }

        private static string Render(Canvas canvas, string outputPath, int width, int height, List<string> log)
        {
            var renderMode = canvas.renderMode;
            var worldCamera = canvas.worldCamera;
            var planeDistance = canvas.planeDistance;

            var cameraGo = new GameObject("__UiCaptureCamera");
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var active = RenderTexture.active;

            try
            {
                var camera = cameraGo.AddComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;

                // Not black: a panel that forgot its own background would be invisible
                // against black and obvious against this.
                camera.backgroundColor = new Color(0.16f, 0.10f, 0.14f, 1f);
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                camera.targetTexture = texture;
                camera.transform.position = new Vector3(0f, 0f, -100f);

                // Screen Space - Camera is the only mode that renders into a
                // RenderTexture; Overlay draws straight to the display and would come
                // back as an empty frame.
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;

                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    log.Add($"scaler {scaler.uiScaleMode} reference {scaler.referenceResolution} factor {scaler.scaleFactor}");
                }

                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture.active = texture;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply();

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
                File.WriteAllBytes(outputPath, image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);

                log.Add($"{width}x{height} canvas rect {((RectTransform)canvas.transform).rect.size}");
                return outputPath;
            }
            finally
            {
                RenderTexture.active = active;
                canvas.renderMode = renderMode;
                canvas.worldCamera = worldCamera;
                canvas.planeDistance = planeDistance;
                UnityEngine.Object.DestroyImmediate(cameraGo);
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}

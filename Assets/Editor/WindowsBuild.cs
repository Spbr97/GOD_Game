using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// The only place a Windows x64 player is produced from
/// (STANDALONE_RELEASE_ROADMAP.md TASK 039).
///
/// Two variants, and the difference between them is the whole point of the task:
///
/// - <b>Development</b> passes <see cref="BuildOptions.Development"/>, which is what
///   makes it a development player. Alongside it the build adds
///   <see cref="DeveloperToolsDefine"/>, and that define is what
///   <see cref="Game.DevTools.DebugMode.IsAvailableInThisBuild"/> compiles against, so
///   the debug console, the overlay and every command behind them exist in this
///   player and nowhere else. It also keeps <c>Assets/Scenes/Test.unity</c>, the
///   developer arena, in the scene list.
/// - <b>Release</b> passes <see cref="BuildOptions.None"/> and drops the developer
///   arena. <see cref="DeveloperToolsDefine"/> is absent, so SPEC.md section 52's "debug mode
///   must be disabled in release builds" holds by compilation rather than by a
///   runtime flag someone could flip.
///
/// Both variants read their scene list from <c>EditorBuildSettings</c> rather than
/// from a hard-coded array here, so adding a region to the project in the usual way
/// ships it, and there is no second list to forget to update. Only the developer
/// arena is subtracted, by path, and <see cref="MissingRequiredScenes"/> refuses to
/// build if that subtraction — or an editing accident — left out a scene the game
/// loads by name at runtime.
///
/// Nothing here zips or copies anything. The wrapper script
/// <c>Tools/build-windows.ps1</c> owns packaging, because Unity's editor assemblies
/// do not carry <c>System.IO.Compression.FileSystem</c> on every API compatibility
/// level and a build method that fails at the last step is worse than one that
/// stops at the player.
/// </summary>
public static class WindowsBuild
{
    /// <summary>Kept stable: the smoke-test record, the wrapper script and the eventual ZIP all name this file.</summary>
    public const string ExecutableName = "TheGodWhoWasForgotten.exe";

    /// <summary>Where builds land, relative to the project root. Covered by <c>.gitignore</c>'s <c>[Bb]uild/</c>.</summary>
    public const string DefaultOutputRoot = "Build/Windows";

    /// <summary>The developer arena. Reachable only from the Editor; never loaded by name at runtime.</summary>
    public const string DeveloperOnlyScene = "Assets/Scenes/Test.unity";

    /// <summary>
    /// The scripting define that compiles the developer tools in. Added to the
    /// Development player and to nothing else, which is what SPEC.md section 52's
    /// "debug mode must be disabled in release builds" rests on.
    ///
    /// This replaces <c>DEVELOPMENT_BUILD</c>, which Unity 6 deprecates (<c>UAC0009</c>)
    /// in favour of <c>DEBUG</c> or a runtime check. Neither was the right answer here. A
    /// runtime check is not a gate at all — the code is still in the Release binary and
    /// a flag can be flipped. <c>DEBUG</c> is a gate, but it is Unity's symbol rather
    /// than this project's, so what it means is decided by a compiler configuration that
    /// a future Editor upgrade is free to change underneath us.
    ///
    /// A define the build sets explicitly has neither problem. The name appears in
    /// exactly two places — here and the <c>#if</c> at the top of each gated file — and
    /// nothing else can define it by accident.
    ///
    /// It also fails in the safe direction. If this were ever dropped, the symbol would
    /// be defined nowhere, and the developer tools would vanish from the Development
    /// player rather than appear in the Release one. The wrapper script's first check
    /// catches that immediately; the opposite mistake would ship.
    /// </summary>
    public const string DeveloperToolsDefine = "GAME_DEVELOPER_TOOLS";

    /// <summary>
    /// Scenes the shipped game loads by name — <c>MainMenuController.startingScene</c>
    /// and <c>PauseMenu.mainMenuScene</c>. A player missing either is not a player,
    /// so their absence is a build failure and not a warning nobody reads.
    ///
    /// Agniya joined them in TASK 041. It is named by the temple door's
    /// <c>SceneExit</c> rather than by code, which is content and not compiled, but it
    /// fails in exactly the same way: one log line, no visible error, and a player
    /// standing in front of a door that does nothing.
    /// </summary>
    public static readonly string[] RequiredScenes =
    {
        "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/Avarsha.unity",
        "Assets/Scenes/Agniya.unity"
    };

    // ------------------------------------------------------------------ entry points

    [MenuItem("God Game/Build/Windows Development Player")]
    public static void DevelopmentFromMenu() => Run(development: true);

    [MenuItem("God Game/Build/Windows Release Player")]
    public static void ReleaseFromMenu() => Run(development: false);

    /// <summary>Batchmode entry point: <c>-executeMethod WindowsBuild.Development</c>.</summary>
    public static void Development() => RunAndExit(development: true);

    /// <summary>Batchmode entry point: <c>-executeMethod WindowsBuild.Release</c>.</summary>
    public static void Release() => RunAndExit(development: false);

    private static void RunAndExit(bool development)
    {
        var succeeded = Run(development);

        // Batchmode's own exit code is 0 as long as Unity itself did not fall over, so
        // a failed BuildPipeline call would otherwise look like a successful run to
        // the wrapper script and to CI.
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(succeeded ? 0 : 1);
        }
    }

    /// <summary>
    /// Runs <see cref="Game.EditorTools.ContentValidation"/> and reports it. Returns false
    /// when the content has errors in it, having said which.
    /// </summary>
    private static bool ContentIsValid(string variant)
    {
        var issues = Game.EditorTools.ContentValidation.ValidateAll();
        Game.EditorTools.ContentValidation.Report(issues, $"[BUILD] Content validation for the {variant} player");

        if (!Game.EditorTools.ContentValidation.HasErrors(issues))
        {
            return true;
        }

        UnityEngine.Debug.LogError(
            $"[BUILD] Refusing to build the {variant} player: the authored content has errors in it. " +
            "Every one is listed above. Run God Game > Validate Content in the Editor to see them with " +
            "the offending asset selected.");
        return false;
    }

    // ------------------------------------------------------------------ the build

    /// <summary>
    /// Builds one variant. Returns false, having logged why, rather than throwing:
    /// the caller's job is an exit code.
    /// </summary>
    public static bool Run(bool development)
    {
        var variant = VariantName(development);
        var scenes = ScenesFor(development);

        var missing = MissingRequiredScenes(scenes);
        if (missing.Count > 0)
        {
            UnityEngine.Debug.LogError(
                $"[BUILD] Refusing to build the {variant} player: {string.Join(", ", missing)} " +
                "is enabled nowhere in Build Settings, and the game loads it by name at runtime. " +
                "Add it under File > Build Profiles before building.");
            return false;
        }

        // TASK 042's gate: invalid content fails validation before a build. Run before
        // anything is deleted or switched, so a refusal costs nothing and leaves the
        // project exactly as it was.
        //
        // Errors only. Warnings are logged and the build continues, because a warning
        // means "look at this", and a project mid-production always has things to look
        // at -- a validator that refuses to build over them is a validator people learn
        // to pass a flag to, which is the same as not having one.
        if (!ContentIsValid(variant))
        {
            return false;
        }

        var outputDirectory = Path.Combine(OutputRoot(), variant);
        var locationPathName = Path.Combine(outputDirectory, ExecutableName);

        // A player directory is not incremental in any way we rely on, and a stale
        // *_Data folder from a different scene list is a genuinely confusing bug to
        // chase in a smoke test. Start empty every time.
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        Directory.CreateDirectory(outputDirectory);

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        }

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = locationPathName,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,

            // BuildOptions.Development still makes this a development player — the
            // profiler, the stack traces, the console. What it no longer does here is
            // decide whether the developer tools are compiled in; that is
            // DeveloperToolsDefine's job, for the reasons given on it.
            options = development ? BuildOptions.Development : BuildOptions.None,

            extraScriptingDefines = development
                ? new[] { DeveloperToolsDefine }
                : Array.Empty<string>()
        };

        UnityEngine.Debug.Log(
            $"[BUILD] {variant} Windows x64 player -> {locationPathName}\n" +
            $"[BUILD] Unity {Application.unityVersion}, {scenes.Length} scene(s):\n  " +
            string.Join("\n  ", scenes));

        // Captured immediately before the build and put back immediately after it,
        // whether it succeeded or not. See RenderSettingsFiles.
        var renderSettings = CaptureRenderSettings();

        var report = BuildPipeline.BuildPlayer(options);

        RestoreRenderSettings(renderSettings);

        var summary = report.summary;
        var succeeded = summary.result == BuildResult.Succeeded;

        if (!succeeded)
        {
            UnityEngine.Debug.LogError(
                $"[BUILD] {variant} build {summary.result} with {summary.totalErrors} error(s) " +
                $"after {summary.totalTime}. See the lines above for the first failure.");
            return false;
        }

        WriteManifest(outputDirectory, variant, development, scenes, summary);

        UnityEngine.Debug.Log(
            $"[BUILD] {variant} build succeeded in {summary.totalTime}, " +
            $"{summary.totalSize / (1024 * 1024)} MB, {summary.totalWarnings} warning(s).");

        return true;
    }

    // --------------------------------------------------- render settings churn

    /// <summary>
    /// The files a player build rewrites as a side effect, and the reason
    /// <c>git status</c> was never clean after one.
    ///
    /// Building a URP project makes the pipeline strip shader variants, and it records
    /// what it stripped by writing <c>m_Prefilter*</c> fields back into the render
    /// pipeline assets and into Graphics Settings. Those are build outputs living in
    /// source files. They change with the build target, the quality level and the scene
    /// list, so every build produced a diff nobody wrote, in four files nobody touched —
    /// which is exactly how a real settings change gets committed by accident inside
    /// "revert the build noise", or reverted by accident along with it.
    /// </summary>
    private static readonly string[] RenderSettingsFiles =
    {
        "Assets/Settings/PC_RPAsset.asset",
        "Assets/Settings/DefaultVolumeProfile.asset",
        "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset",
        "ProjectSettings/GraphicsSettings.asset"
    };

    /// <summary>
    /// Reads the render settings files as they stand, so <see cref="RestoreRenderSettings"/>
    /// can put them back. A file that does not exist is recorded as absent and skipped
    /// on the way back, so renaming one of these is not a build failure.
    /// </summary>
    private static Dictionary<string, string> CaptureRenderSettings()
    {
        var captured = new Dictionary<string, string>();
        var root = ProjectRoot();

        foreach (var relative in RenderSettingsFiles)
        {
            var full = Path.Combine(root, relative);

            try
            {
                if (File.Exists(full))
                {
                    captured[relative] = File.ReadAllText(full);
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    $"[BUILD] Could not read '{relative}' before the build ({exception.Message}). " +
                    "It will be left as the build leaves it.");
            }
        }

        return captured;
    }

    /// <summary>
    /// Puts the render settings back the way <see cref="CaptureRenderSettings"/> found
    /// them, and says which ones the build had changed.
    ///
    /// Restoring rather than committing the churn is deliberate. The stripping record is
    /// derived from the build, so it is reproducible from the build and belongs in the
    /// player, not in the repository. Nothing at runtime reads it back out of these
    /// files — the player carries its own copy.
    /// </summary>
    private static void RestoreRenderSettings(Dictionary<string, string> captured)
    {
        if (captured == null || captured.Count == 0)
        {
            return;
        }

        var root = ProjectRoot();
        var restored = new List<string>();

        foreach (var pair in captured)
        {
            var full = Path.Combine(root, pair.Key);

            try
            {
                if (!File.Exists(full) || File.ReadAllText(full) == pair.Value)
                {
                    continue;
                }

                File.WriteAllText(full, pair.Value);
                restored.Add(pair.Key);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    $"[BUILD] Could not restore '{pair.Key}' after the build ({exception.Message}). " +
                    "Revert it by hand with 'git checkout --' before committing.");
            }
        }

        if (restored.Count == 0)
        {
            return;
        }

        AssetDatabase.Refresh();
        UnityEngine.Debug.Log(
            $"[BUILD] Reverted shader-stripping churn in {restored.Count} render settings file(s): " +
            string.Join(", ", restored));
    }

    // ------------------------------------------------------------------ scene list

    /// <summary>
    /// The enabled scenes from Build Settings, less the developer arena in a Release
    /// build. Reading the real list is deliberate — see the class summary.
    /// </summary>
    public static string[] ScenesFor(bool development)
    {
        var enabled = EditorBuildSettings.scenes
            .Where(scene => scene.enabled && !string.IsNullOrEmpty(scene.path))
            .Select(scene => scene.path);

        if (!development)
        {
            enabled = enabled.Where(path => !SamePath(path, DeveloperOnlyScene));
        }

        return enabled.ToArray();
    }

    /// <summary>
    /// Which of <see cref="RequiredScenes"/> a candidate list lacks. Public so an
    /// EditMode test can assert the same rule the build enforces, without building.
    /// </summary>
    public static List<string> MissingRequiredScenes(IEnumerable<string> scenes)
    {
        var present = scenes as ICollection<string> ?? scenes.ToList();
        return RequiredScenes.Where(required => !present.Any(path => SamePath(path, required))).ToList();
    }

    /// <summary>Scene paths come from a YAML asset and from constants here; compare them the way the filesystem would.</summary>
    private static bool SamePath(string left, string right) =>
        string.Equals(left?.Replace('\\', '/'), right?.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);

    public static string VariantName(bool development) => development ? "Development" : "Release";

    // ------------------------------------------------------------------ build record

    /// <summary>
    /// Writes <c>build-info.txt</c> beside the player. TASK 057 has to be able to say
    /// which commit a downloaded binary came from, and the only moment that is known
    /// for certain is this one. Written after a success so its presence means the
    /// player next to it is real.
    /// </summary>
    private static void WriteManifest(string outputDirectory, string variant, bool development,
        string[] scenes, BuildSummary summary)
    {
        var text = new StringBuilder()
            .AppendLine($"{Application.productName} {PlayerSettings.bundleVersion}")
            .AppendLine($"Variant          : {variant}")
            .AppendLine($"Platform         : Windows x64 (StandaloneWindows64)")
            .AppendLine($"Unity            : {Application.unityVersion}")
            .AppendLine($"Scripting backend: {PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone)}")
            .AppendLine($"{DeveloperToolsDefine}: {(development ? "defined - developer tools present" : "absent - developer tools compiled out")}")
            .AppendLine($"Built (UTC)      : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}")
            .AppendLine($"Commit           : {GitDescription()}")
            .AppendLine($"Size             : {summary.totalSize / (1024 * 1024)} MB")
            .AppendLine($"Build warnings   : {summary.totalWarnings}")
            .AppendLine()
            .AppendLine("Scenes:");

        foreach (var scene in scenes)
        {
            text.AppendLine($"  {scene}");
        }

        File.WriteAllText(Path.Combine(outputDirectory, "build-info.txt"), text.ToString());
    }

    /// <summary>
    /// The current commit and whether its source content differed. Best effort: a build from a
    /// source archive with no <c>.git</c> is legitimate, and should say so rather than
    /// fail a build over provenance metadata.
    /// </summary>
    private static string GitDescription()
    {
        var commit = Git("rev-parse HEAD");
        if (string.IsNullOrEmpty(commit))
        {
            return "unknown (no git metadata available at build time)";
        }

        // Unity can rewrite line endings while importing a fresh clone. Git status
        // then reports those files as modified even when the normalized content is
        // identical to HEAD. Compare actual content and include untracked files so
        // the manifest describes source changes, not import-only file churn.
        var trackedChanges = Git("diff --name-only HEAD --");
        var untrackedFiles = Git("ls-files --others --exclude-standard");
        var dirty = !string.IsNullOrEmpty(trackedChanges) ||
                    !string.IsNullOrEmpty(untrackedFiles);
        return dirty ? $"{commit} (working tree had uncommitted changes)" : commit;
    }

    private static string Git(string arguments)
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = ProjectRoot(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);
            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string OutputRoot()
    {
        var fromCommandLine = CommandLineValue("-buildOutput");
        return string.IsNullOrEmpty(fromCommandLine)
            ? Path.Combine(ProjectRoot(), DefaultOutputRoot)
            : fromCommandLine;
    }

    /// <summary>Application.dataPath is <c>&lt;project&gt;/Assets</c>.</summary>
    private static string ProjectRoot() => Directory.GetParent(Application.dataPath)!.FullName;

    private static string CommandLineValue(string flag)
    {
        var arguments = Environment.GetCommandLineArgs();
        for (var i = 0; i < arguments.Length - 1; i++)
        {
            if (string.Equals(arguments[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return arguments[i + 1];
            }
        }

        return null;
    }
}

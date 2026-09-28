// The whole file, not merely its branches: a release player has no smoke-test
// harness in it at all. STANDALONE_RELEASE_ROADMAP.md TASK 039 asks the Release
// variant to exclude developer-only controls, and a self-driving test rig that can
// wipe save files and quit the process is exactly that (SPEC.md section 52).
#if UNITY_EDITOR || GAME_DEVELOPER_TOOLS

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Core;
using Game.Player;
using Game.Save;
using Game.UI;
using Game.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.DevTools
{
    /// <summary>
    /// Drives the smoke test STANDALONE_RELEASE_ROADMAP.md TASK 039 requires — Main
    /// Menu, New Game, Avarsha, save, back out, Continue — inside the built Windows
    /// player, with no Editor and nobody at the keyboard.
    ///
    /// Why this exists rather than a written manual checklist alone: the failures this
    /// task is meant to catch are the ones that only happen outside the Editor. A
    /// scene missing from the build, a resource that only resolved because the Editor
    /// had it imported, a save path that differs under
    /// <see cref="Application.persistentDataPath"/>. Those cannot be caught by the
    /// PlayMode suite by definition, and a human clicking through once per release
    /// catches them late and inconsistently. This runs the same path every build.
    ///
    /// It deliberately does <em>not</em> replace the manual pass. It presses no
    /// buttons and reads no pixels, so it cannot tell you a label is unreadable or a
    /// control feels wrong. It answers one narrower question honestly: does the
    /// shipped binary get a new player from launch to a reloaded save. The manual
    /// pass recorded in <c>TEST_PLAN.md</c> covers the rest.
    ///
    /// It drives <see cref="MainMenuController"/>'s own New Game and Continue paths
    /// through a development-only seam rather than reimplementing them, so a bug in
    /// difficulty application, world-state reset or save precedence fails this test
    /// instead of hiding behind a parallel copy of the logic.
    ///
    /// Activated only by <c>-smokeTest</c> or <c>-smokeTestResume</c> on the command
    /// line, so double-clicking the Development player starts an ordinary game.
    /// </summary>
    public class StandaloneSmokeTest : MonoBehaviour
    {
        /// <summary>Starts the fresh-run phase of the harness.</summary>
        public const string EnableArgument = "-smokeTest";

        /// <summary>Second process: resume the save written by the first smoke run.</summary>
        public const string ResumeArgument = "-smokeTestResume";

        /// <summary>Optional: <c>-smokeTestReport &lt;path&gt;</c> writes the record where the wrapper script can read it.</summary>
        public const string ReportArgument = "-smokeTestReport";

        private const string MainMenuScene = "MainMenu";
        private const string GameplayScene = "Avarsha";

        /// <summary>The second gameplay scene, reached through the temple door (TASK 041).</summary>
        private const string TempleScene = "Agniya";

        /// <summary>Set before travelling and looked for afterwards, to prove world flags crossed.</summary>
        private const string TravelMarkerFlag = "SMOKE_TEST_TRAVELLED";

        /// <summary>
        /// A distinctive memory integrity, set before travelling. Unlike a world flag —
        /// <c>WorldState</c> survives a scene load on its own — <c>MemoryManager</c> is
        /// scene-scoped and is destroyed with the scene, so this is what actually proves
        /// progression was carried rather than merely surviving by accident of lifetime.
        /// </summary>
        private const float TravelMarkerIntegrity = 0.625f;

        /// <summary>
        /// A hung step must not hang the build. Generous, because the first launch on a
        /// cold shader cache is genuinely slow, and a false failure here would teach
        /// everyone to ignore the result.
        /// </summary>
        private const float StepTimeoutSeconds = 120f;

        private readonly List<string> log = new();
        private readonly List<string> failures = new();
        private string reportPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!HasArgument(EnableArgument) && !HasArgument(ResumeArgument))
            {
                return;
            }

            // Only from the first scene of a launch. AfterSceneLoad runs once per
            // process, but a second harness would still be a hazard worth naming.
            var host = new GameObject("StandaloneSmokeTest");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<StandaloneSmokeTest>();
        }

        private void Start()
        {
            reportPath = ArgumentValue(ReportArgument)
                ?? Path.Combine(Application.persistentDataPath, "smoke-test-report.txt");

            StartCoroutine(HasArgument(ResumeArgument) ? RunResume() : RunAll());
        }

        private IEnumerator RunResume()
        {
            Record($"{Application.productName} smoke-test resume in a new process");
            Record($"Unity {Application.unityVersion}, Windows x64 player");
            Record($"persistentDataPath: {Application.persistentDataPath}");
            Record($"Started (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            Record(string.Empty);

            Check("developer tools are present in a Development player",
                DebugMode.IsAvailableInThisBuild);
            yield return Step("1. reach the Main Menu", () => FindMenu() != null);

            var saveRoot = SaveStorage.DefaultRoot;
            Record($"   save root: {saveRoot}");
            Check("the previous process left a manual save", SaveStorage.Exists(saveRoot, SaveSlot.Manual));
            var hasContinue = SaveBrowser.TryFindMostRecent(saveRoot, out var continueSlot);
            Check("the menu offers the final manual save", hasContinue && continueSlot == SaveSlot.Manual);

            yield return Step("2. Continue loads the previous process's save", () =>
            {
                var menu = FindMenu();
                if (menu == null) return false;
                menu.ContinueForSmokeTest();
                return true;
            });

            yield return Step($"3. '{GameplayScene}' is playable after a cold start",
                () => SceneManager.GetActiveScene().name == GameplayScene && FindPlayer() != null);
            Check("progression survived a process restart",
                WorldState.Instance != null && WorldState.Instance.GetFlag(TravelMarkerFlag)
                && IntegrityIsStillTheMarker());
            Check("saving is allowed after a cold-start load",
                SaveManager.Instance != null && SaveManager.Instance.IsSafeToSave);
            Finish();
        }

        private IEnumerator RunAll()
        {
            Record($"{Application.productName} smoke test");
            // Named rather than derived: this is runtime code and WindowsBuild is an Editor
            // class, so the two say the same word and a test holds them to it.
            Record($"Unity {Application.unityVersion}, Windows x64 player, GAME_DEVELOPER_TOOLS defined");
            Record($"persistentDataPath: {Application.persistentDataPath}");
            Record($"Started (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            Record(string.Empty);

            // The real Editor-free failure mode this catches: a scene that builds fine
            // but was never added to the player.
            Check("developer tools are present in a Development player",
                DebugMode.IsAvailableInThisBuild);

            Check($"the player contains {SceneManager.sceneCountInBuildSettings} scene(s) including '{GameplayScene}'",
                Application.CanStreamedLevelBeLoaded(GameplayScene));

            yield return Step("1. reach the Main Menu", () => FindMenu() != null);

            var saveRoot = SaveRoot();
            Record($"   save root: {saveRoot}");

            // A fresh-save round trip is the point. SaveStorage.DefaultRoot routes
            // smoke runs to SmokeTestSaves, separate from the normal player's Saves.
            ClearSaves(saveRoot);
            Check("no save exists before New Game", !SaveBrowser.TryFindMostRecent(saveRoot, out _));

            yield return Step("2. New Game loads the starting scene", () =>
            {
                var menu = FindMenu();
                if (menu == null)
                {
                    return false;
                }

                menu.BeginNewGameForSmokeTest(DifficultyMode.Normal);
                return true;
            });

            yield return Step($"3. '{GameplayScene}' is active with a player in it",
                () => SceneManager.GetActiveScene().name == GameplayScene && FindPlayer() != null);

            // Systems the save round trip depends on. Checked here rather than assumed,
            // because a manager missing from a scene is precisely the wiring accident a
            // player build surfaces and the Editor does not.
            Check("SaveManager is present in the gameplay scene", SaveManager.Instance != null);
            Check("WorldState is present in the gameplay scene",
                UnityEngine.Object.FindAnyObjectByType<WorldState>(FindObjectsInactive.Include) != null);

            yield return Step("4. a manual save is written", () =>
            {
                var manager = SaveManager.Instance;
                return manager != null && manager.IsSafeToSave && manager.Save(SaveSlot.Manual);
            });

            Check("the save file exists on disk", SaveStorage.Exists(saveRoot, SaveSlot.Manual));
            Check("the menu would now offer Continue", SaveBrowser.TryFindMostRecent(saveRoot, out _));

            // ---------------------------------------------- TASK 041: the round trip

            Check($"the player contains '{TempleScene}'", Application.CanStreamedLevelBeLoaded(TempleScene));

            var world = UnityEngine.Object.FindAnyObjectByType<WorldState>(FindObjectsInactive.Include);
            if (world != null)
            {
                world.SetFlag(TravelMarkerFlag);
            }

            var memories = Game.Memory.MemoryManager.Instance;
            if (memories != null)
            {
                memories.RestoreIntegrity01(TravelMarkerIntegrity);
            }

            var positionInAvarsha = FindPlayer() != null ? FindPlayer().transform.position : Vector3.zero;
            Record($"   left Avarsha from {positionInAvarsha}");

            yield return Step($"5. the temple door leads to '{TempleScene}'", () =>
            {
                var door = FindExitTo(TempleScene);
                if (door == null || !door.CanInteract)
                {
                    return false;
                }

                var player = FindPlayer();
                door.Interact(player != null ? player.gameObject : null);
                return true;
            });

            yield return Step($"6. '{TempleScene}' is active with a player in it",
                () => SceneManager.GetActiveScene().name == TempleScene && FindPlayer() != null);

            Check("the arrival landed on the temple's own spawn point", ArrivedAt("FromAvarsha"));
            Check("world flags survived the journey",
                WorldState.Instance != null && WorldState.Instance.GetFlag(TravelMarkerFlag));
            Check("a scene-scoped manager's state survived the journey", IntegrityIsStillTheMarker());
            Check("SaveManager is present in the temple", SaveManager.Instance != null);
            Check("saving is allowed again once the journey is over",
                SaveManager.Instance != null && SaveManager.Instance.IsSafeToSave);

            yield return Step($"7. the way out leads back to '{GameplayScene}'", () =>
            {
                var door = FindExitTo(GameplayScene);
                if (door == null || !door.CanInteract)
                {
                    return false;
                }

                var player = FindPlayer();
                door.Interact(player != null ? player.gameObject : null);
                return true;
            });

            yield return Step($"8. '{GameplayScene}' is active again with a player in it",
                () => SceneManager.GetActiveScene().name == GameplayScene && FindPlayer() != null);

            Check("the return landed on Avarsha's arrival point", ArrivedAt("FromAgniya"));

            // The defect TASK 041 names: coming back must not restore the position the
            // save recorded, nor a position measured in the temple.
            Check("the return did not restore the position the save was written at",
                FindPlayer() == null
                || Vector3.Distance(FindPlayer().transform.position, positionInAvarsha) > 1f);

            Check("progression is intact after the round trip",
                WorldState.Instance != null && WorldState.Instance.GetFlag(TravelMarkerFlag)
                && IntegrityIsStillTheMarker());

            yield return Step("9. a save can be written after the round trip", () =>
            {
                var manager = SaveManager.Instance;
                return manager != null && manager.IsSafeToSave && manager.Save(SaveSlot.Manual);
            });

            // Standing in for quitting and relaunching. A real process restart cannot be
            // done from inside the process, so this covers the part that breaks in
            // practice — tearing the gameplay scene down and reading the save back
            // through the menu's own Continue path. The wrapper script's second launch
            // covers the rest.
            yield return Step("10. return to the Main Menu", () =>
            {
                var sceneManager = GameSceneManager.Instance;
                if (sceneManager == null)
                {
                    return false;
                }

                sceneManager.LoadScene(MainMenuScene);
                return true;
            });

            yield return Step("11. the Main Menu is active again",
                () => SceneManager.GetActiveScene().name == MainMenuScene && FindMenu() != null);

            yield return Step("12. Continue loads the saved scene", () =>
            {
                var menu = FindMenu();
                if (menu == null)
                {
                    return false;
                }

                menu.ContinueForSmokeTest();
                return true;
            });

            yield return Step("13. the saved game is playable again",
                () => SceneManager.GetActiveScene().name == GameplayScene && FindPlayer() != null);

            Finish();
        }

        // ------------------------------------------------------------------ step plumbing

        /// <summary>
        /// Runs <paramref name="attempt"/> once per frame until it returns true or the
        /// step times out. One shape for both "do a thing" and "wait for a thing"
        /// because a scene load is both: the call starts it and the next frames finish
        /// it, and a step that only fired once would race every load in this sequence.
        /// </summary>
        private IEnumerator Step(string description, Func<bool> attempt)
        {
            if (failures.Count > 0)
            {
                Record($"SKIP {description} (an earlier step already failed)");
                yield break;
            }

            var deadline = Time.realtimeSinceStartup + StepTimeoutSeconds;
            var succeeded = false;

            while (Time.realtimeSinceStartup < deadline)
            {
                var error = TryAttempt(attempt, out succeeded);
                if (error != null)
                {
                    Fail($"{description} - threw {error}");
                    yield break;
                }

                if (succeeded)
                {
                    break;
                }

                yield return null;
            }

            if (succeeded)
            {
                Record($"PASS {description}");
            }
            else
            {
                Fail($"{description} - still not true after {StepTimeoutSeconds:0} s");
            }
        }

        /// <summary>A throw inside a step is a failure with a message, not a dead coroutine. Separate because C# forbids yield inside try/catch.</summary>
        private static string TryAttempt(Func<bool> attempt, out bool succeeded)
        {
            try
            {
                succeeded = attempt();
                return null;
            }
            catch (Exception exception)
            {
                succeeded = false;
                return exception.ToString();
            }
        }

        private void Check(string description, bool condition)
        {
            if (condition)
            {
                Record($"PASS {description}");
            }
            else
            {
                Fail(description);
            }
        }

        private void Record(string line)
        {
            log.Add(line);
            GameLogger.Log(LogCategory.Game, $"[SMOKE] {line}");
        }

        private void Fail(string description)
        {
            var line = $"FAIL {description}";
            log.Add(line);
            failures.Add(description);
            GameLogger.LogError(LogCategory.Game, $"[SMOKE] {line}", this);
        }

        // ------------------------------------------------------------------ scene lookups

        private static MainMenuController FindMenu() =>
            UnityEngine.Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Exclude);

        private static PlayerController FindPlayer() =>
            UnityEngine.Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);

        /// <summary>
        /// The door in this scene that leads to <paramref name="scene"/>. Found by asking
        /// the content rather than by naming an object, so the check exercises the same
        /// wiring a player's keypress would.
        /// </summary>
        private static SceneExit FindExitTo(string scene)
        {
            var exits = UnityEngine.Object.FindObjectsByType<SceneExit>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (var i = 0; i < exits.Length; i++)
            {
                if (exits[i].TargetScene == scene)
                {
                    return exits[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Whether the player is standing at the named arrival point. Two metres of
        /// tolerance: the point is where the player is placed, but a CharacterController
        /// settles onto the ground over the following frames.
        /// </summary>
        private static bool ArrivedAt(string spawnId)
        {
            var spawn = SceneSpawnPoint.Find(spawnId);
            var player = FindPlayer();

            return spawn != null
                   && player != null
                   && Vector3.Distance(player.transform.position, spawn.Position) <= 2f;
        }

        private static bool IntegrityIsStillTheMarker()
        {
            var memories = Game.Memory.MemoryManager.Instance;
            return memories != null && Mathf.Abs(memories.Integrity - TravelMarkerIntegrity) < 0.001f;
        }

        private static string SaveRoot() =>
            SaveManager.Instance != null ? SaveManager.Instance.Root : SaveStorage.DefaultRoot;

        /// <summary>
        /// Removes every smoke-test slot's primary, backup and temporary file so the
        /// run starts from nothing. SaveStorage.DefaultRoot isolates these files from
        /// the player's normal saves.
        /// </summary>
        private void ClearSaves(string root)
        {
            var isolatedRoot = Path.Combine(Application.persistentDataPath, "SmokeTestSaves");
            if (!string.Equals(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(isolatedRoot).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                Fail($"refusing to clear saves outside the isolated smoke-test directory: {root}");
                return;
            }

            var removed = 0;

            foreach (SaveSlot slot in Enum.GetValues(typeof(SaveSlot)))
            {
                foreach (var path in new[]
                         {
                             SaveStorage.PrimaryPath(root, slot),
                             SaveStorage.BackupPath(root, slot),
                             SaveStorage.TemporaryPath(root, slot)
                         })
                {
                    try
                    {
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                            removed++;
                        }
                    }
                    catch (Exception exception)
                    {
                        // Not a test failure by itself — the next check is whether a save
                        // is findable, and that will fail loudly if this mattered.
                        GameLogger.LogWarning(LogCategory.Save,
                            $"[SMOKE] could not delete '{path}': {exception.Message}", this);
                    }
                }
            }

            Record($"   cleared {removed} pre-existing save file(s) so this is a fresh-save run");
        }

        // ------------------------------------------------------------------ result

        private void Finish()
        {
            Record(string.Empty);

            var passed = failures.Count == 0;
            Record(passed
                ? "RESULT: PASS - launch to reloaded save completed in the Windows player."
                : $"RESULT: FAIL - {failures.Count} problem(s): {string.Join("; ", failures)}");

            WriteReport();

            // The wrapper script reads this exit code. Without it a failing smoke test
            // would look to CI exactly like a passing one.
            Application.Quit(passed ? 0 : 1);
        }

        private void WriteReport()
        {
            try
            {
                var directory = Path.GetDirectoryName(reportPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var text = new StringBuilder();
                foreach (var line in log)
                {
                    text.AppendLine(line);
                }

                File.WriteAllText(reportPath, text.ToString());
                GameLogger.Log(LogCategory.Game, $"[SMOKE] report written to {reportPath}");
            }
            catch (Exception exception)
            {
                // The log already carries every line; losing the file is not worth
                // turning a passing run into a failure.
                GameLogger.LogWarning(LogCategory.Game,
                    $"[SMOKE] could not write the report to '{reportPath}': {exception.Message}", this);
            }
        }

        // ------------------------------------------------------------------ command line

        private static bool HasArgument(string flag)
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ArgumentValue(string flag)
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
}

#endif

using System.IO;
using System.Linq;
using NUnit.Framework;
using Game.EditorTools;
using Game.Quests;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Guards the promises <see cref="WindowsBuild"/> makes about the two Windows
    /// variants (STANDALONE_RELEASE_ROADMAP.md TASK 039).
    ///
    /// These cannot verify that a player runs — only a build and the smoke test can do
    /// that, and both take minutes. What they can do is catch the cheap mistakes that
    /// would otherwise be found by shipping: a scene dropped from Build Settings, the
    /// developer arena leaking into a Release download, the boot scene reordered, or
    /// the wrapper script drifting away from the constants it depends on. Every one of
    /// those is a silent failure in the Editor and a broken download to a player.
    /// </summary>
    public class WindowsBuildTests
    {
        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        private static string WrapperScriptPath => Path.Combine(ProjectRoot, "Tools", "build-windows.ps1");

        // ---------------------------------------------------------------- scene lists

        [Test]
        public void ReleaseScenes_ExcludeTheDeveloperArena()
        {
            var scenes = WindowsBuild.ScenesFor(development: false);

            Assert.IsFalse(
                scenes.Any(path => path.Replace('\\', '/') == WindowsBuild.DeveloperOnlyScene),
                $"The Release player must not ship {WindowsBuild.DeveloperOnlyScene}: it is a developer " +
                "arena with no way in from the menu, and SPEC.md section 52 keeps developer-only " +
                "content out of a release build.");
        }

        [Test]
        public void DevelopmentScenes_IncludeTheDeveloperArena()
        {
            // The other half of the contract. Without this, dropping the arena from
            // Build Settings would quietly remove it from the developer's own player
            // and the Release test above would still pass.
            var scenes = WindowsBuild.ScenesFor(development: true);

            Assert.IsTrue(
                scenes.Any(path => path.Replace('\\', '/') == WindowsBuild.DeveloperOnlyScene),
                $"The Development player is where {WindowsBuild.DeveloperOnlyScene} is meant to be usable.");
        }

        [Test]
        public void BothVariants_ContainEverySceneLoadedByName()
        {
            foreach (var development in new[] { true, false })
            {
                var scenes = WindowsBuild.ScenesFor(development);
                var missing = WindowsBuild.MissingRequiredScenes(scenes);

                Assert.IsEmpty(missing,
                    $"The {WindowsBuild.VariantName(development)} player is missing {string.Join(", ", missing)}. " +
                    "MainMenuController loads the starting scene and PauseMenu loads the menu by name, so a " +
                    "player without them dead-ends at runtime with nothing but a log line.");
            }
        }

        [Test]
        public void BuildSettings_StartAtTheMainMenu()
        {
            // A player boots the first enabled scene. If Avarsha or the test arena ever
            // ends up at index 0, the game launches straight into gameplay with no
            // difficulty chosen and no save decision made.
            var first = EditorBuildSettings.scenes.FirstOrDefault(scene => scene.enabled);

            Assert.IsNotNull(first, "No scene is enabled in Build Settings, so no player can be built.");
            Assert.AreEqual("Assets/Scenes/MainMenu.unity", first.path.Replace('\\', '/'),
                "The first enabled scene is what the player boots into, and that has to be the Main Menu.");
        }

        // ---------------------------------------------------------------- the guard itself

        [Test]
        public void MissingRequiredScenes_NamesWhatIsAbsent()
        {
            var missing = WindowsBuild.MissingRequiredScenes(new[] { "Assets/Scenes/MainMenu.unity" });

            Assert.AreEqual(
                new[] { "Assets/Scenes/Avarsha.unity", "Assets/Scenes/Agniya.unity" },
                missing.ToArray());
        }

        [Test]
        public void MissingRequiredScenes_TreatsWindowsSeparatorsAsTheSamePath()
        {
            // Scene paths reach this from a YAML asset, from constants, and on Windows
            // sometimes from Path.Combine. A separator mismatch here would refuse a
            // perfectly good build, which is the kind of failure people work around
            // rather than fix.
            var missing = WindowsBuild.MissingRequiredScenes(new[]
            {
                @"Assets\Scenes\MainMenu.unity",
                @"Assets\Scenes\Avarsha.unity",
                @"Assets\Scenes\Agniya.unity"
            });

            Assert.IsEmpty(missing);
        }

        [Test]
        public void MissingRequiredScenes_ReportsAnEmptyListAsFullyMissing()
        {
            var missing = WindowsBuild.MissingRequiredScenes(new string[0]);

            Assert.AreEqual(WindowsBuild.RequiredScenes.Length, missing.Count);
        }

        // ---------------------------------------------------------------- wrapper script

        [Test]
        public void WrapperScript_IsCheckedIn()
        {
            Assert.IsTrue(File.Exists(WrapperScriptPath),
                "Tools/build-windows.ps1 is the one-command build TASK 039 requires. " +
                "A build method nobody can invoke the same way twice is not reproducible.");
        }

        [Test]
        public void WrapperScript_ExpectsTheSameExecutableNameTheBuildProduces()
        {
            // The script looks for the .exe by name to smoke test and package it. If the
            // constant changes and the script does not, the build "succeeds" and then
            // reports that it produced nothing.
            var script = File.ReadAllText(WrapperScriptPath);

            Assert.IsTrue(script.Contains(WindowsBuild.ExecutableName),
                $"Tools/build-windows.ps1 does not mention '{WindowsBuild.ExecutableName}', " +
                "so it cannot find the player this build method writes.");
        }

        [Test]
        public void WrapperScript_ReadsThePinnedEditorVersionRatherThanHardCodingOne()
        {
            var script = File.ReadAllText(WrapperScriptPath);

            Assert.IsTrue(script.Contains("ProjectVersion.txt"),
                "The wrapper must take the Editor version from ProjectSettings/ProjectVersion.txt. " +
                "A version written into the script goes stale on the next Editor upgrade and the " +
                "build stops matching the project it came from.");
        }

        [Test]
        public void GeneratedPlayers_AreNotTracked()
        {
            // TASK 039 says to keep generated players out of Git, and a multi-hundred-MB
            // accidental commit is not something you undo tidily.
            var gitignore = Path.Combine(ProjectRoot, ".gitignore");
            Assert.IsTrue(File.Exists(gitignore), "No .gitignore at the repository root.");

            var text = File.ReadAllText(gitignore);
            Assert.IsTrue(text.Contains("[Bb]uild/") || text.Contains("Build/"),
                $"'{WindowsBuild.DefaultOutputRoot}' is where players are written and .gitignore does not cover it.");
        }

        /// <summary>
        /// TASK 042 put content validation in front of the build. That the gate is wired
        /// was proved by reading the code; that it actually *refuses* was not, and a gate
        /// nobody has ever seen close is an assumption rather than a guarantee.
        ///
        /// So this breaks the content for real — a quest asset that can start and never
        /// finish, which the validator rates an error — and asks the build to run.
        ///
        /// It also checks where the refusal happens. <see cref="WindowsBuild.Run"/> empties
        /// the output directory before it builds, so a gate that fired after that point
        /// would still refuse, but would have destroyed the player the author was running
        /// five minutes ago in exchange for nothing. The marker file is how that is told
        /// apart from a clean refusal.
        /// </summary>
        [Test]
        public void Build_RefusesBrokenContentBeforeItTouchesThePreviousPlayer()
        {
            const string probePath = "Assets/Data/Quests/__BuildGateProbe.asset";

            var outputDirectory = Path.Combine(ProjectRoot, WindowsBuild.DefaultOutputRoot, "Development");
            var marker = Path.Combine(outputDirectory, "__build-gate-probe.txt");
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(marker, "Written before the build attempt. Its survival is the assertion.");

            try
            {
                var broken = ScriptableObject.CreateInstance<QuestDefinition>();
                broken.Configure("__BUILD_GATE_PROBE", "Build gate probe",
                    "Deliberately broken content, created and deleted by this test.",
                    new QuestObjective[0]);

                AssetDatabase.CreateAsset(broken, probePath);
                AssetDatabase.SaveAssets();

                Assert.IsTrue(ContentValidation.HasErrors(ContentValidation.ValidateAll()),
                    "The probe asset was supposed to be invalid content. If the validator no longer rates an "
                    + "objective-less quest an error, this test is measuring nothing.");

                // Run reports every issue it refuses over, so the errors below are the
                // expected output and not a failure.
                LogAssert.ignoreFailingMessages = true;
                var succeeded = WindowsBuild.Run(development: true);
                LogAssert.ignoreFailingMessages = false;

                Assert.IsFalse(succeeded,
                    "The build gate let content with errors in it through and produced a player.");
                Assert.IsTrue(File.Exists(marker),
                    "The build refused, but only after it had already deleted the previous player. The content "
                    + "gate has to come before anything destructive in Run.");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
                AssetDatabase.DeleteAsset(probePath);
                AssetDatabase.SaveAssets();

                if (File.Exists(marker))
                {
                    File.Delete(marker);
                }
            }
        }
    }
}

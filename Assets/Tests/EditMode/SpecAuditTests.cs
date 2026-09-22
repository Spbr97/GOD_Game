using Game.AI;
using Game.Core;
using Game.Dialogue;
using Game.DevTools;
using Game.World;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// The requirements closed by the SPEC.md audit (TASK 020-026): world bounds,
    /// the missing-dialogue string, boss arena containment, the debug-mode gate and
    /// the display settings.
    ///
    /// Everything here is pure logic or a single component with no scene around it.
    /// The parts that only go wrong after several frames of a real game loop — the
    /// guards actually pulling something back, the arena resetting a live encounter,
    /// a display change deferred across a load — are in
    /// <c>SpecAuditPlayModeTests</c>, where they belong.
    /// </summary>
    public class SpecAuditTests
    {
        private GameObject scratch;

        [SetUp]
        public void SetUp()
        {
            DebugMode.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            DebugMode.ResetForTests();

            if (scratch != null)
            {
                Object.DestroyImmediate(scratch);
                scratch = null;
            }
        }

        private T NewComponent<T>(string name) where T : Component
        {
            scratch = new GameObject(name);
            return scratch.AddComponent<T>();
        }

        // ------------------------------------------------- SPEC.md section 50, 54 (9, 10)

        [Test]
        public void WorldBounds_BelowTheKillPlane_IsOutsideTheWorld()
        {
            var bounds = NewComponent<WorldBounds>("WorldBounds");
            bounds.Configure(-20f, Vector3.zero, new Vector3(100f, 0f, 100f));

            Assert.IsTrue(bounds.Contains(new Vector3(0f, -19.9f, 0f)));
            Assert.IsFalse(bounds.Contains(new Vector3(0f, -20.1f, 0f)));
        }

        [Test]
        public void WorldBounds_HighAboveTheWorld_IsStillInside()
        {
            var bounds = NewComponent<WorldBounds>("WorldBounds");
            bounds.Configure(-20f, Vector3.zero, new Vector3(100f, 0f, 100f));

            // A jump, a staircase or a tall temple is not an escape. Only the kill
            // plane is a vertical limit; see the class comment on WorldBounds.
            Assert.IsTrue(bounds.Contains(new Vector3(0f, 500f, 0f)));
        }

        [Test]
        public void WorldBounds_PastTheFootprint_IsOutsideTheWorld()
        {
            var bounds = NewComponent<WorldBounds>("WorldBounds");
            bounds.Configure(-20f, Vector3.zero, new Vector3(100f, 0f, 60f));

            Assert.IsTrue(bounds.Contains(new Vector3(49f, 0f, 29f)));
            Assert.IsFalse(bounds.Contains(new Vector3(51f, 0f, 0f)), "51 is past the 50 half-width.");
            Assert.IsFalse(bounds.Contains(new Vector3(0f, 0f, 31f)), "31 is past the 30 half-depth.");
        }

        [Test]
        public void WorldBounds_WithHorizontalLimitsOff_OnlyTheKillPlaneApplies()
        {
            var bounds = NewComponent<WorldBounds>("WorldBounds");
            bounds.Configure(-20f, Vector3.zero, new Vector3(10f, 0f, 10f), horizontal: false);

            Assert.IsTrue(bounds.Contains(new Vector3(9999f, 0f, 9999f)));
            Assert.IsFalse(bounds.Contains(new Vector3(0f, -25f, 0f)));
        }

        // --------------------------------------------------- SPEC.md section 50, dialogue

        [Test]
        public void Dialogue_AMissingGraph_ShowsExactlyTheStringTheSpecRequires()
        {
            var runner = NewComponent<DialogueRunner>("Runner");

            runner.Begin(null, "Mira");

            Assert.IsTrue(runner.IsRunning, "A missing graph must still put something on screen.");
            Assert.AreEqual("[Dialogue unavailable]", runner.CurrentNode.Text);
            Assert.AreEqual(DialogueRunner.UnavailableText, runner.CurrentNode.Text);
            Assert.IsTrue(runner.IsShowingUnavailable);
        }

        [Test]
        public void Dialogue_AMissingGraph_ReturnsFalseSoTheCallersConsequencesDoNotFire()
        {
            var runner = NewComponent<DialogueRunner>("Runner");

            // NpcInteractable only sets its "first spoken" flag when this is true.
            // A fallback panel is not a conversation, and recording one that never
            // happened could advance a quest the player has not done.
            Assert.IsFalse(runner.Begin(null, "Mira"));
        }

        [Test]
        public void Dialogue_TheFallback_IsDismissedLikeAnyOtherLine()
        {
            var runner = NewComponent<DialogueRunner>("Runner");
            runner.Begin(null);

            Assert.IsTrue(runner.Advance(), "The player must be able to close the fallback.");
            Assert.IsFalse(runner.IsRunning);
        }

        [Test]
        public void Dialogue_AGraphWithNoUsableNode_ShowsTheSameString()
        {
            var runner = NewComponent<DialogueRunner>("Runner");
            var graph = ScriptableObject.CreateInstance<DialogueGraph>();

            try
            {
                graph.Configure("EMPTY", new string[0], new DialogueNode[0]);

                // A graph with no nodes is an authoring mistake, and DialogueGraph has
                // logged an error for it since TASK 003. That is the right severity
                // and stays; what changes here is that the player now sees something
                // too, rather than the failure living only in the console.
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "[DIALOGUE] Dialogue graph 'EMPTY' has no nodes.");

                Assert.IsFalse(runner.Begin(graph, "Dev"));
                Assert.IsTrue(runner.IsShowingUnavailable);
                Assert.AreEqual(DialogueRunner.UnavailableText, runner.CurrentNode.Text);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        // ------------------------------------- SPEC.md section 54 (edge case 7), 55, 56

        [Test]
        public void BossArena_Containment_IgnoresHeight()
        {
            var arena = NewComponent<BossArena>("Arena");
            arena.transform.position = Vector3.zero;
            arena.Configure(null, null, arenaRadius: 20f, margin: 0f);

            // A balcony above the arena is not an escape; a fight that reset every
            // time the player jumped would be unplayable.
            Assert.IsTrue(arena.Contains(new Vector3(0f, 40f, 0f)));
            Assert.IsTrue(arena.Contains(new Vector3(0f, -40f, 0f)));
        }

        [Test]
        public void BossArena_Containment_AllowsTheEscapeMarginBeforeCountingItAsOutside()
        {
            var arena = NewComponent<BossArena>("Arena");
            arena.transform.position = Vector3.zero;
            arena.Configure(null, null, arenaRadius: 20f, margin: 3f);

            Assert.IsTrue(arena.Contains(new Vector3(22f, 0f, 0f)), "Inside the margin is not yet an escape.");
            Assert.IsFalse(arena.Contains(new Vector3(24f, 0f, 0f)));
        }

        // ----------------------------------------------------------- SPEC.md section 52

        [Test]
        public void DebugMode_IsOffUntilItIsDeliberatelyTurnedOn()
        {
            Assert.IsFalse(DebugMode.IsEnabled, "A stray key in a playtest must not be able to teleport anyone.");

            DebugMode.Enable();
            Assert.AreEqual(DebugMode.IsAvailableInThisBuild, DebugMode.IsEnabled);

            DebugMode.Disable();
            Assert.IsFalse(DebugMode.IsEnabled);
        }

        [Test]
        public void DebugMode_IsCompiledOutOfAReleaseBuild()
        {
            // The test runner only ever runs in the Editor, so this asserts the
            // Editor half of the contract. The release half is the #if in
            // DebugMode.IsAvailableInThisBuild, which makes every call site below
            // dead code the stripper removes.
            Assert.IsTrue(DebugMode.IsAvailableInThisBuild, "Developer tools must exist in the Editor.");
        }

        [Test]
        public void DebugCommands_WhileDebugModeIsOff_RefuseToDoAnything()
        {
            Assert.IsFalse(DebugMode.IsEnabled);

            const string refusal = "Debug mode is not enabled in this build.";
            Assert.AreEqual(refusal, DebugCommands.KillAllEnemies());
            Assert.AreEqual(refusal, DebugCommands.RefillHealth());
            Assert.AreEqual(refusal, DebugCommands.SetWorldFlag("ANY", true));
            Assert.AreEqual(refusal, DebugCommands.Teleport(Vector3.one));
            Assert.AreEqual(refusal, DebugCommands.TriggerBoss());
        }

        [Test]
        public void DebugCommands_Run_ReportsAnUnknownCommandRatherThanFailingSilently()
        {
            DebugMode.Enable();
            StringAssert.Contains("Unknown command", DebugCommands.Run("definitelynotacommand"));
        }

        [Test]
        public void DebugCommands_Help_ListsEverySectionFiftyTwoTool()
        {
            var help = DebugCommands.Help();

            foreach (var tool in new[]
                     {
                         "teleport", "unlock", "quest complete", "quest reset", "spawn", "killall",
                         "memory restore", "memory corrupt", "flag", "boss", "heal", "stamina", "show"
                     })
            {
                StringAssert.Contains(tool, help);
            }
        }

        // -------------------------------------------- SPEC.md section 54 (edge 20, 24)

        [Test]
        public void Settings_HasResolution_IsFalseUntilOneIsChosen()
        {
            var settings = new GameSettings();

            // Zero means "leave whatever the screen is on alone", so a fresh install
            // does not force a resolution the player never picked.
            Assert.IsFalse(settings.HasResolution);
            Assert.AreEqual(-1, settings.QualityLevel, "-1 leaves the build's own quality default alone.");

            settings.ResolutionWidth = 1920;
            settings.ResolutionHeight = 1080;
            Assert.IsTrue(settings.HasResolution);
        }
    }
}

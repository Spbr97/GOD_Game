using System.Linq;
using Game.AI;
using Game.Combat;
using Game.Dialogue;
using Game.EditorTools;
using Game.Quests;
using Game.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests
{
    /// <summary>
    /// Act I's authored content, as content rather than as code (TASK 044).
    ///
    /// The gate TASK 044 has to clear is "a new player can finish Act I and Agniya
    /// without debug commands, dead ends or missing story beats". Most of that is the
    /// manual pass. What a test *can* establish is that the chain of authored references
    /// a player walks along is unbroken end to end — that each beat reports the objective
    /// the next one waits on, and that the temple's reward is reachable without already
    /// having the reward.
    /// </summary>
    public class ActOneContentTests
    {
        private SceneSetup[] previous;

        [SetUp]
        public void SaveOpenScenes() => previous = EditorSceneManager.GetSceneManagerSetup();

        [TearDown]
        public void RestoreOpenScenes()
        {
            foreach (var setup in previous)
                if (setup.isLoaded && setup.isActive)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                    return;
                }
        }

        private const string Agniya = "Assets/Scenes/Agniya.unity";

        private static QuestDefinition Quest(string id) =>
            ContentValidation.LoadAll<QuestDefinition>().FirstOrDefault(quest => quest.QuestId == id);

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        // ------------------------------------------------------------- the temple quest

        [Test]
        public void TheTempleHasAQuest()
        {
            var quest = Quest("Q003");

            Assert.IsNotNull(quest,
                "Agniya had no quest of its own. The scene was complete — puzzle, toll, encounter, boss, "
                + "reward — and nothing told the player what any of it was for.");
            Assert.AreEqual(4, quest.Objectives.Count);
        }

        [Test]
        public void AmaraPointsToAgniyaAfterTheQueensCharge()
        {
            var graph = ContentValidation.LoadAll<DialogueGraph>()
                .FirstOrDefault(dialogue => dialogue.GraphId == "DLG_AMARA");
            Assert.IsNotNull(graph, "Amara's dialogue graph is missing");

            var charge = graph.GetNode("AMARA_AFTER");
            Assert.IsNotNull(charge, "Amara has no post-Q001 temple direction");
            Assert.Contains(charge.DialogueId, graph.EntryNodeIds.ToList());
            Assert.Contains("RETURNED_TO_AMARA", charge.RequiredFlags.ToList());
            Assert.IsTrue(charge.Consequences.Any(consequence =>
                consequence.Type == ConsequenceType.StartQuest && consequence.Target == "Q003"),
                "The player can hear Amara's direction but never receive the temple quest.");

            var afterTemple = graph.GetNode("AMARA_TEMPLE_COMPLETE");
            Assert.IsNotNull(afterTemple);
            Assert.Contains(afterTemple.DialogueId, graph.EntryNodeIds.ToList());
            Assert.Contains("QUEST_Q003_COMPLETE", charge.BlockingFlags.ToList(),
                "After the temple is complete, Amara must stop directing the player there.");
            Assert.Contains("QUEST_Q003_COMPLETE", afterTemple.RequiredFlags.ToList());
            Assert.Less(graph.EntryNodeIds.ToList().IndexOf(afterTemple.DialogueId),
                graph.EntryNodeIds.ToList().IndexOf(charge.DialogueId));
        }

        /// <summary>
        /// Every objective of the temple quest must be reported by something that exists
        /// in the temple. An objective nothing reports is a quest that cannot finish, and
        /// it looks identical to a working one in the journal until the player is standing
        /// in an empty room wondering what they missed.
        /// </summary>
        [Test]
        public void EveryTempleObjectiveIsReportedBySomethingInTheScene()
        {
            var quest = Quest("Q003");
            Assert.IsNotNull(quest);

            var scene = EditorSceneManager.OpenScene(Agniya, OpenSceneMode.Single);
            var reported = new System.Collections.Generic.HashSet<string>();

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var trigger in root.GetComponentsInChildren<LocationTrigger>(true))
                    if (!string.IsNullOrEmpty(trigger.ObjectiveId)) reported.Add(trigger.ObjectiveId);

                foreach (var puzzle in root.GetComponentsInChildren<PuzzleController>(true))
                    if (!string.IsNullOrEmpty(puzzle.ObjectiveIdOnSolve)) reported.Add(puzzle.ObjectiveIdOnSolve);

                foreach (var target in root.GetComponentsInChildren<QuestTarget>(true))
                    if (!string.IsNullOrEmpty(target.ObjectiveId)) reported.Add(target.ObjectiveId);

                foreach (var pickup in root.GetComponentsInChildren<ItemPickup>(true))
                    if (!string.IsNullOrEmpty(pickup.ObjectiveIdOnCollect)) reported.Add(pickup.ObjectiveIdOnCollect);

                // A memory's objective is declared on the MemoryFragment asset, not on the
                // pickup that holds it, so it is collected from the asset the pickup names.
                foreach (var pickup in root.GetComponentsInChildren<Game.Memory.MemoryPickup>(true))
                    if (pickup.Memory != null && !string.IsNullOrEmpty(pickup.Memory.ObjectiveIdOnDiscovery))
                        reported.Add(pickup.Memory.ObjectiveIdOnDiscovery);
            }

            foreach (var objective in quest.Objectives)
            {
                Assert.Contains(objective.ObjectiveId, reported.ToList(),
                    $"Nothing in Agniya reports '{objective.ObjectiveId}', so Q003 stops there forever. "
                    + "Wire it to the trigger, puzzle, QuestTarget, pickup or memory that satisfies it.");
            }
        }

        [Test]
        public void TheTempleQuestStartsWithoutThePlayerHavingToFindIt()
        {
            var scene = EditorSceneManager.OpenScene(Agniya, OpenSceneMode.Single);
            var starts = false;

            foreach (var root in scene.GetRootGameObjects())
                foreach (var trigger in root.GetComponentsInChildren<LocationTrigger>(true))
                    if (trigger.QuestToStart == "Q003") starts = true;

            Assert.IsTrue(starts,
                "Q003 is started by nothing in the temple, so a player who walks in has no journal entry "
                + "and no idea the place has a purpose.");
        }

        [Test]
        public void TheTempleQuestIsInTheSceneCatalogueOrItCannotStart()
        {
            foreach (var path in new[] { Agniya, "Assets/Scenes/Avarsha.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var quests = Find<QuestManager>(scene);

                Assert.IsNotNull(quests, $"{path} has no QuestManager");
                Assert.IsTrue(quests.Catalogue.Any(quest => quest != null && quest.QuestId == "Q003"),
                    $"{path}'s QuestManager does not list Q003. StartQuest would refuse it and log a "
                    + "fallback, which is a story beat that silently never happens.");
            }
        }

        // --------------------------------------------------------- Ember Step's unlock

        /// <summary>
        /// **SPEC.md section 8.1 puts Ember Step behind this temple's boss**, and until
        /// TASK 044 it was usable from the first second of the game. That is not a small
        /// inconsistency: the ability is the temple's entire reward, so handing it over up
        /// front removes the reason to go.
        /// </summary>
        [Test]
        public void EmberStepIsLockedUntilItIsGranted()
        {
            var scene = EditorSceneManager.OpenScene(Agniya, OpenSceneMode.Single);
            var combat = Find<CombatController>(scene);

            Assert.IsNotNull(combat, "Agniya has no player CombatController");
            Assert.IsTrue(combat.RequiresAbilityUnlock,
                "Ember Step is usable from the start, which contradicts SPEC.md section 8.1 and makes the "
                + "temple's reward something the player already has.");
        }

        [Test]
        public void TheBossGrantsEmberStep()
        {
            var scene = EditorSceneManager.OpenScene(Agniya, OpenSceneMode.Single);
            var boss = Find<BossController>(scene);
            var combat = Find<CombatController>(scene);

            Assert.IsNotNull(boss);
            Assert.IsNotNull(combat);

            var expected = combat.AbilityUnlockFlag;

            Assert.Contains(expected, boss.FlagsOnDefeat.ToList(),
                $"Beating the Flame Sovereign does not set '{expected}', so a gated Ember Step would never "
                + "be granted and the temple would be a dead end rather than a reward.");
        }

        /// <summary>
        /// The circular dependency this arrangement could create, checked rather than
        /// assumed: if anything inside the temple required Ember Step to reach, gating the
        /// ability behind the temple's boss would lock the player out of the thing that
        /// unlocks it. That is the worst class of softlock, because it only appears for a
        /// player who does not already have the ability — which after this change is every
        /// player.
        /// </summary>
        [Test]
        public void NothingInTheTempleRequiresTheAbilityTheTempleGrants()
        {
            var scene = EditorSceneManager.OpenScene(Agniya, OpenSceneMode.Single);
            var combat = Find<CombatController>(scene);
            var unlockFlag = combat != null ? combat.AbilityUnlockFlag : "ABILITY_UNLOCKED_EMBER_STEP";

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var interactable in root.GetComponentsInChildren<Interactable>(true))
                {
                    Assert.IsFalse(
                        interactable.RequiredFlags != null && interactable.RequiredFlags.Contains(unlockFlag),
                        $"'{interactable.name}' cannot be used without '{unlockFlag}', which is granted by "
                        + "this temple's boss. The player would need the reward to reach the reward.");
                }
            }
        }

        // ------------------------------------------------------------------ the rewards

        [Test]
        public void TheTempleQuestGrantsWhatItsProseSays()
        {
            var quest = Quest("Q003");

            Assert.IsNotNull(quest.Rewards);
            Assert.IsNotEmpty(quest.Rewards,
                "Q003 promises a reward in prose. Q001 and Q002 did that for months without granting one.");
        }
    }
}

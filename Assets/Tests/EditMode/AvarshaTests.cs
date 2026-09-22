using System.Collections.Generic;
using Game.Core;
using Game.Dialogue;
using Game.Memory;
using Game.Quests;
using Game.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Tests for the TASK 003 systems: world state, dialogue, quests and memories.
    ///
    /// These run in an empty scene of their own. The managers resolve themselves by
    /// searching the loaded scenes, so leaving the project's own scene open would let
    /// Avarsha's managers answer instead of the ones each test sets up, and results
    /// would depend on which scene the developer happened to have open.
    /// </summary>
    public class AvarshaTests
    {
        private readonly List<Object> spawned = new();
        private string previousScenePath;

        [OneTimeSetUp]
        public void OpenIsolatedScene()
        {
            previousScenePath = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [OneTimeTearDown]
        public void RestorePreviousScene()
        {
            if (!string.IsNullOrEmpty(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }

        [TearDown]
        public void Cleanup()
        {
            for (var i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    Object.DestroyImmediate(spawned[i]);
                }
            }

            spawned.Clear();
            EventBus.Clear();
            DialogueGraph.MemoryStateResolver = null;
        }

        // ------------------------------------------------------------------ helpers

        private T NewComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go.AddComponent<T>();
        }

        private T NewAsset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            spawned.Add(asset);
            return asset;
        }

        private WorldState NewWorldState()
        {
            return NewComponent<WorldState>("WorldState");
        }

        private static DialogueConsequence Consequence(ConsequenceType type, string target, int amount = 0)
        {
            return new DialogueConsequence { Type = type, Target = target, Amount = amount };
        }

        private QuestDefinition TwoStepQuest(string questId = "Q_TEST")
        {
            var quest = NewAsset<QuestDefinition>();
            quest.Configure(questId, "Test Quest", "A quest used by the tests.",
                new[]
                {
                    new QuestObjective { ObjectiveId = "STEP_ONE", Description = "Step one", RequiredCount = 1 },
                    new QuestObjective { ObjectiveId = "STEP_TWO", Description = "Step two", RequiredCount = 1 }
                },
                new[] { "Q_TEST_DONE" });
            return quest;
        }

        // ------------------------------------------------------------------ world state

        [Test]
        public void WorldState_SetFlag_IsReadableAndPublishesOnce()
        {
            var state = NewWorldState();
            var events = 0;
            EventBus.Subscribe<WorldFlagChangedEvent>(_ => events++);

            state.SetFlag("A_FLAG");
            state.SetFlag("A_FLAG");

            Assert.IsTrue(state.GetFlag("A_FLAG"));
            Assert.AreEqual(1, events, "Setting a flag to the value it already holds must not publish again.");
        }

        [Test]
        public void WorldState_EmptyRequirements_Pass()
        {
            var state = NewWorldState();

            Assert.IsTrue(state.HasAllFlags(null));
            Assert.IsTrue(state.HasAllFlags(new string[0]));
            Assert.IsTrue(state.HasNoneOfFlags(null));
        }

        [Test]
        public void WorldState_HasAllFlags_RequiresEveryFlag()
        {
            var state = NewWorldState();
            state.SetFlag("ONE");

            Assert.IsFalse(state.HasAllFlags(new[] { "ONE", "TWO" }));
            state.SetFlag("TWO");
            Assert.IsTrue(state.HasAllFlags(new[] { "ONE", "TWO" }));
        }

        // ------------------------------------------------------------------ dialogue

        [Test]
        public void Dialogue_EntryNode_PicksTheFirstEligibleOne()
        {
            var state = NewWorldState();
            var graph = NewAsset<DialogueGraph>();
            graph.Configure("G", new[] { "AFTER", "BEFORE" },
                new[]
                {
                    new DialogueNode { DialogueId = "BEFORE", Text = "before", BlockingFlags = new[] { "DONE" } },
                    new DialogueNode { DialogueId = "AFTER", Text = "after", RequiredFlags = new[] { "DONE" } }
                });

            Assert.AreEqual("BEFORE", graph.GetEntryNode().DialogueId);

            state.SetFlag("DONE");
            Assert.AreEqual("AFTER", graph.GetEntryNode().DialogueId);
        }

        [Test]
        public void Dialogue_Advance_FollowsLinksThenCompletes()
        {
            NewWorldState();
            var runner = NewComponent<DialogueRunner>("Runner");
            var graph = NewAsset<DialogueGraph>();
            graph.Configure("G", new[] { "ONE" },
                new[]
                {
                    new DialogueNode { DialogueId = "ONE", Text = "one", NextDialogueId = "TWO" },
                    new DialogueNode { DialogueId = "TWO", Text = "two" }
                });

            var completed = 0;
            EventBus.Subscribe<DialogueCompletedEvent>(_ => completed++);

            Assert.IsTrue(runner.Begin(graph));
            Assert.AreEqual("ONE", runner.CurrentNode.DialogueId);

            runner.Advance();
            Assert.AreEqual("TWO", runner.CurrentNode.DialogueId);

            runner.Advance();
            Assert.IsFalse(runner.IsRunning, "A node with no next id must end the conversation.");
            Assert.AreEqual(1, completed);
        }

        [Test]
        public void Dialogue_Choice_AppliesConsequencesAndJumps()
        {
            var state = NewWorldState();
            var runner = NewComponent<DialogueRunner>("Runner");
            var graph = NewAsset<DialogueGraph>();
            graph.Configure("G", new[] { "ASK" },
                new[]
                {
                    new DialogueNode
                    {
                        DialogueId = "ASK",
                        Text = "ask",
                        Choices = new[]
                        {
                            new DialogueChoice
                            {
                                Text = "yes",
                                NextDialogueId = "YES",
                                Consequences = new[] { Consequence(ConsequenceType.SetFlag, "AGREED") }
                            }
                        }
                    },
                    new DialogueNode { DialogueId = "YES", Text = "yes" }
                });

            runner.Begin(graph);
            Assert.IsTrue(runner.Choose(0));

            Assert.IsTrue(state.GetFlag("AGREED"));
            Assert.AreEqual("YES", runner.CurrentNode.DialogueId);
        }

        [Test]
        public void Dialogue_AdvanceOnAChoiceNode_IsRefused()
        {
            NewWorldState();
            var runner = NewComponent<DialogueRunner>("Runner");
            var graph = NewAsset<DialogueGraph>();
            graph.Configure("G", new[] { "ASK" },
                new[]
                {
                    new DialogueNode
                    {
                        DialogueId = "ASK",
                        Text = "ask",
                        Choices = new[] { new DialogueChoice { Text = "only option" } }
                    }
                });

            runner.Begin(graph);

            Assert.IsFalse(runner.Advance(), "A decision must not be skippable with the advance key.");
            Assert.IsTrue(runner.IsRunning);
        }

        [Test]
        public void Dialogue_ChoiceBlockedByFlag_IsNotAvailable()
        {
            var state = NewWorldState();
            var choice = new DialogueChoice { Text = "secret", RequiredFlags = new[] { "KNOWS" } };

            Assert.IsFalse(DialogueRunner.IsChoiceAvailable(choice));
            state.SetFlag("KNOWS");
            Assert.IsTrue(DialogueRunner.IsChoiceAvailable(choice));
        }

        [Test]
        public void Dialogue_BrokenLink_EndsCleanlyInsteadOfHanging()
        {
            NewWorldState();
            var runner = NewComponent<DialogueRunner>("Runner");
            var graph = NewAsset<DialogueGraph>();
            graph.Configure("G", new[] { "ONE" },
                new[] { new DialogueNode { DialogueId = "ONE", Text = "one", NextDialogueId = "MISSING" } });

            runner.Begin(graph);
            runner.Advance();

            Assert.IsFalse(runner.IsRunning, "A link to a node that does not exist must end the conversation.");
        }

        [Test]
        public void Dialogue_SecondConversation_IsRefusedWhileOneIsRunning()
        {
            NewWorldState();
            var runner = NewComponent<DialogueRunner>("Runner");
            var graph = NewAsset<DialogueGraph>();
            graph.Configure("G", new[] { "ONE" }, new[] { new DialogueNode { DialogueId = "ONE", Text = "one" } });

            Assert.IsTrue(runner.Begin(graph));
            Assert.IsFalse(runner.Begin(graph));
        }

        [Test]
        public void Dialogue_QuestConsequence_IsPublishedRatherThanAppliedDirectly()
        {
            NewWorldState();
            var runner = NewComponent<DialogueRunner>("Runner");
            var graph = NewAsset<DialogueGraph>();
            graph.Configure("G", new[] { "ONE" },
                new[]
                {
                    new DialogueNode
                    {
                        DialogueId = "ONE",
                        Text = "one",
                        Consequences = new[] { Consequence(ConsequenceType.StartQuest, "Q001") }
                    }
                });

            DialogueConsequenceEvent? received = null;
            EventBus.Subscribe<DialogueConsequenceEvent>(e => received = e);

            runner.Begin(graph);

            Assert.IsTrue(received.HasValue);
            Assert.AreEqual(ConsequenceType.StartQuest, received.Value.Type);
            Assert.AreEqual("Q001", received.Value.Target);
        }

        [Test]
        public void Dialogue_MemoryGate_HidesTheNodeUntilTheMemoryIsInThatState()
        {
            NewWorldState();
            var node = new DialogueNode
            {
                DialogueId = "GATED",
                Text = "gated",
                RequiredMemoryId = "MEM_001",
                RequiredMemoryState = "Known"
            };

            Assert.IsFalse(DialogueGraph.IsEligible(node), "With no resolver installed the gate must stay shut.");

            DialogueGraph.MemoryStateResolver = _ => "Unknown";
            Assert.IsFalse(DialogueGraph.IsEligible(node));

            DialogueGraph.MemoryStateResolver = _ => "Known";
            Assert.IsTrue(DialogueGraph.IsEligible(node));
        }

        // ------------------------------------------------------------------ quests

        [Test]
        public void Quest_CompletingEveryObjective_CompletesTheQuestAndSetsItsFlags()
        {
            var state = NewWorldState();
            var quests = NewComponent<QuestManager>("Quests");
            var quest = TwoStepQuest();
            quests.Configure(new[] { quest });

            var completed = 0;
            EventBus.Subscribe<QuestCompletedEvent>(_ => completed++);

            Assert.IsTrue(quests.StartQuest("Q_TEST"));
            Assert.AreEqual(QuestStatus.Active, quests.GetStatus("Q_TEST"));

            quests.ReportObjective("STEP_ONE");
            Assert.AreEqual(QuestStatus.Active, quests.GetStatus("Q_TEST"));

            quests.ReportObjective("STEP_TWO");
            Assert.AreEqual(QuestStatus.Completed, quests.GetStatus("Q_TEST"));
            Assert.AreEqual(1, completed);
            Assert.IsTrue(state.GetFlag("Q_TEST_DONE"));
        }

        [Test]
        public void Quest_ReportingTheSameObjectiveTwice_CountsOnce()
        {
            NewWorldState();
            var quests = NewComponent<QuestManager>("Quests");
            var quest = TwoStepQuest();
            quests.Configure(new[] { quest });
            quests.StartQuest("Q_TEST");

            var completions = 0;
            EventBus.Subscribe<QuestObjectiveCompletedEvent>(_ => completions++);

            quests.ReportObjective("STEP_ONE");
            quests.ReportObjective("STEP_ONE");

            Assert.AreEqual(1, completions, "An objective must not complete twice.");
            Assert.AreEqual(1, quests.GetProgress("Q_TEST").GetCount("STEP_ONE"));
        }

        [Test]
        public void Quest_StartingTheSameQuestTwice_DoesNotResetProgress()
        {
            NewWorldState();
            var quests = NewComponent<QuestManager>("Quests");
            quests.Configure(new[] { TwoStepQuest() });

            quests.StartQuest("Q_TEST");
            quests.ReportObjective("STEP_ONE");

            Assert.IsFalse(quests.StartQuest("Q_TEST"), "Re-giving a quest must be refused, not restart it.");
            Assert.IsTrue(quests.GetProgress("Q_TEST").IsObjectiveComplete("STEP_ONE"));
        }

        [Test]
        public void Quest_ObjectiveNeedingSeveralReports_CompletesOnTheLastOne()
        {
            NewWorldState();
            var quests = NewComponent<QuestManager>("Quests");
            var quest = NewAsset<QuestDefinition>();
            quest.Configure("Q_COUNT", "Counted", "",
                new[] { new QuestObjective { ObjectiveId = "GATHER", Description = "Gather three", RequiredCount = 3 } });
            quests.Configure(new[] { quest });
            quests.StartQuest("Q_COUNT");

            quests.ReportObjective("GATHER");
            quests.ReportObjective("GATHER");
            Assert.AreEqual(QuestStatus.Active, quests.GetStatus("Q_COUNT"));

            quests.ReportObjective("GATHER");
            Assert.AreEqual(QuestStatus.Completed, quests.GetStatus("Q_COUNT"));
        }

        [Test]
        public void Quest_CurrentObjective_IsTheFirstIncompleteOne()
        {
            NewWorldState();
            var quests = NewComponent<QuestManager>("Quests");
            quests.Configure(new[] { TwoStepQuest() });
            quests.StartQuest("Q_TEST");

            var progress = quests.GetProgress("Q_TEST");
            Assert.AreEqual("STEP_ONE", progress.CurrentObjective.ObjectiveId);

            quests.ReportObjective("STEP_ONE");
            Assert.AreEqual("STEP_TWO", progress.CurrentObjective.ObjectiveId);
        }

        [Test]
        public void Quest_UnknownObjectiveId_IsIgnored()
        {
            NewWorldState();
            var quests = NewComponent<QuestManager>("Quests");
            quests.Configure(new[] { TwoStepQuest() });
            quests.StartQuest("Q_TEST");

            Assert.IsFalse(quests.ReportObjective("NOT_AN_OBJECTIVE"),
                "World triggers report ids blindly, so unknown ids must be harmless.");
        }

        // ------------------------------------------------------------------ memories

        [Test]
        public void Memory_Discover_SetsItsFlagAndReportsItsObjective()
        {
            var state = NewWorldState();
            var quests = NewComponent<QuestManager>("Quests");
            var memories = NewComponent<MemoryManager>("Memories");

            var quest = NewAsset<QuestDefinition>();
            quest.Configure("Q_MEM", "Memory quest", "",
                new[] { new QuestObjective { ObjectiveId = "FIND_IT", Description = "Find it", RequiredCount = 1 } });
            quests.Configure(new[] { quest });
            quests.StartQuest("Q_MEM");

            var memory = NewAsset<MemoryFragment>();
            memory.Configure("MEM_T", "Test Memory", "body", "nobody",
                MemoryCategory.Historical, MemoryImportance.Supporting, "Q_MEM", "FIND_IT", "FOUND_IT");
            memories.Configure(new[] { memory });

            Assert.IsTrue(memories.Discover("MEM_T"));

            Assert.IsTrue(memories.IsDiscovered("MEM_T"));
            Assert.AreEqual(MemoryState.Known, memories.GetState("MEM_T"));
            Assert.IsTrue(state.GetFlag("FOUND_IT"));
            Assert.AreEqual(QuestStatus.Completed, quests.GetStatus("Q_MEM"));
        }

        [Test]
        public void Memory_DiscoveredTwice_IsGrantedOnce()
        {
            NewWorldState();
            var memories = NewComponent<MemoryManager>("Memories");
            var memory = NewAsset<MemoryFragment>();
            memory.Configure("MEM_T", "Test Memory", "body", "nobody",
                MemoryCategory.Personal, MemoryImportance.Supporting);
            memories.Configure(new[] { memory });

            var events = 0;
            EventBus.Subscribe<MemoryDiscoveredEvent>(_ => events++);

            Assert.IsTrue(memories.Discover("MEM_T"));
            Assert.IsFalse(memories.Discover("MEM_T"));

            Assert.AreEqual(1, events);
            Assert.AreEqual(1, memories.DiscoveredCount);
        }

        [Test]
        public void Memory_CriticalMemories_CannotBeCorruptedOrForgotten()
        {
            NewWorldState();
            var memories = NewComponent<MemoryManager>("Memories");
            var memory = NewAsset<MemoryFragment>();
            memory.Configure("MEM_CRIT", "Critical", "body", "nobody",
                MemoryCategory.Divine, MemoryImportance.Critical);
            memories.Configure(new[] { memory });
            memories.Discover("MEM_CRIT");

            Assert.IsFalse(memories.SetState(memory, MemoryState.Corrupted));
            Assert.IsFalse(memories.SetState(memory, MemoryState.Forgotten));
            Assert.IsFalse(memories.SetState(memory, MemoryState.FalseMemory));

            Assert.AreEqual(MemoryState.Known, memories.GetState("MEM_CRIT"),
                "SPEC.md section 20: critical progression data must stay reachable.");
        }

        [Test]
        public void Memory_NonCriticalMemories_CanBeCorrupted()
        {
            NewWorldState();
            var memories = NewComponent<MemoryManager>("Memories");
            var memory = NewAsset<MemoryFragment>();
            memory.Configure("MEM_OPT", "Optional", "body", "nobody",
                MemoryCategory.Lost, MemoryImportance.Optional);
            memories.Configure(new[] { memory });
            memories.Discover("MEM_OPT");

            Assert.IsTrue(memories.SetState(memory, MemoryState.Corrupted));
            Assert.AreEqual(MemoryState.Corrupted, memories.GetState("MEM_OPT"));
        }

        [Test]
        public void Memory_Corrupt_SetsStateAndSpendsIntegrity()
        {
            NewWorldState();
            var memories = NewComponent<MemoryManager>("Memories");
            var memory = NewAsset<MemoryFragment>();
            memory.Configure("MEM_OPT", "Optional", "body", "nobody",
                MemoryCategory.Lost, MemoryImportance.Optional);
            memories.Configure(new[] { memory });
            memories.Discover("MEM_OPT");

            Assert.IsTrue(memories.Corrupt(memory, 0.25f));

            Assert.AreEqual(MemoryState.Corrupted, memories.GetState("MEM_OPT"));
            Assert.AreEqual(0.75f, memories.Integrity, 0.001f);
        }

        [Test]
        public void Memory_Corrupt_OnACriticalMemory_SpendsNothing()
        {
            NewWorldState();
            var memories = NewComponent<MemoryManager>("Memories");
            var memory = NewAsset<MemoryFragment>();
            memory.Configure("MEM_CRIT", "Critical", "body", "nobody",
                MemoryCategory.Divine, MemoryImportance.Critical);
            memories.Configure(new[] { memory });
            memories.Discover("MEM_CRIT");

            Assert.IsFalse(memories.Corrupt(memory, 0.5f),
                "A refused corruption must not still spend integrity as its cost.");

            Assert.AreEqual(MemoryState.Known, memories.GetState("MEM_CRIT"));
            Assert.AreEqual(1f, memories.Integrity, 0.001f);
        }

        [Test]
        public void Memory_Integrity_ClampsBetweenZeroAndOne()
        {
            NewWorldState();
            var memories = NewComponent<MemoryManager>("Memories");

            Assert.AreEqual(1f, memories.Integrity);

            memories.ReduceIntegrity(0.3f);
            Assert.AreEqual(0.7f, memories.Integrity, 0.0001f);

            memories.ReduceIntegrity(5f);
            Assert.AreEqual(0f, memories.Integrity, 0.0001f);

            memories.RestoreIntegrity(5f);
            Assert.AreEqual(1f, memories.Integrity, 0.0001f);
        }

        [Test]
        public void Memory_UnknownId_IsRejectedRatherThanThrowing()
        {
            NewWorldState();
            var memories = NewComponent<MemoryManager>("Memories");
            memories.Configure(new MemoryFragment[0]);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("could not discover memory"));
            Assert.IsFalse(memories.Discover("NOPE"));
        }

        // ------------------------------------------------------------------ world triggers

        [Test]
        public void LocationTrigger_FiresOnceAndReportsItsObjective()
        {
            var state = NewWorldState();
            var quests = NewComponent<QuestManager>("Quests");
            var quest = NewAsset<QuestDefinition>();
            quest.Configure("Q_LOC", "Location quest", "",
                new[] { new QuestObjective { ObjectiveId = "ARRIVE", Description = "Arrive", RequiredCount = 1 } });
            quests.Configure(new[] { quest });
            quests.StartQuest("Q_LOC");

            // The collider goes on first: LocationTrigger requires the abstract
            // Collider type, which Unity cannot add for you.
            var triggerGo = new GameObject("Trigger");
            spawned.Add(triggerGo);
            triggerGo.AddComponent<BoxCollider>();
            var trigger = triggerGo.AddComponent<LocationTrigger>();
            trigger.Configure("Somewhere", "ARRIVE", "ARRIVED");

            Assert.IsTrue(trigger.Fire());
            Assert.IsFalse(trigger.Fire(), "A once-only trigger must not fire twice.");

            Assert.IsTrue(state.GetFlag("ARRIVED"));
            Assert.AreEqual(QuestStatus.Completed, quests.GetStatus("Q_LOC"));
        }

        [Test]
        public void SupernaturalEvent_EndState_SetsItsCompletionFlag()
        {
            var state = NewWorldState();
            var supernatural = NewComponent<SupernaturalEvent>("Event");
            supernatural.Configure("TEST_EVENT", "START_IT", new Transform[0], new Renderer[0], null);

            supernatural.ApplyEndStateImmediately();

            Assert.IsTrue(supernatural.HasPlayed);
            Assert.IsTrue(state.GetFlag(WorldFlags.FirstSupernaturalEvent));
        }

        [Test]
        public void Cinematic_EndState_SetsItsCompletionFlagAndReportsItsObjective()
        {
            var state = NewWorldState();
            var quests = NewComponent<QuestManager>("Quests");
            var quest = NewAsset<QuestDefinition>();
            quest.Configure("Q_CINE", "Cinematic Quest", "",
                new[] { new QuestObjective { ObjectiveId = "CINE_SEEN", Description = "Watch it", RequiredCount = 1 } });
            quests.Configure(new[] { quest });
            quests.StartQuest("Q_CINE");

            var cinematic = NewComponent<CinematicPlayer>("Cinematic");
            cinematic.Configure("TEST_CINEMATIC", "START_IT", "TEST_CINEMATIC_DONE",
                new[] { new CinematicBeat { Subtitle = "Line one", Duration = 1f } }, "CINE_SEEN");

            cinematic.ApplyEndStateImmediately();

            Assert.IsTrue(cinematic.HasPlayed);
            Assert.IsTrue(state.GetFlag("TEST_CINEMATIC_DONE"));
            Assert.AreEqual(QuestStatus.Completed, quests.GetStatus("Q_CINE"));
        }

        [Test]
        public void Cinematic_EndState_IsIdempotent()
        {
            NewWorldState();
            var cinematic = NewComponent<CinematicPlayer>("Cinematic");
            cinematic.Configure("TEST_CINEMATIC", null, "TEST_CINEMATIC_DONE",
                new[] { new CinematicBeat { Subtitle = "Line one", Duration = 1f } });

            cinematic.ApplyEndStateImmediately();
            Assert.DoesNotThrow(() => cinematic.ApplyEndStateImmediately());
            Assert.IsTrue(cinematic.HasPlayed);
        }
    }
}

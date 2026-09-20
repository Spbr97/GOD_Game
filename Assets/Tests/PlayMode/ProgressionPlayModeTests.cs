using System.Collections;
using Game.Combat;
using Game.Core;
using Game.Memory;
using Game.Quests;
using Game.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Play
{
    /// <summary>
    /// Quests and memories driven the way the player drives them (SPEC.md section 53:
    /// quest start, progress, completion, duplicate completion; memory acquisition and
    /// critical-memory protection).
    ///
    /// The point of doing these in PlayMode is that the trigger volume, the kill and
    /// the pickup are real: nothing here calls <c>ReportObjective</c> to simulate an
    /// event that the world is supposed to produce on its own.
    /// </summary>
    public class ProgressionPlayModeTests
    {
        private TestArena arena;
        private int questsCompleted;

        [SetUp]
        public void SetUp()
        {
            arena = new TestArena();
            arena.EnsureWorldState();

            questsCompleted = 0;
            EventBus.Subscribe<QuestCompletedEvent>(CountCompletion);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<QuestCompletedEvent>(CountCompletion);
            arena.Dispose();
        }

        private void CountCompletion(QuestCompletedEvent completed)
        {
            questsCompleted++;
        }

        private QuestDefinition DefeatQuest(string questId, string objectiveId, int required)
        {
            var quest = arena.TrackAsset(ScriptableObject.CreateInstance<QuestDefinition>());
            quest.name = questId;
            quest.Configure(
                questId,
                "Test Quest",
                "A quest that exists only for this test.",
                new[]
                {
                    new QuestObjective
                    {
                        ObjectiveId = objectiveId,
                        Description = "Defeat the targets.",
                        Type = ObjectiveType.DefeatEnemy,
                        RequiredCount = required,
                        CompletionFlag = objectiveId + "_DONE"
                    }
                },
                new[] { questId + "_COMPLETE" });

            return quest;
        }

        [UnityTest]
        public IEnumerator Quest_StartsWhenThePlayerWalksIntoTheTrigger()
        {
            var quest = DefeatQuest("Q_TRIGGER", "KILL_THEM", 1);
            var quests = arena.SpawnQuestManager(quest);

            var player = arena.SpawnPlayer(new Vector3(0f, 0f, -6f));

            var triggerGo = arena.Track(new GameObject("Trigger"));
            triggerGo.SetActive(false);
            var box = triggerGo.AddComponent<BoxCollider>();
            box.size = new Vector3(8f, 6f, 8f);
            var trigger = triggerGo.AddComponent<LocationTrigger>();
            trigger.Configure("Test Gate", null, "GATE_CROSSED", true, "Q_TRIGGER");
            triggerGo.SetActive(true);

            yield return null;
            Assert.AreEqual(QuestStatus.NotStarted, quests.GetStatus("Q_TRIGGER"));

            // Walked in, not teleported: the point of the test is the trigger callback.
            yield return TestArena.Observe(1.5f, () =>
            {
                if (player.Position.z < 0f)
                {
                    player.Position += new Vector3(0f, 0f, 8f * Time.deltaTime);
                }
            });

            Assert.IsTrue(trigger.HasFired, "The player walked through the volume without firing it.");
            Assert.AreEqual(QuestStatus.Active, quests.GetStatus("Q_TRIGGER"));
            Assert.IsTrue(WorldState.Instance.GetFlag("GATE_CROSSED"));
        }

        [UnityTest]
        public IEnumerator Quest_ProgressesAndCompletesAsTheTargetsActuallyDie()
        {
            var quest = DefeatQuest("Q_KILLS", "DEFEAT_TARGETS", 3);
            var quests = arena.SpawnQuestManager(quest);

            yield return null;
            Assert.IsTrue(quests.StartQuest("Q_KILLS"));

            var targets = new DummyRig[3];
            for (var i = 0; i < 3; i++)
            {
                targets[i] = arena.SpawnDummy($"Target{i}", new Vector3(i * 3f, 0f, 0f), 20f);
                var marker = targets[i].Root.AddComponent<QuestTarget>();
                marker.Configure("DEFEAT_TARGETS");
            }

            yield return null;

            targets[0].Health.Kill(DamageData.Create(999f, null));
            yield return null;

            Assert.AreEqual(QuestStatus.Active, quests.GetStatus("Q_KILLS"),
                "One kill out of three finished the quest.");
            Assert.AreEqual(1, quests.GetProgress("Q_KILLS").GetCount("DEFEAT_TARGETS"));

            targets[1].Health.Kill(DamageData.Create(999f, null));
            targets[2].Health.Kill(DamageData.Create(999f, null));
            yield return null;

            Assert.AreEqual(QuestStatus.Completed, quests.GetStatus("Q_KILLS"));
            Assert.IsTrue(WorldState.Instance.GetFlag("DEFEAT_TARGETS_DONE"));
            Assert.IsTrue(WorldState.Instance.GetFlag("Q_KILLS_COMPLETE"));
            Assert.AreEqual(1, questsCompleted);
        }

        [UnityTest]
        public IEnumerator Quest_ADeadTargetThatDiesAgain_CannotCompleteItTwice()
        {
            var quest = DefeatQuest("Q_ONCE", "DEFEAT_TARGETS", 1);
            var quests = arena.SpawnQuestManager(quest);

            yield return null;
            quests.StartQuest("Q_ONCE");

            var target = arena.SpawnDummy("Target", Vector3.zero, 20f);
            var marker = target.Root.AddComponent<QuestTarget>();
            marker.Configure("DEFEAT_TARGETS");

            yield return null;

            target.Health.Kill(DamageData.Create(999f, null));
            yield return null;

            Assert.AreEqual(QuestStatus.Completed, quests.GetStatus("Q_ONCE"));

            // SPEC.md section 54, edge case 5: the player completes a quest twice. The
            // revived-then-rekilled enemy is the realistic way that happens.
            target.Health.ResetHealth();
            target.Health.Kill(DamageData.Create(999f, null));
            yield return null;

            Assert.AreEqual(1, questsCompleted, "The quest completed twice.");
            Assert.AreEqual(QuestStatus.Completed, quests.GetStatus("Q_ONCE"));
        }

        [UnityTest]
        public IEnumerator Quest_BeingGivenAgainWhileActive_DoesNotResetProgress()
        {
            var quest = DefeatQuest("Q_REGIVE", "DEFEAT_TARGETS", 2);
            var quests = arena.SpawnQuestManager(quest);

            yield return null;
            quests.StartQuest("Q_REGIVE");

            var target = arena.SpawnDummy("Target", Vector3.zero, 20f);
            target.Root.AddComponent<QuestTarget>().Configure("DEFEAT_TARGETS");

            yield return null;
            target.Health.Kill(DamageData.Create(999f, null));
            yield return null;

            Assert.AreEqual(1, quests.GetProgress("Q_REGIVE").GetCount("DEFEAT_TARGETS"));

            // SPEC.md section 55: re-entering the conversation that hands out a quest
            // must not send the player back to the start of it.
            Assert.IsFalse(quests.StartQuest("Q_REGIVE"), "The quest was handed out a second time.");
            Assert.AreEqual(1, quests.GetProgress("Q_REGIVE").GetCount("DEFEAT_TARGETS"),
                "Re-giving an active quest wiped its progress.");
        }

        [UnityTest]
        public IEnumerator Memory_APickupGrantsItsMemoryOnceAndReportsItsObjective()
        {
            var memory = arena.TrackAsset(ScriptableObject.CreateInstance<MemoryFragment>());
            memory.name = "MEM_TEST";
            memory.Configure("MEM_TEST", "A Test Memory", "Something half-remembered.", "Nobody",
                MemoryCategory.Personal, MemoryImportance.Supporting,
                objectiveId: "FIND_MEMORY", flag: "MEMORY_FOUND");

            var quest = arena.TrackAsset(ScriptableObject.CreateInstance<QuestDefinition>());
            quest.name = "Q_MEMORY";
            quest.Configure("Q_MEMORY", "Remember", "Find the memory.",
                new[]
                {
                    new QuestObjective
                    {
                        ObjectiveId = "FIND_MEMORY",
                        Description = "Find the memory.",
                        Type = ObjectiveType.ObtainMemory,
                        RequiredCount = 1
                    }
                },
                new[] { "Q_MEMORY_COMPLETE" });

            var quests = arena.SpawnQuestManager(quest);
            var memories = arena.SpawnMemoryManager(memory);
            var player = arena.SpawnPlayer(Vector3.zero);

            yield return null;
            quests.StartQuest("Q_MEMORY");

            var pickupGo = arena.Track(new GameObject("MemoryPickup"));
            pickupGo.SetActive(false);
            pickupGo.transform.position = new Vector3(0f, 1f, 1f);
            var collider = pickupGo.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            var pickup = pickupGo.AddComponent<MemoryPickup>();
            pickup.Configure(memory);
            pickupGo.SetActive(true);

            yield return null;

            pickup.Interact(player.Root);
            yield return null;

            Assert.IsTrue(pickup.Collected);
            Assert.AreEqual(MemoryState.Known, memories.GetState("MEM_TEST"));
            Assert.AreEqual(1, memories.DiscoveredCount);
            Assert.IsTrue(WorldState.Instance.GetFlag("MEMORY_FOUND"));
            Assert.AreEqual(QuestStatus.Completed, quests.GetStatus("Q_MEMORY"));

            // SPEC.md section 54, edge case 25: the player mashes Interact.
            pickup.Interact(player.Root);
            pickup.Interact(player.Root);
            yield return null;

            Assert.AreEqual(1, memories.DiscoveredCount, "The same memory was granted more than once.");
            Assert.AreEqual(1, questsCompleted);
        }

        [UnityTest]
        public IEnumerator Memory_ACriticalMemory_CannotBeCorruptedOrForgotten()
        {
            var critical = arena.TrackAsset(ScriptableObject.CreateInstance<MemoryFragment>());
            critical.name = "MEM_CRITICAL";
            critical.Configure("MEM_CRITICAL", "The Name Beneath the Stone", "It matters.", "Nirvaan",
                MemoryCategory.Divine, MemoryImportance.Critical);

            var ordinary = arena.TrackAsset(ScriptableObject.CreateInstance<MemoryFragment>());
            ordinary.name = "MEM_ORDINARY";
            ordinary.Configure("MEM_ORDINARY", "A Market Day", "It does not matter.", "Nobody",
                MemoryCategory.Personal, MemoryImportance.Optional);

            var memories = arena.SpawnMemoryManager(critical, ordinary);
            yield return null;

            memories.Discover("MEM_CRITICAL");
            memories.Discover("MEM_ORDINARY");

            // SPEC.md section 20: no amount of ordinary play may make a critical memory
            // unreachable, so the refusal is logged rather than silent.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("MEM_CRITICAL"));

            Assert.IsFalse(memories.SetState(critical, MemoryState.Forgotten));
            Assert.AreEqual(MemoryState.Known, memories.GetState("MEM_CRITICAL"));

            Assert.IsTrue(memories.SetState(ordinary, MemoryState.Forgotten),
                "An optional memory should still be losable; only critical ones are protected.");
            Assert.AreEqual(MemoryState.Forgotten, memories.GetState("MEM_ORDINARY"));
        }
    }
}

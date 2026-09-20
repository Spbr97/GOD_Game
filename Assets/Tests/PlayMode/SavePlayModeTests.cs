using System.Collections;
using System.IO;
using Game.Combat;
using Game.Core;
using Game.Dialogue;
using Game.Memory;
using Game.Quests;
using Game.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Play
{
    /// <summary>
    /// A system that keeps state <see cref="SaveData"/> has no field for, used to prove
    /// the <see cref="ISaveParticipant"/> hook works without the save system knowing
    /// this type exists.
    /// </summary>
    public class TestParticipant : MonoBehaviour, ISaveParticipant
    {
        [System.Serializable]
        private class Payload
        {
            public int Value;
        }

        public int Value;

        public string SaveKey => "TEST_PARTICIPANT";

        public string CaptureJson() => JsonUtility.ToJson(new Payload { Value = Value });

        public void RestoreJson(string json)
        {
            Value = string.IsNullOrEmpty(json) ? 0 : JsonUtility.FromJson<Payload>(json).Value;
        }
    }

    /// <summary>
    /// The save system against a live game (SPEC.md sections 31 and 32).
    ///
    /// <c>SaveTests</c> covers the file format. These cover the half that needs a
    /// running game: what gets captured, what comes back, when saving is refused, and
    /// what the player is told when a save has gone bad.
    /// </summary>
    public class SavePlayModeTests
    {
        private TestArena arena;
        private string root;
        private SaveManager saves;

        [SetUp]
        public void SetUp()
        {
            arena = new TestArena();
            arena.EnsureWorldState();

            root = Path.Combine(Path.GetTempPath(), "GodGameSavePlayTests", Path.GetRandomFileName());
            Directory.CreateDirectory(root);

            var go = arena.Track(new GameObject("SaveManager"));
            go.SetActive(false);
            saves = go.AddComponent<SaveManager>();
            saves.Configure(root, autoSave: false);
            go.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            arena.Dispose();

            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private QuestDefinition TestQuest()
        {
            var quest = arena.TrackAsset(ScriptableObject.CreateInstance<QuestDefinition>());
            quest.name = "Q_SAVE";
            quest.Configure("Q_SAVE", "A Saved Quest", "It should survive a reload.",
                new[]
                {
                    new QuestObjective
                    {
                        ObjectiveId = "STEP_ONE",
                        Description = "Do the first thing.",
                        Type = ObjectiveType.DefeatEnemy,
                        RequiredCount = 3
                    },
                    new QuestObjective
                    {
                        ObjectiveId = "STEP_TWO",
                        Description = "Do the second thing.",
                        Type = ObjectiveType.Talk,
                        RequiredCount = 1
                    }
                },
                new[] { "Q_SAVE_COMPLETE" });

            return quest;
        }

        private MemoryFragment TestMemory()
        {
            var memory = arena.TrackAsset(ScriptableObject.CreateInstance<MemoryFragment>());
            memory.name = "MEM_SAVE";
            memory.Configure("MEM_SAVE", "A Saved Memory", "It should survive a reload.", "Nobody",
                MemoryCategory.Personal, MemoryImportance.Supporting);
            return memory;
        }

        [UnityTest]
        public IEnumerator Save_CapturesTheLiveWorldAndPutsItAllBack()
        {
            var quests = arena.SpawnQuestManager(TestQuest());
            var memories = arena.SpawnMemoryManager(TestMemory());
            var player = arena.SpawnPlayer(new Vector3(4f, 0f, 9f));

            yield return null;

            // A world worth saving: a partly finished quest, a discovered memory, a
            // wounded player somewhere specific, and some flags and counters.
            quests.StartQuest("Q_SAVE");
            quests.ReportObjective("STEP_ONE");
            quests.ReportObjective("STEP_ONE");
            memories.Discover("MEM_SAVE");
            WorldState.Instance.SetFlag("SAVED_FLAG");
            WorldState.Instance.AddToCounter("DEATHS", 7);
            player.Health.TakeDamage(DamageData.Create(35f, null));
            Difficulty.Set(DifficultyMode.Warrior);

            Assert.IsTrue(saves.Save(SaveSlot.Manual), "The save was refused.");

            // Now wreck all of it.
            quests.ReportObjective("STEP_ONE");
            quests.ReportObjective("STEP_TWO");
            memories.SetState(memories.Find("MEM_SAVE"), MemoryState.Forgotten);
            WorldState.Instance.SetFlag("SAVED_FLAG", false);
            WorldState.Instance.SetFlag("FLAG_FROM_AFTER_THE_SAVE");
            WorldState.Instance.SetCounter("DEATHS", 99);
            player.Position = new Vector3(-40f, 0f, -40f);
            player.Health.RestoreTo(5f);
            Difficulty.Set(DifficultyMode.Story);

            Assert.IsTrue(saves.Load(SaveSlot.Manual), "The load failed.");
            yield return null;

            Assert.AreEqual(QuestStatus.Active, quests.GetStatus("Q_SAVE"), "The quest came back finished.");
            Assert.AreEqual(2, quests.GetProgress("Q_SAVE").GetCount("STEP_ONE"), "Objective progress was not restored.");
            Assert.IsFalse(quests.GetProgress("Q_SAVE").IsObjectiveComplete("STEP_TWO"));

            Assert.AreEqual(MemoryState.Known, memories.GetState("MEM_SAVE"));
            Assert.AreEqual(1, memories.DiscoveredCount);

            Assert.IsTrue(WorldState.Instance.GetFlag("SAVED_FLAG"));
            Assert.IsFalse(WorldState.Instance.GetFlag("FLAG_FROM_AFTER_THE_SAVE"),
                "Loading left a flag behind that was set after the save was written.");
            Assert.AreEqual(7, WorldState.Instance.GetCounter("DEATHS"));

            Assert.Less(Vector3.Distance(player.Position, new Vector3(4f, 0f, 9f)), 0.01f,
                $"The player came back at {player.Position}.");
            Assert.AreEqual(65f, player.Health.CurrentHealth, 0.01f);
            Assert.AreEqual(DifficultyMode.Warrior, Difficulty.Current);
        }

        [UnityTest]
        public IEnumerator Save_IsRefusedWhileAConversationIsRunning()
        {
            arena.SpawnPlayer(Vector3.zero);
            yield return null;

            // SPEC.md section 31: never save during a state transition that could
            // corrupt progression. A conversation half-applied is exactly that.
            EventBus.Publish(new DialogueStartedEvent(null, "Amara"));
            yield return null;

            Assert.IsFalse(saves.IsSafeToSave);
            Assert.IsFalse(saves.Save(SaveSlot.Manual), "A save was written mid-conversation.");
            Assert.IsFalse(saves.HasSave(SaveSlot.Manual), "A refused save still left a file behind.");

            EventBus.Publish(new DialogueCompletedEvent(null));
            yield return null;

            Assert.IsTrue(saves.IsSafeToSave);
            Assert.IsTrue(saves.Save(SaveSlot.Manual));
        }

        [UnityTest]
        public IEnumerator Save_IsRefusedWhileThePlayerIsDeadAndWaitingToRespawn()
        {
            arena.SpawnCheckpointManager();
            var player = arena.SpawnPlayer(Vector3.zero);
            yield return null;

            player.Health.Kill(DamageData.Create(999f, null));
            yield return null;

            Assert.IsFalse(saves.IsSafeToSave, "Saving was allowed over a corpse.");
            Assert.IsFalse(saves.Save(SaveSlot.Manual));

            yield return TestArena.Until(() => !player.Death.IsDead, "the respawn", 6f);
            yield return null;

            Assert.IsTrue(saves.IsSafeToSave, "Saving was still blocked after the respawn.");
        }

        [UnityTest]
        public IEnumerator Checkpoint_ActivatingOne_WritesAnAutomaticSave()
        {
            saves.Configure(root, autoSave: true);
            arena.SpawnCheckpointManager();
            arena.SpawnPlayer(new Vector3(2f, 0f, 2f));

            var checkpointGo = arena.Track(new GameObject("Checkpoint_01"));
            checkpointGo.SetActive(false);
            checkpointGo.transform.position = new Vector3(-3f, 0f, 5f);
            var box = checkpointGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            var checkpoint = checkpointGo.AddComponent<Checkpoint>();
            checkpointGo.SetActive(true);

            yield return null;
            Assert.IsFalse(saves.HasSave(SaveSlot.Checkpoint));

            checkpoint.Activate();
            yield return null;

            Assert.IsTrue(saves.HasSave(SaveSlot.Checkpoint), "Activating a checkpoint did not write a save.");

            // SPEC.md section 33: activation is idempotent. A second activation must not
            // make this a different game.
            checkpoint.Activate();
            yield return null;

            Assert.IsTrue(saves.Load(SaveSlot.Checkpoint));
            Assert.AreEqual("Checkpoint_01", SaveStorage.Read(root, SaveSlot.Checkpoint).Data.CheckpointId);
        }

        [UnityTest]
        public IEnumerator Load_ACorruptPrimary_RecoversFromBackupAndSaysSoInTheSpecifiedWords()
        {
            arena.SpawnPlayer(new Vector3(1f, 0f, 1f));
            yield return null;

            WorldState.Instance.SetFlag("FIRST_SAVE");
            saves.Save(SaveSlot.Manual);

            WorldState.Instance.SetFlag("SECOND_SAVE");
            saves.Save(SaveSlot.Manual);

            File.WriteAllText(SaveStorage.PrimaryPath(root, SaveSlot.Manual), "garbage");

            var announced = string.Empty;
            void OnRecovered(SaveRecoveredFromBackupEvent recovered) => announced = recovered.PlayerMessage;
            EventBus.Subscribe<SaveRecoveredFromBackupEvent>(OnRecovered);

            LogAssert.ignoreFailingMessages = true;
            var loaded = saves.Load(SaveSlot.Manual);
            LogAssert.ignoreFailingMessages = false;

            EventBus.Unsubscribe<SaveRecoveredFromBackupEvent>(OnRecovered);
            yield return null;

            Assert.IsTrue(loaded, "A corrupt primary took the whole load down with it.");
            Assert.AreEqual(SaveManager.BackupRestoredMessage, announced,
                "SPEC.md section 32 step 3 specifies this message word for word.");

            // The backup is the save before last, so the second flag is gone and the
            // first survives — proof it really came from the backup.
            Assert.IsTrue(WorldState.Instance.GetFlag("FIRST_SAVE"));
            Assert.IsFalse(WorldState.Instance.GetFlag("SECOND_SAVE"));
        }

        [UnityTest]
        public IEnumerator Load_AQuestThisBuildDoesNotHave_IsDroppedRatherThanFailingTheLoad()
        {
            var quests = arena.SpawnQuestManager(TestQuest());
            arena.SpawnPlayer(Vector3.zero);
            yield return null;

            quests.StartQuest("Q_SAVE");
            WorldState.Instance.SetFlag("STILL_HERE");
            saves.Save(SaveSlot.Manual);

            // SPEC.md section 54, edge case 4: an older save meeting a build whose
            // content has moved on. The quest vanishes; the rest of the save must not.
            quests.Configure(new QuestDefinition[0]);

            LogAssert.ignoreFailingMessages = true;
            var loaded = saves.Load(SaveSlot.Manual);
            LogAssert.ignoreFailingMessages = false;
            yield return null;

            Assert.IsTrue(loaded, "One unknown quest failed the entire load.");
            Assert.AreEqual(QuestStatus.NotStarted, quests.GetStatus("Q_SAVE"));
            Assert.IsTrue(WorldState.Instance.GetFlag("STILL_HERE"), "The rest of the save was lost with the quest.");
        }

        [UnityTest]
        public IEnumerator Participant_ContributesItsOwnDataAndGetsItBack()
        {
            arena.SpawnPlayer(Vector3.zero);

            var participantGo = arena.Track(new GameObject("Participant"));
            var participant = participantGo.AddComponent<TestParticipant>();
            participant.Value = 42;

            yield return null;

            Assert.IsTrue(saves.Save(SaveSlot.Manual));

            participant.Value = -1;
            Assert.IsTrue(saves.Load(SaveSlot.Manual));
            yield return null;

            Assert.AreEqual(42, participant.Value,
                "A system saving through ISaveParticipant did not get its data back.");
        }

        [UnityTest]
        public IEnumerator Save_ThatFailsValidation_NeverReplacesTheGoodOne()
        {
            arena.SpawnPlayer(Vector3.zero);
            yield return null;

            WorldState.Instance.SetFlag("GOOD_SAVE");
            Assert.IsTrue(saves.Save(SaveSlot.Manual));

            var before = File.ReadAllText(SaveStorage.PrimaryPath(root, SaveSlot.Manual));

            // A save the serializer will reject on read-back. SPEC.md section 31 step 2
            // is the only thing standing between this and an unloadable file.
            var broken = saves.Capture();
            broken.PlayerStats.MaxHealth = 0f;

            LogAssert.ignoreFailingMessages = true;
            var written = SaveStorage.Write(root, SaveSlot.Manual, broken, out var detail);
            LogAssert.ignoreFailingMessages = false;

            Assert.IsFalse(written, "An invalid save was written over a good one.");
            Assert.IsNotNull(detail);
            Assert.AreEqual(before, File.ReadAllText(SaveStorage.PrimaryPath(root, SaveSlot.Manual)),
                "The existing save was modified by a write that failed.");
            Assert.IsFalse(File.Exists(SaveStorage.TemporaryPath(root, SaveSlot.Manual)));
        }
    }
}

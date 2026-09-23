using System.Collections;
using System.IO;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.Dialogue;
using Game.Inventory;
using Game.Memory;
using Game.Progression;
using Game.Quests;
using Game.Save;
using Game.World;
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
            SaveManager.PendingLoad = null;

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
        public IEnumerator PendingLoad_IsAppliedAutomaticallyWhenASaveManagerWakesUpInTheDestinationScene()
        {
            // Stands in for the Main Menu's Continue/Load: it sets PendingLoad and hands
            // off to a scene load, then a SaveManager it never touches directly applies
            // the save once it exists (MainMenuController does this for real).
            var player = arena.SpawnPlayer(new Vector3(9f, 0f, 3f));
            yield return null;

            player.Health.TakeDamage(DamageData.Create(20f, null));
            WorldState.Instance.SetFlag("BEFORE_PENDING_LOAD");
            saves.Save(SaveSlot.Manual);

            WorldState.Instance.SetFlag("AFTER_THE_SAVE");
            player.Position = Vector3.zero;

            // Simulate the scene tear-down and wake-up a real scene load would do: the
            // old SaveManager goes away, a fresh one arrives with PendingLoad already set.
            Object.DestroyImmediate(saves.gameObject);
            SaveManager.PendingLoad = SaveSlot.Manual;

            var freshGo = arena.Track(new GameObject("SaveManager_Fresh"));
            freshGo.SetActive(false);
            var fresh = freshGo.AddComponent<SaveManager>();
            fresh.Configure(root, autoSave: false);
            freshGo.SetActive(true);

            yield return null;

            Assert.IsNull(SaveManager.PendingLoad, "PendingLoad was not consumed.");
            Assert.IsTrue(WorldState.Instance.GetFlag("BEFORE_PENDING_LOAD"));
            Assert.IsFalse(WorldState.Instance.GetFlag("AFTER_THE_SAVE"),
                "State from after the save survived the pending load.");
            Assert.Less(Vector3.Distance(player.Position, new Vector3(9f, 0f, 3f)), 0.01f);

            saves = fresh;
        }

        [UnityTest]
        public IEnumerator Enemy_DeathSurvivesASaveAndDoesNotResurrectOnLoad()
        {
            arena.SpawnPlayer(Vector3.zero);
            var dummy = arena.SpawnDummy("Enemy_Persistent", new Vector3(3f, 0f, 3f));

            // Inactive while wiring: AddComponent runs Awake immediately on an active
            // object, and EnemyHealth.Awake resolves SaveIdentity via GetComponent, so
            // it must already be present (see TestArena's note on this exact gotcha).
            dummy.Root.SetActive(false);
            dummy.Root.AddComponent<SaveIdentity>().Configure("ENEMY_PERSISTENT");
            dummy.Root.AddComponent<EnemyHealth>();
            dummy.Root.SetActive(true);
            yield return null;

            dummy.Health.Kill(DamageData.Create(999f, null));
            yield return null;

            Assert.IsTrue(saves.Save(SaveSlot.Manual));

            // Undo the death in memory, the way a fresh scene load would hand back a
            // live enemy before the save is applied.
            dummy.Health.RestoreTo(dummy.Health.MaxHealth);
            Assert.IsFalse(dummy.Health.IsDead);

            Assert.IsTrue(saves.Load(SaveSlot.Manual));
            yield return null;

            Assert.IsTrue(dummy.Health.IsDead, "The load did not re-apply the enemy's death.");
            foreach (var renderer in dummy.Root.GetComponentsInChildren<Renderer>(true))
                Assert.IsFalse(renderer.enabled, "A restored-dead enemy should be visually hidden.");
            foreach (var hurtbox in dummy.Root.GetComponentsInChildren<Hurtbox>(true))
                Assert.IsFalse(hurtbox.enabled, "A restored-dead enemy should not absorb attacks.");
        }

        [UnityTest]
        public IEnumerator Enemy_ThatDiesAfterTheSceneAlreadyHasTheDeadFlag_ResurrectsOnlyOnce()
        {
            // A save loaded before this enemy exists (TestArena builds the whole scene
            // up front, unlike a real level, so this proves the check-now half of
            // EnemyHealth.OnEnable, not just the subscribe-for-later half).
            arena.EnsureWorldState();
            WorldState.Instance.SetFlag(WorldObjectState.DeadFlag("ENEMY_PRE_DEAD"));

            var dummy = arena.SpawnDummy("Enemy_PreDead", Vector3.zero);
            dummy.Root.SetActive(false);
            dummy.Root.AddComponent<SaveIdentity>().Configure("ENEMY_PRE_DEAD");
            dummy.Root.AddComponent<EnemyHealth>();
            dummy.Root.SetActive(true);
            yield return null;

            Assert.IsTrue(dummy.Health.IsDead, "An enemy spawned after its dead flag was already set should never have been alive.");
        }

        [UnityTest]
        public IEnumerator Pickup_CollectionSurvivesASaveAndDoesNotReappearOnLoad()
        {
            var memory = TestMemory();
            var memories = arena.SpawnMemoryManager(memory);
            arena.SpawnPlayer(Vector3.zero);

            var pickupGo = arena.Track(new GameObject("Pickup"));
            pickupGo.SetActive(false);
            var collider = pickupGo.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            var pickup = pickupGo.AddComponent<MemoryPickup>();
            pickup.Configure(memory);
            pickupGo.AddComponent<SaveIdentity>().Configure("PICKUP_MEM_SAVE");
            pickupGo.SetActive(true);
            yield return null;

            pickup.Interact(null);
            yield return null;

            Assert.IsTrue(pickup.Collected);
            Assert.IsTrue(saves.Save(SaveSlot.Manual));

            // A fresh pickup, as a scene reload would produce, with the same identity.
            Object.DestroyImmediate(pickupGo);
            memories.SetState(memory, MemoryState.Forgotten);

            var reloadedGo = arena.Track(new GameObject("Pickup_Reloaded"));
            reloadedGo.SetActive(false);
            var reloadedCollider = reloadedGo.AddComponent<SphereCollider>();
            reloadedCollider.isTrigger = true;
            var reloadedPickup = reloadedGo.AddComponent<MemoryPickup>();
            reloadedPickup.Configure(memory);
            reloadedGo.AddComponent<SaveIdentity>().Configure("PICKUP_MEM_SAVE");
            reloadedGo.SetActive(true);
            yield return null;

            Assert.IsTrue(saves.Load(SaveSlot.Manual));
            yield return null;

            Assert.IsTrue(reloadedPickup.Collected, "The reloaded pickup did not pick up the save's collected state.");
            Assert.AreEqual(MemoryState.Known, memories.GetState("MEM_SAVE"),
                "MemoryManager's own restore, not a duplicate Discover from the pickup, should own this.");
        }

        [UnityTest]
        public IEnumerator Checkpoint_ActiveOneIsRestoredByALoad()
        {
            var originalManager = arena.SpawnCheckpointManager();
            arena.SpawnPlayer(Vector3.zero);

            var checkpointGo = arena.Track(new GameObject("Checkpoint_Restore"));
            checkpointGo.SetActive(false);
            var box = checkpointGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            var checkpoint = checkpointGo.AddComponent<Checkpoint>();
            checkpointGo.SetActive(true);
            yield return null;

            checkpoint.Activate();
            yield return null;

            Assert.IsTrue(saves.Save(SaveSlot.Manual));

            // A fresh CheckpointManager, as a scene reload would produce: the old one is
            // gone (CheckpointManager.Instance would otherwise refuse a second one) and
            // nothing has restored an active checkpoint into the new one yet.
            Object.DestroyImmediate(originalManager.gameObject);
            var freshManagerGo = arena.Track(new GameObject("CheckpointManager_Fresh"));
            var freshManager = freshManagerGo.AddComponent<CheckpointManager>();
            yield return null;

            Assert.IsNull(freshManager.ActiveCheckpoint);

            Assert.IsTrue(saves.Load(SaveSlot.Manual));
            yield return null;

            Assert.AreEqual(checkpoint, freshManager.ActiveCheckpoint, "The load did not restore the active checkpoint.");
            Assert.IsTrue(checkpoint.HasBeenActivated);
        }

        [UnityTest]
        public IEnumerator Puzzle_SolvedStateSurvivesASaveAndAppliesToAFreshControllerOnLoad()
        {
            arena.SpawnPlayer(Vector3.zero);

            GameObject NewBrazier(string goName)
            {
                var go = arena.Track(new GameObject(goName));
                go.SetActive(false);
                var collider = go.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                var brazier = go.AddComponent<FireBrazier>();
                brazier.ConfigureBrazier(0f);
                go.SetActive(true);
                return go;
            }

            var braziersGo = new[] { NewBrazier("Brazier_A"), NewBrazier("Brazier_B") };
            var braziers = new MonoBehaviour[] { braziersGo[0].GetComponent<FireBrazier>(), braziersGo[1].GetComponent<FireBrazier>() };

            var controllerGo = arena.Track(new GameObject("Puzzle_Save"));
            controllerGo.SetActive(false);
            var controller = controllerGo.AddComponent<PuzzleController>();
            controller.Configure(braziers, "SAVE_PUZZLE_SOLVED", "SAVE_PUZZLE");
            controllerGo.SetActive(true);
            yield return null;

            foreach (var go in braziersGo)
            {
                go.GetComponent<FireBrazier>().Light();
            }
            yield return null;

            Assert.IsTrue(controller.IsSolved, "The puzzle did not solve in the live game.");
            Assert.IsTrue(saves.Save(SaveSlot.Manual));

            // A fresh scene load: the old controller and braziers are gone, and WorldState
            // (which survives scene loads by design, so a stray true flag from the live
            // game above would otherwise still be sitting there) is wiped the way a real
            // scene transition's own reset would leave it, before anything restores it.
            Object.DestroyImmediate(controllerGo);
            WorldState.Instance.ResetAll();
            var freshBraziersGo = new[] { NewBrazier("Brazier_A_Fresh"), NewBrazier("Brazier_B_Fresh") };
            var freshBraziers = new MonoBehaviour[]
            {
                freshBraziersGo[0].GetComponent<FireBrazier>(), freshBraziersGo[1].GetComponent<FireBrazier>()
            };

            var gateGo = arena.Track(new GameObject("Gate_Save"));
            gateGo.SetActive(false);
            gateGo.AddComponent<BoxCollider>();
            var gate = gateGo.AddComponent<PuzzleGate>();
            gate.Configure("SAVE_PUZZLE");
            gateGo.SetActive(true);

            var freshControllerGo = arena.Track(new GameObject("Puzzle_Save_Fresh"));
            freshControllerGo.SetActive(false);
            var freshController = freshControllerGo.AddComponent<PuzzleController>();
            freshController.Configure(freshBraziers, "SAVE_PUZZLE_SOLVED", "SAVE_PUZZLE");
            freshControllerGo.SetActive(true);
            yield return null;

            Assert.IsFalse(freshController.IsSolved, "The fresh controller started solved before the load applied anything.");
            Assert.IsFalse(gate.IsOpen);

            Assert.IsTrue(saves.Load(SaveSlot.Manual));
            yield return null;

            Assert.IsTrue(freshController.IsSolved, "The load did not restore the puzzle's solved state.");
            Assert.IsTrue(gate.IsOpen, "The restored solve did not reach the gate.");
        }

        [UnityTest]
        public IEnumerator Boss_DefeatSurvivesASaveAndRevealsTheRewardOnAFreshLoad()
        {
            arena.SpawnPlayer(Vector3.zero);

            GameObject NewBoss(string goName, string identityId, GameObject rewardObject)
            {
                var go = arena.Track(new GameObject(goName));
                go.SetActive(false);
                var health = go.AddComponent<HealthComponent>();
                health.Configure(50f);
                go.AddComponent<SaveIdentity>().Configure(identityId);
                go.AddComponent<EnemyHealth>();
                var boss = go.AddComponent<BossController>();
                boss.Configure("SAVE_BOSS", "Test Boss", null, null, rewardObject);
                go.SetActive(true);
                return go;
            }

            var reward = arena.Track(new GameObject("Reward"));
            reward.SetActive(false);

            var bossGo = NewBoss("Boss_Save", "SAVE_BOSS_ENEMY", reward);
            var boss = bossGo.GetComponent<BossController>();
            yield return null;

            bossGo.GetComponent<HealthComponent>().Kill(DamageData.Create(999f, null));
            yield return null;

            Assert.IsTrue(boss.Defeated, "The boss did not register its own live death.");
            Assert.IsTrue(reward.activeSelf, "The reward was not revealed by the live death.");
            Assert.IsTrue(saves.Save(SaveSlot.Manual));

            // A fresh scene load: the old boss is gone, and WorldState (see the puzzle
            // test above) is wiped the way a real scene transition's own reset would
            // leave it, before anything restores it.
            Object.DestroyImmediate(bossGo);
            WorldState.Instance.ResetAll();

            var freshReward = arena.Track(new GameObject("Reward_Fresh"));
            freshReward.SetActive(false);
            var freshBossGo = NewBoss("Boss_Save_Fresh", "SAVE_BOSS_ENEMY", freshReward);
            var freshBoss = freshBossGo.GetComponent<BossController>();
            yield return null;

            Assert.IsFalse(freshBoss.Defeated, "A fresh boss before Load should not already be defeated.");
            Assert.IsFalse(freshReward.activeSelf);

            Assert.IsTrue(saves.Load(SaveSlot.Manual));
            yield return null;

            Assert.IsTrue(freshBoss.Defeated, "The load did not restore the boss's defeat.");
            Assert.IsTrue(freshReward.activeSelf, "The load did not reveal the reward.");
        }

        [UnityTest]
        public IEnumerator MemoryToll_PaymentSurvivesASaveAndDoesNotChargeTwiceOnLoad()
        {
            arena.SpawnPlayer(Vector3.zero);
            var memories = arena.SpawnMemoryManager();

            GameObject NewToll(string goName, string identityId, GameObject barrierObject)
            {
                var go = arena.Track(new GameObject(goName));
                go.SetActive(false);
                var collider = go.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                var toll = go.AddComponent<MemoryToll>();
                toll.Configure(0.2f, "TOLL_SAVE_PAID", barrierObject);
                go.AddComponent<SaveIdentity>().Configure(identityId);
                go.SetActive(true);
                return go;
            }

            var barrier = arena.Track(new GameObject("Barrier"));
            var tollGo = NewToll("Toll_Save", "TOLL_SAVE", barrier);
            yield return null;

            tollGo.GetComponent<MemoryToll>().Interact(null);
            yield return null;

            Assert.AreEqual(0.8f, memories.Integrity, 0.0001f);
            Assert.IsFalse(barrier.activeSelf);
            Assert.IsTrue(saves.Save(SaveSlot.Manual));

            // A fresh scene load: the old toll and barrier are gone, integrity is back
            // to full (nothing has restored it yet), and WorldState is wiped the way a
            // real scene transition's own reset would leave it before anything restores it.
            Object.DestroyImmediate(tollGo);
            Object.DestroyImmediate(barrier);
            WorldState.Instance.ResetAll();
            memories.RestoreIntegrity01(1f);

            var freshBarrier = arena.Track(new GameObject("Barrier_Fresh"));
            var freshTollGo = NewToll("Toll_Save_Fresh", "TOLL_SAVE", freshBarrier);
            yield return null;

            Assert.IsFalse(freshTollGo.GetComponent<MemoryToll>().Paid, "A fresh toll before Load should not already be paid.");
            Assert.IsTrue(freshBarrier.activeSelf);

            Assert.IsTrue(saves.Load(SaveSlot.Manual));
            yield return null;

            Assert.IsTrue(freshTollGo.GetComponent<MemoryToll>().Paid, "The load did not restore the toll's payment.");
            Assert.IsFalse(freshBarrier.activeSelf, "The load did not reopen the barrier.");
            Assert.AreEqual(0.8f, memories.Integrity, 0.0001f, "The load should restore the spent integrity, not charge it again.");
        }

        [UnityTest]
        public IEnumerator Inventory_AndSkillTree_SurviveASaveAndLoadOnFreshManagers()
        {
            arena.SpawnPlayer(Vector3.zero);

            var potion = ScriptableObject.CreateInstance<InventoryItem>();
            potion.Configure("POTION_SAVE", "Potion", ItemCategory.Consumable);
            arena.TrackAsset(potion);

            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            skill.Configure("SKILL_SAVE", "Test Skill", SkillBranch.Warrior, 1, SkillEffectType.AttackDamageMultiplier, 0.1f);
            arena.TrackAsset(skill);

            var inventory = arena.SpawnInventoryManager(potion);
            var skills = arena.SpawnSkillTreeManager(skill);
            yield return null;

            inventory.Add(potion, 2);
            WorldState.Instance.AddToCounter(SkillTreeManager.SkillPointsFlag, 5);
            Assert.IsTrue(skills.Unlock("SKILL_SAVE"));
            yield return null;

            Assert.IsTrue(saves.Save(SaveSlot.Manual));

            // A fresh scene load: new managers with no state, the way a real reload
            // would produce, plus WorldState wiped the way its own reset would leave it.
            Object.DestroyImmediate(inventory.gameObject);
            Object.DestroyImmediate(skills.gameObject);
            WorldState.Instance.ResetAll();

            var freshInventory = arena.SpawnInventoryManager(potion);
            var freshSkills = arena.SpawnSkillTreeManager(skill);
            yield return null;

            Assert.AreEqual(0, freshInventory.GetCount(potion));
            Assert.IsFalse(freshSkills.IsUnlocked("SKILL_SAVE"));

            Assert.IsTrue(saves.Load(SaveSlot.Manual));
            yield return null;

            Assert.AreEqual(2, freshInventory.GetCount(potion), "The load did not restore the held potion count.");
            Assert.IsTrue(freshSkills.IsUnlocked("SKILL_SAVE"), "The load did not restore the unlocked skill.");
            Assert.AreEqual(4, freshSkills.AvailablePoints, "The load did not restore the skill points counter (5 granted, 1 spent).");
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
        [UnityTest]
        public IEnumerator EdgeCase02_SaveInsideBossPhaseTransition_IsAReadableSnapshot()
        {
            arena.SpawnPlayer(Vector3.zero);
            var rig = arena.SpawnEnemy("Transition Boss", new Vector3(0f, 0f, 12f),
                arena.NewArchetype("TRANSITION_BOSS", health: 100f));
            rig.Root.SetActive(false);
            var boss = rig.Root.AddComponent<BossController>();
            boss.Configure("TRANSITION_BOSS", "Transition Boss", rig.Controller, rig.Combatant,
                null, 0.6f, 0.3f);
            rig.Root.SetActive(true);
            yield return null;

            var saved = false;
            void OnPhase(BossPhaseChangedEvent e)
            {
                if (e.Boss == rig.Root && e.Phase == 2)
                    saved = saves.Save(SaveSlot.Manual);
            }
            EventBus.Subscribe<BossPhaseChangedEvent>(OnPhase);
            try
            {
                rig.Health.TakeDamage(DamageData.Create(50f, null));
                Assert.AreEqual(2, boss.Phase);
                Assert.IsTrue(saved, "The phase event left saving in a half-applied state.");
                Assert.IsTrue(SaveStorage.Read(root, SaveSlot.Manual).Loaded);
            }
            finally
            {
                EventBus.Unsubscribe<BossPhaseChangedEvent>(OnPhase);
            }
        }

        [UnityTest]
        public IEnumerator EdgeCase03_SaveBeforeScriptedEvent_LoadRestoresPreEventFlags()
        {
            arena.SpawnPlayer(Vector3.zero);
            var go = arena.Track(new GameObject("Scripted Event"));
            go.SetActive(false);
            var cinematic = go.AddComponent<CinematicPlayer>();
            cinematic.Configure("EVENT", "START_EVENT", "EVENT_DONE",
                new[] { new CinematicBeat { Subtitle = "A change", Duration = 30f } });
            go.SetActive(true);
            yield return null;
            Assert.IsTrue(saves.Save(SaveSlot.Manual));

            WorldState.Instance.SetFlag("START_EVENT");
            cinematic.ApplyEndStateImmediately();
            Assert.IsTrue(WorldState.Instance.GetFlag("EVENT_DONE"));
            Assert.IsTrue(saves.Load(SaveSlot.Manual));
            Assert.IsFalse(WorldState.Instance.GetFlag("START_EVENT"));
            Assert.IsFalse(WorldState.Instance.GetFlag("EVENT_DONE"));
        }

        [UnityTest]
        public IEnumerator EdgeCase04_LoadOlderSave_RemovesLaterAbilityUnlock()
        {
            var player = arena.SpawnPlayer(Vector3.zero, withCombatInput: true);
            player.Combat.ConfigureAbilityUnlock("EMBER_STEP", true);
            yield return null;
            Assert.IsTrue(saves.Save(SaveSlot.Manual));
            WorldState.Instance.SetFlag("ABILITY_UNLOCKED_EMBER_STEP");
            Assert.IsTrue(WorldState.Instance.GetFlag("ABILITY_UNLOCKED_EMBER_STEP"));

            Assert.IsTrue(saves.Load(SaveSlot.Manual));
            Assert.IsFalse(WorldState.Instance.GetFlag("ABILITY_UNLOCKED_EMBER_STEP"));
            Assert.IsFalse(player.Combat.TryAbility(), "An ability from after the save stayed usable.");
        }
    }
}

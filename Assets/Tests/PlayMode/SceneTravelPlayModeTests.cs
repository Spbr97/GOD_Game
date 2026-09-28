using System.Collections;
using System.IO;
using Game.Combat;
using Game.Core;
using Game.Inventory;
using Game.Save;
using Game.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.Play
{
    /// <summary>
    /// Cross-scene progression against a live game (TASK 041).
    ///
    /// The question these answer is the one the roadmap's gate asks: does a journey out
    /// of a scene and back keep everything, and does it keep the player out of positions
    /// that were never theirs. The data-shape half is in <c>SceneTravelTests</c>; the
    /// whole trip through two real scenes in a shipped binary is the standalone smoke
    /// test, because that is the only place where a scene missing from the build, or an
    /// asset that only resolved in the Editor, can show up.
    ///
    /// A PlayMode test cannot load a second gameplay scene without taking the test
    /// runner's own scene with it, so "the other scene" here is a name — which is enough,
    /// since the defect being guarded against is precisely the code treating a record
    /// from one scene as if it belonged to another.
    /// </summary>
    public class SceneTravelPlayModeTests
    {
        private TestArena arena;
        private string root;
        private SaveManager saves;

        private static string ThisScene => SceneManager.GetActiveScene().name;
        private const string ElsewhereScene = "AnotherPlaceEntirely";

        [SetUp]
        public void SetUp()
        {
            arena = new TestArena();
            arena.EnsureWorldState();

            SceneTravel.Clear();
            SceneMemory.Clear();

            root = Path.Combine(Path.GetTempPath(), "GodGameTravelTests", Path.GetRandomFileName());
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
            SceneTravel.Clear();
            SceneMemory.Clear();

            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        // ------------------------------------------------------------- what is captured

        [UnityTest]
        public IEnumerator Capture_RecordsWhereThePlayerStandsUnderTheSceneTheyStandIn()
        {
            arena.SpawnPlayer(new Vector3(4f, 0f, 9f));
            yield return null;

            var data = saves.Capture();

            var here = data.StateFor(ThisScene);
            Assert.That(here, Is.Not.Null, "the scene being saved in must always get a record");
            Assert.That(here.PlayerPosition.x, Is.EqualTo(4f).Within(0.01f));
            Assert.That(here.PlayerPosition.z, Is.EqualTo(9f).Within(0.01f));
        }

        /// <summary>
        /// Without this, every save would forget every scene but the one being stood in:
        /// walk into the temple, save, and the game no longer knows where you were in
        /// Avarsha.
        /// </summary>
        [UnityTest]
        public IEnumerator Capture_KeepsTheRecordOfASceneThatIsNotLoaded()
        {
            SceneMemory.Record(ElsewhereScene, new Vector3(50f, 2f, -7f), Quaternion.identity, "Checkpoint_Elsewhere");

            arena.SpawnPlayer(new Vector3(1f, 0f, 1f));
            yield return null;

            var data = saves.Capture();

            var elsewhere = data.StateFor(ElsewhereScene);
            Assert.That(elsewhere, Is.Not.Null, "a scene left behind must survive a save written in another one");
            Assert.That(elsewhere.PlayerPosition, Is.EqualTo(new Vector3(50f, 2f, -7f)));
            Assert.That(elsewhere.CheckpointId, Is.EqualTo("Checkpoint_Elsewhere"));
            Assert.That(data.StateFor(ThisScene), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Capture_RecordsTheActiveCheckpointAgainstThisSceneOnly()
        {
            arena.SpawnPlayer(Vector3.zero);
            var manager = arena.SpawnCheckpointManager();

            var checkpointObject = arena.Track(new GameObject("Checkpoint_Here"));
            checkpointObject.SetActive(false);
            checkpointObject.AddComponent<BoxCollider>().isTrigger = true;
            var checkpoint = checkpointObject.AddComponent<Checkpoint>();
            checkpointObject.SetActive(true);
            yield return null;

            manager.RestoreActiveCheckpoint(checkpoint.CheckpointId);
            yield return null;

            var data = saves.Capture();

            Assert.That(data.StateFor(ThisScene).CheckpointId, Is.EqualTo(checkpoint.CheckpointId));
        }

        // -------------------------------------------------------- where the player ends up

        /// <summary>
        /// The defect TASK 041 names outright. A save that records a position in one
        /// scene must not place the player at those coordinates in a different one.
        /// </summary>
        [UnityTest]
        public IEnumerator Apply_DoesNotUseAPositionRecordedForAnotherScene()
        {
            var player = arena.SpawnPlayer(new Vector3(3f, 0f, 3f));
            yield return null;

            var data = new SaveData { SceneName = ElsewhereScene };
            data.EnsureStateFor(ElsewhereScene).PlayerPosition = new Vector3(500f, 0f, -500f);

            saves.Apply(data);
            yield return null;

            Assert.That(player.Position.x, Is.EqualTo(3f).Within(0.01f),
                "a position measured in another scene must be ignored here");
            Assert.That(player.Position.z, Is.EqualTo(3f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Apply_UsesThePositionRecordedForThisScene()
        {
            var player = arena.SpawnPlayer(Vector3.zero);
            yield return null;

            var data = new SaveData { SceneName = ThisScene };
            data.EnsureStateFor(ThisScene).PlayerPosition = new Vector3(12f, 0f, -4f);
            data.EnsureStateFor(ElsewhereScene).PlayerPosition = new Vector3(500f, 0f, -500f);

            saves.Apply(data);
            yield return null;

            Assert.That(player.Position.x, Is.EqualTo(12f).Within(0.01f));
            Assert.That(player.Position.z, Is.EqualTo(-4f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Apply_AtASpawnPointBeatsThePositionTheSaveRecordedHere()
        {
            var player = arena.SpawnPlayer(Vector3.zero);

            var spawnObject = arena.Track(new GameObject("Arrival"));
            spawnObject.transform.position = new Vector3(-20f, 0f, 6f);
            spawnObject.AddComponent<SceneSpawnPoint>().Configure("FromAvarsha");
            yield return null;

            var data = new SaveData { SceneName = ThisScene };
            data.EnsureStateFor(ThisScene).PlayerPosition = new Vector3(99f, 0f, 99f);

            saves.Apply(data, PlayerPlacement.AtSpawnPoint, "FromAvarsha");
            yield return null;

            Assert.That(player.Position.x, Is.EqualTo(-20f).Within(0.01f),
                "arriving through a door is not resuming a moment; the door wins");
            Assert.That(player.Position.z, Is.EqualTo(6f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Apply_AtAMissingSpawnPointFallsBackToTheSaveRatherThanTheOrigin()
        {
            var player = arena.SpawnPlayer(new Vector3(1f, 0f, 1f));
            yield return null;

            var data = new SaveData { SceneName = ThisScene };
            data.EnsureStateFor(ThisScene).PlayerPosition = new Vector3(8f, 0f, 8f);

            LogAssert.ignoreFailingMessages = true;
            saves.Apply(data, PlayerPlacement.AtSpawnPoint, "NoSuchSpawnPoint");
            LogAssert.ignoreFailingMessages = false;
            yield return null;

            Assert.That(player.Position.x, Is.EqualTo(8f).Within(0.01f),
                "a typo in a spawn id must not drop the player at the world origin");
        }

        [UnityTest]
        public IEnumerator Apply_WithNothingRecordedForThisSceneLeavesThePlayerWhereTheSceneStartsThem()
        {
            var player = arena.SpawnPlayer(new Vector3(2f, 0f, 2f));
            yield return null;

            var data = new SaveData { SceneName = ElsewhereScene };

            saves.Apply(data);
            yield return null;

            Assert.That(player.Position.x, Is.EqualTo(2f).Within(0.01f));
            Assert.That(player.Position.z, Is.EqualTo(2f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Apply_Unchanged_MovesNobody()
        {
            var player = arena.SpawnPlayer(new Vector3(5f, 0f, 5f));
            yield return null;

            var data = new SaveData { SceneName = ThisScene };
            data.EnsureStateFor(ThisScene).PlayerPosition = new Vector3(40f, 0f, 40f);

            saves.Apply(data, PlayerPlacement.Unchanged, null);
            yield return null;

            Assert.That(player.Position.x, Is.EqualTo(5f).Within(0.01f));
        }

        /// <summary>
        /// The quieter half of the same defect: a checkpoint id from another scene
        /// resolves to nothing here, and the player's next death would send them to the
        /// scene's default spawn instead of to the checkpoint they lit in this one.
        /// </summary>
        [UnityTest]
        public IEnumerator Apply_RestoresTheCheckpointRecordedForThisSceneAndNotAnother()
        {
            arena.SpawnPlayer(Vector3.zero);
            var manager = arena.SpawnCheckpointManager();

            var checkpointObject = arena.Track(new GameObject("Checkpoint_Here"));
            checkpointObject.SetActive(false);
            checkpointObject.AddComponent<BoxCollider>().isTrigger = true;
            var mine = checkpointObject.AddComponent<Checkpoint>();
            checkpointObject.SetActive(true);
            yield return null;

            var data = new SaveData { SceneName = ThisScene };
            data.EnsureStateFor(ThisScene).CheckpointId = mine.CheckpointId;
            data.EnsureStateFor(ElsewhereScene).CheckpointId = "Checkpoint_In_A_Scene_Not_Loaded";

            saves.Apply(data);
            yield return null;

            Assert.That(manager.ActiveCheckpoint, Is.EqualTo(mine));
        }

        [UnityTest]
        public IEnumerator Apply_AdoptsThePerSceneRecordsOfTheSaveItLoaded()
        {
            arena.SpawnPlayer(Vector3.zero);
            SceneMemory.Record(ElsewhereScene, new Vector3(1f, 1f, 1f), Quaternion.identity, "STALE");
            yield return null;

            var data = new SaveData { SceneName = ThisScene };
            data.EnsureStateFor(ThisScene).PlayerPosition = Vector3.zero;

            saves.Apply(data);
            yield return null;

            Assert.That(SceneMemory.Get(ElsewhereScene), Is.Null,
                "a loaded save replaces what this session remembered, it does not merge with it");
        }

        // -------------------------------------------------------------- travel guard rails

        [UnityTest]
        public IEnumerator TravelToASceneThatIsNotInTheBuildIsRefusedAndChangesNothing()
        {
            arena.SpawnPlayer(Vector3.zero);
            yield return null;

            LogAssert.ignoreFailingMessages = true;
            EventBus.Publish(new SceneTravelRequestedEvent("NoSuchScene", "Anywhere", "a test"));
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            Assert.That(SceneTravel.IsTravelling, Is.False, "a refused journey must not be left pending");
            Assert.That(saves.IsSafeToSave, Is.True,
                "refusing travel must not leave the player in a scene they cannot save in");
        }

        [UnityTest]
        public IEnumerator SavingIsRefusedWhileTheWorldIsChanging()
        {
            arena.SpawnPlayer(Vector3.zero);
            yield return null;

            saves.BlockSaves(SceneTravel.SaveBlockReason);

            Assert.That(saves.IsSafeToSave, Is.False);
            Assert.That(saves.Save(SaveSlot.Manual), Is.False);
            Assert.That(saves.HasSave(SaveSlot.Manual), Is.False, "a refused save must write nothing at all");

            saves.AllowSaves(SceneTravel.SaveBlockReason);
            Assert.That(saves.Save(SaveSlot.Manual), Is.True);
        }

        [UnityTest]
        public IEnumerator SavingIsRefusedWhileACinematicIsPlaying()
        {
            arena.SpawnPlayer(Vector3.zero);
            yield return null;

            EventBus.Publish(new CinematicStartedEvent("INTRO", null));
            yield return null;

            Assert.That(saves.IsSafeToSave, Is.False,
                "a cinematic sets flags and moves the player as it runs; half of one is not a save");

            EventBus.Publish(new CinematicCompletedEvent("INTRO"));
            yield return null;

            Assert.That(saves.IsSafeToSave, Is.True);
        }

        /// <summary>A skipped cinematic ends the same transition, and must release the same block.</summary>
        [UnityTest]
        public IEnumerator SkippingACinematicAllowsSavingAgain()
        {
            arena.SpawnPlayer(Vector3.zero);
            yield return null;

            EventBus.Publish(new CinematicStartedEvent("INTRO", null));
            yield return null;
            EventBus.Publish(new CinematicSkippedEvent("INTRO"));
            yield return null;

            Assert.That(saves.IsSafeToSave, Is.True);
        }

        // ------------------------------------------------------------------ the exit itself

        [UnityTest]
        public IEnumerator AnExitPublishesATravelRequestAndNothingElse()
        {
            var heard = 0;
            var target = string.Empty;
            var spawn = string.Empty;

            void OnRequest(SceneTravelRequestedEvent request)
            {
                heard++;
                target = request.TargetScene;
                spawn = request.SpawnId;
            }

            EventBus.Subscribe<SceneTravelRequestedEvent>(OnRequest);

            var exitObject = arena.Track(new GameObject("Door"));
            exitObject.AddComponent<BoxCollider>().isTrigger = true;
            var exit = exitObject.AddComponent<SceneExit>();
            exit.Configure("Agniya", "FromAvarsha", "USED_TEMPLE_DOOR");
            yield return null;

            // The SaveManager in this arena hears the same request and refuses it,
            // because 'Agniya' is not loadable from a PlayMode test scene. That refusal is
            // its own test below; here it is noise.
            LogAssert.ignoreFailingMessages = true;
            exit.Interact(null);
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            EventBus.Unsubscribe<SceneTravelRequestedEvent>(OnRequest);

            Assert.That(heard, Is.EqualTo(1));
            Assert.That(target, Is.EqualTo("Agniya"));
            Assert.That(spawn, Is.EqualTo("FromAvarsha"));
            Assert.That(WorldState.Instance.GetFlag("USED_TEMPLE_DOOR"), Is.True);
        }

        [UnityTest]
        public IEnumerator AnExitRefusesASecondPressWhileAJourneyIsAlreadyInFlight()
        {
            var exitObject = arena.Track(new GameObject("Door"));
            exitObject.AddComponent<BoxCollider>().isTrigger = true;
            var exit = exitObject.AddComponent<SceneExit>();
            exit.Configure("Agniya", "FromAvarsha");
            yield return null;

            Assert.That(exit.CanInteract, Is.True);

            SceneTravel.Begin("Agniya", "FromAvarsha");

            Assert.That(exit.CanInteract, Is.False, "pressing a door twice must not queue two journeys");
        }

        [UnityTest]
        public IEnumerator AnExitWithNoDestinationCannotBeUsed()
        {
            var exitObject = arena.Track(new GameObject("Door"));
            exitObject.AddComponent<BoxCollider>().isTrigger = true;
            var exit = exitObject.AddComponent<SceneExit>();
            exit.Configure(string.Empty, "Somewhere");
            yield return null;

            Assert.That(exit.CanInteract, Is.False);
        }

        [UnityTest]
        public IEnumerator ASpawnPointIsFoundByIdIncludingWhenItsParentIsInactive()
        {
            var parent = arena.Track(new GameObject("ArrivalPoints"));
            var child = new GameObject("Arrival");
            child.transform.SetParent(parent.transform);
            child.transform.position = new Vector3(3f, 0f, 3f);
            child.AddComponent<SceneSpawnPoint>().Configure("FromAvarsha");
            parent.SetActive(false);
            yield return null;

            var found = SceneSpawnPoint.Find("FromAvarsha");

            Assert.That(found, Is.Not.Null);
            Assert.That(found.Position.x, Is.EqualTo(3f).Within(0.01f));
        }
    }

    /// <summary>
    /// The anti-softlock sweep that runs after a save is applied (SPEC.md section 55,
    /// TASK 041). Each test puts the game into a state a player could not get out of and
    /// checks that loading gets them out of it.
    /// </summary>
    public class ProgressionRecoveryPlayModeTests
    {
        private TestArena arena;
        private string root;
        private SaveManager saves;

        [SetUp]
        public void SetUp()
        {
            arena = new TestArena();
            arena.EnsureWorldState();
            SceneMemory.Clear();

            root = Path.Combine(Path.GetTempPath(), "GodGameRecoveryTests", Path.GetRandomFileName());
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
            SceneMemory.Clear();

            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private ItemPickup SpawnPickup(InventoryItem item, string saveId, bool essential)
        {
            var go = arena.Track(new GameObject($"Pickup_{saveId}"));
            go.SetActive(false);
            go.AddComponent<BoxCollider>().isTrigger = true;
            go.AddComponent<SaveIdentity>().Configure(saveId);
            var pickup = go.AddComponent<ItemPickup>();
            pickup.Configure(item, 1, null, essential);
            go.SetActive(true);
            return pickup;
        }

        private static InventoryItem NewItem(TestArena arena, string id)
        {
            var item = arena.TrackAsset(ScriptableObject.CreateInstance<InventoryItem>());
            item.Configure(id, id, ItemCategory.QuestItem, canStack: false);
            return item;
        }

        [UnityTest]
        public IEnumerator AnEssentialItemRecordedAsCollectedButMissingIsGrantedBack()
        {
            var key = NewItem(arena, "TEMPLE_KEY");
            var inventory = arena.SpawnInventoryManager(key);
            arena.SpawnPlayer(Vector3.zero);
            var pickup = SpawnPickup(key, "PICKUP_TEMPLE_KEY", essential: true);
            yield return null;

            // The dead state: the world says it was taken, the bag says otherwise.
            WorldState.Instance.SetFlag(WorldObjectState.CollectedFlag("PICKUP_TEMPLE_KEY"));
            Assert.That(pickup.Collected, Is.True, "the pickup should have applied the remembered collection");
            Assert.That(inventory.GetCount(key), Is.Zero);

            LogAssert.ignoreFailingMessages = true;
            var recovered = ProgressionRecovery.Run();
            LogAssert.ignoreFailingMessages = false;

            Assert.That(inventory.GetCount(key), Is.EqualTo(1), "an essential item must not be lost for good");
            Assert.That(recovered, Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AnItemThatIsNotEssentialIsLeftLost()
        {
            var trinket = NewItem(arena, "TRINKET");
            var inventory = arena.SpawnInventoryManager(trinket);
            arena.SpawnPlayer(Vector3.zero);
            SpawnPickup(trinket, "PICKUP_TRINKET", essential: false);
            yield return null;

            WorldState.Instance.SetFlag(WorldObjectState.CollectedFlag("PICKUP_TRINKET"));

            var recovered = ProgressionRecovery.Run();

            Assert.That(inventory.GetCount(trinket), Is.Zero,
                "recovery exists to prevent softlocks, not to undo every loss");
            Assert.That(recovered, Is.Empty);
        }

        [UnityTest]
        public IEnumerator AnEssentialItemStillHeldIsNotDuplicated()
        {
            var key = NewItem(arena, "TEMPLE_KEY");
            var inventory = arena.SpawnInventoryManager(key);
            arena.SpawnPlayer(Vector3.zero);
            SpawnPickup(key, "PICKUP_TEMPLE_KEY", essential: true);
            yield return null;

            WorldState.Instance.SetFlag(WorldObjectState.CollectedFlag("PICKUP_TEMPLE_KEY"));
            inventory.Add(key, 1);

            var recovered = ProgressionRecovery.Run();

            Assert.That(inventory.GetCount(key), Is.EqualTo(1));
            Assert.That(recovered, Is.Empty);
        }

        [UnityTest]
        public IEnumerator AnUncollectedEssentialItemIsNotGrantedEarly()
        {
            var key = NewItem(arena, "TEMPLE_KEY");
            var inventory = arena.SpawnInventoryManager(key);
            arena.SpawnPlayer(Vector3.zero);
            SpawnPickup(key, "PICKUP_TEMPLE_KEY", essential: true);
            yield return null;

            var recovered = ProgressionRecovery.Run();

            Assert.That(inventory.GetCount(key), Is.Zero, "the pickup is still standing there to be taken");
            Assert.That(recovered, Is.Empty);
        }

        [UnityTest]
        public IEnumerator AnEssentialDoorRecordedAsOpenIsOpened()
        {
            arena.SpawnPlayer(Vector3.zero);

            var go = arena.Track(new GameObject("Gate_Essential"));
            go.SetActive(false);
            go.AddComponent<BoxCollider>();
            var identity = go.AddComponent<SaveIdentity>();
            identity.Configure("GATE_TEMPLE");
            var gate = go.AddComponent<PuzzleGate>();
            gate.Configure("TEMPLE_PUZZLE", identity, isEssential: true);

            // Marked before the gate wakes, which is the order a load produces: the flag
            // is already in WorldState by the time the scene's objects enable.
            WorldState.Instance.SetFlag(WorldObjectState.OpenedFlag("GATE_TEMPLE"));
            go.SetActive(true);
            yield return null;

            Assert.That(gate.IsOpen, Is.True, "the door's own restore path should have opened it");

            var recovered = ProgressionRecovery.Run();
            Assert.That(recovered, Is.Empty, "nothing left for the sweep to put right");
        }

        /// <summary>
        /// The case the sweep is actually for: the door has no record of its own — its
        /// <c>SaveIdentity</c> was added after this save was written — but the puzzle it
        /// guards is recorded as solved. Without the sweep the player stands in front of
        /// a solved puzzle with no way through.
        /// </summary>
        [UnityTest]
        public IEnumerator AnEssentialDoorWhosePuzzleIsSolvedIsOpenedEvenWithNoRecordOfItsOwn()
        {
            arena.SpawnPlayer(Vector3.zero);

            var controllerObject = arena.Track(new GameObject("PuzzleController"));
            controllerObject.SetActive(false);
            var controller = controllerObject.AddComponent<PuzzleController>();
            controller.Configure(new MonoBehaviour[0], "TEMPLE_PUZZLE_SOLVED", "TEMPLE_PUZZLE");

            var go = arena.Track(new GameObject("Gate_Essential"));
            go.SetActive(false);
            go.AddComponent<BoxCollider>();
            var gate = go.AddComponent<PuzzleGate>();
            gate.Configure("TEMPLE_PUZZLE", null, isEssential: true);

            WorldState.Instance.SetFlag("TEMPLE_PUZZLE_SOLVED");
            controllerObject.SetActive(true);
            go.SetActive(true);
            yield return null;

            Assert.That(gate.IsOpen, Is.False, "staging: with no elements the controller does not announce a solve");

            LogAssert.ignoreFailingMessages = true;
            var recovered = ProgressionRecovery.Run();
            LogAssert.ignoreFailingMessages = false;

            Assert.That(gate.IsOpen, Is.True);
            Assert.That(recovered, Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ADoorThatIsNotEssentialIsLeftShut()
        {
            arena.SpawnPlayer(Vector3.zero);

            var controllerObject = arena.Track(new GameObject("PuzzleController"));
            controllerObject.SetActive(false);
            controllerObject.AddComponent<PuzzleController>()
                .Configure(new MonoBehaviour[0], "SIDE_PUZZLE_SOLVED", "SIDE_PUZZLE");

            var go = arena.Track(new GameObject("Gate_Optional"));
            go.SetActive(false);
            go.AddComponent<BoxCollider>();
            var gate = go.AddComponent<PuzzleGate>();
            gate.Configure("SIDE_PUZZLE", null, isEssential: false);

            WorldState.Instance.SetFlag("SIDE_PUZZLE_SOLVED");
            controllerObject.SetActive(true);
            go.SetActive(true);
            yield return null;

            var recovered = ProgressionRecovery.Run();

            Assert.That(gate.IsOpen, Is.False);
            Assert.That(recovered, Is.Empty);
        }

        [UnityTest]
        public IEnumerator RecoveryDoesNothingToAHealthyGame()
        {
            var key = NewItem(arena, "TEMPLE_KEY");
            var inventory = arena.SpawnInventoryManager(key);
            arena.SpawnPlayer(Vector3.zero);
            SpawnPickup(key, "PICKUP_TEMPLE_KEY", essential: true);
            yield return null;

            var data = new SaveData { SceneName = SceneManager.GetActiveScene().name };
            saves.Apply(data);
            yield return null;

            Assert.That(inventory.GetCount(key), Is.Zero);
            Assert.That(ProgressionRecovery.Run(), Is.Empty,
                "a recovery that fires in ordinary play is a bug somewhere else");
        }
    }
}

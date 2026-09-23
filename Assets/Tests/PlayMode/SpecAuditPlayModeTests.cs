using System.Collections;
using Game.AI;
using Game.Animation;
using Game.Audio;
using Game.Combat;
using Game.Core;
using Game.DevTools;
using Game.Dialogue;
using Game.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.Play
{
    /// <summary>
    /// The half of the SPEC.md audit work (TASK 020-026) that only goes wrong after
    /// several frames of a real game loop: a guard noticing something has left the
    /// world and pulling it back, an arena abandoning a live encounter, an
    /// auto-pause, and a display change deferred across a scene load.
    ///
    /// The pure containment arithmetic behind all of it is in
    /// <c>SpecAuditTests</c> (EditMode), where it runs in milliseconds.
    /// </summary>
    public class SpecAuditPlayModeTests
    {
        [UnityTest]
        public IEnumerator EdgeCase13_MissingImportedAudio_ProceduralCueStillPlays()
        {
            SfxSpawner.Play(SfxKind.GuardBreak, Vector3.zero);
            yield return null;
            var source = Object.FindFirstObjectByType<AudioSource>();
            Assert.IsNotNull(source);
            Assert.IsNotNull(source.clip);
            Assert.Greater(source.clip.samples, 0);
        }

        [UnityTest]
        public IEnumerator EdgeCase14_MissingAnimationClip_ProceduralHitReactionStillRuns()
        {
            var player = arena.SpawnPlayer(Vector3.zero);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(player.Root.transform, false);
            var animator = player.Root.AddComponent<PlaceholderAnimator>();
            animator.Configure(visual.transform);
            Assert.IsNull(player.Root.GetComponent<Animator>());
            player.Health.TakeDamage(DamageData.Create(10f, null));
            yield return null;
            Assert.Greater(Quaternion.Angle(Quaternion.identity, visual.transform.localRotation), 0.01f);
            Assert.AreEqual(90f, player.Health.CurrentHealth, 0.01f);
        }

        [Test]
        public void EdgeCase27_BossDefeatedBeforeDialogue_ChoosesPostBossLine()
        {
            arena.EnsureWorldState();
            var flag = WorldObjectState.DeadFlag("BOSS_EARLY");
            var graph = arena.TrackAsset(ScriptableObject.CreateInstance<DialogueGraph>());
            graph.Configure("EARLY_BOSS_DIALOGUE", new[] { "AFTER", "BEFORE" }, new[]
            {
                new DialogueNode { DialogueId = "AFTER", Text = "You have already won.", RequiredFlags = new[] { flag } },
                new DialogueNode { DialogueId = "BEFORE", Text = "Face the guardian.", BlockingFlags = new[] { flag } }
            });
            Assert.AreEqual("BEFORE", graph.GetEntryNode().DialogueId);
            WorldState.Instance.SetFlag(flag);
            Assert.AreEqual("AFTER", graph.GetEntryNode().DialogueId);
        }

        [Test]
        public void EdgeCase29_EnteringAreaBeforeStoryTrigger_DoesNotAdvanceQuest()
        {
            arena.EnsureWorldState();
            var area = arena.Track(new GameObject("Story Area"));
            area.AddComponent<BoxCollider>();
            var trigger = area.AddComponent<LocationTrigger>();
            trigger.Configure("Story Area", null, "AREA_ENTERED", required: new[] { "STORY_READY" });
            Assert.IsFalse(trigger.Fire());
            Assert.IsFalse(WorldState.Instance.GetFlag("AREA_ENTERED"));
            WorldState.Instance.SetFlag("STORY_READY");
            Assert.IsTrue(trigger.Fire());
            Assert.IsTrue(WorldState.Instance.GetFlag("AREA_ENTERED"));
        }

        [Test]
        public void EdgeCase30_LateAreaTraversal_CannotSetProgressFlagBeforePrerequisites()
        {
            arena.EnsureWorldState();
            var area = arena.Track(new GameObject("Late Area"));
            area.AddComponent<BoxCollider>();
            var trigger = area.AddComponent<LocationTrigger>();
            trigger.Configure("Late Area", null, "LATE_AREA_REACHED",
                required: new[] { "FIRST_TEMPLE_CLEAR", "SECOND_TEMPLE_CLEAR" });
            WorldState.Instance.SetFlag("FIRST_TEMPLE_CLEAR");
            Assert.IsFalse(trigger.Fire());
            Assert.IsFalse(WorldState.Instance.GetFlag("LATE_AREA_REACHED"));
            WorldState.Instance.SetFlag("SECOND_TEMPLE_CLEAR");
            Assert.IsTrue(trigger.Fire());
        }

        private TestArena arena;

        [SetUp]
        public void SetUp()
        {
            arena = new TestArena();
            DebugMode.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            DebugMode.ResetForTests();
            arena.Dispose();
        }

        /// <summary>
        /// A bounds volume for the test, built inactive: AddComponent on an already
        /// active object runs Awake before Configure could set the numbers, which is
        /// the gotcha TestArena documents throughout.
        /// </summary>
        private WorldBounds NewBounds(float killPlaneY = -10f, float footprint = 120f)
        {
            var go = arena.Track(new GameObject("WorldBounds"));
            go.SetActive(false);
            var bounds = go.AddComponent<WorldBounds>();
            bounds.Configure(killPlaneY, Vector3.zero, new Vector3(footprint, 0f, footprint));
            go.SetActive(true);
            return bounds;
        }

        // ----------------------------------- SPEC.md section 50, section 54 edge case 10

        [UnityTest]
        public IEnumerator PlayerBoundsGuard_PutsThePlayerBackOnTheLatestCheckpoint()
        {
            NewBounds();
            arena.SpawnCheckpointManager();

            var safe = new Vector3(6f, 0f, 6f);

            var checkpointGo = arena.Track(new GameObject("Checkpoint"));
            checkpointGo.SetActive(false);
            checkpointGo.transform.position = safe;
            checkpointGo.AddComponent<BoxCollider>().isTrigger = true;
            var checkpoint = checkpointGo.AddComponent<Checkpoint>();
            checkpointGo.SetActive(true);
            checkpoint.Activate();

            var player = arena.SpawnPlayer(safe);
            var guard = player.Root.AddComponent<PlayerBoundsGuard>();

            var recoveries = 0;
            void OnRecovered(OutOfWorldRecoveryEvent e) => recoveries++;
            EventBus.Subscribe<OutOfWorldRecoveryEvent>(OnRecovered);

            try
            {
                // Down a hole in the floor.
                player.Position = new Vector3(0f, -40f, 0f);

                yield return TestArena.Until(
                    () => guard.RecoveryCount > 0,
                    "the bounds guard to notice the player had left the world");

                Assert.AreEqual(1, recoveries, "The recovery must be announced, so the player is told why they moved.");
                Assert.Less(Vector3.Distance(player.Position, safe), 0.5f,
                    "SPEC.md section 50 asks for the latest valid checkpoint, not simply somewhere on the floor.");
            }
            finally
            {
                EventBus.Unsubscribe<OutOfWorldRecoveryEvent>(OnRecovered);
            }
        }

        [UnityTest]
        public IEnumerator PlayerBoundsGuard_LeavesThePlayerAloneInsideTheWorld()
        {
            NewBounds();
            var player = arena.SpawnPlayer(new Vector3(3f, 0f, 3f));
            var guard = player.Root.AddComponent<PlayerBoundsGuard>();

            // High above the floor and far from the middle, but still inside: this
            // must not read as an escape (see WorldBounds on why Y is not a limit).
            player.Position = new Vector3(55f, 30f, 55f);

            yield return new WaitForSeconds(0.8f);

            Assert.AreEqual(0, guard.RecoveryCount);
            Assert.AreEqual(new Vector3(55f, 30f, 55f), player.Position);
        }

        // ------------------------------------------------ SPEC.md section 54 edge case 9

        [UnityTest]
        public IEnumerator EnemyBoundsGuard_ReturnsAFallenEnemyToItsHome()
        {
            arena.BuildFloor();
            arena.BuildNavMesh();
            NewBounds();

            var archetype = arena.NewArchetype("FALLER");
            var home = new Vector3(5f, 0f, 5f);
            var rig = arena.SpawnEnemy("Faller", home, archetype);

            rig.Root.SetActive(false);
            var guard = rig.Root.AddComponent<EnemyBoundsGuard>();
            rig.Root.SetActive(true);

            yield return null;

            rig.Agent.enabled = false;
            rig.Root.transform.position = new Vector3(0f, -60f, 0f);

            yield return TestArena.Until(
                () => guard.RecoveryCount > 0,
                "the enemy bounds guard to notice the enemy had left the world");

            Assert.Less(Vector3.Distance(rig.Position, rig.Controller.Home), 1.5f);
            Assert.IsNotNull(rig.Root, "Section 55 says a required NPC cannot disappear; it is put back, not deleted.");
        }

        // ------------------------ SPEC.md section 54 edge case 7, sections 55 and 56

        private BossController NewBoss(EnemyRig rig)
        {
            rig.Root.SetActive(false);
            var boss = rig.Root.AddComponent<BossController>();
            boss.Configure("TEST_BOSS", "Test Boss", rig.Controller, rig.Combatant);
            rig.Root.SetActive(true);
            return boss;
        }

        private BossArena NewArena(BossController boss, Vector3 centre, float radius, float grace)
        {
            var go = arena.Track(new GameObject("BossArena"));
            go.SetActive(false);
            go.transform.position = centre;
            var bossArena = go.AddComponent<BossArena>();
            bossArena.Configure(boss, null, radius, margin: 1f, grace: grace);
            go.SetActive(true);
            return bossArena;
        }

        [UnityTest]
        public IEnumerator BossArena_ResetsTheEncounterWhenThePlayerWalksOutAndStaysOut()
        {
            arena.BuildFloor();
            arena.BuildNavMesh();

            var archetype = arena.NewArchetype("ARENA_BOSS", health: 100f);
            var rig = arena.SpawnEnemy("Boss", Vector3.zero, archetype);
            var boss = NewBoss(rig);
            var bossArena = NewArena(boss, Vector3.zero, radius: 10f, grace: 0.3f);

            var player = arena.SpawnPlayer(new Vector3(2f, 0f, 2f));

            EventBus.Publish(new EnemyAlertedEvent(rig.Root, player.Root));
            yield return null;
            Assert.IsTrue(boss.EncounterStarted);

            // Chip it, so the reset has something to undo.
            rig.Health.TakeDamage(DamageData.Create(50f, player.Root));
            yield return null;
            Assert.Less(rig.Health.HealthFraction, 0.6f);
            Assert.AreEqual(2, boss.Phase, "50 of 100 damage should already be phase 2.");

            var resets = 0;
            void OnReset(BossEncounterResetEvent e) => resets++;
            EventBus.Subscribe<BossEncounterResetEvent>(OnReset);

            try
            {
                player.Position = new Vector3(60f, 0f, 0f);

                yield return TestArena.Until(
                    () => bossArena.ResetCount > 0,
                    "the arena to notice the player had left the fight");

                Assert.AreEqual(1, resets);
                Assert.IsFalse(boss.EncounterStarted, "Re-entering should start the encounter properly, not resume it.");
                Assert.AreEqual(1, boss.Phase);
                Assert.AreEqual(rig.Health.MaxHealth, rig.Health.CurrentHealth,
                    "SPEC.md section 56: chipping a boss from outside the arena must not pay.");
            }
            finally
            {
                EventBus.Unsubscribe<BossEncounterResetEvent>(OnReset);
            }
        }

        [UnityTest]
        public IEnumerator BossArena_LeavesTheFightAloneWhileThePlayerIsStillInIt()
        {
            arena.BuildFloor();
            arena.BuildNavMesh();

            var archetype = arena.NewArchetype("ARENA_BOSS", health: 100f);
            var rig = arena.SpawnEnemy("Boss", Vector3.zero, archetype);
            var boss = NewBoss(rig);
            var bossArena = NewArena(boss, Vector3.zero, radius: 10f, grace: 0.3f);

            var player = arena.SpawnPlayer(new Vector3(2f, 0f, 2f));

            EventBus.Publish(new EnemyAlertedEvent(rig.Root, player.Root));
            yield return null;

            // A dodge that carries the player a step over the line must not end the
            // fight — SPEC.md section 56 also says not to over-restrict. 11.5 is past
            // the radius and its margin, but back inside well before the grace runs out.
            player.Position = new Vector3(11.5f, 0f, 0f);
            yield return new WaitForSeconds(0.15f);
            player.Position = new Vector3(4f, 0f, 0f);

            yield return new WaitForSeconds(0.8f);

            Assert.AreEqual(0, bossArena.ResetCount);
            Assert.IsTrue(boss.EncounterStarted);
        }

        // --------------------------------- SPEC.md section 54 edge cases 21, 22 and 23

        private DeviceWatcher NewDeviceWatcher()
        {
            var go = arena.Track(new GameObject("DeviceWatcher"));
            return go.AddComponent<DeviceWatcher>();
        }

        [UnityTest]
        public IEnumerator DeviceWatcher_LosingWindowFocus_AsksForAPause()
        {
            var manager = arena.SpawnGameManager();
            var watcher = NewDeviceWatcher();

            // GameManager reaches Playing in its own Start, one frame after Awake.
            yield return TestArena.Until(() => manager.CurrentState == GameState.Playing, "the game to start playing");

            var requests = 0;
            void OnRequested(AutoPauseRequestedEvent e) => requests++;
            EventBus.Subscribe<AutoPauseRequestedEvent>(OnRequested);

            try
            {
                watcher.HandleFocusChanged(false);
                yield return null;

                Assert.AreEqual(1, requests);
                Assert.AreEqual(1, watcher.AutoPauseCount);
            }
            finally
            {
                EventBus.Unsubscribe<AutoPauseRequestedEvent>(OnRequested);
            }
        }

        [UnityTest]
        public IEnumerator DeviceWatcher_RegainingFocus_DoesNotResumeByItself()
        {
            var manager = arena.SpawnGameManager();
            var watcher = NewDeviceWatcher();
            yield return TestArena.Until(() => manager.CurrentState == GameState.Playing, "the game to start playing");

            var requests = 0;
            void OnRequested(AutoPauseRequestedEvent e) => requests++;
            EventBus.Subscribe<AutoPauseRequestedEvent>(OnRequested);

            try
            {
                // Coming back to a game that un-paused itself mid-swing is worse than
                // one more keypress.
                watcher.HandleFocusChanged(true);
                yield return null;

                Assert.AreEqual(0, requests);
                Assert.AreEqual(GameState.Playing, manager.CurrentState);
            }
            finally
            {
                EventBus.Unsubscribe<AutoPauseRequestedEvent>(OnRequested);
            }
        }

        [UnityTest]
        public IEnumerator DeviceWatcher_AControllerComingBack_IsAnnouncedWithoutPausing()
        {
            var manager = arena.SpawnGameManager();
            var watcher = NewDeviceWatcher();
            yield return TestArena.Until(() => manager.CurrentState == GameState.Playing, "the game to start playing");

            InputDeviceChangedEvent? received = null;
            void OnChanged(InputDeviceChangedEvent e) => received = e;
            EventBus.Subscribe<InputDeviceChangedEvent>(OnChanged);

            try
            {
                watcher.HandleControllerChanged("Test Pad", connected: true);
                yield return null;

                Assert.IsTrue(received.HasValue);
                Assert.IsTrue(received.Value.Connected);
                Assert.IsNotEmpty(received.Value.PlayerMessage);
                Assert.AreEqual(0, watcher.AutoPauseCount, "Plugging a controller in is not a reason to stop the game.");
            }
            finally
            {
                EventBus.Unsubscribe<InputDeviceChangedEvent>(OnChanged);
            }
        }

        // -------------------------------------- SPEC.md section 54 edge cases 20 and 24

        [UnityTest]
        public IEnumerator SettingsManager_ADisplayChangeDuringALoad_IsDeferredUntilItFinishes()
        {
            var settings = arena.SpawnSettingsManager();
            yield return null;

            var applied = 0;
            void OnApplied(DisplaySettingsAppliedEvent e) => applied++;
            EventBus.Subscribe<DisplaySettingsAppliedEvent>(OnApplied);

            try
            {
                // Stand in for a scene load being in flight. Changing the resolution
                // here would resize the screen underneath a half-built canvas.
                GameSceneManager.SetLoadingForTests(true);

                // The level the editor is already on, so this asserts the deferral
                // without actually switching the editor's graphics quality behind the
                // rest of the suite.
                settings.Current.QualityLevel = QualitySettings.GetQualityLevel();
                settings.ApplyDisplaySettings();

                Assert.IsTrue(settings.DisplayApplyPending);
                Assert.AreEqual(0, applied, "Nothing should be applied while a scene is loading.");

                GameSceneManager.SetLoadingForTests(false);

                yield return TestArena.Until(() => !settings.DisplayApplyPending,
                    "the deferred display change to land once the load finished");

                Assert.AreEqual(1, applied);
            }
            finally
            {
                EventBus.Unsubscribe<DisplaySettingsAppliedEvent>(OnApplied);
                GameSceneManager.SetLoadingForTests(false);
            }
        }

        // ----------------------------------------------------------- SPEC.md section 52

        [UnityTest]
        public IEnumerator DebugCommands_KillAllEnemies_KillsEveryLivingEnemyThroughTheRealDeathPath()
        {
            arena.BuildFloor();
            arena.BuildNavMesh();

            var archetype = arena.NewArchetype("MOB");
            var first = arena.SpawnEnemy("Mob0", new Vector3(3f, 0f, 0f), archetype);
            var second = arena.SpawnEnemy("Mob1", new Vector3(-3f, 0f, 0f), archetype);

            yield return null;

            DebugMode.Enable();
            var result = DebugCommands.KillAllEnemies();

            yield return null;

            StringAssert.Contains("2", result);
            Assert.IsTrue(first.Health.IsDead);
            Assert.IsTrue(second.Health.IsDead);
        }

        [UnityTest]
        public IEnumerator DebugCommands_RefillHealth_RestoresThePlayer()
        {
            var player = arena.SpawnPlayer(Vector3.zero);
            player.Health.TakeDamage(DamageData.Create(40f, null));
            yield return null;

            Assert.Less(player.Health.CurrentHealth, player.Health.MaxHealth);

            DebugMode.Enable();
            DebugCommands.RefillHealth();

            Assert.AreEqual(player.Health.MaxHealth, player.Health.CurrentHealth);
        }

        [UnityTest]
        public IEnumerator DebugCommands_Teleport_MovesThePlayerAndClearsTheFall()
        {
            var player = arena.SpawnPlayer(Vector3.zero);
            yield return null;

            DebugMode.Enable();
            var result = DebugCommands.Run("teleport 12 0 -7");

            yield return null;

            StringAssert.Contains("Teleported", result);
            Assert.Less(Vector3.Distance(player.Position, new Vector3(12f, 0f, -7f)), 0.1f);
        }

        [UnityTest]
        public IEnumerator DebugCommands_SetWorldFlag_GoesThroughTheRealWorldState()
        {
            var state = arena.EnsureWorldState();
            yield return null;

            DebugMode.Enable();
            DebugCommands.Run("flag TEST_AUDIT_FLAG true");

            Assert.IsTrue(state.GetFlag("TEST_AUDIT_FLAG"));

            DebugCommands.Run("flag TEST_AUDIT_FLAG false");
            Assert.IsFalse(state.GetFlag("TEST_AUDIT_FLAG"));
        }

        // ------------------------------------------ SPEC.md section 42, the Map screen

        [UnityTest]
        public IEnumerator MapUI_DrawsAMarkerForThePlayerAndEachCheckpoint()
        {
            NewBounds();
            arena.SpawnCheckpointManager();
            arena.SpawnPlayer(new Vector3(4f, 0f, 4f));

            var checkpointGo = arena.Track(new GameObject("Checkpoint_Map"));
            checkpointGo.SetActive(false);
            checkpointGo.transform.position = new Vector3(-8f, 0f, 10f);
            checkpointGo.AddComponent<BoxCollider>().isTrigger = true;
            checkpointGo.AddComponent<Checkpoint>();
            checkpointGo.SetActive(true);

            var canvasGo = arena.Track(new GameObject("Canvas", typeof(RectTransform)));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // The panel is a child, not the canvas itself: MapUI's Awake hides its
            // root, and a root that is also the object holding MapUI would switch
            // itself off the moment it woke up.
            var rootGo = new GameObject("MapRoot", typeof(RectTransform));
            rootGo.transform.SetParent(canvasGo.transform, false);

            var contentGo = new GameObject("MapContent", typeof(RectTransform));
            contentGo.transform.SetParent(rootGo.transform, false);
            var content = (RectTransform)contentGo.transform;
            content.sizeDelta = new Vector2(600f, 400f);

            var actions = arena.BuildInputActions();
            actions.FindActionMap("Gameplay").AddAction("Map", InputActionType.Button);

            canvasGo.SetActive(false);
            var map = canvasGo.AddComponent<Game.UI.MapUI>();
            map.Configure(actions, rootGo, content);
            canvasGo.SetActive(true);

            yield return null;

            map.Redraw();

            // One marker plus one label each for the player and the checkpoint, and a
            // facing tick for the player.
            Assert.GreaterOrEqual(map.MarkerCount, 4,
                "The map should draw the player and the checkpoint, each with a readable label.");
        }
    }
}

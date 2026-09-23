using System.Collections;
using System.Collections.Generic;
using Game.Combat;
using Game.Core;
using Game.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Play
{
    /// <summary>
    /// The TASK 014 cinematic system against a live scene: entering/exiting
    /// <see cref="GameState.Cutscene"/> for real, subtitle events firing per beat,
    /// and skipping (SPEC.md section 69's "skippable" requirement).
    ///
    /// <see cref="GameManager.EnterCutscene"/> freezes <c>Time.timeScale</c>, so
    /// waits in these tests use <see cref="Time.unscaledDeltaTime"/>/frame counts
    /// rather than <see cref="TestArena.Until"/>, which is driven by scaled
    /// <see cref="Time.time"/> and would never see its deadline arrive while frozen.
    /// </summary>
    public class CinematicPlayModeTests
    {
        private TestArena arena;
        private GameManager gameManager;

        [SetUp]
        public void SetUp()
        {
            arena = new TestArena();
            arena.EnsureWorldState();
            gameManager = arena.SpawnGameManager();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            arena.Dispose();
        }

        private static IEnumerator WaitFrames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private CinematicPlayer NewCinematic(string id, string endFlag, params float[] beatDurations)
        {
            var go = arena.Track(new GameObject($"Cinematic_{id}"));
            go.SetActive(false);
            var player = go.AddComponent<CinematicPlayer>();

            var beats = new CinematicBeat[beatDurations.Length];
            for (var i = 0; i < beatDurations.Length; i++)
            {
                beats[i] = new CinematicBeat { Subtitle = $"Line {i}", Duration = beatDurations[i] };
            }

            player.Configure(id, startFlag: null, endFlag: endFlag, cinematicBeats: beats);
            go.SetActive(true);
            return player;
        }

        [UnityTest]
        public IEnumerator Cinematic_PlayingEntersCutsceneAndExitsOnCompletion()
        {
            yield return WaitFrames(1); // let GameManager.Start() reach Playing.
            Assert.AreEqual(GameState.Playing, gameManager.CurrentState);

            var player = NewCinematic("TEST_CINE", "TEST_CINE_DONE", 0.02f, 0.02f);

            Assert.IsTrue(player.Play());
            Assert.AreEqual(GameState.Cutscene, gameManager.CurrentState, "Playing did not freeze the world.");
            Assert.AreEqual(0f, Time.timeScale);

            yield return new WaitForSecondsRealtime(0.2f);

            Assert.IsTrue(player.HasPlayed, "The cinematic never finished within the realtime wait.");
            Assert.AreEqual(GameState.Playing, gameManager.CurrentState, "Completion did not hand control back.");
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsTrue(WorldState.Instance.GetFlag("TEST_CINE_DONE"));
        }

        [UnityTest]
        public IEnumerator Cinematic_PublishesABeatEventForEachLineInOrder()
        {
            var shown = new List<int>();
            void OnBeat(CinematicBeatShownEvent e) => shown.Add(e.BeatIndex);
            EventBus.Subscribe<CinematicBeatShownEvent>(OnBeat);

            try
            {
                yield return WaitFrames(1);
                var player = NewCinematic("TEST_BEATS", "TEST_BEATS_DONE", 0.01f, 0.01f, 0.01f);
                player.Play();

                yield return new WaitForSecondsRealtime(0.2f);

                Assert.IsTrue(player.HasPlayed);
                CollectionAssert.AreEqual(new[] { 0, 1, 2 }, shown);
            }
            finally
            {
                EventBus.Unsubscribe<CinematicBeatShownEvent>(OnBeat);
            }
        }

        [UnityTest]
        public IEnumerator Cinematic_SkipEndsItImmediatelyAndRestoresControl()
        {
            var skippedFired = false;
            void OnSkipped(CinematicSkippedEvent e) => skippedFired = true;
            EventBus.Subscribe<CinematicSkippedEvent>(OnSkipped);

            try
            {
                yield return WaitFrames(1);

                // A beat far longer than this test could ever wait out naturally —
                // only Skip() should be able to end it inside the frame budget below.
                var player = NewCinematic("TEST_SKIP", "TEST_SKIP_DONE", 30f);
                player.Play();
                yield return WaitFrames(2);

                Assert.IsTrue(player.IsPlaying, "Sanity check: it should still be running before Skip.");

                player.Skip();
                yield return WaitFrames(5);

                Assert.IsTrue(player.HasPlayed);
                Assert.IsFalse(player.IsPlaying);
                Assert.IsTrue(skippedFired, "Skipping should publish CinematicSkippedEvent.");
                Assert.AreEqual(GameState.Playing, gameManager.CurrentState, "Skipping did not hand control back.");
                Assert.IsTrue(WorldState.Instance.GetFlag("TEST_SKIP_DONE"), "A skip must still set the completion flag.");
            }
            finally
            {
                EventBus.Unsubscribe<CinematicSkippedEvent>(OnSkipped);
            }
        }

        [UnityTest]
        public IEnumerator Cinematic_NeverPlaysTwice()
        {
            yield return WaitFrames(1);
            var player = NewCinematic("TEST_ONCE", "TEST_ONCE_DONE", 0.01f);

            Assert.IsTrue(player.Play());
            Assert.IsFalse(player.Play(), "A cinematic already playing must refuse a second Play().");

            yield return WaitFrames(60);
            Assert.IsTrue(player.HasPlayed);
            Assert.IsFalse(player.Play(), "A finished cinematic must refuse to play again.");
        }

        [UnityTest]
        public IEnumerator Cinematic_StartsFromItsTriggerFlagBeingSet()
        {
            yield return WaitFrames(1);

            var go = arena.Track(new GameObject("Cinematic_Triggered"));
            go.SetActive(false);
            var player = go.AddComponent<CinematicPlayer>();
            player.Configure("TEST_TRIGGERED", "START_CINE", "TEST_TRIGGERED_DONE",
                new[] { new CinematicBeat { Subtitle = "Go", Duration = 0.01f } });
            go.SetActive(true);

            Assert.IsFalse(player.IsPlaying);
            WorldState.Instance.SetFlag("START_CINE");

            Assert.IsTrue(player.IsPlaying, "Setting the trigger flag should have started the cinematic.");
        }
        [UnityTest]
        public IEnumerator EdgeCase01_PlayerDiesDuringCutscene_ReleasesCutsceneAndCompletesBeat()
        {
            yield return WaitFrames(1);
            var cinematic = NewCinematic("DEATH_CINE", "DEATH_CINE_DONE", 30f);
            var player = arena.SpawnPlayer(Vector3.zero);
            Assert.IsTrue(cinematic.Play());
            Assert.AreEqual(GameState.Cutscene, gameManager.CurrentState);
            player.Health.Kill(DamageData.Create(100f, player.Root));
            Assert.AreEqual(GameState.Playing, gameManager.CurrentState);
            Assert.IsFalse(cinematic.IsPlaying);
            Assert.IsTrue(WorldState.Instance.GetFlag("DEATH_CINE_DONE"));
            player.Death.RespawnNow();
        }

        [UnityTest]
        public IEnumerator EdgeCase19_PauseDuringCinematic_IsRefusedAndCinematicCanFinish()
        {
            yield return WaitFrames(1);
            var cinematic = NewCinematic("PAUSE_CINE", "PAUSE_CINE_DONE", 30f);
            Assert.IsTrue(cinematic.Play());
            gameManager.Pause();
            Assert.AreEqual(GameState.Cutscene, gameManager.CurrentState);
            cinematic.Skip();
            yield return WaitFrames(2);
            Assert.AreEqual(GameState.Playing, gameManager.CurrentState);
            Assert.IsTrue(WorldState.Instance.GetFlag("PAUSE_CINE_DONE"));
        }
    }
}

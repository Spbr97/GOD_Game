using System.IO;
using Game.Core;
using Game.Save;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// The parts of cross-scene travel that are decisions about data rather than about a
    /// live scene (TASK 041): the hand-off record, the per-scene save shape, this
    /// session's scene memory, and the version 1 → 2 migration.
    ///
    /// What happens to a running game when the player walks through a door is in
    /// <c>SceneTravelPlayModeTests</c>, which needs two scenes and a frame.
    /// </summary>
    public class SceneTravelTests
    {
        [SetUp]
        public void SetUp()
        {
            SceneTravel.Clear();
            SceneMemory.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            SceneTravel.Clear();
            SceneMemory.Clear();
        }

        // ----------------------------------------------------------- the hand-off

        [Test]
        public void Travel_IsNotInFlightUntilItBegins()
        {
            Assert.That(SceneTravel.IsTravelling, Is.False);
            Assert.That(SceneTravel.TryConsume("Avarsha", out _), Is.False);
        }

        [Test]
        public void Travel_IsConsumedByTheSceneItWasMeantFor()
        {
            SceneTravel.Begin("Agniya", "FromAvarsha");

            Assert.That(SceneTravel.IsTravelling, Is.True);
            Assert.That(SceneTravel.TryConsume("Agniya", out var spawn), Is.True);
            Assert.That(spawn, Is.EqualTo("FromAvarsha"));
            Assert.That(SceneTravel.IsTravelling, Is.False, "consuming an arrival should end the journey");
        }

        /// <summary>
        /// The bug this guards against: a journey that never completed leaves a spawn id
        /// in a static, and the next gameplay scene loaded for any reason would honour it
        /// — placing the player at a door they never walked through.
        /// </summary>
        [Test]
        public void Travel_IsNotConsumedByADifferentScene()
        {
            SceneTravel.Begin("Agniya", "FromAvarsha");

            Assert.That(SceneTravel.TryConsume("Avarsha", out var spawn), Is.False);
            Assert.That(spawn, Is.Null);
            Assert.That(SceneTravel.IsTravelling, Is.True, "a mismatch must not discard the pending arrival either");
        }

        [Test]
        public void Travel_WithNoDestinationIsIgnored()
        {
            SceneTravel.Begin(string.Empty, "Somewhere");

            Assert.That(SceneTravel.IsTravelling, Is.False);
        }

        [Test]
        public void Travel_IsAbandonedByClear()
        {
            SceneTravel.Begin("Agniya", "FromAvarsha");
            SceneTravel.Clear();

            Assert.That(SceneTravel.IsTravelling, Is.False);
            Assert.That(SceneTravel.PendingSpawnId, Is.Null);
        }

        // ------------------------------------------------- the per-scene save shape

        [Test]
        public void SceneStates_AreKeptApartByScene()
        {
            var data = new SaveData();

            var avarsha = data.EnsureStateFor("Avarsha");
            avarsha.PlayerPosition = new Vector3(10f, 0f, 20f);
            avarsha.CheckpointId = "Checkpoint_Avarsha_Gate";

            var agniya = data.EnsureStateFor("Agniya");
            agniya.PlayerPosition = new Vector3(-5f, 3f, 0f);
            agniya.CheckpointId = "Checkpoint_Agniya_After";

            Assert.That(data.SceneStates, Has.Count.EqualTo(2));
            Assert.That(data.StateFor("Avarsha").PlayerPosition, Is.EqualTo(new Vector3(10f, 0f, 20f)));
            Assert.That(data.StateFor("Agniya").CheckpointId, Is.EqualTo("Checkpoint_Agniya_After"));
        }

        [Test]
        public void EnsureStateFor_ReturnsTheSameEntryTwice()
        {
            var data = new SaveData();

            var first = data.EnsureStateFor("Avarsha");
            var second = data.EnsureStateFor("Avarsha");

            Assert.That(second, Is.SameAs(first));
            Assert.That(data.SceneStates, Has.Count.EqualTo(1));
        }

        [Test]
        public void StateFor_AnUnvisitedSceneIsNullRatherThanTheOrigin()
        {
            var data = new SaveData();
            data.EnsureStateFor("Avarsha").PlayerPosition = new Vector3(10f, 0f, 20f);

            Assert.That(data.StateFor("Agniya"), Is.Null,
                "an unvisited scene must be distinguishable from one recorded at the origin");
        }

        [Test]
        public void ResolveStateFor_FallsBackToTheTopLevelOnlyForTheSaveOwnScene()
        {
            var data = new SaveData
            {
                SceneName = "Avarsha",
                PlayerPosition = new Vector3(1f, 2f, 3f),
                CheckpointId = "Checkpoint_A"
            };

            var here = data.ResolveStateFor("Avarsha");
            Assert.That(here, Is.Not.Null);
            Assert.That(here.PlayerPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(here.CheckpointId, Is.EqualTo("Checkpoint_A"));

            Assert.That(data.ResolveStateFor("Agniya"), Is.Null,
                "the top-level position describes the save's own scene and no other");
        }

        // -------------------------------------------------------- the scene memory

        [Test]
        public void SceneMemory_RemembersAScenePlayerIsNoLongerIn()
        {
            SceneMemory.Record("Avarsha", new Vector3(4f, 0f, 9f), Quaternion.identity, "Checkpoint_A");
            SceneMemory.Record("Agniya", new Vector3(0f, 1f, 0f), Quaternion.identity, null);

            var snapshot = SceneMemory.Snapshot();

            Assert.That(snapshot, Has.Count.EqualTo(2));
            Assert.That(SceneMemory.Get("Avarsha").PlayerPosition, Is.EqualTo(new Vector3(4f, 0f, 9f)));
        }

        [Test]
        public void SceneMemory_RecordingTheSameSceneTwiceOverwritesRatherThanAppends()
        {
            SceneMemory.Record("Avarsha", new Vector3(1f, 0f, 1f), Quaternion.identity, "A");
            SceneMemory.Record("Avarsha", new Vector3(2f, 0f, 2f), Quaternion.identity, "B");

            Assert.That(SceneMemory.Count, Is.EqualTo(1));
            Assert.That(SceneMemory.Get("Avarsha").CheckpointId, Is.EqualTo("B"));
        }

        /// <summary>
        /// Loading a save puts the player in that save's world. A position this session
        /// remembered from a different playthrough is worse than no position at all, so
        /// adopting replaces rather than merges.
        /// </summary>
        [Test]
        public void SceneMemory_AdoptingASaveReplacesWhatTheSessionRemembered()
        {
            SceneMemory.Record("Avarsha", new Vector3(99f, 0f, 99f), Quaternion.identity, "STALE");

            var loaded = new SaveData();
            loaded.EnsureStateFor("Agniya").PlayerPosition = new Vector3(7f, 0f, 7f);

            SceneMemory.Adopt(loaded.SceneStates);

            Assert.That(SceneMemory.Get("Avarsha"), Is.Null, "the other playthrough's record must be gone");
            Assert.That(SceneMemory.Get("Agniya").PlayerPosition, Is.EqualTo(new Vector3(7f, 0f, 7f)));
        }

        [Test]
        public void SceneMemory_SnapshotIsACopy()
        {
            SceneMemory.Record("Avarsha", new Vector3(1f, 0f, 1f), Quaternion.identity, "A");

            var snapshot = SceneMemory.Snapshot();
            snapshot[0].CheckpointId = "EDITED";

            Assert.That(SceneMemory.Get("Avarsha").CheckpointId, Is.EqualTo("A"),
                "writing a save must not be able to alter what the session remembers");
        }

        [Test]
        public void SceneMemory_AdoptingNothingLeavesNothing()
        {
            SceneMemory.Record("Avarsha", Vector3.zero, Quaternion.identity, "A");

            SceneMemory.Adopt(null);

            Assert.That(SceneMemory.Count, Is.Zero);
        }
    }

    /// <summary>
    /// The version 1 → 2 migration, against the real version 1 file in
    /// <c>Assets/Tests/Fixtures</c> (TASK 041). See that folder's README for why the
    /// fixture is a genuine artefact rather than a <c>SaveData</c> built here.
    /// </summary>
    public class SaveMigrationTests
    {
        private static string FixturePath => Path.Combine(
            Application.dataPath, "Tests", "Fixtures", "save-v1-windows-player.sav");

        private static SaveData LoadFixture()
        {
            Assert.That(File.Exists(FixturePath), $"the version 1 fixture is missing from {FixturePath}");

            var result = SaveSerializer.TryFromJson(File.ReadAllText(FixturePath), out var data, out var detail);

            Assert.That(result, Is.EqualTo(SaveValidationResult.Valid),
                $"the real version 1 file should still load: {detail}");
            return data;
        }

        [Test]
        public void TheFixtureIsAVersionOneFileWithNoPerSceneRecords()
        {
            var text = File.ReadAllText(FixturePath);

            Assert.That(text, Does.Contain("\"Version\": 1"), "the fixture must be a version 1 file");
            Assert.That(text, Does.Not.Contain("SceneStates"),
                "a version 1 file predates SceneStates; if this fails the fixture has been edited");
        }

        [Test]
        public void AVersionOneSaveIsBroughtUpToTheCurrentVersion()
        {
            var data = LoadFixture();

            Assert.That(data.Version, Is.EqualTo(SaveData.CurrentVersion));
        }

        /// <summary>
        /// The point of the step. Version 1 knew where the player stood; leaving
        /// <c>SceneStates</c> empty would throw that away and drop them at the scene's
        /// default spawn, which is not where they saved.
        /// </summary>
        [Test]
        public void MigrationMovesTheOldPositionIntoARecordForTheSceneItWasMeasuredIn()
        {
            var data = LoadFixture();

            Assert.That(data.SceneName, Is.EqualTo("Avarsha"), "this fixture was saved in Avarsha");

            var avarsha = data.StateFor("Avarsha");
            Assert.That(avarsha, Is.Not.Null, "migration should have created a record for the save's own scene");
            Assert.That(avarsha.PlayerPosition, Is.EqualTo(data.PlayerPosition));
            Assert.That(avarsha.PlayerRotation, Is.EqualTo(data.PlayerRotation));
            Assert.That(avarsha.CheckpointId, Is.EqualTo(data.CheckpointId));
        }

        [Test]
        public void MigrationAttributesThePositionToNoOtherScene()
        {
            var data = LoadFixture();

            Assert.That(data.SceneStates, Has.Count.EqualTo(1));
            Assert.That(data.StateFor("Agniya"), Is.Null);
        }

        [Test]
        public void MigrationLeavesTheRestOfTheSaveAlone()
        {
            var data = LoadFixture();

            // Values read off the fixture itself. If the version 1 shape was misremembered
            // these come back as defaults rather than as what the old build wrote.
            Assert.That(data.PlayerPosition.z, Is.EqualTo(-32f).Within(0.001f));
            Assert.That(data.PlayerStats.MaxHealth, Is.EqualTo(100f).Within(0.001f));
            Assert.That(data.MemoryIntegrity, Is.EqualTo(1f).Within(0.001f));
            Assert.That(data.Difficulty, Is.EqualTo(1));
            Assert.That(data.TimestampUtc, Does.StartWith("2026-09-25"));
        }

        [Test]
        public void ASaveFromANewerBuildIsRefusedRatherThanGuessedAt()
        {
            var data = new SaveData { Version = SaveData.CurrentVersion + 1 };

            Assert.That(SaveMigration.TryMigrate(data, out var detail), Is.False);
            Assert.That(detail, Does.Contain("newer"));
        }

        [Test]
        public void ASaveAlreadyAtTheCurrentVersionIsLeftUntouched()
        {
            var data = new SaveData();
            data.EnsureStateFor("Avarsha").CheckpointId = "Checkpoint_A";

            Assert.That(SaveMigration.TryMigrate(data, out _), Is.True);
            Assert.That(data.SceneStates, Has.Count.EqualTo(1));
            Assert.That(data.StateFor("Avarsha").CheckpointId, Is.EqualTo("Checkpoint_A"));
        }

        /// <summary>
        /// A version 1 save written before the player reached gameplay has no scene to
        /// attribute its position to. Inventing one would place the player in a scene the
        /// save never named.
        /// </summary>
        [Test]
        public void AVersionOneSaveWithNoSceneGainsNoRecord()
        {
            var data = new SaveData { Version = 1, SceneName = string.Empty };

            Assert.That(SaveMigration.TryMigrate(data, out _), Is.True);
            Assert.That(data.Version, Is.EqualTo(2));
            Assert.That(data.SceneStates, Is.Empty);
        }

        // ---------------------------------------------- a version 1 save with content in it

        /// <summary>
        /// The genuine fixture above is evidence about version 1's *shape*, and it is the
        /// only thing that can be. What it cannot be is evidence about version 1's
        /// *contents*: it was written seconds into a smoke test, so every list in it is
        /// empty. Until now the migration had therefore only ever been asked to carry
        /// nothing across, and "it did not lose the quests" was an assumption about nine
        /// collections nobody had ever put anything into.
        ///
        /// This builds a version 1 save with something in every one of them. It is
        /// constructed rather than captured, and says so: the shape it uses is this
        /// build's, which is precisely why it is not a substitute for the file on disk.
        /// The two tests answer different questions and the project needs both.
        /// </summary>
        private static SaveData PopulatedVersionOneSave()
        {
            var data = new SaveData
            {
                Version = 1,
                TimestampUtc = "2026-09-25T20:07:46.3258790Z",
                SceneName = "Avarsha",
                CheckpointId = "Checkpoint_TempleSteps",
                PlayerPosition = new Vector3(4f, 1.08f, -32f),
                PlayerRotation = Quaternion.Euler(0f, 90f, 0f),
                MemoryIntegrity = 0.62f,
                Difficulty = 2
            };

            data.PlayerStats.Health = 61f;
            data.PlayerStats.DivineEnergy = 35f;

            data.Inventory.Add("EMBER_DRAUGHT");
            data.Inventory.Add("DIVINE_MARK");
            data.Abilities.Add("EMBER_STEP");
            data.DialogueFlags.Add("AMARA_GREETED");
            data.EndingFlags.Add("SPARED_THE_GUARDIAN");

            data.WorldFlags.Add(new FlagEntry { Key = "QUEENS_CHARGE_GIVEN", Value = true });
            data.WorldFlags.Add(new FlagEntry { Key = "RUIN_GUARDS_DEFEATED", Value = true });
            data.WorldCounters.Add(new CounterEntry { Key = "SKILL_POINTS", Value = 3 });
            data.NpcStates.Add(new FlagEntry { Key = "AMARA_MET", Value = true });
            data.BossStates.Add(new FlagEntry { Key = "BOSS_AGNIYA", Value = false });

            var quest = new QuestEntry { QuestId = "Q001", Status = 1 };
            quest.Objectives.Add(new ObjectiveEntry { ObjectiveId = "REACH_RUINS", Count = 1, Complete = true });
            quest.Objectives.Add(new ObjectiveEntry { ObjectiveId = "FIND_MEMORY", Count = 0, Complete = false });
            data.QuestStates.Add(quest);

            data.MemoryStates.Add(new MemoryEntry { MemoryId = "MEM_NAME_BENEATH_THE_STONE", State = 2 });
            data.Participants.Add(new ParticipantEntry { Key = "DIVINE_ABILITIES", Json = "{}" });

            return data;
        }

        [Test]
        public void MigratingAPopulatedVersionOneSaveCarriesEveryListAcrossIntact()
        {
            var data = PopulatedVersionOneSave();

            Assert.That(SaveMigration.TryMigrate(data, out var detail), Is.True, detail);
            Assert.That(data.Version, Is.EqualTo(SaveData.CurrentVersion));

            Assert.That(data.Inventory, Is.EquivalentTo(new[] { "EMBER_DRAUGHT", "DIVINE_MARK" }));
            Assert.That(data.Abilities, Is.EquivalentTo(new[] { "EMBER_STEP" }));
            Assert.That(data.DialogueFlags, Is.EquivalentTo(new[] { "AMARA_GREETED" }));
            Assert.That(data.EndingFlags, Is.EquivalentTo(new[] { "SPARED_THE_GUARDIAN" }));

            Assert.That(data.WorldFlags, Has.Count.EqualTo(2));
            Assert.That(data.WorldCounters, Has.Count.EqualTo(1));
            Assert.That(data.WorldCounters[0].Value, Is.EqualTo(3));
            Assert.That(data.NpcStates, Has.Count.EqualTo(1));
            Assert.That(data.BossStates, Has.Count.EqualTo(1));
            Assert.That(data.MemoryStates, Has.Count.EqualTo(1));
            Assert.That(data.MemoryStates[0].State, Is.EqualTo(2));
            Assert.That(data.Participants, Has.Count.EqualTo(1));
            Assert.That(data.Participants[0].Key, Is.EqualTo("DIVINE_ABILITIES"));

            Assert.That(data.QuestStates, Has.Count.EqualTo(1));
            Assert.That(data.QuestStates[0].QuestId, Is.EqualTo("Q001"));
            Assert.That(data.QuestStates[0].Objectives, Has.Count.EqualTo(2),
                "a quest's objectives are a list inside a list, which is where a migration loses things");
            Assert.That(data.QuestStates[0].Objectives[0].Complete, Is.True);
            Assert.That(data.QuestStates[0].Objectives[1].Complete, Is.False);

            Assert.That(data.MemoryIntegrity, Is.EqualTo(0.62f).Within(0.0001f));
            Assert.That(data.Difficulty, Is.EqualTo(2));
            Assert.That(data.PlayerStats.Health, Is.EqualTo(61f).Within(0.0001f));
        }

        /// <summary>
        /// The same save, but through the reader rather than straight into
        /// <see cref="SaveMigration"/> — envelope, checksum, payload and all. A version 1
        /// file has no <c>SceneStates</c> key at all, so the payload is written without
        /// one: a migration that works only when the field happens to be present as an
        /// empty array is not a migration of anything a version 1 build ever wrote.
        /// </summary>
        [Test]
        public void APopulatedVersionOneFileLoadsAndMigratesThroughTheOrdinaryReader()
        {
            var payload = JsonUtility.ToJson(PopulatedVersionOneSave(), true);

            payload = System.Text.RegularExpressions.Regex.Replace(
                payload, "\\s*\"SceneStates\": \\[[^\\]]*\\],", string.Empty);

            Assert.That(payload, Does.Not.Contain("SceneStates"),
                "the point of this test is a payload that predates the field");

            var file = JsonUtility.ToJson(new TestEnvelope
            {
                Version = 1,
                Checksum = SaveSerializer.Checksum(payload),
                Payload = payload
            });

            var result = SaveSerializer.TryFromJson(file, out var loaded, out var detail);

            Assert.That(result, Is.EqualTo(SaveValidationResult.Valid), detail);
            Assert.That(loaded.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(loaded.QuestStates, Has.Count.EqualTo(1));
            Assert.That(loaded.QuestStates[0].Objectives, Has.Count.EqualTo(2));
            Assert.That(loaded.Inventory, Has.Count.EqualTo(2));

            var avarsha = loaded.StateFor("Avarsha");
            Assert.That(avarsha, Is.Not.Null, "the position should have become a per-scene record");
            Assert.That(avarsha.CheckpointId, Is.EqualTo("Checkpoint_TempleSteps"));
            Assert.That(avarsha.PlayerPosition.x, Is.EqualTo(4f).Within(0.0001f));
        }

        /// <summary>
        /// A stand-in for <c>SaveSerializer</c>'s private envelope, so this test can write
        /// a file the way an older build would have. Its field names have to match; if
        /// they ever stop matching, these two tests fail together and loudly, which is the
        /// correct outcome for a change to the file format's outermost layer.
        /// </summary>
        [System.Serializable]
        private class TestEnvelope
        {
            public int Version;
            public string Checksum;
            public string Payload;
        }
    }
}

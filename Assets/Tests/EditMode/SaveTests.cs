using System.IO;
using Game.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// The save file format and the rules around replacing one (SPEC.md sections 31,
    /// 32 and 53: save, load, backup, corrupted data, version migration).
    ///
    /// These need no scene and no frame — a save file is text and a directory — so they
    /// belong in EditMode. What happens to the live game when a save is applied is in
    /// <c>SavePlayModeTests</c>.
    /// </summary>
    public class SaveTests
    {
        private string root;

        [SetUp]
        public void SetUp()
        {
            // A folder of this test's own. Nothing here ever touches the player's real
            // save directory.
            root = Path.Combine(Path.GetTempPath(), "GodGameSaveTests", Path.GetRandomFileName());
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private static SaveData NewSave(float health = 80f, string flag = "TEST_FLAG")
        {
            var data = new SaveData();
            data.Stamp();
            data.SceneName = "TestScene";
            data.PlayerPosition = new Vector3(1f, 2f, 3f);
            data.PlayerStats.Health = health;
            data.PlayerStats.MaxHealth = 100f;
            data.PlayerStats.Stamina = 40f;
            data.PlayerStats.MaxStamina = 100f;
            data.WorldFlags.Add(new FlagEntry(flag, true));
            data.WorldCounters.Add(new CounterEntry("DEATHS", 3));
            return data;
        }

        // -------------------------------------------------------------- serialization

        [Test]
        public void Serializer_RoundTripsASave()
        {
            var original = NewSave();

            var result = SaveSerializer.TryFromJson(SaveSerializer.ToJson(original), out var restored, out var detail);

            Assert.AreEqual(SaveValidationResult.Valid, result, detail);
            Assert.AreEqual(original.SceneName, restored.SceneName);
            Assert.AreEqual(original.PlayerPosition, restored.PlayerPosition);
            Assert.AreEqual(80f, restored.PlayerStats.Health, 0.001f);
            Assert.AreEqual(1, restored.WorldFlags.Count);
            Assert.AreEqual("TEST_FLAG", restored.WorldFlags[0].Key);
            Assert.AreEqual(3, restored.WorldCounters[0].Value);
        }

        [Test]
        public void Serializer_ATamperedPayload_FailsItsChecksum()
        {
            var json = SaveSerializer.ToJson(NewSave(flag: "ORIGINAL_FLAG"));
            var tampered = json.Replace("ORIGINAL_FLAG", "TAMPERED_FLAG");

            Assert.AreNotEqual(json, tampered, "The test did not actually change the payload.");

            var result = SaveSerializer.TryFromJson(tampered, out var data, out _);

            Assert.AreEqual(SaveValidationResult.ChecksumMismatch, result);
            Assert.IsNull(data, "A file that failed validation still handed back data.");
        }

        [Test]
        public void Serializer_EmptyOrGarbage_IsRejectedRatherThanThrowing()
        {
            Assert.AreEqual(SaveValidationResult.Empty, SaveSerializer.TryFromJson("", out _, out _));
            Assert.AreEqual(SaveValidationResult.Empty, SaveSerializer.TryFromJson("   ", out _, out _));

            var garbage = SaveSerializer.TryFromJson("this is not json at all", out _, out _);
            Assert.AreNotEqual(SaveValidationResult.Valid, garbage);

            var truncated = SaveSerializer.ToJson(NewSave());
            truncated = truncated.Substring(0, truncated.Length / 2);
            Assert.AreNotEqual(SaveValidationResult.Valid, SaveSerializer.TryFromJson(truncated, out _, out _));
        }

        [Test]
        public void Serializer_ASaveFromANewerBuild_IsRefusedNotGuessedAt()
        {
            var future = NewSave();
            future.Version = SaveData.CurrentVersion + 5;

            var result = SaveSerializer.TryFromJson(SaveSerializer.ToJson(future), out var data, out var detail);

            Assert.AreEqual(SaveValidationResult.UnknownVersion, result);
            Assert.IsNull(data);
            StringAssert.Contains((SaveData.CurrentVersion + 5).ToString(), detail,
                "The refusal should say which version it could not read.");
        }

        [Test]
        public void Serializer_ImplausibleStats_AreCaughtEvenWithAValidChecksum()
        {
            // Each of these is a well-formed file with a correct checksum. Only the
            // plausibility check stands between them and a broken game.
            var overhealed = NewSave();
            overhealed.PlayerStats.Health = 500f;
            Assert.AreEqual(SaveValidationResult.Implausible,
                SaveSerializer.TryFromJson(SaveSerializer.ToJson(overhealed), out _, out _));

            var noMaxHealth = NewSave();
            noMaxHealth.PlayerStats.MaxHealth = 0f;
            Assert.AreEqual(SaveValidationResult.Implausible,
                SaveSerializer.TryFromJson(SaveSerializer.ToJson(noMaxHealth), out _, out _));

            var nowhere = NewSave();
            nowhere.PlayerPosition = new Vector3(float.NaN, 0f, 0f);
            Assert.AreEqual(SaveValidationResult.Implausible,
                SaveSerializer.TryFromJson(SaveSerializer.ToJson(nowhere), out _, out _));

            var impossibleIntegrity = NewSave();
            impossibleIntegrity.MemoryIntegrity = 4f;
            Assert.AreEqual(SaveValidationResult.Implausible,
                SaveSerializer.TryFromJson(SaveSerializer.ToJson(impossibleIntegrity), out _, out _));
        }

        [Test]
        public void Checksum_ChangesWithThePayloadAndIsStable()
        {
            Assert.AreEqual(SaveSerializer.Checksum("hello"), SaveSerializer.Checksum("hello"));
            Assert.AreNotEqual(SaveSerializer.Checksum("hello"), SaveSerializer.Checksum("hellp"));
            Assert.AreEqual(16, SaveSerializer.Checksum("anything").Length);
        }

        // ------------------------------------------------------------------ migration

        [Test]
        public void Migration_ASaveAtTheCurrentVersion_NeedsNoWork()
        {
            var data = NewSave();

            Assert.IsTrue(SaveMigration.TryMigrate(data, out var detail), detail);
            Assert.AreEqual(SaveData.CurrentVersion, data.Version);
        }

        [Test]
        public void Migration_AVersionWithNoStep_IsRefusedRatherThanGuessedAt()
        {
            var ancient = NewSave();
            ancient.Version = 0;

            Assert.IsFalse(SaveMigration.TryMigrate(ancient, out var detail));
            Assert.IsNotNull(detail, "A refusal must say why.");
        }

        // -------------------------------------------------------------------- storage

        [Test]
        public void Storage_WriteThenRead_ReturnsTheSameSave()
        {
            Assert.IsTrue(SaveStorage.Write(root, SaveSlot.Manual, NewSave(health: 61f), out var detail), detail);
            Assert.IsTrue(SaveStorage.Exists(root, SaveSlot.Manual));

            var outcome = SaveStorage.Read(root, SaveSlot.Manual);

            Assert.IsTrue(outcome.Loaded, outcome.Detail);
            Assert.AreEqual(SaveSource.Primary, outcome.Source);
            Assert.AreEqual(61f, outcome.Data.PlayerStats.Health, 0.001f);
        }

        [Test]
        public void Storage_LeavesNoTemporaryFileBehind()
        {
            SaveStorage.Write(root, SaveSlot.Manual, NewSave(), out _);

            Assert.IsFalse(File.Exists(SaveStorage.TemporaryPath(root, SaveSlot.Manual)),
                "The temporary file survived a successful write.");
        }

        [Test]
        public void Storage_ASecondWrite_KeepsTheFirstAsTheBackup()
        {
            SaveStorage.Write(root, SaveSlot.Manual, NewSave(health: 10f), out _);
            SaveStorage.Write(root, SaveSlot.Manual, NewSave(health: 90f), out _);

            var primary = SaveStorage.Read(root, SaveSlot.Manual);
            Assert.AreEqual(90f, primary.Data.PlayerStats.Health, 0.001f);

            Assert.IsTrue(File.Exists(SaveStorage.BackupPath(root, SaveSlot.Manual)),
                "SPEC.md section 31 step 4: a backup must be maintained.");
        }

        [Test]
        public void Storage_ACorruptPrimary_IsReplacedByTheBackupAndKeptOnDisk()
        {
            SaveStorage.Write(root, SaveSlot.Manual, NewSave(health: 10f), out _);
            SaveStorage.Write(root, SaveSlot.Manual, NewSave(health: 90f), out _);

            var primaryPath = SaveStorage.PrimaryPath(root, SaveSlot.Manual);
            File.WriteAllText(primaryPath, "{ this file has been shredded");

            LogAssert.ignoreFailingMessages = true;
            var outcome = SaveStorage.Read(root, SaveSlot.Manual);
            LogAssert.ignoreFailingMessages = false;

            Assert.IsTrue(outcome.Loaded, "The backup did not rescue the load.");
            Assert.IsTrue(outcome.RecoveredFromBackup);
            Assert.AreEqual(10f, outcome.Data.PlayerStats.Health, 0.001f,
                "The backup should hold the save before last.");

            // SPEC.md section 32: never silently delete user saves.
            Assert.IsFalse(File.Exists(primaryPath), "The corrupt file was left where the next write would clobber it.");
            Assert.AreEqual(1, Directory.GetFiles(root, "*.corrupt-*").Length,
                "The corrupt save was deleted rather than quarantined.");
        }

        [Test]
        public void Storage_NoSaveAtAll_FailsCleanlyRatherThanThrowing()
        {
            var outcome = SaveStorage.Read(root, SaveSlot.Chapter);

            Assert.IsFalse(outcome.Loaded);
            Assert.AreEqual(SaveSource.None, outcome.Source);
            Assert.IsNull(outcome.Data);
            Assert.IsNotNull(outcome.Detail);
        }

        [Test]
        public void Storage_NothingToSave_IsRefused()
        {
            Assert.IsFalse(SaveStorage.Write(root, SaveSlot.Manual, null, out var detail));
            Assert.IsNotNull(detail);
            Assert.IsFalse(SaveStorage.Exists(root, SaveSlot.Manual));
        }

        [Test]
        public void Storage_EachSlot_IsItsOwnFile()
        {
            SaveStorage.Write(root, SaveSlot.Manual, NewSave(health: 25f), out _);
            SaveStorage.Write(root, SaveSlot.Checkpoint, NewSave(health: 75f), out _);

            Assert.AreEqual(25f, SaveStorage.Read(root, SaveSlot.Manual).Data.PlayerStats.Health, 0.001f);
            Assert.AreEqual(75f, SaveStorage.Read(root, SaveSlot.Checkpoint).Data.PlayerStats.Health, 0.001f);
            Assert.IsFalse(SaveStorage.Exists(root, SaveSlot.Chapter));
        }
    }
}

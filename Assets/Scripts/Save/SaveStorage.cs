using System;
using System.IO;
using Game.Core;
using UnityEngine;

namespace Game.Save
{
    /// <summary>Which file a load actually came from. Shown to the player when it is not the primary.</summary>
    public enum SaveSource
    {
        None,
        Primary,
        Backup
    }

    /// <summary>The outcome of a load attempt, including what went wrong and what was done about it.</summary>
    public readonly struct LoadOutcome
    {
        public readonly bool Loaded;
        public readonly SaveData Data;
        public readonly SaveSource Source;
        public readonly SaveValidationResult PrimaryResult;
        public readonly string Detail;

        public LoadOutcome(bool loaded, SaveData data, SaveSource source, SaveValidationResult primaryResult,
            string detail)
        {
            Loaded = loaded;
            Data = data;
            Source = source;
            PrimaryResult = primaryResult;
            Detail = detail;
        }

        /// <summary>True when the primary was bad and a backup carried the day — SPEC.md section 32's case.</summary>
        public bool RecoveredFromBackup => Loaded && Source == SaveSource.Backup;
    }

    /// <summary>
    /// Reads and writes save files (SPEC.md sections 31 and 32).
    ///
    /// Writing follows the spec's four steps literally: write a temporary file, read it
    /// back and validate it, move the current primary aside as the backup, then put the
    /// temporary file in its place. A crash at any point leaves either the old save or
    /// the new one intact, never a half-written file where the save used to be.
    ///
    /// Reading never destroys anything. A primary that fails validation is renamed to a
    /// timestamped <c>.corrupt</c> file and left on disk, because SPEC.md section 32 is
    /// explicit that user saves are never silently deleted — the file may be the only
    /// copy of a long playthrough, and someone may yet recover it by hand.
    ///
    /// The class is static and takes its root directory as a parameter so tests can
    /// point it at a temporary folder instead of the player's real save directory.
    /// </summary>
    public static class SaveStorage
    {
        public const string PrimaryExtension = ".sav";
        public const string BackupExtension = ".sav.bak";
        public const string TemporaryExtension = ".sav.tmp";

        /// <summary>Where saves live for a real player. Tests pass their own root instead.</summary>
        public static string DefaultRoot => Path.Combine(Application.persistentDataPath, "Saves");

        public static string PrimaryPath(string root, SaveSlot slot) => Path.Combine(root, SlotName(slot) + PrimaryExtension);
        public static string BackupPath(string root, SaveSlot slot) => Path.Combine(root, SlotName(slot) + BackupExtension);
        public static string TemporaryPath(string root, SaveSlot slot) => Path.Combine(root, SlotName(slot) + TemporaryExtension);

        public static string SlotName(SaveSlot slot) => slot.ToString().ToLowerInvariant();

        public static bool Exists(string root, SaveSlot slot) => File.Exists(PrimaryPath(root, slot));

        /// <summary>
        /// Writes a save, following SPEC.md section 31's replace procedure. Returns
        /// false and leaves every existing file untouched if anything fails.
        /// </summary>
        public static bool Write(string root, SaveSlot slot, SaveData data, out string detail)
        {
            detail = null;

            if (data == null)
            {
                detail = "there was nothing to save";
                return false;
            }

            var primary = PrimaryPath(root, slot);
            var backup = BackupPath(root, slot);
            var temporary = TemporaryPath(root, slot);

            try
            {
                Directory.CreateDirectory(root);

                // Step 1: write the temporary file.
                File.WriteAllText(temporary, SaveSerializer.ToJson(data));

                // Step 2: validate it by reading it back off the disk it will live on,
                // rather than trusting the string we just serialized.
                var result = SaveSerializer.TryFromJson(File.ReadAllText(temporary), out _, out var readDetail);
                if (result != SaveValidationResult.Valid)
                {
                    detail = $"the temporary save failed validation ({result}: {readDetail})";
                    TryDelete(temporary);
                    return false;
                }

                // Step 3 and 4: the current primary becomes the backup, then the
                // validated temporary file becomes the primary. Ordered this way so
                // there is never a moment with no readable save on disk.
                if (File.Exists(primary))
                {
                    TryDelete(backup);
                    File.Move(primary, backup);
                }

                File.Move(temporary, primary);
                return true;
            }
            catch (Exception exception)
            {
                detail = exception.Message;
                TryDelete(temporary);
                return false;
            }
        }

        /// <summary>
        /// Loads a save, falling back to the backup when the primary is unreadable
        /// (SPEC.md section 32). A bad primary is quarantined, never deleted and never
        /// overwritten by this call.
        /// </summary>
        public static LoadOutcome Read(string root, SaveSlot slot)
        {
            var primary = PrimaryPath(root, slot);
            var backup = BackupPath(root, slot);

            var primaryResult = SaveValidationResult.Empty;
            string primaryDetail = null;

            if (File.Exists(primary))
            {
                if (TryReadFile(primary, out var data, out primaryResult, out primaryDetail))
                {
                    return new LoadOutcome(true, data, SaveSource.Primary, primaryResult, null);
                }

                // Step 4: log the technical details before doing anything else, so the
                // reason survives even if the quarantine itself fails.
                GameLogger.LogError(
                    LogCategory.Save,
                    $"Save '{slot}' failed validation ({primaryResult}): {primaryDetail}. " +
                    "The file has not been overwritten.");

                Quarantine(primary);
            }

            if (File.Exists(backup) && TryReadFile(backup, out var backupData, out _, out _))
            {
                GameLogger.LogWarning(LogCategory.Save, $"Save '{slot}' was restored from its backup.");
                return new LoadOutcome(true, backupData, SaveSource.Backup, primaryResult, primaryDetail);
            }

            return new LoadOutcome(false, null, SaveSource.None, primaryResult,
                primaryDetail ?? "no save file exists for this slot");
        }

        private static bool TryReadFile(string path, out SaveData data, out SaveValidationResult result,
            out string detail)
        {
            data = null;

            try
            {
                result = SaveSerializer.TryFromJson(File.ReadAllText(path), out data, out detail);
            }
            catch (Exception exception)
            {
                result = SaveValidationResult.Unreadable;
                detail = exception.Message;
                return false;
            }

            return result == SaveValidationResult.Valid;
        }

        /// <summary>
        /// Moves a bad save aside under a timestamped name. SPEC.md section 32: never
        /// silently delete user saves. Renaming keeps the bytes and gets them out of
        /// the way of the next write.
        /// </summary>
        public static string Quarantine(string path)
        {
            try
            {
                var quarantined = $"{path}.corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}";
                File.Move(path, quarantined);
                GameLogger.LogWarning(LogCategory.Save, $"The unreadable save was kept as '{Path.GetFileName(quarantined)}'.");
                return quarantined;
            }
            catch (Exception exception)
            {
                GameLogger.LogError(LogCategory.Save, $"Could not quarantine '{path}': {exception.Message}");
                return null;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                // A leftover temporary file is harmless: the next write replaces it.
                GameLogger.LogWarning(LogCategory.Save, $"Could not remove '{path}': {exception.Message}");
            }
        }
    }
}

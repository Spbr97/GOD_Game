using System;
using System.IO;
using Game.Core;
using UnityEngine;

namespace Game.Save
{
    /// <summary>
    /// The on-disk half of a journey between scenes (TASK 041's durability gap).
    ///
    /// <see cref="SceneTravel"/> holds the destination and <c>SaveManager</c> holds the
    /// captured progression, and until now both lived only in statics. That is enough
    /// for a journey that completes, and nothing at all for one that does not: a domain
    /// reload, a crash in the destination's first frame, or an Editor stop between the
    /// two scenes takes the snapshot with it, and the run is back at the last real save.
    /// Everything since it is gone — including, after a long temple, an hour of play the
    /// player had no opportunity to write down, because saving is deliberately blocked
    /// for the whole crossing.
    ///
    /// So the crossing writes a journal beside the save files and deletes it on arrival.
    /// It is not a save slot and never appears as one: it names a destination rather
    /// than a place the player is, it is written without being asked for, and it is
    /// consumed exactly once. Keeping it out of <see cref="SaveSlot"/> is deliberate —
    /// every menu that lists slots would otherwise have to learn to hide this one.
    ///
    /// What this does *not* do is offer to resume the journey from the Main Menu. That
    /// is a design decision about what the player is shown after a crash, and it is
    /// recorded in KNOWN_ISSUES.md as open. The durability it needs is here either way.
    /// </summary>
    public static class TravelJournal
    {
        public const string FileName = "travel.journal";

        [Serializable]
        private class Entry
        {
            public string TargetScene;
            public string SpawnId;
            public string Snapshot;
        }

        public static string PathFor(string root) => Path.Combine(root, FileName);

        public static bool Exists(string root) => File.Exists(PathFor(root));

        /// <summary>
        /// Records a crossing. Failure is logged and swallowed: a journal that cannot be
        /// written must not stop the player walking through the door, because the
        /// in-memory hand-off still works and refusing travel would be a worse outcome
        /// than losing a crash-recovery aid.
        /// </summary>
        public static bool Write(string root, string targetScene, string spawnId, SaveData snapshot)
        {
            if (string.IsNullOrEmpty(targetScene) || snapshot == null)
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(root);

                var entry = new Entry
                {
                    TargetScene = targetScene,
                    SpawnId = spawnId,
                    Snapshot = SaveSerializer.ToJson(snapshot)
                };

                File.WriteAllText(PathFor(root), JsonUtility.ToJson(entry));
                return true;
            }
            catch (Exception exception)
            {
                GameLogger.LogWarning(LogCategory.Save,
                    $"Could not write the travel journal to '{PathFor(root)}': {exception.Message}. "
                    + "The journey still works; it is only unrecoverable if the game stops mid-crossing.");
                return false;
            }
        }

        /// <summary>
        /// Reads a recorded crossing if one was headed for <paramref name="sceneName"/>.
        ///
        /// Keyed on the destination for the same reason
        /// <see cref="SceneTravel.TryConsume"/> is: a journal left by an abandoned
        /// journey must not deposit its progression into whatever scene happens to open
        /// next. A mismatch leaves the file alone rather than deleting it, so a journey
        /// interrupted on the way to Agniya survives a detour through the Main Menu.
        /// </summary>
        public static bool TryRead(string root, string sceneName, out string spawnId, out SaveData snapshot)
        {
            spawnId = null;
            snapshot = null;

            var path = PathFor(root);
            if (string.IsNullOrEmpty(sceneName) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                var entry = JsonUtility.FromJson<Entry>(File.ReadAllText(path));

                if (entry == null || !string.Equals(entry.TargetScene, sceneName, StringComparison.Ordinal))
                {
                    return false;
                }

                if (SaveSerializer.TryFromJson(entry.Snapshot, out var data, out var detail) != SaveValidationResult.Valid)
                {
                    GameLogger.LogWarning(LogCategory.Save,
                        $"The travel journal for '{sceneName}' is unreadable ({detail}); discarding it.");
                    Clear(root);
                    return false;
                }

                spawnId = entry.SpawnId;
                snapshot = data;
                return true;
            }
            catch (Exception exception)
            {
                GameLogger.LogWarning(LogCategory.Save,
                    $"Could not read the travel journal at '{path}': {exception.Message}. Discarding it.");
                Clear(root);
                return false;
            }
        }

        /// <summary>Deletes the journal. Called on arrival, and whenever a journey is abandoned.</summary>
        public static void Clear(string root)
        {
            try
            {
                var path = PathFor(root);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                GameLogger.LogWarning(LogCategory.Save,
                    $"Could not delete the travel journal: {exception.Message}. A stale one is harmless: it is "
                    + "keyed on its destination and consumed once.");
            }
        }
    }
}

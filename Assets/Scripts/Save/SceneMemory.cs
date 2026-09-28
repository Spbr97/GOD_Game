using System.Collections.Generic;
using UnityEngine;

namespace Game.Save
{
    /// <summary>
    /// This session's per-scene positions and checkpoints (TASK 041).
    ///
    /// <c>SaveManager.Capture</c> builds a <see cref="SaveData"/> from the live scene, and
    /// a live scene is one scene. Without somewhere to keep them, every record for a scene
    /// that is not currently loaded would be dropped on the next save — walk into the
    /// temple, save, and the game has forgotten where you were standing in Avarsha.
    ///
    /// Static and not a <c>MonoBehaviour</c> for the usual reason: it has to outlive the
    /// scene, and a persistent GameObject would have to exist in every scene that might be
    /// entered first. It is session state, never authoritative — a save file is what
    /// survives a quit, and <see cref="Adopt"/> is how a loaded file overwrites whatever
    /// this session happened to remember.
    /// </summary>
    public static class SceneMemory
    {
        private static readonly Dictionary<string, SceneStateEntry> entries = new();

        /// <summary>How many scenes are remembered. For tests and for the log.</summary>
        public static int Count => entries.Count;

        /// <summary>Remembers where the player was, and whose checkpoint was theirs, in one scene.</summary>
        public static void Record(string sceneName, Vector3 position, Quaternion rotation, string checkpointId)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return;
            }

            if (!entries.TryGetValue(sceneName, out var entry) || entry == null)
            {
                entry = new SceneStateEntry { SceneName = sceneName };
                entries[sceneName] = entry;
            }

            entry.PlayerPosition = position;
            entry.PlayerRotation = rotation;
            entry.CheckpointId = checkpointId;
        }

        public static SceneStateEntry Get(string sceneName)
        {
            return !string.IsNullOrEmpty(sceneName) && entries.TryGetValue(sceneName, out var entry) ? entry : null;
        }

        /// <summary>
        /// Replaces everything remembered with what a save file holds. A replacement rather
        /// than a merge: after loading a save the player is in that save's world, and a
        /// position this session remembered from a different playthrough is worse than no
        /// position at all.
        /// </summary>
        public static void Adopt(IReadOnlyList<SceneStateEntry> loaded)
        {
            entries.Clear();

            if (loaded == null)
            {
                return;
            }

            for (var i = 0; i < loaded.Count; i++)
            {
                var entry = loaded[i];
                if (entry == null || string.IsNullOrEmpty(entry.SceneName))
                {
                    continue;
                }

                entries[entry.SceneName] = new SceneStateEntry
                {
                    SceneName = entry.SceneName,
                    PlayerPosition = entry.PlayerPosition,
                    PlayerRotation = entry.PlayerRotation,
                    CheckpointId = entry.CheckpointId
                };
            }
        }

        /// <summary>A copy of every remembered scene, for writing into a save.</summary>
        public static List<SceneStateEntry> Snapshot()
        {
            var copy = new List<SceneStateEntry>(entries.Count);

            foreach (var pair in entries)
            {
                copy.Add(new SceneStateEntry
                {
                    SceneName = pair.Value.SceneName,
                    PlayerPosition = pair.Value.PlayerPosition,
                    PlayerRotation = pair.Value.PlayerRotation,
                    CheckpointId = pair.Value.CheckpointId
                });
            }

            return copy;
        }

        /// <summary>Forgets everything. A new game, and returning to the Main Menu.</summary>
        public static void Clear()
        {
            entries.Clear();
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Save
{
    /// <summary>Which file a save belongs to (SPEC.md section 31's save types).</summary>
    public enum SaveSlot
    {
        /// <summary>Written automatically when a checkpoint is activated.</summary>
        Checkpoint,

        /// <summary>Written when the player asks for it.</summary>
        Manual,

        /// <summary>Written at chapter boundaries, so a player can go back an act.</summary>
        Chapter
    }

    [Serializable]
    public class FlagEntry
    {
        public string Key;
        public bool Value;

        public FlagEntry() { }

        public FlagEntry(string key, bool value)
        {
            Key = key;
            Value = value;
        }
    }

    [Serializable]
    public class CounterEntry
    {
        public string Key;
        public int Value;

        public CounterEntry() { }

        public CounterEntry(string key, int value)
        {
            Key = key;
            Value = value;
        }
    }

    [Serializable]
    public class ObjectiveEntry
    {
        public string ObjectiveId;
        public int Count;
        public bool Complete;
    }

    [Serializable]
    public class QuestEntry
    {
        public string QuestId;

        /// <summary>A <c>QuestStatus</c>, stored as an int so a renamed enum member does not break old saves.</summary>
        public int Status;

        public List<ObjectiveEntry> Objectives = new();
    }

    [Serializable]
    public class MemoryEntry
    {
        public string MemoryId;

        /// <summary>A <c>MemoryState</c>, stored as an int for the same reason as <see cref="QuestEntry.Status"/>.</summary>
        public int State;
    }

    /// <summary>
    /// Where the player stood, and which checkpoint was theirs, in one particular scene
    /// (TASK 041). Added in save version 2.
    ///
    /// A position is only meaningful in the scene it was measured in, and a checkpoint
    /// id only resolves in the scene that contains that checkpoint. Version 1 kept one
    /// of each at the top level, which was correct while there was one gameplay scene
    /// and becomes a defect the moment there are two: return to a scene you left and the
    /// save hands you coordinates from somewhere else, or a checkpoint id that resolves
    /// to nothing, so death sends you to the scene's default spawn instead of where you
    /// were. Keying both by scene is the fix.
    /// </summary>
    [Serializable]
    public class SceneStateEntry
    {
        public string SceneName;
        public Vector3 PlayerPosition;
        public Quaternion PlayerRotation = Quaternion.identity;

        /// <summary>The checkpoint active in this scene, by id. Empty means none activated here.</summary>
        public string CheckpointId;
    }

    [Serializable]
    public class PlayerStatsData
    {
        public float Health;
        public float MaxHealth = 100f;
        public float Stamina;
        public float MaxStamina = 100f;
        public float DivineEnergy;
        public float MaxDivineEnergy = 100f;
    }

    /// <summary>
    /// One entry contributed by an <see cref="ISaveParticipant"/>: an opaque JSON blob
    /// under a key the participant owns. This is how a system saves something without
    /// <see cref="SaveData"/> growing a field for it.
    /// </summary>
    [Serializable]
    public class ParticipantEntry
    {
        public string Key;
        public string Json;
    }

    /// <summary>
    /// Everything a save file holds (SPEC.md section 31). Field names follow the spec's
    /// list. <see cref="Inventory"/> (TASK 016's <c>InventoryManager</c>) and
    /// <see cref="Abilities"/> (TASK 016's <c>SkillTreeManager</c>, the unlocked skill
    /// ids) are filled now; <see cref="NpcStates"/>, <see cref="BossStates"/>,
    /// <see cref="DialogueFlags"/> and <see cref="EndingFlags"/> remain reserved
    /// because the system behind each does not exist yet, or — for boss state —
    /// already round-trips a different way (a boss's death is an ordinary
    /// <c>WorldObjectState</c> flag via <c>SaveIdentity</c>, the same as any enemy's).
    ///
    /// The still-empty fields are here on purpose. The shape of the file is what
    /// version migration has to reason about, and adding a field later is a migration
    /// where filling an existing empty one is not.
    ///
    /// This is a plain serializable class, not a ScriptableObject and not a struct,
    /// because <c>JsonUtility</c> round-trips exactly this shape.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>
        /// Bump this whenever the meaning of a field changes, and add a step to
        /// <see cref="SaveMigration"/>. Adding a new field with a sensible default does
        /// not need a bump: <c>JsonUtility</c> leaves missing fields at their defaults.
        ///
        /// Version 2 (TASK 041) added <see cref="SceneStates"/>. That did need a bump
        /// even though the field defaults to an empty list, because an empty list is
        /// wrong rather than merely absent: a version 1 save does know where the player
        /// stood and which checkpoint was theirs, and leaving the list empty would throw
        /// that away and drop the player at the scene's default spawn.
        /// </summary>
        public const int CurrentVersion = 2;

        public int Version = CurrentVersion;

        /// <summary>Round-trip ("o") UTC. A string rather than a tick count so a human can read the file.</summary>
        public string TimestampUtc;

        /// <summary>The scene to resume in — the one the player was standing in when this was written.</summary>
        public string SceneName;

        /// <summary>
        /// The checkpoint the player respawns at in <see cref="SceneName"/>, by id.
        /// Since version 2 this mirrors that scene's <see cref="SceneStateEntry"/>; it
        /// stays at the top level because <c>SaveBrowser</c> and the Continue screen read
        /// a save's headline without caring about the other scenes in it.
        /// </summary>
        public string CheckpointId;

        /// <summary>Where the player stood in <see cref="SceneName"/>. Mirrors that scene's <see cref="SceneStateEntry"/>.</summary>
        public Vector3 PlayerPosition;

        public Quaternion PlayerRotation = Quaternion.identity;
        public PlayerStatsData PlayerStats = new();

        /// <summary>
        /// Per-scene position and checkpoint, one entry per gameplay scene the player has
        /// stood in (TASK 041, save version 2). Leaving a scene records the entry; coming
        /// back reads it. The scene the save was written in is always present.
        /// </summary>
        public List<SceneStateEntry> SceneStates = new();

        /// <summary>Held items as "itemId:count" pairs (TASK 016's <c>InventoryManager</c>).</summary>
        public List<string> Inventory = new();

        /// <summary>Unlocked skill ids (TASK 016's <c>SkillTreeManager</c>; skill points are a <see cref="WorldCounters"/> entry instead).</summary>
        public List<string> Abilities = new();

        public List<QuestEntry> QuestStates = new();
        public List<FlagEntry> WorldFlags = new();
        public List<CounterEntry> WorldCounters = new();

        /// <summary>Reserved. NPCs have no per-NPC state beyond world flags.</summary>
        public List<FlagEntry> NpcStates = new();

        public List<MemoryEntry> MemoryStates = new();
        public float MemoryIntegrity = 1f;

        /// <summary>Reserved. A boss's death is an ordinary <c>WorldObjectState</c> flag via its <c>SaveIdentity</c>, the same as any enemy's, rather than a record here.</summary>
        public List<FlagEntry> BossStates = new();

        /// <summary>Reserved. Dialogue records its consequences as world flags.</summary>
        public List<string> DialogueFlags = new();

        /// <summary>Reserved. There are no endings.</summary>
        public List<string> EndingFlags = new();

        /// <summary>A <c>DifficultyMode</c>. Settings also persist separately via SettingsManager.</summary>
        public int Difficulty;

        public List<ParticipantEntry> Participants = new();

        public DateTime Timestamp =>
            DateTime.TryParse(TimestampUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : DateTime.MinValue;

        public void Stamp()
        {
            TimestampUtc = DateTime.UtcNow.ToString("o");
        }

        /// <summary>
        /// This save's record for <paramref name="sceneName"/>, or null when the player
        /// has never stood there. Null is the honest answer and callers want it: a scene
        /// with no record means "put the arrival wherever the scene puts a new arrival",
        /// which is not the same as a record whose position happens to be the origin.
        /// </summary>
        public SceneStateEntry StateFor(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return null;
            }

            for (var i = 0; i < SceneStates.Count; i++)
            {
                if (SceneStates[i] != null && SceneStates[i].SceneName == sceneName)
                {
                    return SceneStates[i];
                }
            }

            return null;
        }

        /// <summary>
        /// This save's record for <paramref name="sceneName"/>, falling back to the
        /// top-level position and checkpoint when there is no per-scene record but
        /// <see cref="SceneName"/> names this very scene.
        ///
        /// The fallback is narrow on purpose. It fires only where the top-level fields
        /// provably describe the scene being asked about, which is the one case where
        /// using them cannot put the player somewhere they never were. It covers a
        /// <see cref="SaveData"/> built in code rather than read from a file — a test, a
        /// tool — which has no <see cref="SceneStates"/> because nothing migrated it.
        /// </summary>
        public SceneStateEntry ResolveStateFor(string sceneName)
        {
            var recorded = StateFor(sceneName);
            if (recorded != null)
            {
                return recorded;
            }

            if (string.IsNullOrEmpty(sceneName) || SceneName != sceneName)
            {
                return null;
            }

            return new SceneStateEntry
            {
                SceneName = sceneName,
                PlayerPosition = PlayerPosition,
                PlayerRotation = PlayerRotation,
                CheckpointId = CheckpointId
            };
        }

        /// <summary>This save's record for <paramref name="sceneName"/>, adding an empty one if there is none.</summary>
        public SceneStateEntry EnsureStateFor(string sceneName)
        {
            var existing = StateFor(sceneName);
            if (existing != null)
            {
                return existing;
            }

            var added = new SceneStateEntry { SceneName = sceneName };
            SceneStates.Add(added);
            return added;
        }
    }
}

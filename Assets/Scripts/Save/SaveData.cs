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

    [Serializable]
    public class PlayerStatsData
    {
        public float Health;
        public float MaxHealth = 100f;
        public float Stamina;
        public float MaxStamina = 100f;
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
    /// list, and several of them are empty because the system behind them does not
    /// exist yet: <see cref="Inventory"/>, <see cref="Abilities"/>, <see cref="NpcStates"/>,
    /// <see cref="BossStates"/>, <see cref="DialogueFlags"/> and <see cref="EndingFlags"/>
    /// are all reserved rather than used.
    ///
    /// They are here on purpose. The shape of the file is what version migration has to
    /// reason about, and adding a field later is a migration where filling an existing
    /// empty one is not.
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
        /// </summary>
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;

        /// <summary>Round-trip ("o") UTC. A string rather than a tick count so a human can read the file.</summary>
        public string TimestampUtc;

        public string SceneName;

        /// <summary>The checkpoint the player respawns at, by id. Empty means none activated yet.</summary>
        public string CheckpointId;

        public Vector3 PlayerPosition;
        public Quaternion PlayerRotation = Quaternion.identity;
        public PlayerStatsData PlayerStats = new();

        /// <summary>Reserved. There is no inventory system (SPEC.md section 31).</summary>
        public List<string> Inventory = new();

        /// <summary>Reserved. There is no ability system.</summary>
        public List<string> Abilities = new();

        public List<QuestEntry> QuestStates = new();
        public List<FlagEntry> WorldFlags = new();
        public List<CounterEntry> WorldCounters = new();

        /// <summary>Reserved. NPCs have no per-NPC state beyond world flags.</summary>
        public List<FlagEntry> NpcStates = new();

        public List<MemoryEntry> MemoryStates = new();
        public float MemoryIntegrity = 1f;

        /// <summary>Reserved. There are no bosses.</summary>
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
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Raised whenever a world flag changes value (SPEC.md section 71: world state
    /// changes trigger events rather than manually editing dozens of objects).
    /// </summary>
    public readonly struct WorldFlagChangedEvent
    {
        public readonly string Flag;
        public readonly bool Value;

        public WorldFlagChangedEvent(string flag, bool value)
        {
            Flag = flag;
            Value = value;
        }
    }

    /// <summary>
    /// The central world-state model (SPEC.md section 71).
    ///
    /// Deliberately a flat named-flag store rather than a class with one field per
    /// story beat: dialogue, quests and memories all reference flags by name from
    /// ScriptableObject data (SPEC.md sections 22, 23, 48), so the set of flags has to
    /// be extensible without recompiling. <see cref="WorldFlags"/> names the ones the
    /// engine itself cares about.
    ///
    /// Counters are kept separate from flags rather than encoding "how many" in a flag
    /// name, so objective progress (SPEC.md section 23) has somewhere to live.
    /// </summary>
    public class WorldState : MonoBehaviour
    {
        private static WorldState instance;

        /// <summary>Resolved on first access; see <see cref="SceneSingleton"/>.</summary>
        public static WorldState Instance => SceneSingleton.Resolve(ref instance);

        private readonly Dictionary<string, bool> flags = new();
        private readonly Dictionary<string, int> counters = new();

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                GameLogger.LogWarning(LogCategory.Game, "A second WorldState was destroyed; only one may exist.", this);
                Destroy(gameObject);
                return;
            }

            instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public bool GetFlag(string flag)
        {
            return !string.IsNullOrEmpty(flag) && flags.TryGetValue(flag, out var value) && value;
        }

        /// <summary>
        /// Sets a flag and publishes a change event. Setting a flag to the value it
        /// already holds publishes nothing, so listeners cannot fire twice for one
        /// story beat if a trigger is entered again.
        /// </summary>
        public void SetFlag(string flag, bool value = true)
        {
            if (string.IsNullOrEmpty(flag))
            {
                GameLogger.LogWarning(LogCategory.Game, "Ignored an attempt to set a flag with no name.", this);
                return;
            }

            if (GetFlag(flag) == value)
            {
                return;
            }

            flags[flag] = value;
            GameLogger.Log(LogCategory.Game, $"World flag '{flag}' = {value}.", this);
            EventBus.Publish(new WorldFlagChangedEvent(flag, value));
        }

        /// <summary>
        /// True when every named flag is set. An empty or null list passes, so data
        /// with no requirements is always available rather than never available.
        /// </summary>
        public bool HasAllFlags(IReadOnlyList<string> required)
        {
            if (required == null)
            {
                return true;
            }

            for (var i = 0; i < required.Count; i++)
            {
                if (!string.IsNullOrEmpty(required[i]) && !GetFlag(required[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>True when none of the named flags is set.</summary>
        public bool HasNoneOfFlags(IReadOnlyList<string> forbidden)
        {
            if (forbidden == null)
            {
                return true;
            }

            for (var i = 0; i < forbidden.Count; i++)
            {
                if (!string.IsNullOrEmpty(forbidden[i]) && GetFlag(forbidden[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Every flag that has ever been set, for the save system to snapshot. Read-only
        /// because writing has to go through <see cref="SetFlag"/> to publish its event.
        /// </summary>
        public IReadOnlyDictionary<string, bool> Flags => flags;

        /// <summary>Every counter that has ever been touched, for the save system to snapshot.</summary>
        public IReadOnlyDictionary<string, int> Counters => counters;

        public int GetCounter(string key)
        {
            return !string.IsNullOrEmpty(key) && counters.TryGetValue(key, out var value) ? value : 0;
        }

        /// <summary>
        /// Sets a counter outright rather than by a delta. Used when restoring a save;
        /// ordinary gameplay uses <see cref="AddToCounter"/>.
        /// </summary>
        public void SetCounter(string key, int value)
        {
            if (!string.IsNullOrEmpty(key))
            {
                counters[key] = value;
            }
        }

        public int AddToCounter(string key, int delta)
        {
            if (string.IsNullOrEmpty(key))
            {
                return 0;
            }

            var next = GetCounter(key) + delta;
            counters[key] = next;
            return next;
        }

        /// <summary>Clears everything. Intended for new games and tests, not for ordinary play.</summary>
        public void ResetAll()
        {
            flags.Clear();
            counters.Clear();
            GameLogger.Log(LogCategory.Game, "World state cleared.", this);
        }
    }

    /// <summary>
    /// Flag names the engine references directly. Content-only flags live in the
    /// ScriptableObject data instead and are never listed here.
    /// </summary>
    public static class WorldFlags
    {
        public const string EnteredAvarsha = "ENTERED_AVARSHA";
        public const string MetAmara = "MET_AMARA";
        public const string SpokeToDev = "SPOKE_TO_DEV";
        public const string SpokeToMira = "SPOKE_TO_MIRA";
        public const string QueensChargeGiven = "QUEENS_CHARGE_GIVEN";
        public const string FirstMemoryFound = "FIRST_MEMORY_FOUND";
        public const string ReturnedToAmara = "RETURNED_TO_AMARA";
        public const string FirstSupernaturalEvent = "FIRST_SUPERNATURAL_EVENT";
        public const string NirvaanAwakened = "NIRVAAN_AWAKENED";
        public const string ArchivistRevealed = "ARCHIVIST_REVEALED";
    }
}

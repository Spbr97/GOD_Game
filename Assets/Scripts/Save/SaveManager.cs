using System.Collections.Generic;
using Game.Combat;
using Game.Core;
using Game.Dialogue;
using Game.Memory;
using Game.Quests;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Save
{
    /// <summary>
    /// Captures the live game into a <see cref="SaveData"/> and puts it back again
    /// (SPEC.md sections 31 and 32).
    ///
    /// This is the one place allowed to know about every system at once. The dependency
    /// runs one way — Save reads Core, Combat, Quests and Memory, and none of them
    /// reference Save — so the rule in SPEC.md section 58 that no system may depend on
    /// every other still holds for gameplay. A system that wants to save something this
    /// class has no field for implements <see cref="ISaveParticipant"/> instead, and is
    /// found without being referenced.
    ///
    /// Saving is refused during a state transition that could corrupt progression
    /// (SPEC.md section 31): mid-conversation, and while the player is dead and waiting
    /// to respawn. Both are tracked by subscribing to events rather than by asking the
    /// systems, so neither has to know saving exists.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        /// <summary>The wording SPEC.md section 32 requires, verbatim.</summary>
        public const string BackupRestoredMessage =
            "Your previous save could not be loaded. A safe backup has been restored.";

        private static SaveManager instance;

        /// <summary>Resolved on first access; see <see cref="SceneSingleton"/>.</summary>
        public static SaveManager Instance => SceneSingleton.Resolve(ref instance);

        [Tooltip("Save automatically whenever a checkpoint is activated (SPEC.md section 31).")]
        [SerializeField] private bool autoSaveOnCheckpoint = true;

        private readonly HashSet<string> saveBlockers = new();
        private string rootOverride;

        /// <summary>Where saves are written. Tests point this at a temporary folder.</summary>
        public string Root => string.IsNullOrEmpty(rootOverride) ? SaveStorage.DefaultRoot : rootOverride;

        /// <summary>
        /// False while something is in progress that a save would capture halfway.
        /// SPEC.md section 31: never save during a critical state transition.
        /// </summary>
        public bool IsSafeToSave => saveBlockers.Count == 0;

        /// <summary>Why saving is currently refused, for a UI to explain the greyed-out button.</summary>
        public IReadOnlyCollection<string> SaveBlockers => saveBlockers;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                GameLogger.LogWarning(LogCategory.Save, "A second SaveManager was destroyed.", this);
                Destroy(this);
                return;
            }

            instance = this;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Subscribe<DialogueCompletedEvent>(OnDialogueCompleted);
            EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
            EventBus.Subscribe<PlayerRespawnedEvent>(OnPlayerRespawned);
            EventBus.Subscribe<CheckpointActivatedEvent>(OnCheckpointActivated);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Unsubscribe<DialogueCompletedEvent>(OnDialogueCompleted);
            EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
            EventBus.Unsubscribe<PlayerRespawnedEvent>(OnPlayerRespawned);
            EventBus.Unsubscribe<CheckpointActivatedEvent>(OnCheckpointActivated);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        // ------------------------------------------------------------------ blocking

        /// <summary>
        /// Refuses saves until <see cref="AllowSaves"/> is called with the same reason.
        /// Reason-keyed rather than a counter so an unbalanced call cannot leave saving
        /// permanently disabled by an amount nobody can see.
        /// </summary>
        public void BlockSaves(string reason)
        {
            if (!string.IsNullOrEmpty(reason))
            {
                saveBlockers.Add(reason);
            }
        }

        public void AllowSaves(string reason)
        {
            if (!string.IsNullOrEmpty(reason))
            {
                saveBlockers.Remove(reason);
            }
        }

        // -------------------------------------------------------------------- saving

        /// <summary>
        /// Writes the current game to <paramref name="slot"/>. Returns false when
        /// saving is unsafe or the write failed; in both cases every existing file is
        /// left exactly as it was.
        /// </summary>
        public bool Save(SaveSlot slot)
        {
            if (!IsSafeToSave)
            {
                var blocked = string.Join(", ", saveBlockers);
                GameLogger.LogWarning(LogCategory.Save, $"Refused to save '{slot}': {blocked}.", this);
                EventBus.Publish(new SaveFailedEvent(slot, "The game cannot be saved right now.", blocked));
                return false;
            }

            var data = Capture();

            if (!SaveStorage.Write(Root, slot, data, out var detail))
            {
                GameLogger.LogError(LogCategory.Save, $"Could not write save '{slot}': {detail}", this);
                EventBus.Publish(new SaveFailedEvent(slot, "The game could not be saved.", detail));
                return false;
            }

            GameLogger.Log(LogCategory.Save, $"Saved '{slot}'.", this);
            EventBus.Publish(new GameSavedEvent(slot));
            return true;
        }

        /// <summary>
        /// Reads <paramref name="slot"/> and applies it. A corrupt primary falls back to
        /// the backup and raises <see cref="SaveRecoveredFromBackupEvent"/> carrying the
        /// message SPEC.md section 32 specifies.
        /// </summary>
        public bool Load(SaveSlot slot)
        {
            var outcome = SaveStorage.Read(Root, slot);

            if (!outcome.Loaded)
            {
                GameLogger.LogWarning(LogCategory.Save, $"Could not load '{slot}': {outcome.Detail}", this);
                EventBus.Publish(new SaveFailedEvent(slot, "That save could not be loaded.", outcome.Detail));
                return false;
            }

            Apply(outcome.Data);

            if (outcome.RecoveredFromBackup)
            {
                // SPEC.md section 32 step 3: tell the player, in these words, and step 5:
                // carry on from the backup rather than stopping.
                EventBus.Publish(new SaveRecoveredFromBackupEvent(slot, BackupRestoredMessage, outcome.Detail));
            }

            GameLogger.Log(LogCategory.Save, $"Loaded '{slot}' from the {outcome.Source.ToString().ToLowerInvariant()}.", this);
            EventBus.Publish(new GameLoadedEvent(slot, outcome.Source));
            return true;
        }

        public bool HasSave(SaveSlot slot) => SaveStorage.Exists(Root, slot);

        // -------------------------------------------------------- capture and restore

        /// <summary>Builds a save from whatever systems are present. Missing ones are simply not captured.</summary>
        public SaveData Capture()
        {
            var data = new SaveData();
            data.Stamp();
            data.SceneName = SceneManager.GetActiveScene().name;
            data.Difficulty = (int)Difficulty.Current;

            var player = Object.FindAnyObjectByType<PlayerDeath>(FindObjectsInactive.Include);
            if (player != null)
            {
                data.PlayerPosition = player.transform.position;
                data.PlayerRotation = player.transform.rotation;

                var health = player.GetComponent<HealthComponent>();
                if (health != null)
                {
                    data.PlayerStats.Health = health.CurrentHealth;
                    data.PlayerStats.MaxHealth = health.MaxHealth;
                }

                var stamina = player.GetComponent<StaminaComponent>();
                if (stamina != null)
                {
                    data.PlayerStats.Stamina = stamina.CurrentStamina;
                    data.PlayerStats.MaxStamina = stamina.MaxStamina;
                }

                var divine = player.GetComponent<DivineEnergyComponent>();
                if (divine != null)
                {
                    data.PlayerStats.DivineEnergy = divine.CurrentEnergy;
                    data.PlayerStats.MaxDivineEnergy = divine.MaxEnergy;
                }
            }

            var checkpoints = CheckpointManager.Instance;
            if (checkpoints != null && checkpoints.ActiveCheckpoint != null)
            {
                data.CheckpointId = checkpoints.ActiveCheckpoint.CheckpointId;
            }

            var world = WorldState.Instance;
            if (world != null)
            {
                foreach (var pair in world.Flags)
                {
                    data.WorldFlags.Add(new FlagEntry(pair.Key, pair.Value));
                }

                foreach (var pair in world.Counters)
                {
                    data.WorldCounters.Add(new CounterEntry(pair.Key, pair.Value));
                }
            }

            var quests = QuestManager.Instance;
            if (quests != null)
            {
                CaptureQuests(data, quests.ActiveQuests);
                CaptureQuests(data, quests.FinishedQuests);
            }

            var memories = MemoryManager.Instance;
            if (memories != null)
            {
                data.MemoryIntegrity = memories.Integrity;
                foreach (var pair in memories.States)
                {
                    data.MemoryStates.Add(new MemoryEntry { MemoryId = pair.Key, State = (int)pair.Value });
                }
            }

            foreach (var participant in FindParticipants())
            {
                var json = participant.CaptureJson();
                if (!string.IsNullOrEmpty(json))
                {
                    data.Participants.Add(new ParticipantEntry { Key = participant.SaveKey, Json = json });
                }
            }

            return data;
        }

        /// <summary>
        /// Applies a save to the live game.
        ///
        /// Order matters. World flags go back before quests, because setting a flag
        /// publishes an event that quest logic reacts to — restoring the quests last
        /// means their saved status wins over anything that reaction decided.
        /// </summary>
        public void Apply(SaveData data)
        {
            if (data == null)
            {
                return;
            }

            Difficulty.Set((DifficultyMode)data.Difficulty);

            var world = WorldState.Instance;
            if (world != null)
            {
                world.ResetAll();

                for (var i = 0; i < data.WorldFlags.Count; i++)
                {
                    world.SetFlag(data.WorldFlags[i].Key, data.WorldFlags[i].Value);
                }

                for (var i = 0; i < data.WorldCounters.Count; i++)
                {
                    world.SetCounter(data.WorldCounters[i].Key, data.WorldCounters[i].Value);
                }
            }

            var memories = MemoryManager.Instance;
            if (memories != null)
            {
                memories.ClearAll();
                for (var i = 0; i < data.MemoryStates.Count; i++)
                {
                    memories.RestoreState(data.MemoryStates[i].MemoryId, (MemoryState)data.MemoryStates[i].State);
                }

                memories.RestoreIntegrity01(data.MemoryIntegrity);
            }

            var quests = QuestManager.Instance;
            if (quests != null)
            {
                quests.ClearAllProgress();
                RestoreQuests(data, quests);
            }

            var player = Object.FindAnyObjectByType<PlayerDeath>(FindObjectsInactive.Include);
            if (player != null)
            {
                RestorePlayer(player, data);
            }

            var participants = FindParticipants();
            foreach (var participant in participants)
            {
                participant.RestoreJson(FindParticipantJson(data, participant.SaveKey));
            }
        }

        private static void CaptureQuests(SaveData data, IReadOnlyDictionary<string, QuestProgress> quests)
        {
            foreach (var pair in quests)
            {
                var entry = new QuestEntry
                {
                    QuestId = pair.Key,
                    Status = (int)pair.Value.Status
                };

                foreach (var count in pair.Value.Counts)
                {
                    entry.Objectives.Add(new ObjectiveEntry
                    {
                        ObjectiveId = count.Key,
                        Count = count.Value,
                        Complete = pair.Value.IsObjectiveComplete(count.Key)
                    });
                }

                // An objective can be complete with no count recorded against it, so the
                // completed set is walked separately rather than inferred from counts.
                foreach (var completed in pair.Value.CompletedObjectives)
                {
                    if (!pair.Value.Counts.ContainsKey(completed))
                    {
                        entry.Objectives.Add(new ObjectiveEntry
                        {
                            ObjectiveId = completed,
                            Count = 0,
                            Complete = true
                        });
                    }
                }

                data.QuestStates.Add(entry);
            }
        }

        private static void RestoreQuests(SaveData data, QuestManager quests)
        {
            var ids = new List<string>();
            var counts = new List<int>();
            var complete = new List<bool>();

            for (var i = 0; i < data.QuestStates.Count; i++)
            {
                var entry = data.QuestStates[i];

                ids.Clear();
                counts.Clear();
                complete.Clear();

                for (var o = 0; o < entry.Objectives.Count; o++)
                {
                    ids.Add(entry.Objectives[o].ObjectiveId);
                    counts.Add(entry.Objectives[o].Count);
                    complete.Add(entry.Objectives[o].Complete);
                }

                quests.RestoreQuest(entry.QuestId, (QuestStatus)entry.Status, ids, counts, complete);
            }
        }

        private static void RestorePlayer(PlayerDeath player, SaveData data)
        {
            var controller = player.GetComponent<CharacterController>();

            // The CharacterController caches its own position and would drag the player
            // back to where they were standing. Same reason PlayerDeath disables it to
            // respawn.
            if (controller != null)
            {
                controller.enabled = false;
            }

            player.transform.SetPositionAndRotation(data.PlayerPosition, data.PlayerRotation);

            if (controller != null)
            {
                controller.enabled = true;
            }

            var health = player.GetComponent<HealthComponent>();
            if (health != null)
            {
                health.RestoreTo(data.PlayerStats.Health);
            }

            var stamina = player.GetComponent<StaminaComponent>();
            if (stamina != null)
            {
                stamina.RestoreTo(data.PlayerStats.Stamina);
            }

            var divine = player.GetComponent<DivineEnergyComponent>();
            if (divine != null)
            {
                divine.RestoreTo(data.PlayerStats.DivineEnergy);
            }
        }

        private static string FindParticipantJson(SaveData data, string key)
        {
            for (var i = 0; i < data.Participants.Count; i++)
            {
                if (data.Participants[i].Key == key)
                {
                    return data.Participants[i].Json;
                }
            }

            return null;
        }

        private static List<ISaveParticipant> FindParticipants()
        {
            var found = new List<ISaveParticipant>();
            var behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);

            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ISaveParticipant participant && !string.IsNullOrEmpty(participant.SaveKey))
                {
                    found.Add(participant);
                }
            }

            return found;
        }

        // -------------------------------------------------------------------- blocking

        private void OnDialogueStarted(DialogueStartedEvent started) => BlockSaves("a conversation is running");

        private void OnDialogueCompleted(DialogueCompletedEvent completed) => AllowSaves("a conversation is running");

        private void OnPlayerDied(PlayerDiedEvent died) => BlockSaves("the player is dead");

        private void OnPlayerRespawned(PlayerRespawnedEvent respawned) => AllowSaves("the player is dead");

        private void OnCheckpointActivated(CheckpointActivatedEvent activated)
        {
            if (autoSaveOnCheckpoint)
            {
                Save(SaveSlot.Checkpoint);
            }
        }

        /// <summary>
        /// Test and tooling seam. Points saves at a directory of the caller's choosing
        /// so tests never touch the player's real save folder, and lets checkpoint
        /// auto-saving be turned off for tests that drive saving explicitly.
        /// </summary>
        public void Configure(string saveRoot, bool autoSave = true)
        {
            rootOverride = saveRoot;
            autoSaveOnCheckpoint = autoSave;
        }
    }
}

using System.Collections.Generic;
using Game.Combat;
using Game.Core;
using Game.Dialogue;
using Game.Inventory;
using Game.Memory;
using Game.Progression;
using Game.Quests;
using Game.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Core.Localization;

namespace Game.Save
{
    /// <summary>
    /// Where the player ends up when a <see cref="SaveData"/> is applied (TASK 041).
    ///
    /// The distinction exists because a position is only meaningful in the scene it was
    /// measured in. Loading a save resumes a moment, so the position comes from the save.
    /// Walking through a door does not resume a moment — the player has just arrived
    /// somewhere — so the position comes from the door's arrival point, and using the
    /// save's would teleport them to wherever they last stood in that scene, possibly on
    /// the far side of it.
    /// </summary>
    public enum PlayerPlacement
    {
        /// <summary>Where the save says the player stood, in this scene. Nothing recorded for this scene means leave them be.</summary>
        FromSave,

        /// <summary>At a named <c>SceneSpawnPoint</c>, falling back to <see cref="FromSave"/> if there is no such point.</summary>
        AtSpawnPoint,

        /// <summary>Leave the player wherever they are. For applying state without moving anyone.</summary>
        Unchanged
    }

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
        /// <summary>
        /// The wording SPEC.md section 32 requires. Externalized in TASK 042, so this is
        /// now the English table's text rather than a constant — the spec fixes what the
        /// sentence must say, not which language it says it in.
        /// </summary>
        public static string BackupRestoredMessage => Strings.Get(StringKeys.SaveBackupRestored);

        private static SaveManager instance;

        /// <summary>Resolved on first access; see <see cref="SceneSingleton"/>.</summary>
        public static SaveManager Instance => SceneSingleton.Resolve(ref instance);

        /// <summary>
        /// Set by the Main Menu's Continue/Load before it asks <see cref="GameSceneManager"/>
        /// to load the save's scene, then consumed by the <see cref="SaveManager"/> that
        /// wakes up in that scene. Static because the menu and the destination scene
        /// never coexist as loaded objects that could pass this to each other directly.
        /// </summary>
        public static SaveSlot? PendingLoad { get; set; }

        /// <summary>
        /// The whole game, captured at the moment the player left the previous scene and
        /// applied again once the next one is live (TASK 041). Static for the same reason
        /// <see cref="PendingLoad"/> is.
        ///
        /// Scene travel goes through capture-and-apply rather than by making the
        /// progression managers survive the load, because only five of them are scene
        /// singletons today and <c>QuestManager</c>, <c>MemoryManager</c>,
        /// <c>InventoryManager</c>, <c>SkillTreeManager</c> and <c>CheckpointManager</c>
        /// are all among them — a scene load drops every one. Making each persistent would
        /// be five lifetime changes, five new duplicate-instance cases, and a second way
        /// for progression to cross a boundary that would then need its own tests. Capture
        /// and apply is the way progression already crosses a boundary, it is the
        /// best-tested path in the project, and any system that is carried wrongly here is
        /// carried wrongly by save and load too, where it would be found anyway.
        ///
        /// It is never written to disk. A save file records where the player is, not that
        /// they were in a doorway.
        /// </summary>
        private static SaveData travelSnapshot;

        [Tooltip("Save automatically whenever a checkpoint is activated (SPEC.md section 31).")]
        [SerializeField] private bool autoSaveOnCheckpoint = true;

        [Tooltip("Save automatically just before crossing into another scene (TASK 040 decision). Crossing a scene boundary is a commitment the player cannot undo, and saving is blocked for the whole crossing.")]
        [SerializeField] private bool autoSaveOnSceneTravel = true;

        private readonly HashSet<string> saveBlockers = new();
        private string rootOverride;

        /// <summary>Where saves are written. Tests point this at a temporary folder.</summary>
        public string Root => string.IsNullOrEmpty(rootOverride) ? SaveStorage.DefaultRoot : rootOverride;

        /// <summary>
        /// False while something is in progress that a save would capture halfway.
        /// SPEC.md section 31: never save during a critical state transition.
        /// </summary>
        public bool IsSafeToSave => saveBlockers.Count == 0;

        /// <summary>
        /// True when the run has changed in a way no save file records yet, and the
        /// change is one a player would be annoyed to lose silently.
        ///
        /// This is deliberately **not** a general "anything happened" flag. Ordinary play
        /// diverges from the last save constantly and that is the normal condition of a
        /// game; flagging it would make the warning meaningless. What sets this is a
        /// change to how the run itself is configured — today only a mid-run difficulty
        /// change (TASK 040) — where the player's mental model is "I have changed a
        /// setting", not "I have played for a while", and losing it to a quit looks like
        /// the game ignoring them.
        ///
        /// Cleared by any successful save, including an autosave, because at that point
        /// the file does record it.
        /// </summary>
        public bool HasUnsavedRunChanges { get; private set; }

        /// <summary>
        /// The reason <see cref="HasUnsavedRunChanges"/> is set, for a confirmation
        /// prompt to quote. Empty when there is nothing outstanding.
        /// </summary>
        public string UnsavedRunChangeReason { get; private set; } = string.Empty;

        /// <summary>
        /// Records that the run has diverged from its file in a way worth warning about.
        /// Call it with what changed, in words a player would recognise.
        /// </summary>
        public void MarkRunDirty(string reason)
        {
            HasUnsavedRunChanges = true;
            UnsavedRunChangeReason = string.IsNullOrEmpty(reason) ? "a setting was changed" : reason;

            GameLogger.Log(LogCategory.Save,
                $"The run has unsaved changes: {UnsavedRunChangeReason}.", this);
        }

        /// <summary>Forgets any outstanding run change. Called on a successful save and when a run ends.</summary>
        public void ClearRunDirty()
        {
            HasUnsavedRunChanges = false;
            UnsavedRunChangeReason = string.Empty;
        }

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
            EventBus.Subscribe<CinematicStartedEvent>(OnCinematicStarted);
            EventBus.Subscribe<CinematicCompletedEvent>(OnCinematicCompleted);
            EventBus.Subscribe<CinematicSkippedEvent>(OnCinematicSkipped);
            EventBus.Subscribe<SceneTravelRequestedEvent>(OnSceneTravelRequested);
            EventBus.Subscribe<DifficultyChangedByPlayerEvent>(OnDifficultyChangedByPlayer);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Unsubscribe<DialogueCompletedEvent>(OnDialogueCompleted);
            EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
            EventBus.Unsubscribe<PlayerRespawnedEvent>(OnPlayerRespawned);
            EventBus.Unsubscribe<CheckpointActivatedEvent>(OnCheckpointActivated);
            EventBus.Unsubscribe<CinematicStartedEvent>(OnCinematicStarted);
            EventBus.Unsubscribe<CinematicCompletedEvent>(OnCinematicCompleted);
            EventBus.Unsubscribe<CinematicSkippedEvent>(OnCinematicSkipped);
            EventBus.Unsubscribe<SceneTravelRequestedEvent>(OnSceneTravelRequested);
            EventBus.Unsubscribe<DifficultyChangedByPlayerEvent>(OnDifficultyChangedByPlayer);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Start()
        {
            if (instance != this)
            {
                return;
            }

            // An arrival is checked before a pending load, and the two cannot both be
            // outstanding: travel is started from inside gameplay and a pending load is
            // set by the menu, which ends any journey.
            if (TryCompleteArrival())
            {
                return;
            }

            if (!PendingLoad.HasValue)
            {
                return;
            }

            var slot = PendingLoad.Value;
            PendingLoad = null;
            Load(slot);
        }

        /// <summary>
        /// Finishes a journey that began in another scene: puts the carried progression
        /// back and places the player at the arrival point. Returns false when this scene
        /// was not the destination, which is the ordinary case.
        /// </summary>
        private bool TryCompleteArrival()
        {
            var active = SceneManager.GetActiveScene().name;
            var carried = travelSnapshot;

            if (!SceneTravel.TryConsume(active, out var spawnId))
            {
                // The statics did not survive — a domain reload, or a SaveManager that is
                // not the one that started the journey. The journal on disk says the same
                // thing and is keyed on the same destination, so the crossing can still be
                // finished rather than silently dropping the run's progression.
                if (!TravelJournal.TryRead(Root, active, out spawnId, out var journalled))
                {
                    return false;
                }

                carried ??= journalled;
                GameLogger.LogWarning(LogCategory.Save,
                    $"Completed the crossing into '{active}' from the travel journal; the in-memory hand-off "
                    + "was lost. Progression was carried over from disk.");
            }

            travelSnapshot = null;
            TravelJournal.Clear(Root);

            // The blocker was added by the SaveManager in the scene we left, which no
            // longer exists; this instance's own set is clean. Clearing it anyway costs
            // nothing and means the invariant holds however travel was started.
            AllowSaves(SceneTravel.SaveBlockReason);

            var placed = false;

            if (carried != null)
            {
                Apply(carried, PlayerPlacement.AtSpawnPoint, spawnId);
                placed = SceneSpawnPoint.Find(spawnId) != null;
            }
            else
            {
                // Travel without a snapshot should not happen. Placing the player is still
                // the right thing to do: arriving at the origin of a scene is worse than
                // arriving at the door with nothing carried, and the log says which it was.
                GameLogger.LogFallback(LogCategory.Save, $"arrival in '{active}'", "SaveManager.TryCompleteArrival",
                    "no progression snapshot was carried across the load",
                    "placing the player at the arrival point with whatever state the scene starts with", this);
                placed = PlaceAtSpawnPoint(spawnId);
            }

            GameLogger.Log(LogCategory.Save,
                $"Arrived in '{active}' at spawn '{spawnId}' (placed: {placed}).", this);
            EventBus.Publish(new SceneArrivedEvent(active, spawnId, placed));
            return true;
        }

        /// <summary>Reads a slot without applying it. For Save/Load and Continue screens.</summary>
        public LoadOutcome Peek(SaveSlot slot) => SaveBrowser.Peek(Root, slot);

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
                EventBus.Publish(new SaveFailedEvent(slot, Strings.Get(StringKeys.SaveRefused), blocked));
                return false;
            }

            var data = Capture();

            if (!SaveStorage.Write(Root, slot, data, out var detail))
            {
                GameLogger.LogError(LogCategory.Save, $"Could not write save '{slot}': {detail}", this);
                EventBus.Publish(new SaveFailedEvent(slot, Strings.Get(StringKeys.SaveFailed), detail));
                return false;
            }

            // The file now records whatever was outstanding, including an autosave's
            // worth. Cleared here rather than per-caller so no save path can forget.
            ClearRunDirty();

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
                EventBus.Publish(new SaveFailedEvent(slot, Strings.Get(StringKeys.SaveLoadFailed), outcome.Detail));
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

        // -------------------------------------------------------------- scene travel

        /// <summary>
        /// Carries the game into another scene. Answers <see cref="SceneTravelRequestedEvent"/>
        /// so the door that asked never learns the save system exists (SPEC.md section 58).
        /// </summary>
        private void OnSceneTravelRequested(SceneTravelRequestedEvent request)
        {
            if (instance != this)
            {
                return;
            }

            if (string.IsNullOrEmpty(request.TargetScene))
            {
                GameLogger.LogError(LogCategory.Save, "Scene travel was requested with no destination.", this);
                return;
            }

            if (SceneTravel.IsTravelling || GameSceneManager.IsLoading)
            {
                GameLogger.LogWarning(LogCategory.Save,
                    $"Ignored travel to '{request.TargetScene}': a journey is already in flight.", this);
                return;
            }

            // Checked before anything is torn down or blocked. A scene missing from Build
            // Settings is a content error, and the player should be left standing in a
            // working scene rather than in a blocked, half-departed one.
            if (!Application.CanStreamedLevelBeLoaded(request.TargetScene))
            {
                GameLogger.LogError(LogCategory.Save,
                    $"Refused travel to '{request.TargetScene}': that scene is not in Build Settings.", this);
                return;
            }

            var scenes = GameSceneManager.Instance;
            if (scenes == null)
            {
                GameLogger.LogFallback(LogCategory.Save, $"travel to '{request.TargetScene}'",
                    "SaveManager.OnSceneTravelRequested", "no GameSceneManager exists",
                    "the player stays where they are", this);
                return;
            }

            var from = SceneManager.GetActiveScene().name;

            // An autosave, written before anything is blocked (TASK 040 decision).
            //
            // Crossing into a temple is a commitment: the scene the player was in stops
            // existing, and saving is refused for the whole crossing, so a crash or a
            // quit between the two scenes lands them at whatever their last real save
            // was. That could be an hour ago. The travel journal keeps the crossing
            // itself recoverable, but recovering a journey is a stranger thing to offer a
            // player than simply having saved — so the decision is to save.
            //
            // It goes in the Checkpoint slot rather than a new one. That slot already
            // means "written automatically", the Main Menu's Continue takes whichever
            // slot is newest, and adding a fourth slot would make every screen that
            // lists slots learn about it for no gain.
            //
            // Ordered first deliberately. Save() refuses while a blocker is set, so this
            // has to happen before BlockSaves below, and capturing for travel afterwards
            // costs nothing because nothing has changed in between.
            if (autoSaveOnSceneTravel)
            {
                var outcome = Save(SaveSlot.Checkpoint);

                if (!outcome)
                {
                    // Not fatal, and deliberately not a refusal to travel: a door that
                    // stops working because the disk is full is worse than a door that
                    // works and says so.
                    GameLogger.LogWarning(LogCategory.Save,
                        $"Could not autosave before travelling to '{request.TargetScene}'. The journey "
                        + "continues; the travel journal is still written, so the crossing itself is "
                        + "recoverable.", this);
                }
            }

            // Capture before the teardown starts, so the record of the scene being left is
            // the scene as the player leaves it.
            travelSnapshot = Capture();

            // On disk as well as in memory. Saving is blocked for the whole crossing, so
            // if the game stops between the two scenes there is nothing else holding
            // everything the player has done since their last real save.
            TravelJournal.Write(Root, request.TargetScene, request.SpawnId, travelSnapshot);

            BlockSaves(SceneTravel.SaveBlockReason);
            SceneTravel.Begin(request.TargetScene, request.SpawnId);
            EventBus.Publish(new SceneTravelStartedEvent(from, request.TargetScene, request.SpawnId));

            GameLogger.Log(LogCategory.Save,
                $"Travelling from '{from}' to '{request.TargetScene}' ({request.Reason}).", this);

            scenes.LoadSceneAsync(request.TargetScene);
        }

        /// <summary>
        /// Moves the player to the named arrival point. Returns false when there is no
        /// such point or no player, leaving both alone.
        /// </summary>
        private static bool PlaceAtSpawnPoint(string spawnId)
        {
            var spawn = SceneSpawnPoint.Find(spawnId);
            if (spawn == null)
            {
                return false;
            }

            var player = Object.FindAnyObjectByType<PlayerDeath>(FindObjectsInactive.Include);
            if (player == null)
            {
                return false;
            }

            MovePlayer(player.transform, spawn.Position, spawn.Rotation);
            return true;
        }

        /// <summary>
        /// Puts the player somewhere, working around the <see cref="CharacterController"/>
        /// caching its own position and dragging them back. Same reason
        /// <c>PlayerDeath</c> disables it to respawn.
        /// </summary>
        private static void MovePlayer(Transform player, Vector3 position, Quaternion rotation)
        {
            var controller = player.GetComponent<CharacterController>();

            if (controller != null)
            {
                controller.enabled = false;
            }

            player.SetPositionAndRotation(position, rotation);

            if (controller != null)
            {
                controller.enabled = true;
            }
        }

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
                    if (!pair.Key.StartsWith("REL_"))
                    {
                        data.WorldCounters.Add(new CounterEntry(pair.Key, pair.Value));
                    }
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

            var inventory = InventoryManager.Instance;
            if (inventory != null)
            {
                data.Inventory = inventory.CaptureEntries();
            }

            var skills = SkillTreeManager.Instance;
            if (skills != null)
            {
                data.Abilities = new List<string>(skills.UnlockedIds);
            }

            foreach (var participant in FindParticipants())
            {
                var json = participant.CaptureJson();
                if (!string.IsNullOrEmpty(json))
                {
                    data.Participants.Add(new ParticipantEntry { Key = participant.SaveKey, Json = json });
                }
            }

            // TASK 041. Last, so it mirrors whatever the player and checkpoint sections
            // above actually found. SceneMemory supplies the scenes that are not loaded:
            // without it every save would forget every scene but the one being stood in.
            SceneMemory.Record(data.SceneName, data.PlayerPosition, data.PlayerRotation, data.CheckpointId);
            data.SceneStates = SceneMemory.Snapshot();

            return data;
        }

        /// <summary>
        /// Applies a save to the live game.
        ///
        /// Order matters. World flags go back before quests, because setting a flag
        /// publishes an event that quest logic reacts to — restoring the quests last
        /// means their saved status wins over anything that reaction decided.
        /// </summary>
        public void Apply(SaveData data) => Apply(data, PlayerPlacement.FromSave, null);

        /// <summary>
        /// Applies a save, choosing where the player ends up (TASK 041). See
        /// <see cref="PlayerPlacement"/> for why that is a caller's decision rather than
        /// always "wherever the file says".
        /// </summary>
        public void Apply(SaveData data, PlayerPlacement placement, string spawnId)
        {
            if (data == null)
            {
                return;
            }

            // Ownership (TASK 041). The slot owns the difficulty in force during a
            // playthrough: it is chosen at New Game and belongs to that run, so loading a
            // save must not inherit whatever the last run was played on. SettingsManager
            // owns the global copy — display, audio, accessibility, rebinds — and is told
            // the new value so the Settings panel is not left describing a difficulty the
            // game is no longer running at. The write goes one way only; SettingsManager
            // never pushes difficulty back into a loaded slot.
            var mode = (DifficultyMode)data.Difficulty;
            Difficulty.Set(mode);
            SettingsManager.Instance?.AdoptDifficultyFromSave(mode);

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
                    if (!string.IsNullOrEmpty(data.WorldCounters[i].Key)
                        && !data.WorldCounters[i].Key.StartsWith("REL_"))
                    {
                        world.SetCounter(data.WorldCounters[i].Key, data.WorldCounters[i].Value);
                    }
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

            InventoryManager.Instance?.RestoreEntries(data.Inventory);

            // Before RestorePlayer: restoring the unlocked skill set recomputes the
            // player's max health/energy (PlayerProgressionStats), and RestorePlayer's
            // own HealthComponent.RestoreTo clamps to whatever max is current when it
            // runs. Doing this after would clamp a skilled-up player back down.
            SkillTreeManager.Instance?.RestoreUnlocked(data.Abilities);

            var active = SceneManager.GetActiveScene().name;
            var here = data.ResolveStateFor(active);

            SceneMemory.Adopt(data.SceneStates);

            var player = Object.FindAnyObjectByType<PlayerDeath>(FindObjectsInactive.Include);
            if (player != null)
            {
                RestorePlayerStats(player, data);
                PlacePlayer(player, placement, spawnId, here);
            }

            // This scene's checkpoint and no other. An id recorded in a different scene
            // resolves to nothing here, and CheckpointManager would be left with none —
            // so the next death would send the player to the scene's default spawn
            // instead of to the checkpoint they actually lit. That is TASK 041's "without
            // restoring the wrong player position", in its most damaging form.
            CheckpointManager.Instance?.RestoreActiveCheckpoint(here?.CheckpointId);

            var participants = FindParticipants();
            foreach (var participant in participants)
            {
                participant.RestoreJson(FindParticipantJson(data, participant.SaveKey));
            }

            // Last: the sweep reads the state everything above has just put back.
            ProgressionRecovery.Run();

            // The run now matches a file by definition. Cleared here so a dirty flag from
            // a previous run cannot follow a freshly loaded one around — SaveManager
            // survives the scene load that a run does not.
            ClearRunDirty();
        }

        /// <summary>
        /// Puts the player where <paramref name="placement"/> says (TASK 041).
        ///
        /// <see cref="PlayerPlacement.AtSpawnPoint"/> falls back to the save's own record
        /// when the named point is missing, and that in turn falls back to leaving the
        /// player alone. Both fallbacks are deliberate: a content error in an arrival
        /// point should leave the player standing somewhere legitimate in the destination,
        /// not at the world origin, which in a scene with a floor means under it.
        /// </summary>
        private static void PlacePlayer(PlayerDeath player, PlayerPlacement placement, string spawnId,
            SceneStateEntry here)
        {
            if (placement == PlayerPlacement.Unchanged)
            {
                return;
            }

            if (placement == PlayerPlacement.AtSpawnPoint)
            {
                var spawn = SceneSpawnPoint.Find(spawnId);
                if (spawn != null)
                {
                    MovePlayer(player.transform, spawn.Position, spawn.Rotation);
                    return;
                }

                GameLogger.LogFallback(LogCategory.Save, $"arrival at spawn point '{spawnId}'",
                    "SaveManager.PlacePlayer", "no SceneSpawnPoint with that id is in the scene",
                    here != null
                        ? "using the position this save recorded for this scene"
                        : "leaving the player where the scene starts them", player);
            }

            if (here == null)
            {
                // Nothing recorded for this scene: the player has never stood here, so the
                // scene's own starting position is the only honest answer.
                return;
            }

            MovePlayer(player.transform, here.PlayerPosition, here.PlayerRotation);
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

        /// <summary>
        /// Health, stamina and divine energy. Position is <see cref="PlacePlayer"/>'s job:
        /// the two were one method until TASK 041, when where the player stands stopped
        /// being a property of the file and became a property of how the file is being
        /// used.
        /// </summary>
        private static void RestorePlayerStats(PlayerDeath player, SaveData data)
        {
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

        /// <summary>
        /// A cinematic is an irreversible transition in the sense SPEC.md section 31
        /// means (TASK 041): it sets story flags and moves the player as it runs, so a
        /// save taken halfway through records a world part-way through a beat that will
        /// never play again. Both ways out — finishing and skipping — release the block,
        /// which is why the two are subscribed separately rather than trusting a
        /// completion event to fire for a skip.
        /// </summary>
        private const string CinematicBlockReason = "a scene is playing";

        private void OnCinematicStarted(CinematicStartedEvent started) => BlockSaves(CinematicBlockReason);

        private void OnCinematicCompleted(CinematicCompletedEvent completed) => AllowSaves(CinematicBlockReason);

        private void OnCinematicSkipped(CinematicSkippedEvent skipped) => AllowSaves(CinematicBlockReason);

        /// <summary>
        /// A mid-run difficulty change marks the run dirty (TASK 040 decision).
        ///
        /// SPEC.md section 44 allows the change and does not say whether it should
        /// survive a reload without a save. Before this it silently did not: the global
        /// settings took it immediately, the live <c>Difficulty</c> took it immediately,
        /// and the loaded slot only learned about it at the next save — so quitting
        /// without saving discarded it with no indication that anything had been lost.
        ///
        /// Refusing the change while a game is loaded was the alternative and is worse:
        /// the single most common reason to change difficulty is that the fight in front
        /// of you is too hard, which is exactly when a game is loaded.
        /// </summary>
        private void OnDifficultyChangedByPlayer(DifficultyChangedByPlayerEvent changed)
        {
            if (instance != this)
            {
                return;
            }

            MarkRunDirty($"difficulty was changed from {changed.Previous} to {changed.Current}");
        }

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

            // Both automatic paths, from one flag. A test that says "I drive saving
            // myself" means all of it, and a travel autosave appearing in a test that
            // asked for none would be a surprise in whichever assertion happened to
            // notice it rather than an obvious failure here.
            autoSaveOnCheckpoint = autoSave;
            autoSaveOnSceneTravel = autoSave;
        }
    }
}

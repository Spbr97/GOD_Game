namespace Game.Save
{
    /// <summary>Raised after a save has been written and verified on disk.</summary>
    public readonly struct GameSavedEvent
    {
        public readonly SaveSlot Slot;

        public GameSavedEvent(SaveSlot slot)
        {
            Slot = slot;
        }
    }

    /// <summary>Raised after a save has been applied to the live game.</summary>
    public readonly struct GameLoadedEvent
    {
        public readonly SaveSlot Slot;
        public readonly SaveSource Source;

        public GameLoadedEvent(SaveSlot slot, SaveSource source)
        {
            Slot = slot;
            Source = source;
        }
    }

    /// <summary>
    /// Raised when a save could not be written or read. <see cref="PlayerMessage"/> is
    /// the sentence to put on screen; <see cref="Detail"/> is for the log, never for
    /// the player.
    /// </summary>
    public readonly struct SaveFailedEvent
    {
        public readonly SaveSlot Slot;
        public readonly string PlayerMessage;
        public readonly string Detail;

        public SaveFailedEvent(SaveSlot slot, string playerMessage, string detail)
        {
            Slot = slot;
            PlayerMessage = playerMessage;
            Detail = detail;
        }
    }

    /// <summary>
    /// Raised when a corrupt primary save was replaced by its backup. Carries the exact
    /// wording SPEC.md section 32 requires.
    /// </summary>
    public readonly struct SaveRecoveredFromBackupEvent
    {
        public readonly SaveSlot Slot;
        public readonly string PlayerMessage;
        public readonly string Detail;

        public SaveRecoveredFromBackupEvent(SaveSlot slot, string playerMessage, string detail)
        {
            Slot = slot;
            PlayerMessage = playerMessage;
            Detail = detail;
        }
    }

    /// <summary>
    /// A system that wants to save something <see cref="SaveData"/> has no field for.
    /// Implementers are found in the loaded scenes at save time, so nothing has to
    /// register — and, more importantly, the save system does not have to reference
    /// them.
    ///
    /// <see cref="SaveKey"/> must be stable across builds: it is what a later load
    /// matches the blob back up by.
    /// </summary>
    public interface ISaveParticipant
    {
        string SaveKey { get; }

        /// <summary>Returns this system's state as JSON, or null to save nothing.</summary>
        string CaptureJson();

        /// <summary>
        /// Applies previously captured JSON. Called with null when the save had no entry
        /// for this key, which is what a save from before this system existed looks like.
        /// </summary>
        void RestoreJson(string json);
    }
}

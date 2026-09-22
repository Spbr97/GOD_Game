using System;

namespace Game.Save
{
    /// <summary>
    /// Read-only queries over save files for menu UI (SPEC.md section 42's Save/Load
    /// and Continue screens). Kept apart from <see cref="SaveManager"/> and free of
    /// MonoBehaviour so a menu can ask "what's on disk" before any scene with a live
    /// game exists, and so the question is testable in EditMode without a scene.
    ///
    /// Peeking never applies anything to the live game — that is still
    /// <see cref="SaveManager.Load"/>'s job, run after the target scene has loaded.
    /// </summary>
    public static class SaveBrowser
    {
        /// <summary>Reads a slot without touching the live game. Same fallback-to-backup rules as a real load.</summary>
        public static LoadOutcome Peek(string root, SaveSlot slot) => SaveStorage.Read(root, slot);

        /// <summary>
        /// The slot with the newest timestamp among every slot that has a save, for the
        /// Main Menu's "Continue" button. False when nothing has ever been saved.
        /// </summary>
        public static bool TryFindMostRecent(string root, out SaveSlot slot)
        {
            slot = default;
            var found = false;
            var latest = DateTime.MinValue;

            foreach (SaveSlot candidate in Enum.GetValues(typeof(SaveSlot)))
            {
                var outcome = Peek(root, candidate);
                if (!outcome.Loaded)
                {
                    continue;
                }

                var timestamp = outcome.Data.Timestamp;
                if (!found || timestamp > latest)
                {
                    found = true;
                    latest = timestamp;
                    slot = candidate;
                }
            }

            return found;
        }
    }
}

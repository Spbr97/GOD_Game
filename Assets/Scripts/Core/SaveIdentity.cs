using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// A stable identity for an object whose state needs to survive a save
    /// independently of where it sits in the scene hierarchy (SPEC.md TASK 009).
    ///
    /// Lives in Core, not Save: this is a generic "what am I called, stably" fact
    /// about an object, the same kind of fact <see cref="WorldState"/> already is.
    /// The save system reads that state back through <see cref="WorldObjectState"/>'s
    /// ordinary world flags — nothing in Combat or Memory needs to reference Save for
    /// this (SPEC.md section 58: the save system depends on everything, nothing
    /// depends on it).
    ///
    /// Optional and opt-in: an object with no gameplay state worth remembering (a
    /// hazard volume, a purely cosmetic prop) needs none of this. Anything that does
    /// — an enemy's death, a pickup's collection — carries one of these so
    /// <see cref="WorldObjectState"/> has something stable to key a flag on, rather
    /// than the GameObject's name, which content authors rename freely.
    ///
    /// Falls back to the GameObject's name when no id is set, matching the idiom
    /// already used by <see cref="Game.Combat.Checkpoint.CheckpointId"/> and
    /// <see cref="Game.Quests.QuestTarget"/>'s objective id.
    /// </summary>
    public class SaveIdentity : MonoBehaviour
    {
        [SerializeField] private string id;

        public string Id => string.IsNullOrEmpty(id) ? name : id;

        /// <summary>Test and tooling seam.</summary>
        public void Configure(string identity)
        {
            id = identity;
        }
    }
}

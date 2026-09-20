using UnityEngine;

namespace Game.Memory
{
    /// <summary>Memory categories from SPEC.md section 19.</summary>
    public enum MemoryCategory
    {
        Personal,
        Historical,
        Divine,
        Forbidden,
        Lost,
        False
    }

    /// <summary>
    /// Memory states from SPEC.md section 21. <c>Unknown</c> is added as the
    /// before-discovery state; the spec's list describes memories the player already
    /// has some relationship with.
    /// </summary>
    public enum MemoryState
    {
        Unknown,
        Known,
        PartiallyRemembered,
        Forgotten,
        Corrupted,
        Restored,
        FalseMemory
    }

    public enum MemoryImportance
    {
        Optional,
        Supporting,
        Critical
    }

    /// <summary>
    /// One collectible memory (SPEC.md sections 19 and 48). Fields follow the spec's
    /// list; <c>AudioNarration</c> is a string id rather than a clip reference because
    /// no voice assets exist yet (SPEC.md section 41).
    /// </summary>
    [CreateAssetMenu(fileName = "Memory_", menuName = "God Game/Memory Fragment")]
    public class MemoryFragment : ScriptableObject
    {
        [SerializeField] private string memoryId;
        [SerializeField] private string title = "Untitled Memory";

        [TextArea(3, 10)]
        [SerializeField] private string description;

        [Tooltip("Whose memory this is.")]
        [SerializeField] private string owner;

        [Tooltip("Where in the world it was found, for the memory log.")]
        [SerializeField] private string location;

        [SerializeField] private MemoryCategory category = MemoryCategory.Personal;
        [SerializeField] private MemoryImportance importance = MemoryImportance.Supporting;

        [Tooltip("Characters this memory concerns.")]
        [SerializeField] private string[] associatedCharacters;

        [Tooltip("Quest id this memory belongs to. Optional.")]
        [SerializeField] private string associatedQuestId;

        [Tooltip("Objective id reported when this memory is discovered. Optional.")]
        [SerializeField] private string objectiveIdOnDiscovery;

        [Tooltip("Flag set when this memory is discovered. Optional.")]
        [SerializeField] private string discoveryFlag;

        [Tooltip("Placeholder stand-in for the visual representation (SPEC.md section 70).")]
        [SerializeField] private Color visualTint = new(1f, 0.85f, 0.35f);

        [Tooltip("Id of the narration line. No audio exists yet; see KNOWN_ISSUES.md.")]
        [SerializeField] private string audioNarrationId;

        [Tooltip("State the memory enters on discovery. Most start Known; corrupted ones do not.")]
        [SerializeField] private MemoryState stateOnDiscovery = MemoryState.Known;

        public string MemoryId => string.IsNullOrEmpty(memoryId) ? name : memoryId;
        public string Title => title;
        public string Description => description;
        public string Owner => owner;
        public string Location => location;
        public MemoryCategory Category => category;
        public MemoryImportance Importance => importance;
        public string[] AssociatedCharacters => associatedCharacters;
        public string AssociatedQuestId => associatedQuestId;
        public string ObjectiveIdOnDiscovery => objectiveIdOnDiscovery;
        public string DiscoveryFlag => discoveryFlag;
        public Color VisualTint => visualTint;
        public string AudioNarrationId => audioNarrationId;
        public MemoryState StateOnDiscovery => stateOnDiscovery;

        /// <summary>
        /// Critical memories are protected from corruption and loss (SPEC.md sections
        /// 20 and 21: critical story memories must never become permanently inaccessible).
        /// </summary>
        public bool IsProtected => importance == MemoryImportance.Critical;

        /// <summary>Test and tooling seam for authoring without the Inspector.</summary>
        public void Configure(string id, string memoryTitle, string memoryDescription, string memoryOwner,
            MemoryCategory memoryCategory, MemoryImportance memoryImportance,
            string questId = null, string objectiveId = null, string flag = null, string place = null)
        {
            memoryId = id;
            title = memoryTitle;
            description = memoryDescription;
            owner = memoryOwner;
            category = memoryCategory;
            importance = memoryImportance;
            associatedQuestId = questId;
            objectiveIdOnDiscovery = objectiveId;
            discoveryFlag = flag;
            location = place;
        }
    }
}

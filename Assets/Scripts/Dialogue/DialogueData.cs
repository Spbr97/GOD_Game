using System;
using UnityEngine;

namespace Game.Dialogue
{
    /// <summary>
    /// One thing a dialogue node or choice does to the world when it is taken
    /// (SPEC.md section 22: choices modify quest flags, memory states and NPC behaviour).
    ///
    /// Consequences name their targets by string rather than holding object
    /// references, so a dialogue asset can be authored before the quest or memory it
    /// refers to exists, and so dialogue does not have to reference the quest and
    /// memory assemblies (SPEC.md section 58, rule 7).
    /// </summary>
    public enum ConsequenceType
    {
        SetFlag,
        ClearFlag,
        StartQuest,
        CompleteObjective,
        DiscoverMemory
    }

    [Serializable]
    public class DialogueConsequence
    {
        [Tooltip("What this consequence does.")]
        public ConsequenceType Type = ConsequenceType.SetFlag;

        [Tooltip("Flag name, quest id, objective id or memory id, depending on Type.")]
        public string Target;

        [Tooltip("Reserved for consequences that need a numeric amount.")]
        public int Amount;
    }

    /// <summary>A player-selectable reply (SPEC.md section 22, Choices).</summary>
    [Serializable]
    public class DialogueChoice
    {
        [TextArea(1, 3)]
        public string Text;

        [Tooltip("Node to jump to when this choice is taken. Empty ends the conversation.")]
        public string NextDialogueId;

        [Tooltip("All of these flags must be set for the choice to be offered.")]
        public string[] RequiredFlags;

        [Tooltip("Any of these flags being set hides the choice.")]
        public string[] BlockingFlags;

        public DialogueConsequence[] Consequences;

        [Tooltip("Quest objective reported when the player selects this choice.")]
        public string ObjectiveId;
    }

    /// <summary>
    /// One node of a conversation, matching the recommended data shape in SPEC.md
    /// section 22. <c>VoiceAsset</c> is a string id rather than an AudioClip reference
    /// because no voice lines exist yet (SPEC.md section 41) and holding direct clip
    /// references would load the whole conversation's audio with the asset.
    /// </summary>
    [Serializable]
    public class DialogueNode
    {
        [Tooltip("Unique within this graph. Referenced by NextDialogueId.")]
        public string DialogueId;

        [Tooltip("Display name of whoever is speaking.")]
        public string Speaker;

        [TextArea(2, 6)]
        public string Text;

        [Tooltip("All of these flags must be set for this node to be eligible as an entry point.")]
        public string[] RequiredFlags;

        [Tooltip("Any of these flags being set makes this node ineligible as an entry point.")]
        public string[] BlockingFlags;

        [Tooltip("Memory id whose state gates this node. Leave empty to ignore.")]
        public string RequiredMemoryId;

        [Tooltip("State RequiredMemoryId must be in. Ignored when RequiredMemoryId is empty.")]
        public string RequiredMemoryState;

        [Tooltip("Applied when this node is shown.")]
        public DialogueConsequence[] Consequences;

        [Tooltip("Offered to the player instead of an automatic advance, when non-empty.")]
        public DialogueChoice[] Choices;

        [Tooltip("Node shown on advance when there are no choices. Empty ends the conversation.")]
        public string NextDialogueId;

        [Tooltip("Subtitle override. Falls back to Text when empty (SPEC.md section 43).")]
        [TextArea(1, 3)]
        public string Subtitle;

        [TextArea(2, 6)]
        [Tooltip("Optional alternate line shown below 35% memory integrity.")]
        public string LowIntegrityText;

        [Tooltip("Id of the voice line for this node. No audio exists yet; see KNOWN_ISSUES.md.")]
        public string VoiceAssetId;

        public bool HasChoices => Choices != null && Choices.Length > 0;
        public string SubtitleText => string.IsNullOrEmpty(Subtitle) ? Text : Subtitle;
    }
}

using System;
using UnityEngine;

namespace Game.Quests
{
    /// <summary>
    /// The objective kinds SPEC.md section 23 requires.
    ///
    /// Each kind has a world adapter that reports its objective id to QuestManager.
    /// </summary>
    public enum ObjectiveType
    {
        ReachLocation,
        DefeatEnemy,
        CollectItem,
        Interact,
        Talk,
        SolvePuzzle,
        Survive,
        Escort,
        ChooseDialogue,
        ObtainMemory
    }

    public enum QuestStatus
    {
        NotStarted,
        Active,
        Completed,
        Failed
    }

    /// <summary>
    /// One step of a quest. Objectives complete by id: whatever satisfies them —
    /// dialogue, a trigger volume, a memory pickup — reports the id to
    /// <see cref="QuestManager"/>, which is the only thing that knows the quest's shape.
    /// </summary>
    [Serializable]
    public class QuestObjective
    {
        [Tooltip("Unique within the quest. What the world reports to complete this step.")]
        public string ObjectiveId;

        [TextArea(1, 3)]
        public string Description;

        public ObjectiveType Type = ObjectiveType.Talk;

        [Tooltip("How many times the objective must be reported before it completes.")]
        [Min(1)]
        public int RequiredCount = 1;

        [Tooltip("Hidden from the quest log until completed, for spoiler-sensitive steps.")]
        public bool Hidden;

        [Tooltip("Optional: does not block quest completion.")]
        public bool Optional;

        [Tooltip("Flag set when this objective completes. Optional.")]
        public string CompletionFlag;
    }
}

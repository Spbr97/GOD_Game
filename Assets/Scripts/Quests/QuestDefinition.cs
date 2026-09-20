using System.Collections.Generic;
using UnityEngine;

namespace Game.Quests
{
    /// <summary>
    /// A quest as authored content (SPEC.md sections 23 and 48). This is immutable
    /// data; the player's progress through it lives in <see cref="QuestProgress"/>, so
    /// the same asset can be referenced by save data without being mutated at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "Quest_", menuName = "God Game/Quest")]
    public class QuestDefinition : ScriptableObject
    {
        [SerializeField] private string questId;
        [SerializeField] private string title = "Untitled Quest";

        [TextArea(2, 5)]
        [SerializeField] private string description;

        [Tooltip("Objectives are completed in order; the first incomplete one is the current objective.")]
        [SerializeField] private QuestObjective[] objectives;

        [Tooltip("All of these flags must be set before the quest can start.")]
        [SerializeField] private string[] requiredFlags;

        [Tooltip("Flags set when the quest completes (SPEC.md section 23, CompletionFlags).")]
        [SerializeField] private string[] completionFlags;

        [Tooltip("Flags that fail the quest if set while it is active (SPEC.md section 23, FailureConditions).")]
        [SerializeField] private string[] failureFlags;

        [TextArea(1, 3)]
        [Tooltip("Human-readable rewards. No inventory or progression system exists yet to grant them.")]
        [SerializeField] private string rewardsSummary;

        public string QuestId => string.IsNullOrEmpty(questId) ? name : questId;
        public string Title => title;
        public string Description => description;
        public IReadOnlyList<QuestObjective> Objectives => objectives;
        public IReadOnlyList<string> RequiredFlags => requiredFlags;
        public IReadOnlyList<string> CompletionFlags => completionFlags;
        public IReadOnlyList<string> FailureFlags => failureFlags;
        public string RewardsSummary => rewardsSummary;

        public int ObjectiveCount => objectives?.Length ?? 0;

        public QuestObjective GetObjective(string objectiveId)
        {
            if (objectives == null || string.IsNullOrEmpty(objectiveId))
            {
                return null;
            }

            for (var i = 0; i < objectives.Length; i++)
            {
                if (objectives[i] != null && objectives[i].ObjectiveId == objectiveId)
                {
                    return objectives[i];
                }
            }

            return null;
        }

        public int IndexOf(string objectiveId)
        {
            if (objectives == null || string.IsNullOrEmpty(objectiveId))
            {
                return -1;
            }

            for (var i = 0; i < objectives.Length; i++)
            {
                if (objectives[i] != null && objectives[i].ObjectiveId == objectiveId)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Replaces this quest's content. Used by editor tooling and tests.</summary>
        public void Configure(string id, string questTitle, string questDescription, QuestObjective[] questObjectives,
            string[] completion = null, string[] required = null, string[] failure = null, string rewards = null)
        {
            questId = id;
            title = questTitle;
            description = questDescription;
            objectives = questObjectives;
            completionFlags = completion;
            requiredFlags = required;
            failureFlags = failure;
            rewardsSummary = rewards;
        }
    }
}

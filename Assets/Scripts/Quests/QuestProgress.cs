using System.Collections.Generic;

namespace Game.Quests
{
    /// <summary>
    /// The player's state within one quest. Kept separate from
    /// <see cref="QuestDefinition"/> because the definition is a ScriptableObject:
    /// mutating it at runtime would write progress into the asset in the Editor and
    /// leak between play sessions.
    /// </summary>
    public class QuestProgress
    {
        private readonly Dictionary<string, int> counts = new();
        private readonly HashSet<string> completedObjectives = new();

        public QuestProgress(QuestDefinition definition)
        {
            Definition = definition;
            Status = QuestStatus.Active;
        }

        public QuestDefinition Definition { get; }
        public QuestStatus Status { get; internal set; }

        public bool IsObjectiveComplete(string objectiveId)
        {
            return !string.IsNullOrEmpty(objectiveId) && completedObjectives.Contains(objectiveId);
        }

        public int GetCount(string objectiveId)
        {
            return !string.IsNullOrEmpty(objectiveId) && counts.TryGetValue(objectiveId, out var value) ? value : 0;
        }

        /// <summary>
        /// The objective the quest log should show: the first incomplete, non-optional,
        /// non-hidden one. Null when nothing is left to display.
        /// </summary>
        public QuestObjective CurrentObjective
        {
            get
            {
                var objectives = Definition.Objectives;
                if (objectives == null)
                {
                    return null;
                }

                for (var i = 0; i < objectives.Count; i++)
                {
                    var objective = objectives[i];
                    if (objective == null || objective.Hidden || objective.Optional)
                    {
                        continue;
                    }

                    if (!IsObjectiveComplete(objective.ObjectiveId))
                    {
                        return objective;
                    }
                }

                return null;
            }
        }

        /// <summary>True when every required objective is done. Optional ones do not block.</summary>
        public bool AllRequiredObjectivesComplete
        {
            get
            {
                var objectives = Definition.Objectives;
                if (objectives == null || objectives.Count == 0)
                {
                    return true;
                }

                for (var i = 0; i < objectives.Count; i++)
                {
                    var objective = objectives[i];
                    if (objective != null && !objective.Optional && !IsObjectiveComplete(objective.ObjectiveId))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Records one report against an objective. Returns the new count; the caller
        /// decides whether that means the objective completed.
        /// </summary>
        internal int Increment(string objectiveId)
        {
            var next = GetCount(objectiveId) + 1;
            counts[objectiveId] = next;
            return next;
        }

        internal void MarkComplete(string objectiveId)
        {
            completedObjectives.Add(objectiveId);
        }
    }
}

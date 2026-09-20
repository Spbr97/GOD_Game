namespace Game.Quests
{
    /// <summary>EventBus payloads for quests (SPEC.md section 49).</summary>
    public readonly struct QuestStartedEvent
    {
        public readonly QuestDefinition Quest;

        public QuestStartedEvent(QuestDefinition quest)
        {
            Quest = quest;
        }
    }

    public readonly struct QuestObjectiveCompletedEvent
    {
        public readonly QuestDefinition Quest;
        public readonly QuestObjective Objective;

        public QuestObjectiveCompletedEvent(QuestDefinition quest, QuestObjective objective)
        {
            Quest = quest;
            Objective = objective;
        }
    }

    public readonly struct QuestObjectiveAdvancedEvent
    {
        public readonly QuestDefinition Quest;
        public readonly QuestObjective Objective;
        public readonly int Count;

        public QuestObjectiveAdvancedEvent(QuestDefinition quest, QuestObjective objective, int count)
        {
            Quest = quest;
            Objective = objective;
            Count = count;
        }
    }

    public readonly struct QuestCompletedEvent
    {
        public readonly QuestDefinition Quest;

        public QuestCompletedEvent(QuestDefinition quest)
        {
            Quest = quest;
        }
    }

    public readonly struct QuestFailedEvent
    {
        public readonly QuestDefinition Quest;
        public readonly string Reason;

        public QuestFailedEvent(QuestDefinition quest, string reason)
        {
            Quest = quest;
            Reason = reason;
        }
    }
}

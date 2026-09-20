namespace Game.Dialogue
{
    /// <summary>EventBus payloads for dialogue (SPEC.md section 49).</summary>
    public readonly struct DialogueStartedEvent
    {
        public readonly DialogueGraph Graph;
        public readonly string SpeakerName;

        public DialogueStartedEvent(DialogueGraph graph, string speakerName)
        {
            Graph = graph;
            SpeakerName = speakerName;
        }
    }

    public readonly struct DialogueNodeShownEvent
    {
        public readonly DialogueGraph Graph;
        public readonly DialogueNode Node;

        public DialogueNodeShownEvent(DialogueGraph graph, DialogueNode node)
        {
            Graph = graph;
            Node = node;
        }
    }

    public readonly struct DialogueChoiceMadeEvent
    {
        public readonly DialogueGraph Graph;
        public readonly DialogueNode Node;
        public readonly int ChoiceIndex;

        public DialogueChoiceMadeEvent(DialogueGraph graph, DialogueNode node, int choiceIndex)
        {
            Graph = graph;
            Node = node;
            ChoiceIndex = choiceIndex;
        }
    }

    /// <summary>
    /// Raised for each consequence the runner cannot apply itself.
    ///
    /// The runner handles flags directly because it already depends on Core, but
    /// quest and memory consequences are published instead: routing them through the
    /// bus keeps Dialogue from referencing the Quest and Memory systems, which would
    /// make it depend on every content system at once (SPEC.md section 47).
    /// </summary>
    public readonly struct DialogueConsequenceEvent
    {
        public readonly ConsequenceType Type;
        public readonly string Target;
        public readonly int Amount;

        public DialogueConsequenceEvent(ConsequenceType type, string target, int amount)
        {
            Type = type;
            Target = target;
            Amount = amount;
        }
    }

    public readonly struct DialogueCompletedEvent
    {
        public readonly DialogueGraph Graph;

        public DialogueCompletedEvent(DialogueGraph graph)
        {
            Graph = graph;
        }
    }
}

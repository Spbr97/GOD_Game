using Game.Core;
using UnityEngine;

namespace Game.Dialogue
{
    /// <summary>
    /// Walks a <see cref="DialogueGraph"/> one node at a time and applies each node's
    /// consequences (SPEC.md section 22, TASK 003).
    ///
    /// This is the logic only — it holds no UI references and draws nothing.
    /// <c>DialogueUI</c> subscribes to the events and renders them, so dialogue can be
    /// driven headlessly by tests and by the debug console.
    /// </summary>
    public class DialogueRunner : MonoBehaviour
    {
        /// <summary>
        /// The exact string SPEC.md section 50 requires when dialogue is missing.
        /// Public and const so a test asserts on the requirement itself rather than
        /// on a copy of it.
        /// </summary>
        public const string UnavailableText = "[Dialogue unavailable]";

        private const string FallbackGraphId = "DIALOGUE_UNAVAILABLE";
        private const string FallbackNodeId = "UNAVAILABLE";

        private static DialogueGraph fallbackGraph;

        private static DialogueRunner instance;

        /// <summary>Resolved on first access; see <see cref="Game.Core.SceneSingleton"/>.</summary>
        public static DialogueRunner Instance => SceneSingleton.Resolve(ref instance);

        [Tooltip("Prefix for the WorldState counter that stores relationship values.")]
        [SerializeField] private string relationshipCounterPrefix = "REL_";

        public bool IsRunning => CurrentNode != null;
        public DialogueGraph CurrentGraph { get; private set; }
        public DialogueNode CurrentNode { get; private set; }

        /// <summary>Speaker shown for nodes that do not name one, e.g. the NPC's own name.</summary>
        public string DefaultSpeaker { get; private set; }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                GameLogger.LogWarning(LogCategory.Dialogue, "A second DialogueRunner was destroyed.", this);
                Destroy(this);
                return;
            }

            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        /// <summary>
        /// Starts a conversation.
        ///
        /// Returns false when the graph is missing or has no usable node, or when a
        /// conversation is already running — starting a second conversation over the
        /// first would strand the first one's consequences.
        ///
        /// A missing graph still puts <see cref="UnavailableText"/> on screen, because
        /// SPEC.md section 50 requires it: an NPC who silently does nothing reads as a
        /// game that ignored the button, not as content that is not finished. It
        /// returns false anyway, and that is the point — the caller's own consequences
        /// (<see cref="NpcInteractable"/>'s "first spoken" flag, and anything a quest
        /// hangs off it) must not fire for a conversation that did not actually
        /// happen. A display fallback is not a conversation.
        ///
        /// A link to a node that does not exist mid-conversation is a different
        /// failure and stays as it was: the conversation ends cleanly. The player has
        /// already read real lines by then, so ending is honest, whereas interrupting
        /// them with an error string is not.
        /// </summary>
        public bool Begin(DialogueGraph graph, string defaultSpeaker = null)
        {
            if (IsRunning)
            {
                GameLogger.LogWarning(LogCategory.Dialogue, "Ignored a dialogue start while one was already running.", this);
                return false;
            }

            if (graph == null)
            {
                GameLogger.LogFallback(
                    LogCategory.Dialogue,
                    "could not start dialogue",
                    "DialogueRunner.Begin",
                    "no dialogue graph was supplied",
                    $"showing \"{UnavailableText}\" so the failure is visible, and applying none of the caller's consequences",
                    this);
                ShowUnavailable(defaultSpeaker);
                return false;
            }

            var entry = graph.GetEntryNode();
            if (entry == null)
            {
                GameLogger.LogFallback(
                    LogCategory.Dialogue,
                    $"could not start dialogue '{graph.GraphId}'",
                    "DialogueRunner.Begin",
                    "the graph has no usable entry node",
                    $"showing \"{UnavailableText}\" so the failure is visible, and applying none of the caller's consequences",
                    this);
                ShowUnavailable(defaultSpeaker);
                return false;
            }

            CurrentGraph = graph;
            DefaultSpeaker = defaultSpeaker;

            GameLogger.Log(LogCategory.Dialogue, $"Dialogue '{graph.GraphId}' started.", this);
            EventBus.Publish(new DialogueStartedEvent(graph, defaultSpeaker));

            Show(entry);
            return true;
        }

        /// <summary>
        /// Runs the one-line fallback conversation (SPEC.md section 50's missing
        /// dialogue). It is a real conversation on a real graph rather than a special
        /// case threaded through the UI, so <c>DialogueUI</c>, the advance key, the
        /// player-control lock and the completion event all behave exactly as they do
        /// for authored dialogue — the player dismisses it the normal way instead of
        /// being stuck in front of a panel nothing knows how to close.
        /// </summary>
        private void ShowUnavailable(string defaultSpeaker)
        {
            var graph = FallbackGraph();

            CurrentGraph = graph;
            DefaultSpeaker = defaultSpeaker;

            EventBus.Publish(new DialogueStartedEvent(graph, defaultSpeaker));
            Show(graph.GetEntryNode());
        }

        /// <summary>
        /// The fallback graph, built in code because it must exist even when the
        /// project's dialogue assets are what failed to load.
        ///
        /// Deliberately <c>== null</c> rather than <c>??=</c>: a domain reload
        /// destroys the cached ScriptableObject, and C#'s <c>is null</c> pattern —
        /// which <c>??=</c> compiles to — does not see Unity's "fake null", so the
        /// destroyed instance would be handed out forever. Same reason as
        /// <see cref="Game.VFX.VfxSpawner"/>'s shared material.
        /// </summary>
        private static DialogueGraph FallbackGraph()
        {
            if (fallbackGraph == null)
            {
                fallbackGraph = ScriptableObject.CreateInstance<DialogueGraph>();
                fallbackGraph.name = FallbackGraphId;

                // Not an asset and not owned by a scene, so say so: without this Unity
                // reports it as a leaked ScriptableObject when the domain reloads
                // between test runs.
                fallbackGraph.hideFlags = HideFlags.HideAndDontSave;
                fallbackGraph.Configure(
                    FallbackGraphId,
                    new[] { FallbackNodeId },
                    new[] { new DialogueNode { DialogueId = FallbackNodeId, Text = UnavailableText } });
            }

            return fallbackGraph;
        }

        /// <summary>Whether what is on screen is the missing-dialogue fallback rather than authored content.</summary>
        public bool IsShowingUnavailable => CurrentGraph != null && CurrentGraph.GraphId == FallbackGraphId;

        /// <summary>
        /// Advances a node that has no choices. Nodes with choices ignore this and wait
        /// for <see cref="Choose"/>, so a mashed advance key cannot skip a decision.
        /// </summary>
        public bool Advance()
        {
            if (!IsRunning || CurrentNode.HasChoices)
            {
                return false;
            }

            GoTo(CurrentNode.NextDialogueId);
            return true;
        }

        /// <summary>Takes one of the current node's choices by index.</summary>
        public bool Choose(int choiceIndex)
        {
            if (!IsRunning || !CurrentNode.HasChoices)
            {
                return false;
            }

            var choices = CurrentNode.Choices;
            if (choiceIndex < 0 || choiceIndex >= choices.Length)
            {
                GameLogger.LogWarning(LogCategory.Dialogue, $"Choice index {choiceIndex} is out of range.", this);
                return false;
            }

            var choice = choices[choiceIndex];
            if (!IsChoiceAvailable(choice))
            {
                GameLogger.LogWarning(LogCategory.Dialogue, "Ignored a choice whose requirements are not met.", this);
                return false;
            }

            var node = CurrentNode;
            ApplyConsequences(choice.Consequences);
            EventBus.Publish(new DialogueChoiceMadeEvent(CurrentGraph, node, choiceIndex));

            GoTo(choice.NextDialogueId);
            return true;
        }

        /// <summary>Whether a choice's flag gates pass and it should be offered.</summary>
        public static bool IsChoiceAvailable(DialogueChoice choice)
        {
            if (choice == null)
            {
                return false;
            }

            var state = WorldState.Instance;
            if (state == null)
            {
                return (choice.RequiredFlags == null || choice.RequiredFlags.Length == 0)
                       && (choice.BlockingFlags == null || choice.BlockingFlags.Length == 0);
            }

            return state.HasAllFlags(choice.RequiredFlags) && state.HasNoneOfFlags(choice.BlockingFlags);
        }

        /// <summary>Ends the conversation early, applying nothing further.</summary>
        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            var graph = CurrentGraph;
            CurrentNode = null;
            CurrentGraph = null;
            DefaultSpeaker = null;

            GameLogger.Log(LogCategory.Dialogue, $"Dialogue '{graph.GraphId}' completed.", this);
            EventBus.Publish(new DialogueCompletedEvent(graph));
        }

        private void GoTo(string nextId)
        {
            if (string.IsNullOrEmpty(nextId))
            {
                Stop();
                return;
            }

            var next = CurrentGraph.GetNode(nextId);
            if (next == null)
            {
                GameLogger.LogFallback(
                    LogCategory.Dialogue,
                    $"could not follow the link to node '{nextId}'",
                    $"dialogue graph '{CurrentGraph.GraphId}'",
                    "no node in the graph has that id",
                    "the conversation ends cleanly instead of hanging",
                    this);
                Stop();
                return;
            }

            Show(next);
        }

        private void Show(DialogueNode node)
        {
            CurrentNode = node;
            ApplyConsequences(node.Consequences);
            EventBus.Publish(new DialogueNodeShownEvent(CurrentGraph, node));
        }

        private void ApplyConsequences(DialogueConsequence[] consequences)
        {
            if (consequences == null)
            {
                return;
            }

            for (var i = 0; i < consequences.Length; i++)
            {
                var consequence = consequences[i];
                if (consequence == null || string.IsNullOrEmpty(consequence.Target))
                {
                    continue;
                }

                switch (consequence.Type)
                {
                    case ConsequenceType.SetFlag:
                        WorldState.Instance?.SetFlag(consequence.Target, true);
                        break;

                    case ConsequenceType.ClearFlag:
                        WorldState.Instance?.SetFlag(consequence.Target, false);
                        break;

                    case ConsequenceType.ChangeRelationship:
                        // Relationships are stored as WorldState counters rather than in
                        // their own system: nothing in TASK 003 reads them back yet, and
                        // a counter is honest about that. See KNOWN_ISSUES.md.
                        WorldState.Instance?.AddToCounter(relationshipCounterPrefix + consequence.Target, consequence.Amount);
                        break;

                    default:
                        EventBus.Publish(new DialogueConsequenceEvent(consequence.Type, consequence.Target, consequence.Amount));
                        break;
                }
            }
        }
    }
}

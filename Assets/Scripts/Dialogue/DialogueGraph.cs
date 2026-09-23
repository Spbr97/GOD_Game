using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Dialogue
{
    /// <summary>
    /// A whole conversation as a ScriptableObject (SPEC.md section 48). Nodes are a
    /// flat list keyed by <see cref="DialogueNode.DialogueId"/>; the "graph" is the
    /// NextDialogueId links between them, which keeps the asset editable as a plain
    /// list in the Inspector without a custom graph editor.
    /// </summary>
    [CreateAssetMenu(fileName = "Dialogue_", menuName = "God Game/Dialogue Graph")]
    public class DialogueGraph : ScriptableObject
    {
        [Tooltip("Identifies this conversation in logs and save data.")]
        [SerializeField] private string graphId;

        [Tooltip("Nodes tried in order as entry points; the first eligible one starts the conversation.")]
        [SerializeField] private string[] entryNodeIds;

        [SerializeField] private DialogueNode[] nodes;

        private Dictionary<string, DialogueNode> lookup;

        public string GraphId => string.IsNullOrEmpty(graphId) ? name : graphId;
        public IReadOnlyList<DialogueNode> Nodes => nodes;
        public IReadOnlyList<string> EntryNodeIds => entryNodeIds;

        /// <summary>
        /// Returns the node with this id, or null. The index is built once and thrown
        /// away on domain reload with the asset, so authoring changes are picked up.
        /// </summary>
        public DialogueNode GetNode(string dialogueId)
        {
            if (string.IsNullOrEmpty(dialogueId))
            {
                return null;
            }

            BuildLookup();
            return lookup.TryGetValue(dialogueId, out var node) ? node : null;
        }

        /// <summary>
        /// The first entry node whose flag requirements are met, so an NPC says
        /// something different after the quest is given than before it.
        ///
        /// Falls back to the first node in the list when no entry node is eligible, so
        /// a misauthored asset produces the wrong line rather than a silent NPC
        /// (SPEC.md section 50: every system must fail gracefully).
        /// </summary>
        public DialogueNode GetEntryNode()
        {
            BuildLookup();

            if (entryNodeIds != null)
            {
                for (var i = 0; i < entryNodeIds.Length; i++)
                {
                    var node = GetNode(entryNodeIds[i]);
                    if (node != null && IsEligible(node))
                    {
                        return node;
                    }
                }
            }

            if (nodes == null || nodes.Length == 0)
            {
                GameLogger.LogError(LogCategory.Dialogue, $"Dialogue graph '{GraphId}' has no nodes.", this);
                return null;
            }

            for (var i = 0; i < nodes.Length; i++)
            {
                if (IsEligible(nodes[i]))
                {
                    return nodes[i];
                }
            }

            GameLogger.LogFallback(
                LogCategory.Dialogue,
                "no eligible entry node",
                $"DialogueGraph '{GraphId}'",
                "every node's flag requirements failed",
                "using the first node so the NPC still speaks",
                this);

            return nodes[0];
        }

        /// <summary>
        /// Answers "what state is memory X in?" for <see cref="DialogueNode.RequiredMemoryState"/>.
        ///
        /// A hook rather than a direct call so Dialogue does not reference the Memory
        /// system (SPEC.md section 47). The Memory system installs it on startup; while
        /// it is null, memory gates are treated as unsatisfied, which hides
        /// memory-specific lines rather than showing them wrongly.
        /// </summary>
        public static System.Func<string, string> MemoryStateResolver { get; set; }

        /// <summary>Whether a node's requirements currently pass.</summary>
        public static bool IsEligible(DialogueNode node)
        {
            if (node == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(node.RequiredMemoryId) && !MemoryGatePasses(node))
            {
                return false;
            }

            var state = WorldState.Instance;
            if (state == null)
            {
                return (node.RequiredFlags == null || node.RequiredFlags.Length == 0)
                       && (node.BlockingFlags == null || node.BlockingFlags.Length == 0);
            }

            return state.HasAllFlags(node.RequiredFlags) && state.HasNoneOfFlags(node.BlockingFlags);
        }

        private static bool MemoryGatePasses(DialogueNode node)
        {
            if (string.IsNullOrEmpty(node.RequiredMemoryState))
            {
                return true;
            }

            if (MemoryStateResolver == null)
            {
                return false;
            }

            var actual = MemoryStateResolver(node.RequiredMemoryId);
            return string.Equals(actual, node.RequiredMemoryState, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Replaces this graph's content. Used by editor tooling and tests.</summary>
        public void Configure(string id, string[] entryIds, DialogueNode[] graphNodes)
        {
            graphId = id;
            entryNodeIds = entryIds;
            nodes = graphNodes;
            lookup = null;
        }

        private void BuildLookup()
        {
            if (lookup != null)
            {
                return;
            }

            lookup = new Dictionary<string, DialogueNode>();
            if (nodes == null)
            {
                return;
            }

            for (var i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node == null || string.IsNullOrEmpty(node.DialogueId))
                {
                    continue;
                }

                if (!lookup.TryAdd(node.DialogueId, node))
                {
                    GameLogger.LogWarning(
                        LogCategory.Dialogue,
                        $"Dialogue graph '{GraphId}' has more than one node with id '{node.DialogueId}'; the first wins.",
                        this);
                }
            }
        }

        private void OnValidate()
        {
            lookup = null;
        }
    }
}

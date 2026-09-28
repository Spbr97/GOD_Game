using System;
using System.Collections.Generic;
using Game.Dialogue;
using UnityEditor;

namespace Game.EditorTools
{
    /// <summary>
    /// The dialogue half of <see cref="ContentValidation"/> (TASK 036, folded into the
    /// wider validator by TASK 042).
    ///
    /// Kept as its own file because graph reachability is a different kind of check from
    /// the rest — it walks a structure rather than comparing ids against a set — and
    /// because "validate the dialogue" is a thing an author asks for on its own while
    /// writing a conversation.
    ///
    /// It reports <see cref="ContentIssue"/>s rather than logging, so the build, the tests
    /// and the menu item all see the same result.
    /// </summary>
    public static class DialogueValidation
    {
        [MenuItem("God Game/Validate Dialogue")]
        public static void ValidateFromMenu()
        {
            ContentValidation.Report(Validate(ContentCatalogue.Load()), "Dialogue validation");
        }

        public static List<ContentIssue> Validate(ContentCatalogue catalogue)
        {
            var issues = new List<ContentIssue>();

            foreach (var graph in catalogue.Graphs)
            {
                ValidateGraph(issues, graph, catalogue);
            }

            return issues;
        }

        private static void ValidateGraph(List<ContentIssue> issues, DialogueGraph graph, ContentCatalogue catalogue)
        {
            var subject = graph.GraphId;

            if (graph.Nodes == null || graph.Nodes.Count == 0)
            {
                issues.Add(new ContentIssue(ContentSeverity.Error, "dialogue-shape", subject,
                    "has no nodes", graph));
                return;
            }

            var nodes = new Dictionary<string, DialogueNode>();

            foreach (var node in graph.Nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.DialogueId))
                {
                    issues.Add(new ContentIssue(ContentSeverity.Error, "missing-id", subject,
                        "contains a node without an id", graph));
                    continue;
                }

                if (!nodes.TryAdd(node.DialogueId, node))
                {
                    issues.Add(new ContentIssue(ContentSeverity.Error, "duplicate-id", subject,
                        $"two nodes share the id '{node.DialogueId}'", graph));
                }
            }

            var reached = Walk(issues, graph, nodes);

            foreach (var node in graph.Nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.DialogueId))
                {
                    continue;
                }

                if (!reached.Contains(node.DialogueId))
                {
                    issues.Add(new ContentIssue(ContentSeverity.Warning, "dialogue-reach", subject,
                        $"node '{node.DialogueId}' cannot be reached from any entry point", graph));

                    // An unreachable node's own links were never followed, so check them
                    // here — a dangling link in dead content is still a link that will
                    // dangle the day the content is reconnected.
                    CheckLink(issues, graph, node.NextDialogueId, nodes);
                }

                if (!string.IsNullOrEmpty(node.RequiredMemoryId) && !catalogue.MemoryIds.Contains(node.RequiredMemoryId))
                {
                    issues.Add(new ContentIssue(ContentSeverity.Error, "unknown-id", subject,
                        $"node '{node.DialogueId}' is gated on memory '{node.RequiredMemoryId}', which does not exist",
                        graph));
                }

                CheckFlags(issues, graph, node.RequiredFlags, catalogue);
                CheckFlags(issues, graph, node.BlockingFlags, catalogue);
                CheckConsequences(issues, graph, node.Consequences, catalogue);

                if (node.Choices == null)
                {
                    continue;
                }

                foreach (var choice in node.Choices)
                {
                    if (choice == null)
                    {
                        continue;
                    }

                    if (!reached.Contains(node.DialogueId))
                    {
                        CheckLink(issues, graph, choice.NextDialogueId, nodes);
                    }

                    if (!string.IsNullOrEmpty(choice.ObjectiveId) && !catalogue.ObjectiveIds.Contains(choice.ObjectiveId))
                    {
                        issues.Add(new ContentIssue(ContentSeverity.Error, "unknown-id", subject,
                            $"a choice reports objective '{choice.ObjectiveId}', which no quest defines", graph));
                    }

                    CheckFlags(issues, graph, choice.RequiredFlags, catalogue);
                    CheckFlags(issues, graph, choice.BlockingFlags, catalogue);
                    CheckConsequences(issues, graph, choice.Consequences, catalogue);
                }
            }
        }

        private static HashSet<string> Walk(List<ContentIssue> issues, DialogueGraph graph,
            Dictionary<string, DialogueNode> nodes)
        {
            var reached = new HashSet<string>();
            var pending = new Queue<string>();

            if (graph.EntryNodeIds != null && graph.EntryNodeIds.Count > 0)
            {
                foreach (var id in graph.EntryNodeIds)
                {
                    if (!nodes.ContainsKey(id))
                    {
                        issues.Add(new ContentIssue(ContentSeverity.Error, "dialogue-shape", graph.GraphId,
                            $"names '{id}' as an entry point, but there is no such node", graph));
                    }
                    else
                    {
                        pending.Enqueue(id);
                    }
                }
            }
            else if (graph.Nodes[0] != null)
            {
                pending.Enqueue(graph.Nodes[0].DialogueId);
            }

            while (pending.Count > 0)
            {
                var id = pending.Dequeue();

                if (!reached.Add(id) || !nodes.TryGetValue(id, out var node))
                {
                    continue;
                }

                Follow(issues, graph, node.NextDialogueId, nodes, pending);

                if (node.Choices == null)
                {
                    continue;
                }

                foreach (var choice in node.Choices)
                {
                    if (choice != null)
                    {
                        Follow(issues, graph, choice.NextDialogueId, nodes, pending);
                    }
                }
            }

            return reached;
        }

        private static void Follow(List<ContentIssue> issues, DialogueGraph graph, string id,
            Dictionary<string, DialogueNode> nodes, Queue<string> pending)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            if (!nodes.ContainsKey(id))
            {
                issues.Add(new ContentIssue(ContentSeverity.Error, "dialogue-link", graph.GraphId,
                    $"links to '{id}', which is not a node in this graph", graph));
            }
            else
            {
                pending.Enqueue(id);
            }
        }

        private static void CheckLink(List<ContentIssue> issues, DialogueGraph graph, string id,
            Dictionary<string, DialogueNode> nodes)
        {
            if (!string.IsNullOrEmpty(id) && !nodes.ContainsKey(id))
            {
                issues.Add(new ContentIssue(ContentSeverity.Error, "dialogue-link", graph.GraphId,
                    $"links to '{id}', which is not a node in this graph", graph));
            }
        }

        private static void CheckFlags(List<ContentIssue> issues, DialogueGraph graph, string[] flags,
            ContentCatalogue catalogue)
        {
            if (flags == null)
            {
                return;
            }

            foreach (var flag in flags)
            {
                if (!string.IsNullOrEmpty(flag) && !catalogue.KnownFlags.Contains(flag))
                {
                    issues.Add(new ContentIssue(ContentSeverity.Warning, "unknown-flag", graph.GraphId,
                        $"reads flag '{flag}', which nothing ever sets", graph));
                }
            }
        }

        private static void CheckConsequences(List<ContentIssue> issues, DialogueGraph graph,
            DialogueConsequence[] consequences, ContentCatalogue catalogue)
        {
            if (consequences == null)
            {
                return;
            }

            foreach (var item in consequences)
            {
                if (item == null || !Enum.IsDefined(typeof(ConsequenceType), item.Type)
                                 || string.IsNullOrWhiteSpace(item.Target))
                {
                    issues.Add(new ContentIssue(ContentSeverity.Error, "dialogue-shape", graph.GraphId,
                        "has a consequence with no type or no target", graph));
                    continue;
                }

                switch (item.Type)
                {
                    case ConsequenceType.StartQuest when !catalogue.QuestIds.Contains(item.Target):
                        issues.Add(new ContentIssue(ContentSeverity.Error, "unknown-id", graph.GraphId,
                            $"starts quest '{item.Target}', which does not exist", graph));
                        break;

                    case ConsequenceType.CompleteObjective when !catalogue.ObjectiveIds.Contains(item.Target):
                        issues.Add(new ContentIssue(ContentSeverity.Error, "unknown-id", graph.GraphId,
                            $"completes objective '{item.Target}', which no quest defines", graph));
                        break;

                    case ConsequenceType.DiscoverMemory when !catalogue.MemoryIds.Contains(item.Target):
                        issues.Add(new ContentIssue(ContentSeverity.Error, "unknown-id", graph.GraphId,
                            $"discovers memory '{item.Target}', which does not exist", graph));
                        break;
                }
            }
        }
    }
}

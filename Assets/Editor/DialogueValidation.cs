using System;
using System.Collections.Generic;
using Game.Dialogue;
using Game.Memory;
using Game.Quests;
using UnityEditor;
using UnityEngine;

public static class DialogueValidation
{
    [MenuItem("God Game/Validate Dialogue")]
    public static void ValidateAll()
    {
        var graphs = LoadAll<DialogueGraph>();
        var quests = LoadAll<QuestDefinition>();
        var memories = LoadAll<MemoryFragment>();
        var questIds = new HashSet<string>();
        var objectiveIds = new HashSet<string>();
        var memoryIds = new HashSet<string>();
        var knownFlags = new HashSet<string>();
        foreach (var quest in quests)
        {
            questIds.Add(quest.QuestId);
            Add(knownFlags, quest.CompletionFlags);
            if (quest.Objectives == null) continue;
            foreach (var objective in quest.Objectives)
            {
                if (objective == null) continue;
                objectiveIds.Add(objective.ObjectiveId);
                if (!string.IsNullOrEmpty(objective.CompletionFlag)) knownFlags.Add(objective.CompletionFlag);
            }
        }
        foreach (var memory in memories)
        {
            memoryIds.Add(memory.MemoryId);
            if (!string.IsNullOrEmpty(memory.DiscoveryFlag)) knownFlags.Add(memory.DiscoveryFlag);
        }
        foreach (var graph in graphs)
        {
            if (graph.Nodes == null) continue;
            foreach (var node in graph.Nodes)
            {
                if (node == null) continue;
                GatherFlags(node.Consequences, knownFlags);
                if (node.Choices == null) continue;
                foreach (var choice in node.Choices)
                    if (choice != null) GatherFlags(choice.Consequences, knownFlags);
            }
        }

        var errors = 0;
        var warnings = 0;
        foreach (var graph in graphs)
        {
            var nodes = new Dictionary<string, DialogueNode>();
            if (graph.Nodes == null || graph.Nodes.Count == 0)
            {
                Error(graph, "has no nodes", ref errors);
                continue;
            }
            foreach (var node in graph.Nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.DialogueId))
                {
                    Error(graph, "contains a node without an id", ref errors);
                    continue;
                }
                if (!nodes.TryAdd(node.DialogueId, node))
                    Error(graph, "duplicate node id " + node.DialogueId, ref errors);
            }
            var reached = new HashSet<string>();
            var pending = new Queue<string>();
            if (graph.EntryNodeIds != null && graph.EntryNodeIds.Count > 0)
            {
                foreach (var id in graph.EntryNodeIds)
                {
                    if (!nodes.ContainsKey(id)) Error(graph, "missing entry node " + id, ref errors);
                    else pending.Enqueue(id);
                }
            }
            else if (graph.Nodes[0] != null) pending.Enqueue(graph.Nodes[0].DialogueId);

            while (pending.Count > 0)
            {
                var id = pending.Dequeue();
                if (!reached.Add(id) || !nodes.TryGetValue(id, out var node)) continue;
                Follow(graph, node.NextDialogueId, nodes, pending, ref errors);
                if (node.Choices == null) continue;
                foreach (var choice in node.Choices)
                    if (choice != null) Follow(graph, choice.NextDialogueId, nodes, pending, ref errors);
            }
            foreach (var node in graph.Nodes)
            {
                if (node == null) continue;
                if (!reached.Contains(node.DialogueId))
                {
                    Warning(graph, "unreachable node " + node.DialogueId, ref warnings);
                    CheckLink(graph, node.NextDialogueId, nodes, ref errors);
                }
                if (!string.IsNullOrEmpty(node.RequiredMemoryId) && !memoryIds.Contains(node.RequiredMemoryId))
                    Error(graph, "unknown gated memory " + node.RequiredMemoryId, ref errors);
                CheckFlags(graph, node.RequiredFlags, knownFlags, ref warnings);
                CheckFlags(graph, node.BlockingFlags, knownFlags, ref warnings);
                CheckConsequences(graph, node.Consequences, questIds, objectiveIds, memoryIds, ref errors);
                if (node.Choices == null) continue;
                foreach (var choice in node.Choices)
                {
                    if (choice == null) continue;
                    if (!reached.Contains(node.DialogueId)) CheckLink(graph, choice.NextDialogueId, nodes, ref errors);
                    if (!string.IsNullOrEmpty(choice.ObjectiveId) && !objectiveIds.Contains(choice.ObjectiveId))
                        Error(graph, "unknown choice objective " + choice.ObjectiveId, ref errors);
                    CheckFlags(graph, choice.RequiredFlags, knownFlags, ref warnings);
                    CheckFlags(graph, choice.BlockingFlags, knownFlags, ref warnings);
                    CheckConsequences(graph, choice.Consequences, questIds, objectiveIds, memoryIds, ref errors);
                }
            }
        }
        Debug.Log($"Dialogue validation: {graphs.Count} graphs, {errors} errors, {warnings} warnings.");
    }

    private static List<T> LoadAll<T>() where T : UnityEngine.Object
    {
        var result = new List<T>();
        foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) result.Add(asset);
        }
        return result;
    }

    private static void Follow(DialogueGraph graph, string id, Dictionary<string, DialogueNode> nodes,
        Queue<string> pending, ref int errors)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!nodes.ContainsKey(id)) Error(graph, "dangling link to " + id, ref errors);
        else pending.Enqueue(id);
    }

    private static void CheckLink(DialogueGraph graph, string id, Dictionary<string, DialogueNode> nodes, ref int errors)
    {
        if (!string.IsNullOrEmpty(id) && !nodes.ContainsKey(id))
            Error(graph, "dangling link to " + id, ref errors);
    }

    private static void Add(HashSet<string> flags, IReadOnlyList<string> values)
    {
        if (values == null) return;
        foreach (var flag in values) if (!string.IsNullOrEmpty(flag)) flags.Add(flag);
    }

    private static void GatherFlags(DialogueConsequence[] consequences, HashSet<string> flags)
    {
        if (consequences == null) return;
        foreach (var item in consequences)
            if (item != null && (item.Type == ConsequenceType.SetFlag || item.Type == ConsequenceType.ClearFlag)
                && !string.IsNullOrEmpty(item.Target)) flags.Add(item.Target);
    }

    private static void CheckFlags(DialogueGraph graph, string[] flags, HashSet<string> known, ref int warnings)
    {
        if (flags == null) return;
        foreach (var flag in flags)
            if (!string.IsNullOrEmpty(flag) && !known.Contains(flag))
                Warning(graph, "unknown flag " + flag, ref warnings);
    }

    private static void CheckConsequences(DialogueGraph graph, DialogueConsequence[] consequences,
        HashSet<string> quests, HashSet<string> objectives, HashSet<string> memories, ref int errors)
    {
        if (consequences == null) return;
        foreach (var item in consequences)
        {
            if (item == null || !Enum.IsDefined(typeof(ConsequenceType), item.Type)
                || string.IsNullOrWhiteSpace(item.Target))
            {
                Error(graph, "invalid or empty consequence", ref errors);
                continue;
            }
            if (item.Type == ConsequenceType.StartQuest && !quests.Contains(item.Target))
                Error(graph, "unknown quest " + item.Target, ref errors);
            if (item.Type == ConsequenceType.CompleteObjective && !objectives.Contains(item.Target))
                Error(graph, "unknown objective " + item.Target, ref errors);
            if (item.Type == ConsequenceType.DiscoverMemory && !memories.Contains(item.Target))
                Error(graph, "unknown memory " + item.Target, ref errors);
        }
    }

    private static void Error(DialogueGraph graph, string message, ref int count)
    {
        count++;
        Debug.LogError($"{graph.GraphId}: {message}", graph);
    }
    private static void Warning(DialogueGraph graph, string message, ref int count)
    {
        count++;
        Debug.LogWarning($"{graph.GraphId}: {message}", graph);
    }
}
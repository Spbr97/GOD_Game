using System;
using Game.Core;
using Game.Inventory;
using Game.Memory;
using Game.Progression;
using UnityEngine;

namespace Game.Quests
{
    public enum QuestRewardType
    {
        Item,
        AbilityUnlock,
        MemoryRestore,
        SkillPoints,
        WorldFlag
    }

    /// <summary>A concrete reward granted once when its quest completes.</summary>
    [Serializable]
    public class QuestReward
    {
        public QuestRewardType Type;
        public InventoryItem Item;
        [Min(1)] public int Amount = 1;
        public string TargetId;
        [Range(0f, 1f)] public float IntegrityAmount;
        public bool FlagValue = true;

        public bool Grant()
        {
            switch (Type)
            {
                case QuestRewardType.Item:
                    if (Item == null || InventoryManager.Instance == null) return false;
                    InventoryManager.Instance.Add(Item, Mathf.Max(1, Amount));
                    return true;
                case QuestRewardType.AbilityUnlock:
                    if (string.IsNullOrEmpty(TargetId) || WorldState.Instance == null) return false;
                    WorldState.Instance.SetFlag("ABILITY_UNLOCKED_" + TargetId);
                    return true;
                case QuestRewardType.MemoryRestore:
                    if (MemoryManager.Instance == null) return false;
                    if (!string.IsNullOrEmpty(TargetId))
                    {
                        var memory = MemoryManager.Instance.Find(TargetId);
                        if (memory == null || !MemoryManager.Instance.IsDiscovered(TargetId)) return false;
                        MemoryManager.Instance.SetState(memory, MemoryState.Restored);
                    }
                    if (IntegrityAmount > 0f) MemoryManager.Instance.RestoreIntegrity(IntegrityAmount);
                    return true;
                case QuestRewardType.SkillPoints:
                    if (WorldState.Instance == null) return false;
                    WorldState.Instance.AddToCounter(SkillTreeManager.SkillPointsFlag, Mathf.Max(1, Amount));
                    return true;
                case QuestRewardType.WorldFlag:
                    if (string.IsNullOrEmpty(TargetId) || WorldState.Instance == null) return false;
                    WorldState.Instance.SetFlag(TargetId, FlagValue);
                    return true;
                default:
                    return false;
            }
        }
    }
}
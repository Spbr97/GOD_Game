using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Progression
{
    /// <summary>
    /// The skill tree (SPEC.md section 30): four branches, unlocked with points earned
    /// through play and spent by the player. Points live as an ordinary
    /// <see cref="WorldState"/> counter (<see cref="SkillPointsFlag"/>) rather than a
    /// new save field, the same way <see cref="Game.Memory.MemoryManager"/>'s discovery
    /// flags reuse world flags instead of inventing per-system storage — the save
    /// system already round-trips counters generically. Which skills are unlocked is
    /// the one genuinely new piece of state, and fills <c>SaveData.Abilities</c>
    /// (TASK 016), reserved since the save format was written.
    ///
    /// Scene-scoped like <see cref="Game.Quests.QuestManager"/>/<see cref="Game.Memory.MemoryManager"/>
    /// (see ARCHITECTURE.md's "Manager lookup"), not a persistent singleton: its state
    /// is restored fresh by <see cref="Game.Save.SaveManager.Apply"/> on every scene load.
    /// </summary>
    public class SkillTreeManager : MonoBehaviour
    {
        /// <summary>The WorldState counter key skill points are kept under.</summary>
        public const string SkillPointsFlag = "SKILL_POINTS";

        private static SkillTreeManager instance;

        /// <summary>Resolved on first access; see <see cref="SceneSingleton"/>.</summary>
        public static SkillTreeManager Instance => SceneSingleton.Resolve(ref instance);

        [Tooltip("Every skill that exists, so it can be resolved by id from save data and the UI.")]
        [SerializeField] private SkillDefinition[] catalogue;

        [Tooltip("Points granted when a quest completes.")]
        [SerializeField] private int pointsPerQuestCompleted = 1;

        [Tooltip("Points granted when a boss is defeated.")]
        [SerializeField] private int pointsPerBossDefeated = 2;

        private readonly Dictionary<string, SkillDefinition> byId = new();
        private readonly HashSet<string> unlocked = new();

        public int AvailablePoints => WorldState.Instance != null ? WorldState.Instance.GetCounter(SkillPointsFlag) : 0;

        public IReadOnlyCollection<string> UnlockedIds => unlocked;

        /// <summary>Every skill id in the catalogue, for the UI to list by branch.</summary>
        public IReadOnlyCollection<string> CatalogueIds => byId.Keys;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                GameLogger.LogWarning(LogCategory.Game, "A second SkillTreeManager was destroyed.", this);
                Destroy(this);
                return;
            }

            instance = this;
            BuildCatalogue();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<Game.Quests.QuestCompletedEvent>(OnQuestCompleted);
            EventBus.Subscribe<Game.AI.BossDefeatedEvent>(OnBossDefeated);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<Game.Quests.QuestCompletedEvent>(OnQuestCompleted);
            EventBus.Unsubscribe<Game.AI.BossDefeatedEvent>(OnBossDefeated);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public SkillDefinition Find(string skillId)
        {
            return !string.IsNullOrEmpty(skillId) && byId.TryGetValue(skillId, out var skill) ? skill : null;
        }

        public bool IsUnlocked(string skillId) => !string.IsNullOrEmpty(skillId) && unlocked.Contains(skillId);

        /// <summary>Whether this skill could be unlocked right now: exists, not already unlocked, prerequisite met, affordable.</summary>
        public bool CanUnlock(string skillId)
        {
            var skill = Find(skillId);
            if (skill == null || IsUnlocked(skillId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(skill.PrerequisiteId) && !IsUnlocked(skill.PrerequisiteId))
            {
                return false;
            }

            return AvailablePoints >= skill.Cost;
        }

        /// <summary>Spends points and unlocks the skill. Returns false and changes nothing if it could not be unlocked.</summary>
        public bool Unlock(string skillId)
        {
            if (!CanUnlock(skillId))
            {
                return false;
            }

            var skill = Find(skillId);
            WorldState.Instance?.AddToCounter(SkillPointsFlag, -skill.Cost);
            unlocked.Add(skillId);

            GameLogger.Log(LogCategory.Game, $"Skill unlocked: {skill.DisplayName} ({skillId}).", this);
            EventBus.Publish(new SkillUnlockedEvent(skill));
            EventBus.Publish(new SkillBonusesChangedEvent());
            return true;
        }

        /// <summary>
        /// The sum of <see cref="SkillDefinition.EffectValue"/> across every unlocked
        /// skill of this type. Zero when none are unlocked, so a caller can always add
        /// this to a base value without a null check.
        /// </summary>
        public float GetBonus(SkillEffectType effectType)
        {
            var total = 0f;
            foreach (var skillId in unlocked)
            {
                var skill = Find(skillId);
                if (skill != null && skill.EffectType == effectType)
                {
                    total += skill.EffectValue;
                }
            }

            return total;
        }

        /// <summary>
        /// Puts the unlocked set back where a save left it, for restoring — no points
        /// are spent (the save already reflects a spent state) and no
        /// <see cref="SkillUnlockedEvent"/> fires, only <see cref="SkillBonusesChangedEvent"/>,
        /// the same "fact, not a moment" distinction <c>MemoryManager.RestoreState</c>
        /// draws against <c>Discover</c>.
        /// </summary>
        public void RestoreUnlocked(IEnumerable<string> skillIds)
        {
            unlocked.Clear();
            if (skillIds != null)
            {
                foreach (var id in skillIds)
                {
                    if (Find(id) != null)
                    {
                        unlocked.Add(id);
                    }
                }
            }

            EventBus.Publish(new SkillBonusesChangedEvent());
        }

        private void OnQuestCompleted(Game.Quests.QuestCompletedEvent completed)
        {
            if (pointsPerQuestCompleted != 0)
            {
                WorldState.Instance?.AddToCounter(SkillPointsFlag, pointsPerQuestCompleted);
            }
        }

        private void OnBossDefeated(Game.AI.BossDefeatedEvent defeated)
        {
            if (pointsPerBossDefeated != 0)
            {
                WorldState.Instance?.AddToCounter(SkillPointsFlag, pointsPerBossDefeated);
            }
        }

        private void BuildCatalogue()
        {
            byId.Clear();
            if (catalogue == null)
            {
                return;
            }

            for (var i = 0; i < catalogue.Length; i++)
            {
                if (catalogue[i] != null)
                {
                    byId[catalogue[i].SkillId] = catalogue[i];
                }
            }
        }

        /// <summary>Test and tooling seam for supplying the catalogue without the Inspector.</summary>
        public void Configure(SkillDefinition[] skills)
        {
            catalogue = skills;
            BuildCatalogue();
        }
    }
}

using System.Collections;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.Memory;
using Game.Player;
using Game.Progression;
using Game.Quests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Play
{
    /// <summary>
    /// The skill tree (SPEC.md section 30, TASK 016) against a live scene: points
    /// earned from real milestones, and the four wired effects actually changing what
    /// their systems do (the other eight are declared but unwired; see KNOWN_ISSUES.md).
    /// </summary>
    public class SkillTreePlayModeTests
    {
        private TestArena arena;

        [SetUp]
        public void SetUp()
        {
            arena = new TestArena();
            arena.EnsureWorldState();
        }

        [TearDown]
        public void TearDown()
        {
            arena.Dispose();
        }

        private SkillDefinition NewSkill(string id, SkillBranch branch, int cost, SkillEffectType type, float value)
        {
            var skill = arena.TrackAsset(ScriptableObject.CreateInstance<SkillDefinition>());
            skill.Configure(id, id, branch, cost, type, value);
            return skill;
        }

        [UnityTest]
        public IEnumerator SkillTree_QuestCompletionGrantsAPoint()
        {
            var quest = arena.TrackAsset(ScriptableObject.CreateInstance<QuestDefinition>());
            quest.Configure("Q_SKILL", "Skill Quest", "",
                new[] { new QuestObjective { ObjectiveId = "DO_IT", Description = "Do it", RequiredCount = 1 } });

            var quests = arena.SpawnQuestManager(quest);
            var tree = arena.SpawnSkillTreeManager();
            yield return null;

            quests.StartQuest("Q_SKILL");
            Assert.AreEqual(0, tree.AvailablePoints);

            quests.ReportObjective("DO_IT");
            yield return null;

            Assert.AreEqual(1, tree.AvailablePoints, "Completing a quest should have granted a skill point.");
        }

        [UnityTest]
        public IEnumerator SkillTree_BossDefeatGrantsTwoPoints()
        {
            var tree = arena.SpawnSkillTreeManager();
            yield return null;

            var bossGo = arena.Track(new GameObject("Boss"));
            bossGo.SetActive(false);
            var health = bossGo.AddComponent<HealthComponent>();
            health.Configure(10f);
            bossGo.AddComponent<BossController>();
            bossGo.SetActive(true);
            yield return null;

            Assert.AreEqual(0, tree.AvailablePoints);

            health.Kill(DamageData.Create(999f, null));
            yield return null;

            Assert.AreEqual(2, tree.AvailablePoints, "Defeating a boss should have granted two skill points.");
        }

        [UnityTest]
        public IEnumerator SkillTree_AttackDamageSkillIncreasesWeaponDamage()
        {
            var skill = NewSkill("DMG", SkillBranch.Warrior, 1, SkillEffectType.AttackDamageMultiplier, 0.5f);
            var tree = arena.SpawnSkillTreeManager(skill);
            WorldState.Instance.AddToCounter(SkillTreeManager.SkillPointsFlag, 1);

            var player = arena.SpawnPlayer(Vector3.zero, withWeapon: true);
            var dummy = arena.SpawnDummy("Dummy", new Vector3(0f, 0f, 1f));
            yield return null;

            player.Weapon.TrySwing(AttackType.Light);
            yield return TestArena.Until(() => player.WeaponHitbox.IsActive, "the hitbox to open");
            yield return null;

            var baselineDamage = dummy.Health.MaxHealth - dummy.Health.CurrentHealth;
            Assert.Greater(baselineDamage, 0f, "Sanity check: the swing should have dealt some damage.");

            // Let the first swing's recovery finish, heal back up, unlock the skill,
            // and swing again with the same base attack.
            yield return TestArena.Until(() => !player.Weapon.IsSwinging, "the first swing to finish");
            dummy.Health.RestoreTo(dummy.Health.MaxHealth);
            Assert.IsTrue(tree.Unlock("DMG"));
            yield return null;

            player.Weapon.TrySwing(AttackType.Light);
            yield return TestArena.Until(() => player.WeaponHitbox.IsActive, "the hitbox to open again");
            yield return null;

            var boostedDamage = dummy.Health.MaxHealth - dummy.Health.CurrentHealth;
            Assert.Greater(boostedDamage, baselineDamage * 1.4f,
                $"A 50% damage skill should noticeably increase the swing's damage (baseline {baselineDamage}, boosted {boostedDamage}).");
        }

        [UnityTest]
        public IEnumerator SkillTree_MemoryManipulationSkillReducesCorruptionCost()
        {
            var memory = arena.TrackAsset(ScriptableObject.CreateInstance<MemoryFragment>());
            memory.Configure("MEM_X", "Test Memory", "", "Owner", MemoryCategory.Personal, MemoryImportance.Optional);
            var memories = arena.SpawnMemoryManager(memory);
            memories.Discover(memory);

            var skill = NewSkill("MANIP", SkillBranch.Memory, 1, SkillEffectType.MemoryCorruptionCostMultiplier, -0.5f);
            var tree = arena.SpawnSkillTreeManager(skill);
            WorldState.Instance.AddToCounter(SkillTreeManager.SkillPointsFlag, 1);
            tree.Unlock("MANIP");
            yield return null;

            memories.Corrupt(memory, 0.2f);

            Assert.AreEqual(0.9f, memories.Integrity, 0.0001f, "A 50% cost reduction on a 0.2 cost should only spend 0.1.");
        }

        [UnityTest]
        public IEnumerator PlayerProgressionStats_HealthAndDivineEnergySkillsRaiseTheCaps()
        {
            var healthSkill = NewSkill("HP", SkillBranch.Guardian, 1, SkillEffectType.MaxHealthBonus, 25f);
            var energySkill = NewSkill("NRG", SkillBranch.Divine, 1, SkillEffectType.MaxDivineEnergyBonus, 15f);
            var tree = arena.SpawnSkillTreeManager(healthSkill, energySkill);
            WorldState.Instance.AddToCounter(SkillTreeManager.SkillPointsFlag, 2);

            var player = arena.SpawnPlayer(Vector3.zero);
            player.Root.SetActive(false);
            var stats = player.Root.AddComponent<PlayerProgressionStats>();
            player.Root.SetActive(true);
            yield return null;

            Assert.AreEqual(100f, player.Health.MaxHealth, 0.001f, "Sanity check: base max health before any skill.");

            Assert.IsTrue(tree.Unlock("HP"));
            yield return null;
            Assert.AreEqual(125f, player.Health.MaxHealth, 0.001f);

            Assert.IsTrue(tree.Unlock("NRG"));
            yield return null;
            Assert.AreEqual(115f, player.DivineEnergy.MaxEnergy, 0.001f);

            // Unlocking one must not disturb the other's already-applied bonus.
            Assert.AreEqual(125f, player.Health.MaxHealth, 0.001f);
        }

        [UnityTest]
        public IEnumerator Inventory_DiscoveringADivineMemoryGrantsADivineMark()
        {
            var mark = arena.TrackAsset(ScriptableObject.CreateInstance<Game.Inventory.InventoryItem>());
            mark.Configure("MARK", "Divine Mark", Game.Inventory.ItemCategory.DivineMark);

            var divineMemory = arena.TrackAsset(ScriptableObject.CreateInstance<MemoryFragment>());
            divineMemory.Configure("MEM_D", "Divine Memory", "", "Owner", MemoryCategory.Divine, MemoryImportance.Supporting);

            var personalMemory = arena.TrackAsset(ScriptableObject.CreateInstance<MemoryFragment>());
            personalMemory.Configure("MEM_P", "Personal Memory", "", "Owner", MemoryCategory.Personal, MemoryImportance.Optional);

            var memories = arena.SpawnMemoryManager(divineMemory, personalMemory);
            var inventoryGo = arena.Track(new GameObject("Inventory"));
            inventoryGo.SetActive(false);
            var inventory = inventoryGo.AddComponent<Game.Inventory.InventoryManager>();
            inventory.Configure(new[] { mark }, mark);
            inventoryGo.SetActive(true);
            yield return null;

            memories.Discover(personalMemory);
            yield return null;
            Assert.AreEqual(0, inventory.GetCount(mark), "A non-Divine memory must not grant a mark.");

            memories.Discover(divineMemory);
            yield return null;
            Assert.AreEqual(1, inventory.GetCount(mark));
        }
    }
}

using System.Collections.Generic;
using Game.Combat;
using Game.Core;
using Game.Inventory;
using Game.Memory;
using Game.Progression;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// The skill tree (SPEC.md section 30) and inventory (section 28), TASK 016.
    ///
    /// Runs in an empty scene of its own for the same reason <c>PuzzleTests</c> and
    /// <c>MemoryTollTests</c> do — <see cref="SkillTreeManager.Instance"/>,
    /// <see cref="InventoryManager.Instance"/> and <see cref="WorldState.Instance"/>
    /// all resolve by searching loaded scenes.
    /// </summary>
    public class ProgressionTests
    {
        private readonly List<GameObject> spawned = new();
        private readonly List<Object> assets = new();
        private string previousScenePath;

        [OneTimeSetUp]
        public void OpenIsolatedScene()
        {
            previousScenePath = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [OneTimeTearDown]
        public void RestorePreviousScene()
        {
            if (!string.IsNullOrEmpty(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }

        [TearDown]
        public void Cleanup()
        {
            for (var i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    Object.DestroyImmediate(spawned[i]);
                }
            }

            for (var i = 0; i < assets.Count; i++)
            {
                if (assets[i] != null)
                {
                    Object.DestroyImmediate(assets[i]);
                }
            }

            spawned.Clear();
            assets.Clear();
            EventBus.Clear();
        }

        private T NewComponent<T>(string goName) where T : Component
        {
            var go = new GameObject(goName);
            spawned.Add(go);
            return go.AddComponent<T>();
        }

        private T NewAsset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;
        }

        // ------------------------------------------------------------------- skills

        private SkillDefinition NewSkill(string id, SkillBranch branch, int cost, SkillEffectType type, float value, string prerequisite = null)
        {
            var skill = NewAsset<SkillDefinition>();
            skill.Configure(id, id, branch, cost, type, value, prerequisite);
            return skill;
        }

        private (WorldState, SkillTreeManager) NewSkillTree(int startingPoints, params SkillDefinition[] skills)
        {
            var world = NewComponent<WorldState>("WorldState");
            if (startingPoints != 0)
            {
                world.AddToCounter(SkillTreeManager.SkillPointsFlag, startingPoints);
            }

            var manager = NewComponent<SkillTreeManager>("SkillTree");
            manager.Configure(skills);
            return (world, manager);
        }

        [Test]
        public void SkillTree_CannotUnlockWithoutEnoughPoints()
        {
            var skill = NewSkill("A", SkillBranch.Warrior, 3, SkillEffectType.AttackDamageMultiplier, 0.1f);
            var (_, tree) = NewSkillTree(2, skill);

            Assert.IsFalse(tree.CanUnlock("A"));
            Assert.IsFalse(tree.Unlock("A"));
            Assert.IsFalse(tree.IsUnlocked("A"));
        }

        [Test]
        public void SkillTree_UnlockSpendsPointsAndMarksUnlocked()
        {
            var skill = NewSkill("A", SkillBranch.Warrior, 2, SkillEffectType.AttackDamageMultiplier, 0.1f);
            var (world, tree) = NewSkillTree(3, skill);

            Assert.IsTrue(tree.Unlock("A"));

            Assert.IsTrue(tree.IsUnlocked("A"));
            Assert.AreEqual(1, world.GetCounter(SkillTreeManager.SkillPointsFlag));
            Assert.IsFalse(tree.Unlock("A"), "An already-unlocked skill must refuse a second unlock.");
            Assert.AreEqual(1, world.GetCounter(SkillTreeManager.SkillPointsFlag), "A refused unlock must not spend points.");
        }

        [Test]
        public void SkillTree_RequiresPrerequisite()
        {
            var first = NewSkill("A", SkillBranch.Warrior, 1, SkillEffectType.AttackDamageMultiplier, 0.1f);
            var second = NewSkill("B", SkillBranch.Warrior, 1, SkillEffectType.ComboWindowBonusSeconds, 0.2f, "A");
            var (_, tree) = NewSkillTree(5, first, second);

            Assert.IsFalse(tree.CanUnlock("B"), "B requires A first.");

            tree.Unlock("A");
            Assert.IsTrue(tree.CanUnlock("B"));
            Assert.IsTrue(tree.Unlock("B"));
        }

        [Test]
        public void SkillTree_GetBonusSumsAcrossUnlockedSkillsOfTheSameType()
        {
            var a = NewSkill("A", SkillBranch.Warrior, 1, SkillEffectType.AttackDamageMultiplier, 0.1f);
            var b = NewSkill("B", SkillBranch.Guardian, 1, SkillEffectType.AttackDamageMultiplier, 0.2f);
            var c = NewSkill("C", SkillBranch.Divine, 1, SkillEffectType.MaxDivineEnergyBonus, 50f);
            var (_, tree) = NewSkillTree(3, a, b, c);

            Assert.AreEqual(0f, tree.GetBonus(SkillEffectType.AttackDamageMultiplier), 0.0001f);

            tree.Unlock("A");
            tree.Unlock("B");
            Assert.AreEqual(0.3f, tree.GetBonus(SkillEffectType.AttackDamageMultiplier), 0.0001f);
            Assert.AreEqual(0f, tree.GetBonus(SkillEffectType.MaxDivineEnergyBonus), 0.0001f, "Unlocking A and B must not affect an unrelated effect type.");
        }

        [Test]
        public void SkillTree_RestoreUnlockedDoesNotSpendPoints()
        {
            var skill = NewSkill("A", SkillBranch.Memory, 4, SkillEffectType.MemoryCorruptionCostMultiplier, -0.2f);
            var (world, tree) = NewSkillTree(0, skill);

            tree.RestoreUnlocked(new[] { "A" });

            Assert.IsTrue(tree.IsUnlocked("A"));
            Assert.AreEqual(0, world.GetCounter(SkillTreeManager.SkillPointsFlag), "Restoring must not touch the points counter.");
        }

        [Test]
        public void SkillTree_RestoreUnlockedDropsUnknownIds()
        {
            var (_, tree) = NewSkillTree(0);

            Assert.DoesNotThrow(() => tree.RestoreUnlocked(new[] { "GHOST" }));
            Assert.IsFalse(tree.IsUnlocked("GHOST"));
        }

        // ---------------------------------------------------------------- inventory

        private InventoryItem NewItem(string id, ItemCategory category, bool stackable = true, float heal = 0f)
        {
            var item = NewAsset<InventoryItem>();
            item.Configure(id, id, category, stackable, heal);
            return item;
        }

        private InventoryManager NewInventory(params InventoryItem[] items)
        {
            var manager = NewComponent<InventoryManager>("Inventory");
            manager.Configure(items);
            return manager;
        }

        [Test]
        public void Inventory_AddAndRemoveTracksCount()
        {
            var potion = NewItem("POTION", ItemCategory.Consumable);
            var inventory = NewInventory(potion);

            inventory.Add(potion, 2);
            Assert.AreEqual(2, inventory.GetCount(potion));

            inventory.Add(potion);
            Assert.AreEqual(3, inventory.GetCount(potion));

            Assert.AreEqual(2, inventory.Remove(potion, 2));
            Assert.AreEqual(1, inventory.GetCount(potion));

            Assert.AreEqual(1, inventory.Remove(potion, 5), "Removing more than held should clamp to what is actually held.");
            Assert.AreEqual(0, inventory.GetCount(potion));
        }

        [Test]
        public void Inventory_NonStackableItemCapsAtOne()
        {
            var blade = NewItem("BLADE", ItemCategory.Weapon, stackable: false);
            var inventory = NewInventory(blade);

            inventory.Add(blade, 5);
            Assert.AreEqual(1, inventory.GetCount(blade), "A non-stackable item must never read as more than one.");
        }

        [Test]
        public void Inventory_UseConsumableHealsAndDecrements()
        {
            var draught = NewItem("DRAUGHT", ItemCategory.Consumable, heal: 30f);
            var inventory = NewInventory(draught);
            inventory.Add(draught);

            var playerGo = NewComponent<HealthComponent>("Player").gameObject;
            playerGo.AddComponent<PlayerDeath>();
            var health = playerGo.GetComponent<HealthComponent>();
            health.Configure(100f);
            health.TakeDamage(DamageData.Create(50f, null));
            Assert.AreEqual(50f, health.CurrentHealth, 0.001f);

            Assert.IsTrue(inventory.UseConsumable(draught));

            Assert.AreEqual(80f, health.CurrentHealth, 0.001f);
            Assert.AreEqual(0, inventory.GetCount(draught));
        }

        [Test]
        public void Inventory_UseConsumableRefusesWhenNoneHeld()
        {
            var draught = NewItem("DRAUGHT", ItemCategory.Consumable, heal: 30f);
            var inventory = NewInventory(draught);

            Assert.IsFalse(inventory.UseConsumable(draught));
        }

        // Inventory_DiscoveringADivineMemoryGrantsADivineMark lives in
        // SkillTreePlayModeTests.cs instead: InventoryManager's OnMemoryDiscovered
        // subscription is wired in OnEnable, which EditMode does not call for a
        // component added by AddComponent (see AvarshaTests's note on this exact
        // gotcha, and TASK 011's Ember Step tests moving for the same reason).

        [Test]
        public void Inventory_CaptureAndRestoreEntriesRoundTrip()
        {
            var potion = NewItem("POTION", ItemCategory.Consumable);
            var blade = NewItem("BLADE", ItemCategory.Weapon, stackable: false);
            var inventory = NewInventory(potion, blade);

            inventory.Add(potion, 3);
            inventory.Add(blade);

            var captured = inventory.CaptureEntries();

            var fresh = NewInventory(potion, blade);
            fresh.RestoreEntries(captured);

            Assert.AreEqual(3, fresh.GetCount(potion));
            Assert.AreEqual(1, fresh.GetCount(blade));
        }
    }
}

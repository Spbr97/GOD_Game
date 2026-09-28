using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Combat;
using Game.Core;
using Game.Memory;
using Game.Progression;
using Game.Save;
using Game.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Play
{
    /// <summary>
    /// TASK 043's gate, in tests: **every currently selectable skill changes play and
    /// survives save/load.**
    ///
    /// Before this task, eight of the twelve skills in SPEC.md section 30 could be
    /// unlocked, cost a point, appeared in the tree and did nothing at all. That is worse
    /// than a missing skill — the player spends a finite resource on it.
    ///
    /// Each test here measures a real quantity with the skill locked, unlocks it, and
    /// measures again. Not "the bonus sums correctly" — that is arithmetic, and it passed
    /// for all twelve while eight of them were inert. The measurement has to be taken
    /// from the system that would actually use it.
    /// </summary>
    public class SkillEffectPlayModeTests
    {
        private TestArena arena;
        private SkillTreeManager skills;
        private readonly List<SkillDefinition> catalogue = new();

        [SetUp]
        public void SetUp()
        {
            arena = new TestArena();
            arena.EnsureWorldState();
            catalogue.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            arena.Dispose();
            catalogue.Clear();
        }

        /// <summary>
        /// One skill, with the same id, type and value as the shipped asset of that name.
        /// Built here rather than loaded so a test cannot be made to pass by editing the
        /// content it is meant to be checking.
        /// </summary>
        private SkillDefinition Skill(string id, SkillEffectType type, float value, SkillBranch branch = SkillBranch.Warrior)
        {
            var skill = arena.TrackAsset(ScriptableObject.CreateInstance<SkillDefinition>());
            skill.name = "Skill_" + id;
            skill.Configure(id, id, branch, 1, type, value);
            catalogue.Add(skill);
            return skill;
        }

        private SkillTreeManager Tree(int points = 10)
        {
            skills = arena.SpawnSkillTreeManager(catalogue.ToArray());
            WorldState.Instance.AddToCounter(SkillTreeManager.SkillPointsFlag, points);
            return skills;
        }

        // ------------------------------------------------------------- Warrior branch

        [UnityTest]
        public IEnumerator WarriorCombo_WidensTheComboWindow()
        {
            Skill("WARRIOR_COMBO", SkillEffectType.ComboWindowBonusSeconds, 0.3f);
            var tree = Tree();

            var player = arena.SpawnPlayer(Vector3.zero, withWeapon: true, withCombatInput: true);
            yield return null;

            var before = player.Combat.ComboWindowInUse;
            Assert.IsTrue(tree.Unlock("WARRIOR_COMBO"));
            yield return null;

            Assert.That(player.Combat.ComboWindowInUse, Is.EqualTo(before + 0.3f).Within(0.001f),
                "the combo window must widen, and the tracker is built once so the change has to reach it");
        }

        [UnityTest]
        public IEnumerator WarriorParry_WidensTheParryWindow()
        {
            Skill("WARRIOR_PARRY", SkillEffectType.ParryWindowMultiplier, 0.2f);
            var tree = Tree();

            var player = arena.SpawnPlayer(Vector3.zero, withCombatInput: true);
            var guard = player.Root.GetComponent<GuardController>();
            yield return null;

            var before = guard.ScaledParryWindow;
            Assert.IsTrue(tree.Unlock("WARRIOR_PARRY"));

            Assert.That(guard.ScaledParryWindow, Is.EqualTo(before * 1.2f).Within(0.0001f));
            Assert.Greater(guard.ScaledPerfectParryWindow, 0f, "the perfect window scales with it");
        }

        // ------------------------------------------------------------ Guardian branch

        [UnityTest]
        public IEnumerator GuardianBlock_MakesBlockingCostLessStamina()
        {
            Skill("GUARDIAN_BLOCK", SkillEffectType.BlockReductionBonus, 0.1f, SkillBranch.Guardian);
            var tree = Tree();

            var player = arena.SpawnPlayer(Vector3.zero, withCombatInput: true);
            var guard = player.Root.GetComponent<GuardController>();
            yield return null;

            var before = guard.ScaledStaminaPerDamageBlocked;
            Assert.IsTrue(tree.Unlock("GUARDIAN_BLOCK"));

            Assert.Less(guard.ScaledStaminaPerDamageBlocked, before,
                "a block-reduction bonus must make blocking cheaper, not dearer");
            Assert.That(guard.ScaledStaminaPerDamageBlocked, Is.EqualTo(before * 0.9f).Within(0.0001f));
        }

        /// <summary>
        /// Measured by hitting the player, not by reading a number: this effect is applied
        /// inside <c>Hurtbox</c>, and the thing that could go wrong is it being applied to
        /// the wrong faction.
        /// </summary>
        [UnityTest]
        public IEnumerator GuardianReduction_MakesThePlayerTakeLessDamage()
        {
            Skill("GUARDIAN_REDUCTION", SkillEffectType.IncomingDamageMultiplier, -0.1f, SkillBranch.Guardian);
            var tree = Tree();

            var player = arena.SpawnPlayer(Vector3.zero);
            yield return null;

            var health = player.Health;
            var hurtbox = player.Hurtbox;

            hurtbox.ApplyDamage(new DamageData { Amount = 50f, Type = DamageType.Physical });
            var unskilled = 100f - health.CurrentHealth;

            health.RestoreTo(100f);
            Assert.IsTrue(tree.Unlock("GUARDIAN_REDUCTION"));

            hurtbox.ApplyDamage(new DamageData { Amount = 50f, Type = DamageType.Physical });
            var skilled = 100f - health.CurrentHealth;

            Assert.Less(skilled, unskilled, "the player should now take less from the same hit");
            Assert.That(skilled, Is.EqualTo(unskilled * 0.9f).Within(0.01f));
        }

        /// <summary>
        /// The mistake this catches: applying the reduction in <c>HealthComponent</c>,
        /// which every enemy shares, so the player's own attacks would be weakened too.
        /// </summary>
        [UnityTest]
        public IEnumerator GuardianReduction_DoesNotProtectEnemies()
        {
            Skill("GUARDIAN_REDUCTION", SkillEffectType.IncomingDamageMultiplier, -0.5f, SkillBranch.Guardian);
            var tree = Tree();
            arena.SpawnPlayer(Vector3.zero);

            var dummy = arena.SpawnDummy("Dummy", new Vector3(5f, 0f, 0f), health: 100f);
            yield return null;

            Assert.IsTrue(tree.Unlock("GUARDIAN_REDUCTION"));

            dummy.Hurtboxes[0].ApplyDamage(new DamageData { Amount = 40f, Type = DamageType.Physical });

            Assert.That(dummy.Health.CurrentHealth, Is.EqualTo(60f).Within(0.01f),
                "an enemy must take the full hit; the skill protects the player only");
        }

        // -------------------------------------------------------------- Divine branch

        [UnityTest]
        public IEnumerator DivineCooldown_ShortensTheAbilityCooldown()
        {
            Skill("DIVINE_COOLDOWN", SkillEffectType.AbilityCooldownMultiplier, -0.2f, SkillBranch.Divine);
            var tree = Tree();

            var player = arena.SpawnPlayer(Vector3.zero, withCombatInput: true);
            yield return null;

            var before = player.Combat.ScaledAbilityCooldown;
            Assert.IsTrue(tree.Unlock("DIVINE_COOLDOWN"));

            Assert.Less(player.Combat.ScaledAbilityCooldown, before);
            Assert.That(player.Combat.ScaledAbilityCooldown, Is.EqualTo(before * 0.8f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator DivinePower_SpeedsUpTheAbilityDash()
        {
            Skill("DIVINE_POWER", SkillEffectType.AbilityDashSpeedMultiplier, 0.2f, SkillBranch.Divine);
            var tree = Tree();

            var player = arena.SpawnPlayer(Vector3.zero, withCombatInput: true);
            yield return null;

            var before = player.Combat.ScaledAbilityDashSpeed;
            Assert.IsTrue(tree.Unlock("DIVINE_POWER"));

            Assert.That(player.Combat.ScaledAbilityDashSpeed, Is.EqualTo(before * 1.2f).Within(0.0001f));
        }

        // -------------------------------------------------------------- Memory branch

        [UnityTest]
        public IEnumerator MemoryDetection_LetsAMemoryBeSensedFromFurtherAway()
        {
            Skill("MEMORY_DETECTION", SkillEffectType.MemoryDetectionRangeMultiplier, 0.5f, SkillBranch.Memory);
            var tree = Tree();
            arena.SpawnPlayer(Vector3.zero);

            var fragment = arena.TrackAsset(ScriptableObject.CreateInstance<MemoryFragment>());
            fragment.Configure("MEM_TEST", "A memory", "Something remembered", "Nobody",
                MemoryCategory.Personal, MemoryImportance.Optional);

            var go = arena.Track(new GameObject("Memory_Test"));
            go.SetActive(false);
            go.AddComponent<SphereCollider>().isTrigger = true;
            var pickup = go.AddComponent<MemoryPickup>();
            pickup.ConfigureInteraction("Recover", "A memory", 2f);
            go.SetActive(true);
            yield return null;

            var before = pickup.InteractionRange;
            Assert.IsTrue(tree.Unlock("MEMORY_DETECTION"));

            Assert.That(pickup.InteractionRange, Is.EqualTo(before * 1.5f).Within(0.0001f));
        }

        /// <summary>The same skill must not also let the player talk to NPCs from across a room.</summary>
        [UnityTest]
        public IEnumerator MemoryDetection_DoesNotWidenOrdinaryInteractions()
        {
            Skill("MEMORY_DETECTION", SkillEffectType.MemoryDetectionRangeMultiplier, 0.5f, SkillBranch.Memory);
            var tree = Tree();
            arena.SpawnPlayer(Vector3.zero);

            var go = arena.Track(new GameObject("Door"));
            go.SetActive(false);
            go.AddComponent<BoxCollider>().isTrigger = true;
            var exit = go.AddComponent<SceneExit>();
            exit.ConfigureInteraction("Enter", "a door", 2f);
            go.SetActive(true);
            yield return null;

            Assert.IsTrue(tree.Unlock("MEMORY_DETECTION"));

            Assert.That(exit.InteractionRange, Is.EqualTo(2f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator MemoryRestoration_BringsAForgottenMemoryBackSooner()
        {
            Skill("MEMORY_RESTORATION", SkillEffectType.EmberStepForgetRestoreMultiplier, -0.3f, SkillBranch.Memory);
            var tree = Tree();
            arena.SpawnPlayer(Vector3.zero);

            var memories = arena.SpawnMemoryManager();
            yield return null;

            var before = memories.ScaledForgetSeconds;
            Assert.IsTrue(tree.Unlock("MEMORY_RESTORATION"));

            Assert.Less(memories.ScaledForgetSeconds, before);
            Assert.That(memories.ScaledForgetSeconds, Is.EqualTo(before * 0.7f).Within(0.001f));
        }

        // ------------------------------------------------------ the rest of the gate

        /// <summary>
        /// The other half of TASK 043's gate: survives save/load. Unlocks one skill of
        /// every effect type, writes a save, wipes the tree, loads, and checks the
        /// measured effect is back — not merely that the id is in a list.
        /// </summary>
        [UnityTest]
        public IEnumerator EverySkillEffectSurvivesASaveAndLoad()
        {
            foreach (SkillEffectType type in System.Enum.GetValues(typeof(SkillEffectType)))
            {
                Skill("SKILL_" + type, type, type.ToString().EndsWith("Bonus") ? 5f : 0.25f);
            }

            var tree = Tree(points: 100);
            var player = arena.SpawnPlayer(Vector3.zero, withCombatInput: true);
            var guard = player.Root.GetComponent<GuardController>();
            var memories = arena.SpawnMemoryManager();

            var root = Path.Combine(Path.GetTempPath(), "GodGameSkillSave", Path.GetRandomFileName());
            Directory.CreateDirectory(root);

            var saveObject = arena.Track(new GameObject("SaveManager"));
            saveObject.SetActive(false);
            var saves = saveObject.AddComponent<SaveManager>();
            saves.Configure(root, autoSave: false);
            saveObject.SetActive(true);
            yield return null;

            foreach (var skill in catalogue)
            {
                Assert.IsTrue(tree.Unlock(skill.SkillId), $"could not unlock {skill.SkillId}");
            }

            yield return null;

            var expected = new Dictionary<string, float>
            {
                { "combo", player.Combat.ComboWindowInUse },
                { "parry", guard.ScaledParryWindow },
                { "block", guard.ScaledStaminaPerDamageBlocked },
                { "cooldown", player.Combat.ScaledAbilityCooldown },
                { "dash", player.Combat.ScaledAbilityDashSpeed },
                { "forget", memories.ScaledForgetSeconds }
            };

            Assert.IsTrue(saves.Save(SaveSlot.Manual));

            // As a scene reload would leave it: the tree is empty and every effect is back
            // at its unskilled value.
            tree.RestoreUnlocked(new string[0]);
            yield return null;

            Assert.AreNotEqual(expected["combo"], player.Combat.ComboWindowInUse,
                "staging: clearing the tree should have changed the measured values");

            Assert.IsTrue(saves.Load(SaveSlot.Manual));
            yield return null;

            Assert.That(player.Combat.ComboWindowInUse, Is.EqualTo(expected["combo"]).Within(0.0001f));
            Assert.That(guard.ScaledParryWindow, Is.EqualTo(expected["parry"]).Within(0.0001f));
            Assert.That(guard.ScaledStaminaPerDamageBlocked, Is.EqualTo(expected["block"]).Within(0.0001f));
            Assert.That(player.Combat.ScaledAbilityCooldown, Is.EqualTo(expected["cooldown"]).Within(0.0001f));
            Assert.That(player.Combat.ScaledAbilityDashSpeed, Is.EqualTo(expected["dash"]).Within(0.0001f));
            Assert.That(memories.ScaledForgetSeconds, Is.EqualTo(expected["forget"]).Within(0.001f));

            Directory.Delete(root, true);
        }

        /// <summary>
        /// Guards the whole point of the task. If a skill is added to the enum without a
        /// system reading it, this fails — which is the state eight of them were in before
        /// TASK 043.
        /// </summary>
        [Test]
        public void EverySkillEffectTypeIsReadBySomeSystem()
        {
            var sources = new[]
            {
                "Assets/Scripts/Combat", "Assets/Scripts/Memory", "Assets/Scripts/Player",
                "Assets/Scripts/World", "Assets/Scripts/Progression"
            };

            var text = string.Concat(sources
                .Where(Directory.Exists)
                .SelectMany(folder => Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
                .Where(file => !file.EndsWith("SkillDefinition.cs") && !file.EndsWith("SkillTreeManager.cs"))
                .Select(File.ReadAllText));

            var unread = System.Enum.GetNames(typeof(SkillEffectType))
                .Where(name => !text.Contains("SkillEffectType." + name))
                .ToList();

            Assert.IsEmpty(unread,
                "These skills can be unlocked with a finite resource and change nothing:\n  "
                + string.Join("\n  ", unread));
        }
    }
}

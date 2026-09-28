using System.Collections;
using System.IO;
using Game.Combat;
using Game.Combat.Abilities;
using Game.Core;
using Game.Progression;
using Game.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Play
{
    /// <summary>
    /// A second divine ability, written the way TASK 045–050 will write theirs: an
    /// ability id, a readiness rule, and what it does. Nothing about unlocking, paying,
    /// cooling down or saving.
    ///
    /// Its existence in this file is the test. If a temple's ability needed more than
    /// this, the contract would not be a contract.
    /// </summary>
    public class TestTideStepEffect : MonoBehaviour, IDivineAbilityEffect
    {
        public const string Id = "TIDE_STEP";

        public int Performed;
        public bool Blocked;

        public string AbilityId => Id;

        public bool CanPerform => !Blocked;

        public void Perform(DivineAbilityDefinition definition) => Performed++;
    }

    /// <summary>
    /// The shared divine-ability contract (SPEC.md sections 8 and 16, TASK 043).
    ///
    /// Before this, Ember Step's unlock, cost and cooldown were six fields on
    /// <c>CombatController</c>. That works for one ability and is the wrong shape for
    /// seven: each temple would have arrived with its own copy of the same three checks,
    /// each free to drift. These tests hold the shared half still.
    /// </summary>
    public class DivineAbilityPlayModeTests
    {
        private TestArena arena;

        [SetUp]
        public void SetUp()
        {
            arena = new TestArena();
            arena.EnsureWorldState();
        }

        [TearDown]
        public void TearDown() => arena.Dispose();

        private (DivineAbilityController Controller, TestTideStepEffect Effect, DivineEnergyComponent Energy)
            NewAbility(float cost = 20f, float cooldown = 3f, bool requiresUnlock = true)
        {
            var go = arena.Track(new GameObject("Caster"));
            go.SetActive(false);
            var energy = go.AddComponent<DivineEnergyComponent>();
            var effect = go.AddComponent<TestTideStepEffect>();
            var controller = go.AddComponent<DivineAbilityController>();
            go.SetActive(true);

            // Divine energy starts empty by design — it is earned by parrying — so a
            // test about abilities has to fill it, or it is a test about running out.
            energy.Configure(100f, 100f);

            var definition = arena.TrackAsset(ScriptableObject.CreateInstance<DivineAbilityDefinition>());
            definition.Configure(TestTideStepEffect.Id, cost, cooldown, 0.25f, requiresUnlock);
            controller.Configure(energy, definition);

            return (controller, effect, energy);
        }

        private static void Unlock(string abilityId) =>
            WorldState.Instance.SetFlag("ABILITY_UNLOCKED_" + abilityId);

        // --------------------------------------------------------------- the contract

        [UnityTest]
        public IEnumerator ALockedAbilityIsRefusedAndCostsNothing()
        {
            var (controller, effect, energy) = NewAbility();
            yield return null;

            var before = energy.CurrentEnergy;

            Assert.IsFalse(controller.TryUse(TestTideStepEffect.Id));
            Assert.IsFalse(controller.CanUse(TestTideStepEffect.Id, out var refusal));
            Assert.AreEqual(DivineAbilityRefusal.Locked, refusal);
            Assert.AreEqual(0, effect.Performed);
            Assert.That(energy.CurrentEnergy, Is.EqualTo(before).Within(0.001f),
                "a refusal must never charge the player");
        }

        [UnityTest]
        public IEnumerator AnUnlockedAbilityRunsAndSpendsItsCost()
        {
            var (controller, effect, energy) = NewAbility(cost: 20f);
            Unlock(TestTideStepEffect.Id);
            yield return null;

            var before = energy.CurrentEnergy;

            Assert.IsTrue(controller.TryUse(TestTideStepEffect.Id));
            Assert.AreEqual(1, effect.Performed);
            Assert.That(energy.CurrentEnergy, Is.EqualTo(before - 20f).Within(0.001f));
            Assert.AreEqual(1, controller.UseCount(TestTideStepEffect.Id));
        }

        [UnityTest]
        public IEnumerator AnAbilityOnCooldownIsRefusedUntilItElapses()
        {
            var (controller, effect, _) = NewAbility(cost: 0f, cooldown: 0.3f);
            Unlock(TestTideStepEffect.Id);
            yield return null;

            Assert.IsTrue(controller.TryUse(TestTideStepEffect.Id));
            Assert.IsFalse(controller.TryUse(TestTideStepEffect.Id), "a second use during the cooldown");
            Assert.Greater(controller.CooldownRemaining(TestTideStepEffect.Id), 0f);

            yield return new WaitForSeconds(0.35f);

            Assert.That(controller.CooldownRemaining(TestTideStepEffect.Id), Is.EqualTo(0f).Within(0.001f));
            Assert.IsTrue(controller.TryUse(TestTideStepEffect.Id));
            Assert.AreEqual(2, effect.Performed);
        }

        [UnityTest]
        public IEnumerator AnAbilityIsRefusedWithoutTheEnergyToPayForIt()
        {
            var (controller, effect, energy) = NewAbility(cost: 40f);
            Unlock(TestTideStepEffect.Id);
            yield return null;

            energy.TrySpend(energy.CurrentEnergy);

            Assert.IsFalse(controller.CanUse(TestTideStepEffect.Id, out var refusal));
            Assert.AreEqual(DivineAbilityRefusal.NotEnoughEnergy, refusal);
            Assert.IsFalse(controller.TryUse(TestTideStepEffect.Id));
            Assert.AreEqual(0, effect.Performed);
        }

        /// <summary>The effect's own reason to refuse — mid-swing, already dashing — costs nothing either.</summary>
        [UnityTest]
        public IEnumerator AnEffectThatIsBusyRefusesWithoutCharging()
        {
            var (controller, effect, energy) = NewAbility(cost: 20f);
            Unlock(TestTideStepEffect.Id);
            effect.Blocked = true;
            yield return null;

            var before = energy.CurrentEnergy;

            Assert.IsFalse(controller.CanUse(TestTideStepEffect.Id, out var refusal));
            Assert.AreEqual(DivineAbilityRefusal.Busy, refusal);
            Assert.That(energy.CurrentEnergy, Is.EqualTo(before).Within(0.001f));
            Assert.That(controller.CooldownRemaining(TestTideStepEffect.Id), Is.EqualTo(0f).Within(0.001f),
                "a refused use must not start a cooldown either");
        }

        [UnityTest]
        public IEnumerator AnAbilityWithNoEffectSaysSoRatherThanFailingSilently()
        {
            var go = arena.Track(new GameObject("Caster"));
            go.SetActive(false);
            var energy = go.AddComponent<DivineEnergyComponent>();
            var controller = go.AddComponent<DivineAbilityController>();
            go.SetActive(true);

            var definition = arena.TrackAsset(ScriptableObject.CreateInstance<DivineAbilityDefinition>());
            definition.Configure("NOTHING_IMPLEMENTS_THIS", 0f, 0f, 0f, needsUnlock: false);
            controller.Configure(energy, definition);
            yield return null;

            Assert.IsFalse(controller.CanUse("NOTHING_IMPLEMENTS_THIS", out var refusal));
            Assert.AreEqual(DivineAbilityRefusal.NoEffect, refusal);
        }

        [UnityTest]
        public IEnumerator UsingAnAbilityPublishesOneEventForAnyAbility()
        {
            var heard = 0;
            var id = string.Empty;

            void OnUsed(DivineAbilityUsedEvent used)
            {
                heard++;
                id = used.AbilityId;
            }

            EventBus.Subscribe<DivineAbilityUsedEvent>(OnUsed);

            var (controller, _, _) = NewAbility(cost: 0f, cooldown: 0f);
            Unlock(TestTideStepEffect.Id);
            yield return null;

            controller.TryUse(TestTideStepEffect.Id);
            EventBus.Unsubscribe<DivineAbilityUsedEvent>(OnUsed);

            Assert.AreEqual(1, heard);
            Assert.AreEqual(TestTideStepEffect.Id, id);
        }

        /// <summary>
        /// The Divine branch's cooldown skill applies to every ability through the
        /// definition, not per ability. A temple that forgets to wire it cannot exist.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCooldownSkillShortensAnyAbilityWithoutTheAbilityKnowing()
        {
            var skill = arena.TrackAsset(ScriptableObject.CreateInstance<SkillDefinition>());
            skill.Configure("DIVINE_COOLDOWN", "Cooldown", SkillBranch.Divine, 1,
                SkillEffectType.AbilityCooldownMultiplier, -0.2f);

            var tree = arena.SpawnSkillTreeManager(skill);
            WorldState.Instance.AddToCounter(SkillTreeManager.SkillPointsFlag, 1);

            var (controller, _, _) = NewAbility(cooldown: 10f);
            yield return null;

            var definition = controller.Find(TestTideStepEffect.Id);
            Assert.That(definition.ScaledCooldown, Is.EqualTo(10f).Within(0.001f));

            Assert.IsTrue(tree.Unlock("DIVINE_COOLDOWN"));

            Assert.That(definition.ScaledCooldown, Is.EqualTo(8f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator AnAbilitysUnlockFlagDefaultsToTheOneQuestRewardsWrite()
        {
            var definition = arena.TrackAsset(ScriptableObject.CreateInstance<DivineAbilityDefinition>());
            definition.Configure("VARUNA_TIDE", 10f, 1f);
            yield return null;

            Assert.AreEqual("ABILITY_UNLOCKED_VARUNA_TIDE", definition.UnlockFlag,
                "QuestReward.AbilityUnlock writes this name, so granting an ability as a reward needs no wiring");
        }

        // ------------------------------------------------------------------- the save

        [UnityTest]
        public IEnumerator UseCountsAndCooldownsSurviveASaveAndLoad()
        {
            var (controller, _, _) = NewAbility(cost: 0f, cooldown: 30f);
            Unlock(TestTideStepEffect.Id);

            var root = Path.Combine(Path.GetTempPath(), "GodGameAbilitySave", Path.GetRandomFileName());
            Directory.CreateDirectory(root);

            var saveObject = arena.Track(new GameObject("SaveManager"));
            saveObject.SetActive(false);
            var saves = saveObject.AddComponent<SaveManager>();
            saves.Configure(root, autoSave: false);
            saveObject.SetActive(true);

            arena.SpawnPlayer(Vector3.zero);
            yield return null;

            Assert.IsTrue(controller.TryUse(TestTideStepEffect.Id));
            Assert.AreEqual(1, controller.UseCount(TestTideStepEffect.Id));
            Assert.Greater(controller.CooldownRemaining(TestTideStepEffect.Id), 1f);

            Assert.IsTrue(saves.Save(SaveSlot.Manual));

            controller.RestoreJson(null);
            Assert.AreEqual(0, controller.UseCount(TestTideStepEffect.Id), "staging: the state should be cleared");

            Assert.IsTrue(saves.Load(SaveSlot.Manual));
            yield return null;

            Assert.AreEqual(1, controller.UseCount(TestTideStepEffect.Id),
                "SPEC.md section 20's repeated-use costs count uses across a save");
            Assert.Greater(controller.CooldownRemaining(TestTideStepEffect.Id), 1f,
                "loading must not hand back a free use of an ability that was cooling down");

            Directory.Delete(root, true);
        }

        /// <summary>
        /// A cooldown is stored as seconds remaining, not as an absolute time, because
        /// <c>Time.time</c> restarts with the scene — an absolute one would come back
        /// either already elapsed or hours away.
        /// </summary>
        [UnityTest]
        public IEnumerator ACooldownIsRememberedAsTimeRemainingRatherThanAnAbsoluteMoment()
        {
            var (controller, _, _) = NewAbility(cost: 0f, cooldown: 30f);
            Unlock(TestTideStepEffect.Id);
            yield return null;

            controller.TryUse(TestTideStepEffect.Id);
            var json = controller.CaptureJson();

            Assert.That(json, Does.Not.Contain("ReadyAt"));
            Assert.That(json, Does.Contain("CooldownsRemaining"));

            controller.RestoreJson(json);
            Assert.Greater(controller.CooldownRemaining(TestTideStepEffect.Id), 25f);
        }

        // ------------------------------------------------ Ember Step through the same door

        /// <summary>
        /// Ember Step is now expressed in the shared contract rather than in
        /// <c>CombatController</c>'s own fields. This is what proves the contract handles
        /// the ability that already existed, not just a new one written to fit it.
        /// </summary>
        [UnityTest]
        public IEnumerator EmberStepGoesThroughTheSameContract()
        {
            var player = arena.SpawnPlayer(Vector3.zero, withCombatInput: true);
            player.DivineEnergy.Configure(100f, 100f);
            player.Combat.ConfigureAbilityUnlock("EMBER_STEP", true);
            player.Combat.ConfigureAbility(10f, 1f);
            yield return null;

            Assert.IsFalse(player.Combat.TryAbility(), "locked, so refused");

            Unlock("EMBER_STEP");

            Assert.IsTrue(player.Combat.TryAbility());

            var controller = player.Root.GetComponent<DivineAbilityController>();
            Assert.IsNotNull(controller, "using the ability should have created the shared contract");
            Assert.AreEqual(1, controller.UseCount("EMBER_STEP"));
            Assert.Greater(controller.CooldownRemaining("EMBER_STEP"), 0f);
        }
    }
}

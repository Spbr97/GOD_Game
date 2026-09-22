using System;
using System.Collections;
using System.Collections.Generic;
using Game.AI;
using Game.Combat;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Play
{
    /// <summary>
    /// TASK 007 over real frames: a parry against a real enemy swing, a block that
    /// costs stamina, lock-on against live targets, a finisher, and a combo chain
    /// whose multiplier shows up in the damage actually dealt.
    ///
    /// The EditMode tests prove the rules; these prove the pieces are wired to each
    /// other — the enemy's hitbox reaches the player's guard, the guard's event
    /// reaches the enemy's stagger, the chain's multiplier reaches the hitbox.
    /// </summary>
    public class CombatActionPlayModeTests
    {
        private TestArena arena;
        private readonly List<Action> unsubscribes = new();

        [SetUp]
        public void SetUp()
        {
            arena = new TestArena();
            Difficulty.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var unsubscribe in unsubscribes)
            {
                unsubscribe();
            }

            unsubscribes.Clear();
            arena.Dispose();
            Difficulty.Reset();
        }

        /// <summary>Subscribes for the duration of one test. Lambdas cannot be unsubscribed by hand, so keep the handle.</summary>
        private void Listen<T>(Action<T> handler)
        {
            EventBus.Subscribe(handler);
            unsubscribes.Add(() => EventBus.Unsubscribe(handler));
        }

        private IEnumerator Ground()
        {
            arena.BuildFloor();
            arena.BuildNavMesh();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Parry_StaggersTheEnemyMidSwingAndTakesNoDamage()
        {
            yield return Ground();

            var archetype = arena.NewArchetype(telegraph: 0.6f, attackRange: 2.5f, damage: 25f, health: 200f);
            var enemy = arena.SpawnEnemy("Attacker", Vector3.zero, archetype);
            var player = arena.SpawnPlayer(new Vector3(0f, 0f, 2f), withWeapon: true, withCombatInput: true);

            // A generous window so the press can be made at the start of the telegraph
            // rather than on the exact frame the hitbox opens; the EditMode tests own
            // the precise timing.
            player.Guard.Configure(1.5f, 0.08f, 0.6f, 0.8f);

            yield return TestArena.Until(() => enemy.Combatant.IsTelegraphing, "a swing to begin", 5f);
            Assert.IsTrue(player.Combat.TryGuard(), "The guard was refused.");

            yield return TestArena.Until(() => enemy.Stagger.IsStaggered, "the parry to stagger the enemy", 3f);

            Assert.AreEqual(100f, player.Health.CurrentHealth, 0.01f, "A parried swing still hurt the player.");
            Assert.IsFalse(enemy.Combatant.IsAttacking, "The stagger did not interrupt the swing.");
            Assert.AreEqual(EnemyState.Stagger, enemy.State);
            Assert.IsNotNull(player.Combat.Combo);
            Assert.AreEqual(ComboStep.Parry, player.Combat.Combo.History[player.Combat.Combo.History.Count - 1],
                "The parry should be recorded so Parry→Heavy can follow.");
        }

        [UnityTest]
        public IEnumerator Block_AbsorbsTheEnemySwingForStamina()
        {
            yield return Ground();

            var archetype = arena.NewArchetype(telegraph: 0.5f, attackRange: 2.5f, damage: 25f, health: 200f);
            var enemy = arena.SpawnEnemy("Attacker", Vector3.zero, archetype);
            var player = arena.SpawnPlayer(new Vector3(0f, 0f, 2f), withWeapon: true, withCombatInput: true);

            // A tiny parry window, raised well before the swing, so the hit meets a
            // held block rather than a parry.
            player.Guard.Configure(0.01f, 0.005f, 0.6f, 0.8f);
            player.Stamina.Configure(100f, 0f, 10f);

            var blocked = false;
            Listen<AttackBlockedEvent>(_ => blocked = true);

            Assert.IsTrue(player.Combat.TryGuard());
            yield return TestArena.Until(() => blocked, "the enemy's swing to be blocked", 6f);

            Assert.AreEqual(100f, player.Health.CurrentHealth, 0.01f, "A blocked swing still hurt the player.");
            Assert.AreEqual(85f, player.Stamina.CurrentStamina, 0.5f, "25 damage at 0.6 per point is 15 stamina.");
            Assert.IsFalse(enemy.Stagger.IsStaggered, "A block is not a parry; the enemy should not reel.");
        }

        [UnityTest]
        public IEnumerator LockOn_PicksTheTargetInFrontAndDropsItWhenItDies()
        {
            var player = arena.SpawnPlayer(Vector3.zero, withWeapon: true, withCombatInput: true);
            player.Root.transform.rotation = Quaternion.LookRotation(Vector3.forward);

            var ahead = arena.SpawnDummy("Ahead", new Vector3(0f, 0f, 4f));
            arena.SpawnDummy("Behind", new Vector3(0f, 0f, -2f));
            arena.SpawnDummy("Far", new Vector3(0f, 0f, 40f));
            yield return null;

            LockOnChangedEvent? last = null;
            Listen<LockOnChangedEvent>(e => last = e);

            Assert.IsTrue(player.LockOn.Acquire(), "Nothing was acquired.");
            Assert.AreEqual(ahead.Health, player.LockOn.Target,
                $"Locked on to {player.LockOn.Target?.name}; the nearer dummy behind the player is outside the view cone and the far one is out of range.");
            Assert.IsTrue(last.HasValue && last.Value.Target == ahead.Root);

            ahead.Health.Kill(DamageData.Create(999f, player.Root));
            yield return TestArena.Until(() => !player.LockOn.IsLocked, "the lock to drop on death", 2f);

            Assert.IsTrue(last.HasValue && last.Value.Target == null, "Release should publish a null target.");
        }

        [UnityTest]
        public IEnumerator LockOn_ToggleReleasesAndRangeBreaksTheLock()
        {
            var player = arena.SpawnPlayer(Vector3.zero, withWeapon: true, withCombatInput: true);
            player.Root.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var dummy = arena.SpawnDummy("Dummy", new Vector3(0f, 0f, 4f));
            yield return null;

            player.LockOn.Toggle();
            Assert.IsTrue(player.LockOn.IsLocked);
            player.LockOn.Toggle();
            Assert.IsFalse(player.LockOn.IsLocked, "A second press should release.");

            player.LockOn.LockTo(dummy.Health);
            dummy.Root.transform.position = new Vector3(0f, 0f, 60f);
            yield return TestArena.Until(() => !player.LockOn.IsLocked, "the lock to break at range", 2f);
        }

        [UnityTest]
        public IEnumerator Finisher_ExecutesAWeakenedEnemyWithOneLightAttack()
        {
            var player = arena.SpawnPlayer(Vector3.zero, withWeapon: true, withCombatInput: true);
            player.Root.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var dummy = arena.SpawnDummy("Weakened", new Vector3(0f, 0f, 1.2f), 100f);
            yield return null;

            // Healthy: a light attack is a light attack.
            Assert.IsTrue(player.Combat.TryAttack(AttackType.Light));
            Assert.AreEqual(AttackType.Light, player.Weapon.CurrentAttack, "A healthy enemy must not be finishable.");
            yield return TestArena.Until(() => !player.Weapon.IsSwinging, "the light swing to end", 2f);

            dummy.Health.RestoreTo(15f);
            FinisherStartedEvent? started = null;
            Listen<FinisherStartedEvent>(e => started = e);

            Assert.IsTrue(player.Combat.TryAttack(AttackType.Light));
            Assert.AreEqual(AttackType.Finisher, player.Weapon.CurrentAttack, "Below the threshold the light attack should become a finisher.");
            Assert.IsTrue(started.HasValue && started.Value.Target == dummy.Root);

            yield return TestArena.Until(() => dummy.Health.IsDead, "the finisher to kill", 2f);
        }

        [UnityTest]
        public IEnumerator Combo_LightLightHeavyHitsHarderThanAPlainHeavy()
        {
            var player = arena.SpawnPlayer(Vector3.zero, withWeapon: true, withCombatInput: true);
            player.Root.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var dummy = arena.SpawnDummy("Dummy", new Vector3(0f, 0f, 1.2f), 1000f);
            player.Stamina.Configure(1000f, 0f, 0f);
            yield return null;

            var heavyBase = player.Weapon.BaseDamage(AttackType.Heavy);

            // Plain heavy first, for the baseline.
            Assert.IsTrue(player.Combat.TryAttack(AttackType.Heavy));
            yield return TestArena.Until(() => !player.Weapon.IsSwinging, "the baseline heavy to end", 2f);
            var afterPlainHeavy = dummy.Health.CurrentHealth;
            Assert.AreEqual(1000f - heavyBase, afterPlainHeavy, 0.01f, "A first heavy completes no chain and should deal base damage.");

            // Let the window lapse so the chain starts clean. The window is measured
            // from the end of the swing, so this has to outlast comboWindow alone.
            yield return new WaitForSeconds(1.5f);

            string performed = null;
            Listen<ComboPerformedEvent>(e => performed = e.ChainName);

            Assert.IsTrue(player.Combat.TryAttack(AttackType.Light));
            yield return TestArena.Until(() => !player.Weapon.IsSwinging, "light one to end", 2f);
            Assert.IsTrue(player.Combat.TryAttack(AttackType.Light));
            yield return TestArena.Until(() => !player.Weapon.IsSwinging, "light two to end", 2f);

            var beforeHeavy = dummy.Health.CurrentHealth;
            Assert.IsTrue(player.Combat.TryAttack(AttackType.Heavy));
            Assert.AreEqual("Light Light Heavy", performed);
            yield return TestArena.Until(() => !player.Weapon.IsSwinging, "the chain heavy to end", 2f);

            var chainDamage = beforeHeavy - dummy.Health.CurrentHealth;
            Assert.AreEqual(heavyBase * 1.6f, chainDamage, 0.01f, "The chain's multiplier did not reach the hitbox.");
        }

        [UnityTest]
        public IEnumerator Guard_IsRefusedMidSwingAndLoweredByAttacking()
        {
            var player = arena.SpawnPlayer(Vector3.zero, withWeapon: true, withCombatInput: true);
            yield return null;

            Assert.IsTrue(player.Combat.TryAttack(AttackType.Heavy));
            Assert.IsFalse(player.Combat.TryGuard(), "A whiffed swing must not be cancellable into a free parry.");
            yield return TestArena.Until(() => !player.Weapon.IsSwinging, "the swing to end", 2f);

            Assert.IsTrue(player.Combat.TryGuard());
            Assert.IsTrue(player.Combat.IsGuarding);
            Assert.IsTrue(player.Combat.TryAttack(AttackType.Light));
            Assert.IsFalse(player.Combat.IsGuarding, "Attacking should lower the guard.");
        }

        [UnityTest]
        public IEnumerator Ability_SpendsDivineEnergyAndOpensAnInvulnerabilityWindow()
        {
            // No PlayerController in this rig (see TestArena.SpawnPlayer), so the dash's
            // actual displacement is untestable here, the same reason the Dodge tests
            // check the i-frame window rather than position too.
            var player = arena.SpawnPlayer(Vector3.zero, withCombatInput: true);
            player.DivineEnergy.Configure(100f, 100f);
            player.Combat.ConfigureAbility(20f, 3f);
            yield return null;

            Assert.IsFalse(player.Health.IsInvulnerable);
            Assert.IsTrue(player.Combat.TryAbility());
            Assert.AreEqual(80f, player.DivineEnergy.CurrentEnergy, 0.01f);

            yield return TestArena.Until(() => player.Health.IsInvulnerable, "Ember Step's i-frame window to open", 1f);
            yield return TestArena.Until(() => !player.Health.IsInvulnerable, "Ember Step's i-frame window to close", 1f);
            yield return TestArena.Until(() => !player.Combat.IsUsingAbility, "the dash to end", 1f);

            Assert.IsFalse(player.Health.IsInvulnerable, "The player was left invulnerable after Ember Step.");
        }

        [UnityTest]
        public IEnumerator Ability_RefusedWithoutEnoughDivineEnergy()
        {
            var player = arena.SpawnPlayer(Vector3.zero, withCombatInput: true);
            yield return null;

            Assert.AreEqual(0f, player.DivineEnergy.CurrentEnergy, "The test assumes divine energy starts empty.");
            Assert.IsFalse(player.Combat.TryAbility());
            Assert.IsFalse(player.Combat.IsUsingAbility);
        }

        [UnityTest]
        public IEnumerator Ability_IsRefusedDuringItsOwnCooldownThenWorksAgain()
        {
            var player = arena.SpawnPlayer(Vector3.zero, withCombatInput: true);
            player.DivineEnergy.Configure(1000f, 1000f);
            player.Combat.ConfigureAbility(10f, 0.3f);
            yield return null;

            Assert.IsTrue(player.Combat.TryAbility());
            yield return TestArena.Until(() => !player.Combat.IsUsingAbility, "the first dash to finish", 2f);

            Assert.IsFalse(player.Combat.TryAbility(), "A second use during the cooldown should be refused.");

            yield return new WaitForSeconds(0.35f);

            Assert.IsTrue(player.Combat.TryAbility(), "The cooldown should have elapsed by now.");
        }

        [UnityTest]
        public IEnumerator Ability_PublishesAnIncrementingUseCount()
        {
            var player = arena.SpawnPlayer(Vector3.zero, withCombatInput: true);
            player.DivineEnergy.Configure(1000f, 1000f);
            player.Combat.ConfigureAbility(10f, 0.1f);
            yield return null;

            var uses = new List<int>();
            Listen<EmberStepUsedEvent>(e => uses.Add(e.TotalUses));

            Assert.IsTrue(player.Combat.TryAbility());
            yield return TestArena.Until(() => !player.Combat.IsUsingAbility, "the first dash to finish", 2f);
            yield return new WaitForSeconds(0.15f);

            Assert.IsTrue(player.Combat.TryAbility());
            yield return null;

            CollectionAssert.AreEqual(new[] { 1, 2 }, uses);
        }

        [UnityTest]
        public IEnumerator Ability_RecordsTheComboStepSoAbilityStrikeCanFollow()
        {
            var player = arena.SpawnPlayer(Vector3.zero, withWeapon: true, withCombatInput: true);
            player.DivineEnergy.Configure(1000f, 1000f);
            player.Combat.ConfigureAbility(10f, 0.1f);
            yield return null;

            Assert.IsTrue(player.Combat.TryAbility());
            yield return TestArena.Until(() => !player.Combat.IsUsingAbility, "the dash to finish", 2f);

            Assert.IsTrue(player.Combat.TryAttack(AttackType.Light));

            Assert.IsNotNull(player.Combat.CurrentChain);
            Assert.AreEqual("Ability Strike", player.Combat.CurrentChain.Name);
        }
    }
}

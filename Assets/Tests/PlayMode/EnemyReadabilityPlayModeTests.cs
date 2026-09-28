using System.Collections;
using Game.AI;
using Game.Combat;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Play
{
    /// <summary>
    /// Unblockable attacks against a live guard (SPEC.md section 16, TASK 043).
    ///
    /// The point of the mechanic is that the guard stops being the answer to everything.
    /// Before this, every enemy attack could be blocked, so holding block was never worse
    /// than dodging and the telegraph was decoration. These check the three things that
    /// make it fair: it is decided before the wind-up, it looks different while it winds
    /// up, and it actually goes through a guard.
    /// </summary>
    public class EnemyReadabilityPlayModeTests
    {
        private TestArena arena;

        [SetUp]
        public void SetUp()
        {
            arena = new TestArena();
            arena.EnsureWorldState();
            Difficulty.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            arena.Dispose();
            Difficulty.Reset();
        }

        private EnemyArchetype Archetype(int unblockableEveryNth, float telegraph = 0.1f)
        {
            var archetype = arena.NewArchetype("TEST_READABLE", health: 100f, damage: 10f);
            archetype.ConfigureUnblockable(unblockableEveryNth, 1.5f, new Color(0.85f, 0.15f, 1f));
            archetype.ConfigurePresentation(new Color(0.3f, 0.3f, 0.3f), new Color(1f, 0.72f, 0.2f));
            archetype.Configure("TEST_READABLE", "Readable", EnemyClass.ForgottenSoldier, 100f,
                10f, 2.4f, telegraph, 40f, 12f, 120f);
            archetype.ConfigureUnblockable(unblockableEveryNth, 1.5f, new Color(0.85f, 0.15f, 1f));
            return archetype;
        }

        /// <summary>
        /// The attack must know what it is before the wind-up plays, or the player could
        /// not have read it — which would make it unfair rather than hard.
        /// </summary>
        [UnityTest]
        public IEnumerator AnUnblockableIsDecidedBeforeItsWindUpStarts()
        {
            var archetype = Archetype(unblockableEveryNth: 2, telegraph: 0.4f);
            var enemy = arena.SpawnEnemy("Enemy", new Vector3(3f, 0f, 0f), archetype);
            yield return null;

            Assert.IsTrue(enemy.Combatant.TryAttack());
            yield return null;

            Assert.IsTrue(enemy.Combatant.IsTelegraphing);
            Assert.IsFalse(enemy.Combatant.NextAttackIsUnblockable, "attack 1 of every 2 is ordinary");

            // CanAttack, not !IsAttacking: the cooldown is set when the routine ends, so
            // the enemy is idle and still not ready for a moment after.
            yield return TestArena.Until(() => enemy.Combatant.CanAttack, "the enemy to be ready again", 8f);

            Assert.IsTrue(enemy.Combatant.TryAttack());
            yield return null;

            Assert.IsTrue(enemy.Combatant.IsTelegraphing);
            Assert.IsTrue(enemy.Combatant.NextAttackIsUnblockable,
                "attack 2 is the unblockable, and it must be known during the wind-up");
        }

        [UnityTest]
        public IEnumerator AnUnblockableWindUpFlashesADifferentColour()
        {
            var archetype = Archetype(unblockableEveryNth: 1, telegraph: 0.4f);
            var enemy = arena.SpawnEnemy("Enemy", new Vector3(3f, 0f, 0f), archetype);
            yield return null;

            enemy.Combatant.TryAttack();
            yield return null;

            Assert.IsTrue(enemy.Combatant.IsTelegraphing);
            Assert.AreEqual(archetype.UnblockableTelegraphColour, enemy.Combatant.CurrentTelegraphColour);
            Assert.AreNotEqual(archetype.TelegraphColour, enemy.Combatant.CurrentTelegraphColour);
        }

        [UnityTest]
        public IEnumerator AnOrdinaryWindUpFlashesTheOrdinaryColour()
        {
            var archetype = Archetype(unblockableEveryNth: 0, telegraph: 0.4f);
            var enemy = arena.SpawnEnemy("Enemy", new Vector3(3f, 0f, 0f), archetype);
            yield return null;

            enemy.Combatant.TryAttack();
            yield return null;

            Assert.IsFalse(enemy.Combatant.NextAttackIsUnblockable);
            Assert.AreEqual(archetype.TelegraphColour, enemy.Combatant.CurrentTelegraphColour);
        }

        /// <summary>
        /// SPEC.md section 43: never colour alone.
        ///
        /// The scale pulse was already there, but it was the same size for both kinds of
        /// swing — so it announced that an attack was coming and said nothing about which
        /// one. A player who cannot tell violet from orange was reading the difference
        /// that decides whether to block or to dodge off hue and nothing else. An
        /// unblockable now swells noticeably further, which is the cue that survives.
        /// </summary>
        [UnityTest]
        public IEnumerator AnUnblockableWindUpSwellsFurtherThanAnOrdinaryOne()
        {
            var ordinaryArchetype = Archetype(unblockableEveryNth: 0, telegraph: 0.4f);
            var ordinary = arena.SpawnEnemy("Ordinary", new Vector3(3f, 0f, 0f), ordinaryArchetype);
            yield return null;

            ordinary.Combatant.TryAttack();
            yield return null;

            Assert.IsFalse(ordinary.Combatant.NextAttackIsUnblockable);
            var ordinaryPulse = ordinary.Combatant.CurrentTelegraphPulse;

            var unblockableArchetype = Archetype(unblockableEveryNth: 1, telegraph: 0.4f);
            var dangerous = arena.SpawnEnemy("Dangerous", new Vector3(6f, 0f, 0f), unblockableArchetype);
            yield return null;

            dangerous.Combatant.TryAttack();
            yield return null;

            Assert.IsTrue(dangerous.Combatant.NextAttackIsUnblockable);
            var unblockablePulse = dangerous.Combatant.CurrentTelegraphPulse;

            Assert.Greater(unblockablePulse, ordinaryPulse * 1.1f,
                $"an unblockable wind-up scales to {unblockablePulse:0.00} and an ordinary one to "
                + $"{ordinaryPulse:0.00}. That is not a difference a player can see, which leaves colour as the "
                + "only tell for the one attack they must not block.");
        }

        /// <summary>
        /// The mechanic itself: a raised guard stops an ordinary swing and does not stop
        /// this one.
        /// </summary>
        [UnityTest]
        public IEnumerator AnUnblockableGoesThroughARaisedGuard()
        {
            var player = arena.SpawnPlayer(Vector3.zero, withCombatInput: true);
            var guard = player.Root.GetComponent<GuardController>();
            yield return null;

            guard.BeginGuard();

            // Past the parry window, so this is a block and not a parry.
            yield return new WaitForSeconds(guard.ScaledParryWindow + 0.1f);
            Assert.IsTrue(guard.IsBlocking);

            var ordinary = new DamageData
            {
                Amount = 10f,
                Type = DamageType.Physical,
                Unblockable = false
            };

            Assert.IsTrue(guard.TryGuard(ref ordinary), "an ordinary swing should be blocked");

            var unblockable = new DamageData
            {
                Amount = 15f,
                Type = DamageType.Physical,
                Unblockable = true
            };

            Assert.IsFalse(guard.TryGuard(ref unblockable),
                "an unblockable must land even against a raised guard");
            Assert.IsTrue(guard.IsGuardBroken, "and it breaks the guard, so there is a cost to guessing wrong");
        }

        /// <summary>
        /// An unblockable hits harder, because the player gave up their guard to avoid
        /// it. If it did not, dodging it would be a chore rather than a decision.
        /// </summary>
        [UnityTest]
        public IEnumerator AnUnblockableHitsHarderThanAnOrdinarySwing()
        {
            var archetype = Archetype(unblockableEveryNth: 1, telegraph: 0.05f);
            var enemy = arena.SpawnEnemy("Enemy", new Vector3(3f, 0f, 0f), archetype);
            yield return null;

            Assert.That(archetype.UnblockableDamageMultiplier, Is.GreaterThan(1f));
            Assert.That(archetype.DamageFor() * archetype.UnblockableDamageMultiplier,
                Is.GreaterThan(archetype.DamageFor()));
        }

        /// <summary>
        /// Staggering an enemy mid-wind-up must clear the unblockable state with the
        /// swing. Otherwise the next attack inherits a flag from an attack that never
        /// landed, and the player is hit by an unblockable that telegraphed as ordinary.
        /// </summary>
        [UnityTest]
        public IEnumerator CancellingAnAttackClearsItsUnblockableState()
        {
            var archetype = Archetype(unblockableEveryNth: 1, telegraph: 0.5f);
            var enemy = arena.SpawnEnemy("Enemy", new Vector3(3f, 0f, 0f), archetype);
            yield return null;

            enemy.Combatant.TryAttack();
            yield return null;
            Assert.IsTrue(enemy.Combatant.NextAttackIsUnblockable);

            enemy.Combatant.CancelAttack();

            Assert.IsFalse(enemy.Combatant.NextAttackIsUnblockable,
                "a cancelled swing must not leave its flag behind for the next one");
        }
    }
}

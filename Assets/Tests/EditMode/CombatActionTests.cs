using System.Collections.Generic;
using Game.Combat;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// EditMode coverage for TASK 007: combo chains, block, parry, divine energy and
    /// the difficulty scaling of timing windows (SPEC.md sections 14, 15, 44, 53).
    ///
    /// The guard reads time through an injected clock, so every window here is
    /// stepped by hand. Nothing depends on a frame.
    /// </summary>
    public class CombatActionTests
    {
        private readonly List<GameObject> spawned = new();
        private float now;

        [SetUp]
        public void SetUp()
        {
            now = 0f;
            Difficulty.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            spawned.Clear();
            EventBus.Clear();
            Difficulty.Reset();
        }

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go;
        }

        private sealed class Defender
        {
            public GameObject Root;
            public HealthComponent Health;
            public StaminaComponent Stamina;
            public DivineEnergyComponent Divine;
            public GuardController Guard;
        }

        /// <summary>
        /// Wires the guard by hand because Awake and OnEnable do not run on
        /// AddComponent in EditMode; in play the guard registers itself.
        /// </summary>
        private Defender NewDefender(float stamina = 100f, float parry = 0.2f, float perfect = 0.08f,
            float staminaPerDamage = 0.6f, float stun = 0.8f)
        {
            var root = NewObject("Defender");
            var health = root.AddComponent<HealthComponent>();
            health.Configure(100f);
            var pool = root.AddComponent<StaminaComponent>();
            pool.Configure(stamina, 0f, 0f);
            var divine = root.AddComponent<DivineEnergyComponent>();
            divine.Configure(100f);
            var guard = root.AddComponent<GuardController>();
            guard.Configure(parry, perfect, staminaPerDamage, stun, () => now);
            health.Guard = guard;

            return new Defender { Root = root, Health = health, Stamina = pool, Divine = divine, Guard = guard };
        }

        private DamageData Swing(float amount, GameObject attacker, bool unblockable = false,
            DamageType type = DamageType.Physical)
        {
            var damage = DamageData.Create(amount, attacker, type);
            damage.Unblockable = unblockable;
            return damage;
        }

        // ---------------------------------------------------------------- combos

        [Test]
        public void Combo_LongestMatchingChainWins()
        {
            var tracker = new ComboTracker(ComboChain.DefaultChains(), 0.7f);

            tracker.Record(ComboStep.Light, 0f);
            tracker.Record(ComboStep.Light, 0.3f);
            var chain = tracker.Record(ComboStep.Heavy, 0.6f);

            Assert.IsNotNull(chain);
            Assert.AreEqual("Light Light Heavy", chain.Name, "Light→Heavy also fits the tail; the longer chain should win.");
        }

        [Test]
        public void Combo_SingleSwingCompletesNothing()
        {
            var tracker = new ComboTracker(ComboChain.DefaultChains(), 0.7f);
            Assert.IsNull(tracker.Record(ComboStep.Light, 0f));
            Assert.IsNull(tracker.Record(ComboStep.Heavy, 5f));
        }

        [Test]
        public void Combo_LateInputStartsAFreshChain()
        {
            var tracker = new ComboTracker(ComboChain.DefaultChains(), 0.7f);

            tracker.Record(ComboStep.Light, 0f);
            tracker.Record(ComboStep.Light, 0.5f);
            var chain = tracker.Record(ComboStep.Light, 5f);

            Assert.IsNull(chain, "A swing long after the window lapsed should not complete Triple Light.");
            Assert.AreEqual(1, tracker.History.Count);
        }

        [Test]
        public void Combo_LingerKeepsTheWindowOpenPastTheSwing()
        {
            var tracker = new ComboTracker(ComboChain.DefaultChains(), 0.7f);

            // A 1s swing at t=0 keeps the chain alive until 1.7s; without linger it
            // would have expired at 0.7s.
            tracker.Record(ComboStep.Light, 0f, linger: 1f);
            Assert.IsFalse(tracker.HasExpired(1.5f));
            Assert.IsTrue(tracker.HasExpired(1.8f));
        }

        [Test]
        public void Combo_EverySectionFourteenChainIsReachable()
        {
            var tracker = new ComboTracker(ComboChain.DefaultChains(), 0.7f);

            void Expect(string name, params ComboStep[] steps)
            {
                tracker.Reset();
                ComboChain result = null;
                for (var i = 0; i < steps.Length; i++)
                {
                    result = tracker.Record(steps[i], i * 0.1f);
                }

                Assert.IsNotNull(result, $"{name} did not match.");
                Assert.AreEqual(name, result.Name);
            }

            Expect("Triple Light", ComboStep.Light, ComboStep.Light, ComboStep.Light);
            Expect("Light Light Heavy", ComboStep.Light, ComboStep.Light, ComboStep.Heavy);
            Expect("Light Heavy", ComboStep.Light, ComboStep.Heavy);
            Expect("Double Heavy", ComboStep.Heavy, ComboStep.Heavy);
            Expect("Dodge Strike", ComboStep.Dodge, ComboStep.Light);
            Expect("Parry Riposte", ComboStep.Parry, ComboStep.Heavy);
            Expect("Ability Strike", ComboStep.Ability, ComboStep.Light);
        }

        // ----------------------------------------------------------------- parry

        [Test]
        public void Parry_InsideTheWindowAbsorbsTheHitAndAnnouncesTheAttacker()
        {
            var defender = NewDefender();
            var attacker = NewObject("Attacker");
            ParryEvent? seen = null;
            EventBus.Subscribe<ParryEvent>(e => seen = e);

            Assert.IsTrue(defender.Guard.BeginGuard());
            now = 0.15f;
            var landed = defender.Health.TakeDamage(Swing(30f, attacker));

            Assert.IsFalse(landed);
            Assert.AreEqual(100f, defender.Health.CurrentHealth);
            Assert.IsTrue(seen.HasValue, "No ParryEvent was published.");
            Assert.AreEqual(attacker, seen.Value.Attacker, "The event must name the attacker so it can stagger itself.");
            Assert.IsFalse(seen.Value.Perfect, "0.15s is outside the 0.08s perfect window.");
        }

        [Test]
        public void Parry_PerfectTimingRestoresDivineEnergy()
        {
            var defender = NewDefender();
            var attacker = NewObject("Attacker");

            defender.Guard.BeginGuard();
            now = 0.03f;
            defender.Health.TakeDamage(Swing(30f, attacker));

            Assert.Greater(defender.Divine.CurrentEnergy, 0f, "A perfect parry should restore divine energy (SPEC.md section 15).");
        }

        [Test]
        public void Parry_OnePressDeflectsOneAttack()
        {
            var defender = NewDefender();
            var attacker = NewObject("Attacker");
            var parries = 0;
            EventBus.Subscribe<ParryEvent>(_ => parries++);

            defender.Guard.BeginGuard();
            now = 0.05f;
            defender.Health.TakeDamage(Swing(10f, attacker));
            now = 0.1f;
            defender.Health.TakeDamage(Swing(10f, attacker));

            Assert.AreEqual(1, parries, "The second hit inside the window should be blocked, not parried again.");
            Assert.AreEqual(100f, defender.Health.CurrentHealth, "The second hit should still have been blocked.");
            Assert.Less(defender.Stamina.CurrentStamina, 100f, "Blocking the second hit should have cost stamina.");
        }

        [Test]
        public void Parry_WorksAgainstUnblockableAttacks()
        {
            var defender = NewDefender();
            var attacker = NewObject("Attacker");

            defender.Guard.BeginGuard();
            now = 0.1f;
            var landed = defender.Health.TakeDamage(Swing(40f, attacker, unblockable: true));

            Assert.IsFalse(landed, "A heavy goes through a block but a parry must still answer it.");
            Assert.IsFalse(defender.Guard.IsGuardBroken);
        }

        [Test]
        public void Parry_TooLateIsABlockAndTooLateWithoutHoldingIsAHit()
        {
            var held = NewDefender();
            var attacker = NewObject("Attacker");

            held.Guard.BeginGuard();
            now = 1f;
            Assert.IsFalse(held.Health.TakeDamage(Swing(10f, attacker)), "Still holding: the hit should be blocked.");

            var released = NewDefender();
            released.Guard.BeginGuard();
            released.Guard.EndGuard();
            now = 2f;
            Assert.IsTrue(released.Health.TakeDamage(Swing(10f, attacker)), "Let go after the window: the hit should land.");
            Assert.AreEqual(90f, released.Health.CurrentHealth);
        }

        // ----------------------------------------------------------------- block

        [Test]
        public void Guard_DoesNotBlockOrParryAnAttackerBehindThePlayer()
        {
            var defender = NewDefender();
            var attacker = NewObject("Rear attacker");
            attacker.transform.position = Vector3.back * 2f;

            defender.Guard.BeginGuard();
            now = 0.05f;
            Assert.IsTrue(defender.Health.TakeDamage(Swing(10f, attacker)));
            Assert.AreEqual(90f, defender.Health.CurrentHealth);

            attacker.transform.position = Vector3.forward * 2f;
            Assert.IsFalse(defender.Health.TakeDamage(Swing(10f, attacker)));
            Assert.AreEqual(90f, defender.Health.CurrentHealth);
        }

        [Test]
        public void Block_AbsorbsDamageInExchangeForStamina()
        {
            var defender = NewDefender(staminaPerDamage: 0.5f);
            var attacker = NewObject("Attacker");
            var blocked = 0;
            EventBus.Subscribe<AttackBlockedEvent>(_ => blocked++);

            defender.Guard.BeginGuard();
            now = 1f;
            defender.Health.TakeDamage(Swing(20f, attacker));

            Assert.AreEqual(100f, defender.Health.CurrentHealth);
            Assert.AreEqual(90f, defender.Stamina.CurrentStamina, 0.01f, "20 damage at 0.5 stamina per point is 10 stamina.");
            Assert.AreEqual(1, blocked);
        }

        [Test]
        public void Block_BreaksWhenStaminaRunsOutAndTheHitLands()
        {
            var defender = NewDefender(stamina: 5f, staminaPerDamage: 1f, stun: 0.8f);
            var attacker = NewObject("Attacker");
            GuardBrokenEvent? broken = null;
            EventBus.Subscribe<GuardBrokenEvent>(e => broken = e);

            defender.Guard.BeginGuard();
            now = 1f;
            var landed = defender.Health.TakeDamage(Swing(20f, attacker));

            Assert.IsTrue(landed, "With no stamina to pay, the hit should go through.");
            Assert.AreEqual(80f, defender.Health.CurrentHealth);
            Assert.IsTrue(broken.HasValue);
            Assert.IsTrue(defender.Guard.IsGuardBroken);
            Assert.IsFalse(defender.Guard.IsBlocking, "A broken guard is down.");
            Assert.IsFalse(defender.Guard.BeginGuard(), "The guard cannot be raised while stunned.");

            now = 2f;
            Assert.IsTrue(defender.Guard.BeginGuard(), "Once the stun passes the guard should work again.");
        }

        [Test]
        public void Block_UnblockableAttackBreaksTheGuard()
        {
            var defender = NewDefender();
            var attacker = NewObject("Attacker");

            defender.Guard.BeginGuard();
            now = 1f;
            var landed = defender.Health.TakeDamage(Swing(28f, attacker, unblockable: true));

            Assert.IsTrue(landed);
            Assert.AreEqual(72f, defender.Health.CurrentHealth);
            Assert.IsTrue(defender.Guard.IsGuardBroken);
            Assert.AreEqual(100f, defender.Stamina.CurrentStamina, "No stamina should be charged for a hit that was not absorbed.");
        }

        [Test]
        public void Block_DoesNothingAgainstEnvironmentalDamage()
        {
            var defender = NewDefender();

            defender.Guard.BeginGuard();
            now = 0.05f;
            var landed = defender.Health.TakeDamage(Swing(15f, null, type: DamageType.Environmental));

            Assert.IsTrue(landed, "A shield does not help against standing in fire, even inside the parry window.");
            Assert.AreEqual(85f, defender.Health.CurrentHealth);
        }

        [Test]
        public void Guard_ParriedSwingCannotLandFromASecondCollider()
        {
            var defender = NewDefender();
            var attacker = NewObject("Attacker");

            defender.Guard.BeginGuard();
            now = 0.05f;
            var swing = Swing(30f, attacker);
            defender.Health.TakeDamage(swing);

            // Same AttackId arriving through another hurtbox after the guard dropped.
            defender.Guard.EndGuard();
            now = 5f;
            Assert.IsFalse(defender.Health.TakeDamage(swing), "One swing, once: the id was consumed by the parry.");
            Assert.AreEqual(100f, defender.Health.CurrentHealth);
        }

        // ------------------------------------------------------------ difficulty

        [Test]
        public void Difficulty_MovesTheParryWindowNotTheDamage()
        {
            var defender = NewDefender(parry: 0.2f, perfect: 0.08f);

            Difficulty.Set(DifficultyMode.Story);
            Assert.AreEqual(0.2f * 1.3f, defender.Guard.ScaledParryWindow, 0.0001f);
            Assert.AreEqual(0.08f * 1.3f, defender.Guard.ScaledPerfectParryWindow, 0.0001f);

            Difficulty.Set(DifficultyMode.Mythic);
            Assert.AreEqual(0.2f * 0.7f, defender.Guard.ScaledParryWindow, 0.0001f);

            // A hit that Story would parry is a block on Mythic.
            var attacker = NewObject("Attacker");
            defender.Guard.BeginGuard();
            now = 0.18f;
            var parried = false;
            EventBus.Subscribe<ParryEvent>(_ => parried = true);
            defender.Health.TakeDamage(Swing(10f, attacker));
            Assert.IsFalse(parried, "0.18s is inside the Normal window but outside Mythic's 0.14s.");
            Assert.AreEqual(100f, defender.Health.CurrentHealth, "It should have been blocked instead.");
        }

        [Test]
        public void Difficulty_ScalesTheDodgeWindowLength()
        {
            var root = NewObject("Player");
            root.AddComponent<HealthComponent>();
            var combat = root.AddComponent<CombatController>();

            var normal = combat.ScaledInvulnerabilityDuration;
            Difficulty.Set(DifficultyMode.Story);
            Assert.AreEqual(normal * 1.3f, combat.ScaledInvulnerabilityDuration, 0.0001f);
            Difficulty.Set(DifficultyMode.Warrior);
            Assert.AreEqual(normal * 0.85f, combat.ScaledInvulnerabilityDuration, 0.0001f);
        }

        // ---------------------------------------------------------- divine energy

        [Test]
        public void DivineEnergy_StartsEmptyClampsAndSpends()
        {
            var divine = NewObject("Player").AddComponent<DivineEnergyComponent>();
            divine.Configure(50f);

            Assert.AreEqual(0f, divine.CurrentEnergy, "Divine energy is earned, not given.");
            divine.Gain(80f);
            Assert.AreEqual(50f, divine.CurrentEnergy);
            Assert.IsFalse(divine.TrySpend(60f));
            Assert.AreEqual(50f, divine.CurrentEnergy, "A refused spend must not deduct.");
            Assert.IsTrue(divine.TrySpend(20f));
            Assert.AreEqual(30f, divine.CurrentEnergy);
        }
    }
}

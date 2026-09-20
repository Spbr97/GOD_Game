using System.Collections.Generic;
using Game.Combat;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// EditMode coverage for the TASK 002 combat rules (SPEC.md section 53).
    ///
    /// These exercise the damage and resource logic, which is where the acceptance
    /// criteria actually live. Trigger-collider behaviour is not covered here because
    /// EditMode has no physics step; see KNOWN_ISSUES.md.
    /// </summary>
    public class CombatTests
    {
        private readonly List<GameObject> spawned = new();

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
        }

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go;
        }

        private HealthComponent NewHealth(string name, float max = 100f)
        {
            var health = NewObject(name).AddComponent<HealthComponent>();
            health.Configure(max);
            return health;
        }

        private static DamageData Hit(float amount, GameObject source = null)
        {
            return new DamageData
            {
                Amount = amount,
                Type = DamageType.Physical,
                Source = source,
                AttackId = DamageData.NextAttackId()
            };
        }

        [Test]
        public void TakeDamage_ReducesHealth()
        {
            var health = NewHealth("Enemy", 100f);

            Assert.IsTrue(health.TakeDamage(Hit(30f)));
            Assert.AreEqual(70f, health.CurrentHealth, 0.001f);
            Assert.IsFalse(health.IsDead);
        }

        [Test]
        public void TakeDamage_AtOrBelowZero_Kills()
        {
            var health = NewHealth("Enemy", 40f);
            var died = 0;
            health.Died += _ => died++;

            health.TakeDamage(Hit(55f));

            Assert.IsTrue(health.IsDead);
            Assert.AreEqual(0f, health.CurrentHealth, 0.001f);
            Assert.AreEqual(1, died, "Died must be raised exactly once.");
        }

        [Test]
        public void TakeDamage_AfterDeath_IsIgnored()
        {
            var health = NewHealth("Enemy", 10f);
            var died = 0;
            health.Died += _ => died++;

            health.TakeDamage(Hit(10f));
            health.TakeDamage(Hit(10f));

            Assert.AreEqual(1, died, "A corpse must not die a second time.");
        }

        /// <summary>The "no duplicate damage events" acceptance criterion for TASK 002.</summary>
        [Test]
        public void TakeDamage_SameAttackIdTwice_AppliesOnce()
        {
            var health = NewHealth("Enemy", 100f);
            var swing = Hit(25f);

            Assert.IsTrue(health.TakeDamage(swing), "First application should land.");
            Assert.IsFalse(health.TakeDamage(swing), "Repeat of the same swing should be rejected.");
            Assert.AreEqual(75f, health.CurrentHealth, 0.001f);
        }

        /// <summary>
        /// The realistic shape of the duplicate-damage bug: one swing overlapping two
        /// hurtboxes that share a health pool.
        /// </summary>
        [Test]
        public void TwoHurtboxes_SharingHealth_ApplyOneSwingOnce()
        {
            var health = NewHealth("Enemy", 100f);

            var torso = new GameObject("Torso");
            torso.transform.SetParent(health.transform);
            torso.AddComponent<BoxCollider>();
            var torsoHurtbox = torso.AddComponent<Hurtbox>();

            var head = new GameObject("Head");
            head.transform.SetParent(health.transform);
            head.AddComponent<BoxCollider>();
            var headHurtbox = head.AddComponent<Hurtbox>();

            var swing = Hit(20f);

            Assert.IsTrue(torsoHurtbox.ApplyDamage(swing));
            Assert.IsFalse(headHurtbox.ApplyDamage(swing));
            Assert.AreEqual(80f, health.CurrentHealth, 0.001f);
        }

        [Test]
        public void TakeDamage_WhileInvulnerable_IsIgnored()
        {
            var health = NewHealth("Player", 100f);
            health.IsInvulnerable = true;

            Assert.IsFalse(health.TakeDamage(Hit(40f)));
            Assert.AreEqual(100f, health.CurrentHealth, 0.001f);
        }

        [Test]
        public void TakeDamage_FromSelf_IsIgnored()
        {
            var health = NewHealth("Player", 100f);

            Assert.IsFalse(health.TakeDamage(Hit(40f, health.gameObject)));
            Assert.AreEqual(100f, health.CurrentHealth, 0.001f);
        }

        [Test]
        public void ResetHealth_RevivesAtFull()
        {
            var health = NewHealth("Player", 80f);
            health.TakeDamage(Hit(200f));
            Assert.IsTrue(health.IsDead);

            health.ResetHealth();

            Assert.IsFalse(health.IsDead);
            Assert.IsFalse(health.IsInvulnerable);
            Assert.AreEqual(80f, health.CurrentHealth, 0.001f);
        }

        /// <summary>
        /// Attack ids are recycled after death, so a respawned player is not immune to
        /// an id they happened to see in their previous life.
        /// </summary>
        [Test]
        public void ResetHealth_ClearsAttackHistory()
        {
            var health = NewHealth("Player", 100f);
            var swing = Hit(10f);

            health.TakeDamage(swing);
            health.ResetHealth();

            Assert.IsTrue(health.TakeDamage(swing));
            Assert.AreEqual(90f, health.CurrentHealth, 0.001f);
        }

        [Test]
        public void DamageData_NextAttackId_IsUniqueAndNonZero()
        {
            var ids = new HashSet<int>();
            for (var i = 0; i < 100; i++)
            {
                var id = DamageData.NextAttackId();
                Assert.AreNotEqual(0, id, "Zero means 'untracked' and must never be allocated.");
                Assert.IsTrue(ids.Add(id), "Attack ids must not repeat.");
            }
        }

        [Test]
        public void Stamina_TrySpend_FailsWithoutEnoughAndSpendsNothing()
        {
            var stamina = NewObject("Player").AddComponent<StaminaComponent>();
            stamina.Configure(30f, 10f, 0.5f);

            Assert.IsTrue(stamina.TrySpend(25f));
            Assert.AreEqual(5f, stamina.CurrentStamina, 0.001f);

            Assert.IsFalse(stamina.TrySpend(25f));
            Assert.AreEqual(5f, stamina.CurrentStamina, 0.001f, "A rejected spend must not deduct.");
        }

        [Test]
        public void Stamina_DoesNotRegenerateDuringTheDelay()
        {
            var stamina = NewObject("Player").AddComponent<StaminaComponent>();
            stamina.Configure(100f, 50f, 1f);
            stamina.TrySpend(40f);

            var spentAt = Time.time;
            stamina.Regenerate(0.5f, spentAt + 0.5f);
            Assert.AreEqual(60f, stamina.CurrentStamina, 0.001f, "Regen must stay blocked inside the delay.");

            stamina.Regenerate(0.5f, spentAt + 1.5f);
            Assert.AreEqual(85f, stamina.CurrentStamina, 0.001f);
        }

        [Test]
        public void Stamina_RegenerationClampsToMax()
        {
            var stamina = NewObject("Player").AddComponent<StaminaComponent>();
            stamina.Configure(50f, 1000f, 0f);
            stamina.TrySpend(10f);

            stamina.Regenerate(10f, 1000f);

            Assert.AreEqual(50f, stamina.CurrentStamina, 0.001f);
        }

        /// <summary>SPEC.md section 33: activating twice must not duplicate rewards.</summary>
        [Test]
        public void Checkpoint_Activate_IsIdempotent()
        {
            var go = NewObject("Checkpoint");
            go.AddComponent<BoxCollider>();
            var checkpoint = go.AddComponent<Checkpoint>();

            var activations = 0;
            void Handler(CheckpointActivatedEvent _) => activations++;
            EventBus.Subscribe<CheckpointActivatedEvent>(Handler);

            checkpoint.Activate();
            checkpoint.Activate();
            checkpoint.Activate();

            EventBus.Unsubscribe<CheckpointActivatedEvent>(Handler);

            Assert.IsTrue(checkpoint.HasBeenActivated);
            Assert.AreEqual(1, activations, "Only the first activation may raise the event.");
        }

        [Test]
        public void Checkpoint_RespawnPosition_FallsBackToItsOwnTransform()
        {
            var go = NewObject("Checkpoint");
            go.AddComponent<BoxCollider>();
            go.transform.position = new Vector3(3f, 0f, 7f);
            var checkpoint = go.AddComponent<Checkpoint>();

            Assert.AreEqual(new Vector3(3f, 0f, 7f), checkpoint.RespawnPosition);
        }
    }
}

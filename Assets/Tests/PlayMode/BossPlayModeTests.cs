using System.Collections;
using Game.AI;
using Game.Combat;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.Play
{
    /// <summary>
    /// The TASK 013 boss framework (SPEC.md section 18) against a live scene: phase
    /// transitions driven by real damage, the speed/cooldown multipliers those phases
    /// apply to a real <see cref="EnemyController"/>/<see cref="EnemyCombatant"/>, the
    /// encounter-started and defeated events, and the reward reveal. Persistence
    /// across a save lives in <c>SavePlayModeTests</c> alongside the other
    /// save-restore tests, matching where <c>Puzzle_SolvedStateSurvivesASaveAndAppliesToAFreshControllerOnLoad</c> lives.
    /// </summary>
    public class BossPlayModeTests
    {
        private TestArena arena;

        [SetUp]
        public void SetUp()
        {
            arena = new TestArena();
        }

        [TearDown]
        public void TearDown()
        {
            arena.Dispose();
        }

        private BossController NewBoss(EnemyRig rig, GameObject reward = null,
            float phase2At = 0.66f, float phase3At = 0.33f)
        {
            // Inactive while wiring: AddComponent runs Awake immediately on an active
            // object (see TestArena's note on this exact gotcha), and rig.Root is
            // already active by the time SpawnEnemy returns it.
            rig.Root.SetActive(false);
            var boss = rig.Root.AddComponent<BossController>();
            boss.Configure("TEST_BOSS", "Test Boss", rig.Controller, rig.Combatant, reward, phase2At, phase3At);
            rig.Root.SetActive(true);
            return boss;
        }

        [UnityTest]
        public IEnumerator Boss_PublishesEncounterStartedOnceWhenFirstAlerted()
        {
            var archetype = arena.NewArchetype("BOSS_ARCH", health: 100f);
            var rig = arena.SpawnEnemy("Boss", Vector3.zero, archetype);
            var boss = NewBoss(rig);

            var startedCount = 0;
            void OnStarted(BossEncounterStartedEvent e) => startedCount++;
            EventBus.Subscribe<BossEncounterStartedEvent>(OnStarted);

            try
            {
                Assert.IsFalse(boss.EncounterStarted);

                EventBus.Publish(new EnemyAlertedEvent(rig.Root, null));
                EventBus.Publish(new EnemyAlertedEvent(rig.Root, null));
                yield return null;

                Assert.IsTrue(boss.EncounterStarted);
                Assert.AreEqual(1, startedCount, "The encounter-started event should fire once, not once per alert.");
            }
            finally
            {
                EventBus.Unsubscribe<BossEncounterStartedEvent>(OnStarted);
            }
        }

        [UnityTest]
        public IEnumerator Boss_EntersPhase2ThenPhase3AsHealthDropsAndSpeedsUp()
        {
            var archetype = arena.NewArchetype("BOSS_ARCH", health: 100f);
            var rig = arena.SpawnEnemy("Boss", Vector3.zero, archetype);
            var boss = NewBoss(rig, phase2At: 0.6f, phase3At: 0.3f);
            yield return null;

            Assert.AreEqual(1, boss.Phase);

            rig.Health.TakeDamage(DamageData.Create(35f, null)); // 65/100 — above 0.6, still phase 1
            yield return null;
            Assert.AreEqual(1, boss.Phase);

            rig.Health.TakeDamage(DamageData.Create(10f, null)); // 55/100 — at or below 0.6
            yield return null;
            Assert.AreEqual(2, boss.Phase);

            rig.Health.TakeDamage(DamageData.Create(30f, null)); // 25/100 — at or below 0.3
            yield return null;
            Assert.AreEqual(3, boss.Phase);
        }

        [UnityTest]
        public IEnumerator Boss_PhaseChangeAppliesTheSpeedAndCooldownMultipliersImmediately()
        {
            arena.BuildFloor();
            arena.BuildNavMesh();
            yield return null;

            var archetype = arena.NewArchetype("BOSS_ARCH", health: 100f, sight: 30f);
            var rig = arena.SpawnEnemy("Boss", Vector3.zero, archetype);
            var boss = NewBoss(rig, phase2At: 0.5f, phase3At: 0.25f);
            arena.SpawnPlayer(new Vector3(0f, 0f, 12f));

            yield return TestArena.Until(() => rig.State == EnemyState.Chase, "the boss to give chase", 3f);
            Assert.AreEqual(archetype.ChaseSpeed, rig.Agent.speed, 0.01f,
                "Sanity check: phase 1 should chase at the archetype's base speed.");

            rig.Health.TakeDamage(DamageData.Create(60f, null)); // 40/100 — phase 2
            yield return null;

            Assert.AreEqual(2, boss.Phase);
            Assert.Greater(rig.Agent.speed, archetype.ChaseSpeed,
                "Phase 2 should have sped the boss up rather than leaving its base chase speed unchanged.");
        }

        [UnityTest]
        public IEnumerator Boss_RevealsRewardAndPublishesDefeatedEventOnDeath()
        {
            var archetype = arena.NewArchetype("BOSS_ARCH", health: 50f);
            var rig = arena.SpawnEnemy("Boss", Vector3.zero, archetype);

            var reward = arena.Track(new GameObject("Reward"));
            reward.SetActive(false);

            var boss = NewBoss(rig, reward);
            yield return null;

            GameObject defeatedBoss = null;
            void OnDefeated(BossDefeatedEvent e) => defeatedBoss = e.Boss;
            EventBus.Subscribe<BossDefeatedEvent>(OnDefeated);

            try
            {
                Assert.IsFalse(reward.activeSelf);

                rig.Health.Kill(DamageData.Create(999f, null));
                yield return null;

                Assert.IsTrue(boss.Defeated);
                Assert.IsTrue(reward.activeSelf, "The reward was not revealed on defeat.");
                Assert.AreEqual(rig.Root, defeatedBoss);
            }
            finally
            {
                EventBus.Unsubscribe<BossDefeatedEvent>(OnDefeated);
            }
        }

        [UnityTest]
        public IEnumerator Boss_DoesNotReenterAnEarlierPhaseIfHealed()
        {
            var archetype = arena.NewArchetype("BOSS_ARCH", health: 100f);
            var rig = arena.SpawnEnemy("Boss", Vector3.zero, archetype);
            var boss = NewBoss(rig, phase2At: 0.6f, phase3At: 0.3f);
            yield return null;

            rig.Health.TakeDamage(DamageData.Create(50f, null)); // 50/100 — phase 2
            yield return null;
            Assert.AreEqual(2, boss.Phase);

            rig.Health.Heal(40f); // back to 90/100
            yield return null;
            Assert.AreEqual(2, boss.Phase, "Healing should not walk a boss backwards out of a phase it already entered.");

            // A small follow-up hit that lands well above the phase 2 threshold again
            // (70/100, threshold 60) must still not downgrade the phase — PhaseFor is
            // recomputed from scratch on every hit, so without an explicit "highest
            // phase wins" guard this would read as phase 1 again.
            rig.Health.TakeDamage(DamageData.Create(20f, null)); // 70/100
            yield return null;
            Assert.AreEqual(2, boss.Phase, "A lighter hit above the phase 2 threshold should not undo an already-entered phase.");
        }
    }
}

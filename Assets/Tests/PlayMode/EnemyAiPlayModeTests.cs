using System.Collections;
using Game.AI;
using Game.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Play
{
    /// <summary>
    /// Enemy behaviour on a real, baked NavMesh (SPEC.md section 53: patrol, chase,
    /// attack, death, navigation failure).
    ///
    /// <c>EnemyAiTests.cs</c> proves the transition table is right by calling
    /// <see cref="EnemyController.Decide"/> directly. These prove that an enemy
    /// wired into a scene actually does the thing the table says — which is a
    /// different claim, and the one that was false when TASK 004 first ran.
    /// </summary>
    public class EnemyAiPlayModeTests
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

        /// <summary>Ground first, then the NavMesh, then the actors. See TestArena.BuildNavMesh.</summary>
        private IEnumerator Ground()
        {
            arena.BuildFloor();
            arena.BuildNavMesh();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Patrol_WalksItsRouteUnprompted()
        {
            yield return Ground();

            var archetype = arena.NewArchetype();
            var route = arena.SpawnPatrolRoute(
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 8f),
                new Vector3(8f, 0f, 8f),
                new Vector3(8f, 0f, 0f));

            // No player in the arena at all, so nothing can provoke it: whatever
            // movement happens is the patrol itself.
            var enemy = arena.SpawnEnemy("Patroller", Vector3.zero, archetype, route);
            yield return null;

            var start = enemy.Position;
            yield return new WaitForSeconds(4f);

            var travelled = Vector3.Distance(enemy.Position, start);

            Assert.AreEqual(EnemyState.Patrol, enemy.State, "The enemy left patrol with nothing to react to.");
            Assert.Greater(travelled, 2f,
                $"A patrolling enemy moved only {travelled:0.0}m in four seconds. " +
                "This is the shape of the TASK 004 defect where Patrol and ReturnHome fought each other.");
        }

        [UnityTest]
        public IEnumerator Chase_ClosesOnThePlayerItCanSee()
        {
            yield return Ground();

            var archetype = arena.NewArchetype(sight: 30f, attackRange: 1.5f);
            var enemy = arena.SpawnEnemy("Chaser", Vector3.zero, archetype);
            var player = arena.SpawnPlayer(new Vector3(0f, 0f, 12f));

            yield return TestArena.Until(() => enemy.State == EnemyState.Chase, "the enemy to give chase", 3f);

            var startDistance = Vector3.Distance(enemy.Position, player.Position);
            yield return new WaitForSeconds(2f);
            var endDistance = Vector3.Distance(enemy.Position, player.Position);

            Assert.Less(endDistance, startDistance - 2f,
                $"The enemy entered Chase but closed only {startDistance - endDistance:0.0}m in two seconds.");
        }

        [UnityTest]
        public IEnumerator LineOfSight_AWallBetweenThem_HidesThePlayer()
        {
            arena.BuildFloor();
            arena.BuildWall(new Vector3(0f, 2f, 6f), new Vector3(16f, 4f, 1f));
            arena.BuildNavMesh();
            yield return null;

            var archetype = arena.NewArchetype(sight: 30f);
            var enemy = arena.SpawnEnemy("Guard", Vector3.zero, archetype);
            arena.SpawnPlayer(new Vector3(0f, 0f, 12f));

            var everSaw = false;
            yield return TestArena.Observe(1.5f, () => everSaw |= enemy.Perception.HasLineOfSight);

            Assert.IsFalse(everSaw, "The enemy saw the player through a solid wall.");
            Assert.AreEqual(EnemyState.Idle, enemy.State, "The enemy reacted to a player it could not see.");
        }

        [UnityTest]
        public IEnumerator Attack_TelegraphsBeforeTheHitboxOpens()
        {
            yield return Ground();

            var archetype = arena.NewArchetype(telegraph: 0.5f, attackRange: 2.5f);
            var enemy = arena.SpawnEnemy("Attacker", Vector3.zero, archetype);
            arena.SpawnPlayer(new Vector3(0f, 0f, 2f));

            var telegraphSeconds = 0f;
            var hitboxOpened = false;
            var hitboxOpenedDuringTelegraph = false;

            yield return TestArena.Observe(4f, () =>
            {
                if (enemy.Combatant.IsTelegraphing)
                {
                    telegraphSeconds += Time.deltaTime;
                    hitboxOpenedDuringTelegraph |= enemy.Combatant.IsHitboxOpen;
                }

                hitboxOpened |= enemy.Combatant.IsHitboxOpen;
            });

            Assert.Greater(telegraphSeconds, 0.2f,
                "The enemy never telegraphed, or the wind-up was too short to read.");
            Assert.IsFalse(hitboxOpenedDuringTelegraph,
                "The hitbox was open during the telegraph, so the wind-up was not a warning at all.");
            Assert.IsTrue(hitboxOpened, "The telegraph was never followed by a damage window.");
        }

        [UnityTest]
        public IEnumerator Attack_DamagesThePlayerInReach()
        {
            yield return Ground();

            var archetype = arena.NewArchetype(damage: 10f, attackRange: 2.5f, telegraph: 0.3f);
            var enemy = arena.SpawnEnemy("Attacker", Vector3.zero, archetype);
            var player = arena.SpawnPlayer(new Vector3(0f, 0f, 2f));

            yield return TestArena.Until(() => player.Health.CurrentHealth < 100f,
                "the enemy to land a hit on the player", 5f);

            Assert.AreEqual(90f, player.Health.CurrentHealth, 0.01f,
                "The hit landed but for the wrong amount.");
            Assert.AreEqual(EnemyState.Attack, enemy.State);
        }

        [UnityTest]
        public IEnumerator Leash_BreaksOffRatherThanFollowingThePlayerForever()
        {
            yield return Ground();

            // Sees a long way, gives up late, but is tied to a short leash: the only
            // thing that can stop this chase is the anti-cheese rule in SPEC.md 56.
            var archetype = arena.NewArchetype(sight: 60f, sightCone: 360f, attackRange: 1.5f);
            archetype.ConfigureBehaviour(6f, 0f, 2f, 5f, 2f, 3f);

            var enemy = arena.SpawnEnemy("Leashed", Vector3.zero, archetype);
            arena.SpawnPlayer(new Vector3(0f, 0f, 25f));

            yield return TestArena.Until(() => enemy.State == EnemyState.Chase, "the chase to begin", 3f);

            var brokeOffWhileTheTargetWasVisible = false;
            var furthestFromHome = 0f;

            yield return TestArena.Observe(8f, () =>
            {
                furthestFromHome = Mathf.Max(furthestFromHome, enemy.DistanceFromHome);

                if (enemy.State == EnemyState.ReturnHome && enemy.Perception.HasLineOfSight)
                {
                    brokeOffWhileTheTargetWasVisible = true;
                }
            });

            Assert.IsTrue(brokeOffWhileTheTargetWasVisible,
                "The enemy never broke off, so aggro can be dragged across the map. " +
                "Until now the leash range had only ever been unit-tested.");
            Assert.Less(furthestFromHome, archetype.LeashRange + 3f,
                $"The enemy reached {furthestFromHome:0.0}m from home against a {archetype.LeashRange:0.#}m leash.");
        }

        [UnityTest]
        public IEnumerator Stagger_InterruptsTheSwingThatWasComing()
        {
            yield return Ground();

            var archetype = arena.NewArchetype(poise: 20f, telegraph: 0.8f, attackRange: 2.5f, health: 200f);
            var enemy = arena.SpawnEnemy("Attacker", Vector3.zero, archetype);
            var player = arena.SpawnPlayer(new Vector3(0f, 0f, 2f));

            yield return TestArena.Until(() => enemy.Combatant.IsTelegraphing, "a swing to begin", 5f);

            enemy.Health.TakeDamage(DamageData.Create(30f, player.Root));
            yield return null;

            Assert.IsTrue(enemy.Stagger.IsStaggered, "Damage past the poise pool did not break it.");
            Assert.IsFalse(enemy.Combatant.IsAttacking, "The stagger did not interrupt the swing.");
            Assert.AreEqual(EnemyState.Stagger, enemy.State);

            yield return TestArena.Until(() => !enemy.Stagger.IsStaggered, "the stagger to expire", 3f);
            Assert.AreNotEqual(EnemyState.Stagger, enemy.State, "The enemy never recovered from the stagger.");
        }

        [UnityTest]
        public IEnumerator Death_StopsTheEnemyActing()
        {
            yield return Ground();

            var archetype = arena.NewArchetype(attackRange: 2.5f);
            var enemy = arena.SpawnEnemy("Doomed", Vector3.zero, archetype);
            var player = arena.SpawnPlayer(new Vector3(0f, 0f, 2f));

            yield return TestArena.Until(() => enemy.State == EnemyState.Attack, "the enemy to engage", 4f);

            enemy.Health.Kill(DamageData.Create(999f, player.Root));
            yield return null;

            Assert.AreEqual(EnemyState.Dead, enemy.State);

            var restingPlace = enemy.Position;
            var healthAfterDeath = player.Health.CurrentHealth;

            yield return new WaitForSeconds(2f);

            Assert.Less(Vector3.Distance(enemy.Position, restingPlace), 0.2f, "A dead enemy kept moving.");
            Assert.IsFalse(enemy.Combatant.IsAttacking, "A dead enemy kept swinging.");
            Assert.AreEqual(healthAfterDeath, player.Health.CurrentHealth, 0.01f,
                "A dead enemy went on damaging the player.");
        }

        [UnityTest]
        public IEnumerator Navigation_ADestinationOffTheNavMesh_FailsInsteadOfSilentlySucceeding()
        {
            yield return Ground();

            var enemy = arena.SpawnEnemy("Wanderer", Vector3.zero, arena.NewArchetype());
            yield return null;

            var accepted = enemy.Navigator.SetDestination(new Vector3(5000f, 0f, 5000f));

            Assert.IsFalse(accepted, "A destination far outside the NavMesh was accepted.");
            Assert.IsTrue(enemy.Navigator.LastMoveFailed, "The failure was not reported to the caller.");

            // SPEC.md section 50: after a navigation failure the enemy must keep
            // running, not freeze where it stands.
            yield return new WaitForSeconds(1f);
            Assert.AreNotEqual(EnemyState.Dead, enemy.State);
            Assert.IsTrue(enemy.Agent.isOnNavMesh, "The enemy left the NavMesh chasing an invalid destination.");
        }

        [UnityTest]
        public IEnumerator Navigation_WithoutAUsableAgent_SteersDirectlyInsteadOfFreezing()
        {
            yield return Ground();

            var archetype = arena.NewArchetype(sight: 30f, attackRange: 1.5f);
            var enemy = arena.SpawnEnemy("Unmeshed", Vector3.zero, archetype);
            var player = arena.SpawnPlayer(new Vector3(0f, 0f, 12f));

            yield return null;

            // SPEC.md section 54, edge case 11: the navigation mesh becomes unavailable.
            enemy.Agent.enabled = false;
            yield return null;

            Assert.IsTrue(enemy.Navigator.IsUsingFallbackSteering);

            var startDistance = Vector3.Distance(enemy.Position, player.Position);
            yield return new WaitForSeconds(2.5f);
            var endDistance = Vector3.Distance(enemy.Position, player.Position);

            Assert.Less(endDistance, startDistance - 2f,
                $"With no agent the enemy closed only {startDistance - endDistance:0.0}m; it should steer directly.");
        }

        [UnityTest]
        public IEnumerator Group_NeverLetsMoreThanItsLimitSwingAtOnce()
        {
            yield return Ground();

            var archetype = arena.NewArchetype(damage: 4f, attackRange: 2.5f, telegraph: 0.3f);
            var group = arena.SpawnGroup(Vector3.zero, 1);

            arena.SpawnEnemy("A", new Vector3(-2f, 0f, 0f), archetype, group: group);
            arena.SpawnEnemy("B", new Vector3(2f, 0f, 0f), archetype, group: group);
            arena.SpawnEnemy("C", new Vector3(0f, 0f, -2f), archetype, group: group);
            arena.SpawnPlayer(Vector3.zero);

            var highWaterMark = 0;
            var everAttacked = false;

            yield return TestArena.Observe(6f, () =>
            {
                var attacking = 0;
                foreach (var member in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                {
                    if (member.State == EnemyState.Attack)
                    {
                        attacking++;
                    }
                }

                everAttacked |= attacking > 0;
                highWaterMark = Mathf.Max(highWaterMark, attacking);
            });

            Assert.IsTrue(everAttacked, "None of the three enemies ever engaged, so the limit proved nothing.");
            Assert.LessOrEqual(highWaterMark, group.MaxSimultaneousAttackers,
                $"{highWaterMark} enemies attacked at once against a limit of {group.MaxSimultaneousAttackers}.");
        }

        [UnityTest]
        public IEnumerator FriendlyFire_TwoEnemiesOnOnePlayer_DoNotKillEachOther()
        {
            yield return Ground();

            var archetype = arena.NewArchetype(damage: 6f, attackRange: 3f, telegraph: 0.3f, health: 80f);
            var group = arena.SpawnGroup(Vector3.zero, 2);

            // Close enough together that each stands inside the other's swing.
            var left = arena.SpawnEnemy("Left", new Vector3(-1f, 0f, 0f), archetype, group: group);
            var right = arena.SpawnEnemy("Right", new Vector3(1f, 0f, 0f), archetype, group: group);
            var player = arena.SpawnPlayer(Vector3.zero);

            yield return TestArena.Until(() => player.Health.CurrentHealth < 100f,
                "the pair to land a hit on the player", 6f);

            yield return new WaitForSeconds(2f);

            Assert.AreEqual(80f, left.Health.CurrentHealth, 0.01f, "An enemy was wounded by its own side.");
            Assert.AreEqual(80f, right.Health.CurrentHealth, 0.01f, "An enemy was wounded by its own side.");
        }
    }
}

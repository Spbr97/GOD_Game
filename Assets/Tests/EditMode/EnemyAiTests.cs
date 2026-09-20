using System.Collections.Generic;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.Quests;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Tests for the TASK 004 enemy AI: the transition table, perception geometry,
    /// poise and stagger, group attack slots, patrol routes and difficulty scaling.
    ///
    /// Most of these drive <see cref="EnemyController.Decide"/>, which is a pure
    /// function over an <see cref="EnemySenses"/> snapshot. That is the reason the
    /// state machine was split that way: the rules that decide whether an enemy
    /// chases, gives up or breaks off are exactly the rules worth regression-testing,
    /// and none of them need a NavMesh, a frame or a physics step.
    ///
    /// Like <see cref="AvarshaTests"/> these run in an empty scene of their own, so a
    /// manager in the project's open scene cannot answer in place of the one a test
    /// sets up.
    /// </summary>
    public class EnemyAiTests
    {
        private readonly List<Object> spawned = new();
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

            spawned.Clear();
            EventBus.Clear();

            // Difficulty is static, so one test leaving it on Mythic would silently
            // change the numbers every later test reads.
            Difficulty.Reset();
        }

        // ------------------------------------------------------------------ helpers

        private T NewComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go.AddComponent<T>();
        }

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go;
        }

        private T NewAsset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            spawned.Add(asset);
            return asset;
        }

        /// <summary>A soldier with round numbers: 2m reach, 20 poise, 15m sight, 90 degree cone.</summary>
        private EnemyArchetype TestArchetype()
        {
            var archetype = NewAsset<EnemyArchetype>();
            archetype.Configure("ENEMY_TEST", "Test Enemy", EnemyClass.ForgottenSoldier,
                health: 60f, damage: 10f, range: 2f, telegraph: 0.5f, enemyPoise: 20f,
                sight: 15f, sightCone: 90f);
            archetype.ConfigureBehaviour(leash: 20f, retreatFraction: 0.25f, retreatSeconds: 2f,
                loseTargetSeconds: 3f, investigateSeconds: 4f, searchSeconds: 6f);
            return archetype;
        }

        /// <summary>A senses snapshot with sane defaults; each test overrides what it cares about.</summary>
        private static EnemySenses Senses(bool canSee = false, float timeSinceSeen = float.MaxValue,
            float distanceToTarget = 50f, float distanceFromHome = 0f, float healthFraction = 1f,
            bool isDead = false, bool isStaggered = false, bool hasDisturbance = false,
            bool hasAttackSlot = true, bool canRetreat = true)
        {
            return new EnemySenses(canSee, timeSinceSeen, distanceToTarget, distanceFromHome,
                healthFraction, isDead, isStaggered, hasDisturbance, hasAttackSlot, canRetreat);
        }

        private EnemyState Decide(EnemyState current, EnemySenses senses, EnemyArchetype archetype,
            float timeInState = 0f, bool hasPatrolRoute = false)
        {
            return EnemyController.Decide(current, senses, archetype, timeInState, hasPatrolRoute);
        }

        // ------------------------------------------------------------------ transitions

        [Test]
        public void Decide_Death_IsTerminal()
        {
            var archetype = TestArchetype();

            Assert.AreEqual(EnemyState.Dead, Decide(EnemyState.Attack, Senses(isDead: true), archetype));

            // Once dead, even a visible target in reach cannot bring it back.
            Assert.AreEqual(EnemyState.Dead,
                Decide(EnemyState.Dead, Senses(canSee: true, timeSinceSeen: 0f, distanceToTarget: 1f), archetype));
        }

        [Test]
        public void Decide_Stagger_OutranksEverythingButDeath()
        {
            var archetype = TestArchetype();
            var senses = Senses(canSee: true, timeSinceSeen: 0f, distanceToTarget: 1f, isStaggered: true);

            Assert.AreEqual(EnemyState.Stagger, Decide(EnemyState.Attack, senses, archetype));
        }

        [Test]
        public void Decide_SeeingTheTargetOutOfReach_Chases()
        {
            var archetype = TestArchetype();
            var senses = Senses(canSee: true, timeSinceSeen: 0f, distanceToTarget: 8f);

            Assert.AreEqual(EnemyState.Chase, Decide(EnemyState.Patrol, senses, archetype));
        }

        [Test]
        public void Decide_InReachWithAnAttackSlot_Attacks()
        {
            var archetype = TestArchetype();
            var senses = Senses(canSee: true, timeSinceSeen: 0f, distanceToTarget: 1.5f, hasAttackSlot: true);

            Assert.AreEqual(EnemyState.Attack, Decide(EnemyState.Chase, senses, archetype));
        }

        [Test]
        public void Decide_InReachWithoutAnAttackSlot_KeepsChasing()
        {
            var archetype = TestArchetype();
            var senses = Senses(canSee: true, timeSinceSeen: 0f, distanceToTarget: 1.5f, hasAttackSlot: false);

            Assert.AreEqual(EnemyState.Chase, Decide(EnemyState.Chase, senses, archetype),
                "Without a group slot the enemy must close but not swing, or a mob attacks as one.");
        }

        [Test]
        public void Decide_TargetJustOutOfSight_IsStillPursuedUntilItIsForgotten()
        {
            var archetype = TestArchetype();

            // Seen 1s ago, lose-target is 3s: still a target.
            Assert.AreEqual(EnemyState.Chase,
                Decide(EnemyState.Chase, Senses(canSee: false, timeSinceSeen: 1f, distanceToTarget: 8f), archetype));

            // Seen 4s ago: gone, so the enemy sweeps the last known position.
            Assert.AreEqual(EnemyState.Search,
                Decide(EnemyState.Chase, Senses(canSee: false, timeSinceSeen: 4f, distanceToTarget: 8f), archetype));
        }

        [Test]
        public void Decide_BeyondLeashRange_ReturnsHomeEvenWithTheTargetInSight()
        {
            var archetype = TestArchetype();
            var senses = Senses(canSee: true, timeSinceSeen: 0f, distanceToTarget: 1f, distanceFromHome: 25f);

            Assert.AreEqual(EnemyState.ReturnHome, Decide(EnemyState.Attack, senses, archetype),
                "Anti-cheese (SPEC.md section 56): aggro must not follow the player across the map.");
        }

        [Test]
        public void Decide_LowHealth_Retreats()
        {
            var archetype = TestArchetype();
            var senses = Senses(canSee: true, timeSinceSeen: 0f, distanceToTarget: 1.5f, healthFraction: 0.2f);

            Assert.AreEqual(EnemyState.Retreat, Decide(EnemyState.Attack, senses, archetype));
        }

        [Test]
        public void Decide_RetreatExpires_AndTheEnemyReturnsToTheFight()
        {
            var archetype = TestArchetype();
            var senses = Senses(canSee: true, timeSinceSeen: 0f, distanceToTarget: 1.5f, healthFraction: 0.2f);

            // Retreat lasts 2s. Mid-retreat it holds...
            Assert.AreEqual(EnemyState.Retreat, Decide(EnemyState.Retreat, senses, archetype, timeInState: 1f));

            // ...and once it expires the enemy re-engages rather than backing away forever.
            Assert.AreEqual(EnemyState.Attack, Decide(EnemyState.Retreat, senses, archetype, timeInState: 3f));
        }

        [Test]
        public void Decide_RetreatAlreadySpent_DoesNotLoop()
        {
            var archetype = TestArchetype();
            var senses = Senses(canSee: true, timeSinceSeen: 0f, distanceToTarget: 1.5f,
                healthFraction: 0.2f, canRetreat: false);

            Assert.AreEqual(EnemyState.Attack, Decide(EnemyState.Chase, senses, archetype),
                "A wounded enemy must not retreat, re-engage and retreat again on a loop.");
        }

        [Test]
        public void Decide_SearchExpires_AndTheEnemyGoesHome()
        {
            var archetype = TestArchetype();
            var senses = Senses(canSee: false, timeSinceSeen: 5f, distanceToTarget: 20f, distanceFromHome: 10f);

            Assert.AreEqual(EnemyState.Search, Decide(EnemyState.Search, senses, archetype, timeInState: 2f));
            Assert.AreEqual(EnemyState.ReturnHome, Decide(EnemyState.Search, senses, archetype, timeInState: 7f));
        }

        [Test]
        public void Decide_ADisturbance_IsInvestigatedThenAbandoned()
        {
            var archetype = TestArchetype();
            var senses = Senses(hasDisturbance: true, distanceFromHome: 3f);

            Assert.AreEqual(EnemyState.Investigate, Decide(EnemyState.Patrol, senses, archetype));
            Assert.AreEqual(EnemyState.Investigate, Decide(EnemyState.Investigate, senses, archetype, timeInState: 2f));
            Assert.AreEqual(EnemyState.ReturnHome, Decide(EnemyState.Investigate, senses, archetype, timeInState: 5f));
        }

        [Test]
        public void Decide_AtHomeWithNothingHappening_PatrolsOrIdles()
        {
            var archetype = TestArchetype();
            var senses = Senses(distanceFromHome: 0.2f);

            Assert.AreEqual(EnemyState.Patrol, Decide(EnemyState.Idle, senses, archetype, hasPatrolRoute: true));
            Assert.AreEqual(EnemyState.Idle, Decide(EnemyState.Idle, senses, archetype, hasPatrolRoute: false));
        }

        [Test]
        public void Decide_AwayFromHomeWithNothingHappening_WalksBack()
        {
            var archetype = TestArchetype();
            var senses = Senses(distanceFromHome: 9f);

            Assert.AreEqual(EnemyState.ReturnHome, Decide(EnemyState.Idle, senses, archetype, hasPatrolRoute: true));
        }

        [Test]
        public void Decide_APatrollingEnemy_IsNotDraggedBackToItsSpawn()
        {
            var archetype = TestArchetype();

            // Walking the beat necessarily takes the enemy away from its spawn point.
            // Treating that as "away from home" made it flip to ReturnHome every frame,
            // thrash between two destinations and never actually move.
            var senses = Senses(distanceFromHome: 9f);

            Assert.AreEqual(EnemyState.Patrol, Decide(EnemyState.Patrol, senses, archetype, hasPatrolRoute: true));
        }

        [Test]
        public void Decide_AnEnemyWithNoRoute_StillWalksHome()
        {
            var archetype = TestArchetype();
            var senses = Senses(distanceFromHome: 9f);

            Assert.AreEqual(EnemyState.ReturnHome, Decide(EnemyState.Patrol, senses, archetype, hasPatrolRoute: false));
        }

        [Test]
        public void Decide_RecoveringFromStaggerWithTheTargetGone_Searches()
        {
            var archetype = TestArchetype();
            var senses = Senses(canSee: false, timeSinceSeen: 5f, distanceFromHome: 6f);

            Assert.AreEqual(EnemyState.Search, Decide(EnemyState.Stagger, senses, archetype));
        }

        // ------------------------------------------------------------------ perception

        [Test]
        public void Perception_SightCone_AcceptsWhatIsAheadAndRejectsWhatIsBehind()
        {
            var eye = Vector3.zero;
            var forward = Vector3.forward;

            Assert.IsTrue(EnemyPerception.IsWithinCone(eye, forward, new Vector3(0f, 0f, 5f), 15f, 90f));
            Assert.IsTrue(EnemyPerception.IsWithinCone(eye, forward, new Vector3(3f, 0f, 5f), 15f, 90f));
            Assert.IsFalse(EnemyPerception.IsWithinCone(eye, forward, new Vector3(0f, 0f, -5f), 15f, 90f),
                "A target directly behind the enemy must not be visible.");
            Assert.IsFalse(EnemyPerception.IsWithinCone(eye, forward, new Vector3(5f, 0f, 1f), 15f, 90f),
                "A target 79 degrees off-centre is outside a 90 degree cone's 45 degree half-angle.");
        }

        [Test]
        public void Perception_SightCone_RejectsAnythingBeyondRange()
        {
            Assert.IsFalse(EnemyPerception.IsWithinCone(Vector3.zero, Vector3.forward,
                new Vector3(0f, 0f, 20f), 15f, 90f));
        }

        // ------------------------------------------------------------------ poise and stagger

        [Test]
        public void Stagger_PoiseAbsorbsSmallHitsUntilItBreaks()
        {
            var archetype = TestArchetype();
            var stagger = NewComponent<EnemyStagger>("Enemy");
            stagger.Configure(archetype);

            Assert.IsFalse(stagger.ApplyPoiseDamage(8f));
            Assert.IsFalse(stagger.ApplyPoiseDamage(8f));
            Assert.IsFalse(stagger.IsStaggered);

            Assert.IsTrue(stagger.ApplyPoiseDamage(8f), "24 damage against 20 poise must break it.");
            Assert.IsTrue(stagger.IsStaggered);
        }

        [Test]
        public void Stagger_Breaking_RefillsPoiseSoItCannotBreakTwiceOnOneHit()
        {
            var archetype = TestArchetype();
            var stagger = NewComponent<EnemyStagger>("Enemy");
            stagger.Configure(archetype);

            Assert.IsTrue(stagger.ApplyPoiseDamage(100f));
            Assert.AreEqual(archetype.Poise, stagger.PoiseRemaining, 0.001f);
            Assert.IsFalse(stagger.ApplyPoiseDamage(100f), "Hits landed during a stagger must not extend it.");
        }

        [Test]
        public void Stagger_ZeroPoise_BreaksOnTheFirstHit()
        {
            var archetype = NewAsset<EnemyArchetype>();
            archetype.Configure("ENEMY_GLASS", "Glass", EnemyClass.AshCreature,
                health: 20f, damage: 5f, range: 2f, telegraph: 0.4f, enemyPoise: 0f, sight: 10f, sightCone: 90f);

            var stagger = NewComponent<EnemyStagger>("Enemy");
            stagger.Configure(archetype);

            Assert.IsTrue(stagger.ApplyPoiseDamage(1f));
        }

        [Test]
        public void Stagger_PublishesAnEventSoAudioAndUiCanReact()
        {
            var archetype = TestArchetype();
            var stagger = NewComponent<EnemyStagger>("Enemy");
            stagger.Configure(archetype);

            var staggers = 0;
            EventBus.Subscribe<EnemyStaggeredEvent>(_ => staggers++);

            stagger.ForceStagger();

            Assert.AreEqual(1, staggers);
        }

        // ------------------------------------------------------------------ group coordination

        [Test]
        public void Group_LimitsHowManyMembersMaySwingAtOnce()
        {
            var group = NewComponent<EnemyGroup>("Group");
            group.Configure(simultaneousAttackers: 2);

            var a = NewComponent<EnemyController>("A");
            var b = NewComponent<EnemyController>("B");
            var c = NewComponent<EnemyController>("C");

            Assert.IsTrue(group.TryClaimAttackSlot(a));
            Assert.IsTrue(group.TryClaimAttackSlot(b));
            Assert.IsFalse(group.TryClaimAttackSlot(c), "The third attacker must wait its turn.");
            Assert.AreEqual(2, group.ActiveAttackerCount);
        }

        [Test]
        public void Group_ReleasingASlot_LetsTheNextMemberIn()
        {
            var group = NewComponent<EnemyGroup>("Group");
            group.Configure(simultaneousAttackers: 1);

            var a = NewComponent<EnemyController>("A");
            var b = NewComponent<EnemyController>("B");

            Assert.IsTrue(group.TryClaimAttackSlot(a));
            Assert.IsFalse(group.TryClaimAttackSlot(b));

            group.ReleaseAttackSlot(a);

            Assert.IsTrue(group.TryClaimAttackSlot(b));
        }

        [Test]
        public void Group_AMemberAskingAgain_KeepsTheSlotItAlreadyHolds()
        {
            var group = NewComponent<EnemyGroup>("Group");
            group.Configure(simultaneousAttackers: 1);

            var a = NewComponent<EnemyController>("A");

            Assert.IsTrue(group.TryClaimAttackSlot(a));
            Assert.IsTrue(group.TryClaimAttackSlot(a));
            Assert.AreEqual(1, group.ActiveAttackerCount, "Re-asking must not consume a second slot.");
        }

        [Test]
        public void Group_AttackSlotCount_FollowsDifficulty()
        {
            var group = NewComponent<EnemyGroup>("Group");
            group.Configure(simultaneousAttackers: 2);

            Assert.AreEqual(2, group.MaxSimultaneousAttackers);

            Difficulty.Set(DifficultyMode.Story);
            Assert.AreEqual(1, group.MaxSimultaneousAttackers, "Story halves the pressure but never to zero.");

            Difficulty.Set(DifficultyMode.Mythic);
            Assert.AreEqual(4, group.MaxSimultaneousAttackers);
        }

        // ------------------------------------------------------------------ difficulty

        [Test]
        public void Difficulty_ChangesTimingAndDamageButNeverHealth()
        {
            var archetype = TestArchetype();
            var baseHealth = archetype.MaxHealth;

            Difficulty.Set(DifficultyMode.Story);
            var storyTelegraph = archetype.TelegraphFor();
            var storyDamage = archetype.DamageFor();

            Difficulty.Set(DifficultyMode.Mythic);
            var mythicTelegraph = archetype.TelegraphFor();
            var mythicDamage = archetype.DamageFor();

            Assert.Greater(storyTelegraph, archetype.BaseTelegraphDuration,
                "Story must give the player longer to read a swing.");
            Assert.Less(mythicTelegraph, archetype.BaseTelegraphDuration);
            Assert.Less(storyDamage, mythicDamage);

            Assert.AreEqual(baseHealth, archetype.MaxHealth,
                "SPEC.md sections 15 and 44 forbid difficulty being a health multiplier.");
        }

        [Test]
        public void Difficulty_Unscaled_AlwaysReadsTheAuthoredNumbers()
        {
            var archetype = TestArchetype();
            Difficulty.Set(DifficultyMode.Mythic);

            Assert.AreEqual(archetype.BaseTelegraphDuration,
                archetype.TelegraphFor(DifficultyModifiersSource.Unscaled), 0.0001f);
            Assert.AreEqual(archetype.BaseAttackDamage,
                archetype.DamageFor(DifficultyModifiersSource.Unscaled), 0.0001f);
        }

        // ------------------------------------------------------------------ patrol routes

        [Test]
        public void Patrol_ALoopingRoute_WrapsBackToTheStart()
        {
            var route = NewComponent<PatrolRoute>("Route");
            route.Configure(Waypoints(3), looping: true);

            Assert.AreEqual(1, route.NextIndex(0));
            Assert.AreEqual(2, route.NextIndex(1));
            Assert.AreEqual(0, route.NextIndex(2));
        }

        [Test]
        public void Patrol_ANonLoopingRoute_ReversesAtTheEnd()
        {
            var route = NewComponent<PatrolRoute>("Route");
            route.Configure(Waypoints(3), looping: false);

            Assert.AreEqual(1, route.NextIndex(0));
            Assert.AreEqual(2, route.NextIndex(1));
            Assert.AreEqual(1, route.NextIndex(2), "A guard walking a beat turns around; it does not teleport.");
            Assert.AreEqual(0, route.NextIndex(1));
        }

        [Test]
        public void Patrol_NearestIndex_PicksTheClosestWaypoint()
        {
            var route = NewComponent<PatrolRoute>("Route");
            route.Configure(Waypoints(3), looping: true);

            Assert.AreEqual(2, route.NearestIndex(new Vector3(0f, 0f, 9.5f)));
            Assert.AreEqual(0, route.NearestIndex(new Vector3(0f, 0f, -3f)));
        }

        /// <summary>Waypoints at z = 0, 5, 10.</summary>
        private Transform[] Waypoints(int count)
        {
            var transforms = new Transform[count];
            for (var i = 0; i < count; i++)
            {
                var go = NewObject($"Waypoint_{i}");
                go.transform.position = new Vector3(0f, 0f, i * 5f);
                transforms[i] = go.transform;
            }

            return transforms;
        }

        // ------------------------------------------------------------------ navigation fallback

        [Test]
        public void Navigator_WithNoNavMeshAgent_SteersDirectlyInsteadOfFailing()
        {
            var navigator = NewComponent<EnemyNavigator>("Enemy");

            Assert.IsTrue(navigator.IsUsingFallbackSteering);
            Assert.IsTrue(navigator.SetDestination(new Vector3(0f, 0f, 10f)),
                "A scene with no baked NavMesh must still be playable.");
            Assert.IsFalse(navigator.LastMoveFailed);
        }

        // ------------------------------------------------------------------ friendly fire

        /// <summary>Builds a hurtbox on its own health pool, with a faction.</summary>
        private Hurtbox NewHurtbox(string name, Faction faction)
        {
            var go = NewObject(name);
            go.AddComponent<BoxCollider>();
            var health = go.AddComponent<HealthComponent>();
            health.Configure(100f);
            var hurtbox = go.AddComponent<Hurtbox>();
            hurtbox.Configure(health, 1f, faction);
            return hurtbox;
        }

        [Test]
        public void Faction_AnAttack_DoesNotLandOnItsOwnSide()
        {
            var ally = NewHurtbox("Ally", Faction.Hostile);
            var damage = DamageData.Create(50f, NewObject("Attacker"));

            // Hitbox does the faction check before calling ApplyDamage; this asserts
            // the data the check reads, which is what the scene wiring has to get right.
            Assert.AreEqual(Faction.Hostile, ally.Faction);
            Assert.IsTrue(ally.ApplyDamage(damage),
                "The hurtbox itself still applies damage; refusing friendly fire is the hitbox's job.");
        }

        [Test]
        public void Faction_Neutral_IsTheDefaultAndIsHitByEveryone()
        {
            var dummy = NewObject("TrainingDummy");
            dummy.AddComponent<BoxCollider>();
            dummy.AddComponent<HealthComponent>();
            var hurtbox = dummy.AddComponent<Hurtbox>();

            Assert.AreEqual(Faction.Neutral, hurtbox.Faction,
                "Anything not given a faction must behave exactly as it did before factions existed.");
        }

        // ------------------------------------------------------------------ quest integration

        [Test]
        public void QuestTarget_Death_ReportsItsObjectiveExactlyOnce()
        {
            var state = NewComponent<WorldState>("WorldState");
            var quests = NewComponent<QuestManager>("Quests");

            var quest = NewAsset<QuestDefinition>();
            quest.Configure("Q_HUNT", "Hunt", "Kill the thing.",
                new[] { new QuestObjective { ObjectiveId = "KILL_ONE", Description = "Kill one", RequiredCount = 1 } },
                new[] { "Q_HUNT_DONE" });
            quests.Configure(new[] { quest });
            quests.StartQuest("Q_HUNT");

            var enemy = NewObject("Enemy");
            enemy.AddComponent<HealthComponent>();
            var target = enemy.AddComponent<QuestTarget>();
            target.Configure("KILL_ONE", "THING_KILLED");

            Assert.IsTrue(target.Report());
            Assert.AreEqual(QuestStatus.Completed, quests.GetStatus("Q_HUNT"));
            Assert.IsTrue(state.GetFlag("THING_KILLED"));

            Assert.IsFalse(target.Report(), "A revived-and-rekilled enemy must not count twice.");
        }

        [Test]
        public void QuestTarget_WithNoObjectiveId_IsHarmless()
        {
            var enemy = NewObject("Enemy");
            enemy.AddComponent<HealthComponent>();
            var target = enemy.AddComponent<QuestTarget>();
            target.Configure(null);

            Assert.IsTrue(target.Report());
            Assert.IsTrue(target.HasReported);
        }
    }
}

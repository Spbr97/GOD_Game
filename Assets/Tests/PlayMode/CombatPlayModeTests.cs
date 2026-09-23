using System.Collections;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.Memory;
using Game.Quests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Play
{
    /// <summary>
    /// Combat over real frames (SPEC.md section 53: damage, death, dodge).
    ///
    /// These cover what <c>CombatTests.cs</c> cannot: a swing is a coroutine, damage
    /// arrives through a physics query, and death leads to a respawn two seconds
    /// later. All three need a running game loop.
    /// </summary>
    public class CombatPlayModeTests
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

        [UnityTest]
        public IEnumerator Swing_OpensTheHitboxOnlyAfterTheWindup()
        {
            var player = arena.SpawnPlayer(Vector3.zero, withWeapon: true);

            Assert.IsTrue(player.Weapon.TrySwing(AttackType.Light), "The swing was refused.");
            Assert.IsFalse(player.Weapon.IsHitboxOpen, "The hitbox opened on the same frame as the input, with no wind-up.");

            yield return TestArena.Until(() => player.Weapon.IsHitboxOpen, "the active window to open", 1f);
            yield return TestArena.Until(() => !player.Weapon.IsHitboxOpen, "the active window to close", 1f);
            yield return TestArena.Until(() => !player.Weapon.IsSwinging, "the recovery to finish", 1f);
        }

        [UnityTest]
        public IEnumerator Swing_DamagesATargetStandingInTheVolume()
        {
            var player = arena.SpawnPlayer(Vector3.zero, withWeapon: true);
            var dummy = arena.SpawnDummy("Dummy", new Vector3(0f, 0f, 1.2f), 60f);

            player.Weapon.TrySwing(AttackType.Light);

            yield return TestArena.Until(() => dummy.Health.CurrentHealth < 60f, "the dummy to take damage", 2f);

            Assert.AreEqual(48f, dummy.Health.CurrentHealth, 0.01f,
                "A light swing should deal exactly its configured damage.");
        }

        [UnityTest]
        public IEnumerator Swing_LandsOnceEvenWhenTheTargetHasSeveralHurtboxes()
        {
            var player = arena.SpawnPlayer(Vector3.zero, withWeapon: true);
            var dummy = arena.SpawnDummy("Dummy", new Vector3(0f, 0f, 1.2f), 60f, hurtboxes: 3);

            var damageEvents = 0;
            dummy.Health.Damaged += _ => damageEvents++;

            player.Weapon.TrySwing(AttackType.Light);

            yield return TestArena.Until(() => !player.Weapon.IsSwinging, "the swing to finish", 2f);

            Assert.AreEqual(1, damageEvents, "One swing against three hurtboxes must apply damage once.");
            Assert.AreEqual(48f, dummy.Health.CurrentHealth, 0.01f);
        }

        [UnityTest]
        public IEnumerator Dodge_OpensAnInvulnerabilityWindowAndClosesIt()
        {
            var player = arena.SpawnPlayer(Vector3.zero, withWeapon: true, withCombatInput: true);

            Assert.IsFalse(player.Health.IsInvulnerable, "The player started invulnerable.");
            Assert.IsTrue(player.Combat.TryDodge(), "The dodge was refused.");

            yield return TestArena.Until(() => player.Health.IsInvulnerable, "the i-frame window to open", 1f);
            yield return TestArena.Until(() => !player.Health.IsInvulnerable, "the i-frame window to close", 1f);
            yield return TestArena.Until(() => !player.Combat.IsDodging, "the dodge to end", 1f);

            Assert.IsFalse(player.Health.IsInvulnerable, "The player was left invulnerable after dodging.");
        }

        [UnityTest]
        public IEnumerator Dodge_CancelsASwingInProgress()
        {
            var player = arena.SpawnPlayer(Vector3.zero, withWeapon: true, withCombatInput: true);
            var dummy = arena.SpawnDummy("Dummy", new Vector3(0f, 0f, 1.2f), 60f);

            player.Weapon.TrySwing(AttackType.Heavy);
            yield return null;

            Assert.IsTrue(player.Combat.TryDodge(), "The dodge was refused mid-swing.");
            Assert.IsFalse(player.Weapon.IsSwinging, "Dodging should cancel the swing, not queue behind it.");

            // Long enough for the cancelled swing's active window to have come and gone.
            yield return new WaitForSeconds(1f);

            Assert.AreEqual(60f, dummy.Health.CurrentHealth, 0.01f,
                "A cancelled swing still landed its damage.");
        }

        [UnityTest]
        public IEnumerator Hazard_DamagesThePlayerStandingInItOnItsOwnCadence()
        {
            var player = arena.SpawnPlayer(Vector3.zero);

            var hazardGo = arena.Track(new GameObject("Hazard"));
            hazardGo.SetActive(false);
            hazardGo.transform.position = Vector3.zero;
            var box = hazardGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(6f, 6f, 6f);
            hazardGo.AddComponent<DamageVolume>();
            hazardGo.SetActive(true);

            yield return TestArena.Until(() => player.Health.CurrentHealth <= 85f,
                "the hazard's first tick", 3f);

            // A second tick proves the cadence, and that the duplicate-damage guard does
            // not swallow repeated damage from the same source.
            yield return TestArena.Until(() => player.Health.CurrentHealth <= 70f,
                "the hazard's second tick", 3f);
        }

        [UnityTest]
        public IEnumerator Death_DisablesControlsThenRespawnsAtTheActiveCheckpoint()
        {
            arena.SpawnCheckpointManager();
            var player = arena.SpawnPlayer(new Vector3(0f, 0f, 20f), withWeapon: true, withCombatInput: true);

            var checkpointGo = arena.Track(new GameObject("Checkpoint"));
            checkpointGo.SetActive(false);
            checkpointGo.transform.position = new Vector3(-3f, 0f, 5f);
            var checkpointCollider = checkpointGo.AddComponent<BoxCollider>();
            checkpointCollider.isTrigger = true;
            var checkpoint = checkpointGo.AddComponent<Checkpoint>();
            checkpointGo.SetActive(true);

            yield return null;
            checkpoint.Activate();

            player.Health.Kill(DamageData.Create(999f, null));

            Assert.IsTrue(player.Death.IsDead);
            Assert.IsFalse(player.Combat.enabled, "Controls stayed live through death.");

            yield return TestArena.Until(() => !player.Death.IsDead, "the respawn", 6f);

            Assert.Less(Vector3.Distance(player.Position, new Vector3(-3f, 0f, 5f)), 0.01f,
                $"The player respawned at {player.Position} rather than at the active checkpoint.");
            Assert.AreEqual(player.Health.MaxHealth, player.Health.CurrentHealth, 0.01f);
            Assert.IsTrue(player.Combat.enabled, "Controls were not handed back after respawning.");
        }
        [UnityTest]
        public IEnumerator Death_ResetsOrdinaryEncounterButKeepsQuestAndMemoryProgress()
        {
            arena.EnsureWorldState();
            arena.SpawnCheckpointManager();
            var quest = arena.TrackAsset(ScriptableObject.CreateInstance<QuestDefinition>());
            quest.Configure("Q_KEEP", "Keep progress", "",
                new[] { new QuestObjective { ObjectiveId = "BEAT", Description = "Beat" } });
            var quests = arena.SpawnQuestManager(quest);
            quests.StartQuest(quest);
            quests.ReportObjective("BEAT");
            var memory = arena.TrackAsset(ScriptableObject.CreateInstance<MemoryFragment>());
            memory.Configure("MEM_KEEP", "Keep memory", "", "Witness",
                MemoryCategory.Personal, MemoryImportance.Supporting);
            var memories = arena.SpawnMemoryManager(memory);
            memories.Discover("MEM_KEEP");

            var enemy = arena.SpawnEnemy("Reset enemy", new Vector3(0f, 0f, 8f),
                arena.NewArchetype("RESET_ENEMY", health: 50f));
            enemy.Root.SetActive(false);
            enemy.Root.AddComponent<EnemyHealth>();
            enemy.Root.SetActive(true);
            var player = arena.SpawnPlayer(Vector3.zero);
            yield return null;
            enemy.Health.Kill(DamageData.Create(100f, player.Root));
            Assert.AreEqual(EnemyState.Dead, enemy.State);
            player.Health.Kill(DamageData.Create(100f, null));
            player.Death.RespawnNow();
            Assert.AreEqual(50f, enemy.Health.CurrentHealth, 0.01f);
            Assert.IsFalse(enemy.Health.IsDead);
            Assert.AreNotEqual(EnemyState.Dead, enemy.State);
            Assert.Less(Vector3.Distance(enemy.Root.transform.position, enemy.Controller.Home), 0.1f);
            Assert.AreEqual(QuestStatus.Completed, quests.GetStatus("Q_KEEP"));
            Assert.AreEqual(MemoryState.Known, memories.GetState("MEM_KEEP"));
        }
    }
}

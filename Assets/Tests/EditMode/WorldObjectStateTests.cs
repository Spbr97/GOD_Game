using Game.Core;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Per-object save identity (SPEC.md TASK 009): a stable id for an object, and the
    /// namespaced world flags <see cref="WorldObjectState"/> keys on it. These run in
    /// an empty scene of their own for the same reason <c>AvarshaTests</c> does —
    /// <see cref="WorldState.Instance"/> resolves by searching loaded scenes.
    /// </summary>
    public class WorldObjectStateTests
    {
        private GameObject worldStateGo;
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

        [SetUp]
        public void SpawnWorldState()
        {
            worldStateGo = new GameObject("WorldState");
            worldStateGo.AddComponent<WorldState>();
        }

        [TearDown]
        public void Cleanup()
        {
            if (worldStateGo != null)
            {
                Object.DestroyImmediate(worldStateGo);
            }

            EventBus.Clear();
        }

        // ------------------------------------------------------------- SaveIdentity

        [Test]
        public void SaveIdentity_UsesTheConfiguredId()
        {
            var go = new GameObject("SomeEnemy");
            var identity = go.AddComponent<SaveIdentity>();
            identity.Configure("ENEMY_042");

            Assert.AreEqual("ENEMY_042", identity.Id);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void SaveIdentity_FallsBackToTheGameObjectNameWhenUnset()
        {
            var go = new GameObject("Enemy_ForgottenSoldier");
            var identity = go.AddComponent<SaveIdentity>();

            Assert.AreEqual("Enemy_ForgottenSoldier", identity.Id);

            Object.DestroyImmediate(go);
        }

        // --------------------------------------------------------- WorldObjectState

        [Test]
        public void WorldObjectState_DeadAndCollectedFlagsAreNamespacedAndDistinct()
        {
            var dead = WorldObjectState.DeadFlag("X");
            var collected = WorldObjectState.CollectedFlag("X");

            Assert.AreNotEqual(dead, collected, "The same id must not collide between death and collection.");
            StringAssert.Contains("X", dead);
            StringAssert.Contains("X", collected);
        }

        [Test]
        public void WorldObjectState_MarkSetsTheFlagAndIsMarkedReadsItBack()
        {
            var flag = WorldObjectState.DeadFlag("ENEMY_1");

            Assert.IsFalse(WorldObjectState.IsMarked(flag));

            WorldObjectState.Mark(flag);

            Assert.IsTrue(WorldObjectState.IsMarked(flag));
        }

        [Test]
        public void WorldObjectState_TwoDifferentIds_DoNotShareState()
        {
            WorldObjectState.Mark(WorldObjectState.DeadFlag("ENEMY_A"));

            Assert.IsTrue(WorldObjectState.IsMarked(WorldObjectState.DeadFlag("ENEMY_A")));
            Assert.IsFalse(WorldObjectState.IsMarked(WorldObjectState.DeadFlag("ENEMY_B")),
                "Marking one object's id marked a different object's flag too.");
        }

        [Test]
        public void WorldObjectState_WithNoWorldState_FailsQuietlyRatherThanThrowing()
        {
            Object.DestroyImmediate(worldStateGo);
            worldStateGo = null;

            Assert.DoesNotThrow(() => WorldObjectState.Mark(WorldObjectState.DeadFlag("X")));
            Assert.IsFalse(WorldObjectState.IsMarked(WorldObjectState.DeadFlag("X")));
        }
    }
}

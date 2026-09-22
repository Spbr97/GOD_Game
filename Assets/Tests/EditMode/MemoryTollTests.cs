using System.Collections.Generic;
using Game.Core;
using Game.Memory;
using Game.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// <see cref="MemoryToll"/> (SPEC.md Phase 3's "memory cost", TASK 015): a one-time
    /// barrier paid in memory integrity rather than an item.
    ///
    /// Runs in an empty scene of its own for the same reason <c>PuzzleTests</c> does —
    /// <see cref="MemoryManager.Instance"/> and <see cref="WorldState.Instance"/> both
    /// resolve by searching loaded scenes.
    /// </summary>
    public class MemoryTollTests
    {
        private readonly List<GameObject> spawned = new();
        private readonly List<Object> assets = new();
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
                    UnityEngine.Object.DestroyImmediate(spawned[i]);
                }
            }

            for (var i = 0; i < assets.Count; i++)
            {
                if (assets[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(assets[i]);
                }
            }

            spawned.Clear();
            assets.Clear();
            EventBus.Clear();
        }

        private T NewComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go.AddComponent<T>();
        }

        private MemoryManager NewMemoryManager()
        {
            var manager = NewComponent<MemoryManager>("MemoryManager");
            manager.Configure(System.Array.Empty<MemoryFragment>());
            return manager;
        }

        private MemoryToll NewToll(float cost, string flag, GameObject barrier)
        {
            var go = new GameObject("Toll");
            spawned.Add(go);

            // The collider goes on first: Interactable requires the abstract Collider
            // type, which Unity cannot add for you (see AvarshaTests's LocationTrigger note).
            go.AddComponent<SphereCollider>();

            var toll = go.AddComponent<MemoryToll>();
            toll.Configure(cost, flag, barrier);
            return toll;
        }

        [Test]
        public void Toll_PayingSpendsIntegrityAndOpensTheBarrier()
        {
            var memories = NewMemoryManager();
            var barrier = new GameObject("Barrier");
            spawned.Add(barrier);
            var toll = NewToll(0.2f, "TOLL_PAID", barrier);

            Assert.IsTrue(toll.CanInteract);

            toll.Interact(null);

            Assert.IsTrue(toll.Paid);
            Assert.AreEqual(0.8f, memories.Integrity, 0.0001f);
            Assert.IsFalse(barrier.activeSelf, "Paying the toll should have opened its barrier.");
        }

        [Test]
        public void Toll_CannotBePaidTwice()
        {
            var memories = NewMemoryManager();
            var toll = NewToll(0.2f, null, null);

            toll.Interact(null);
            Assert.AreEqual(0.8f, memories.Integrity, 0.0001f);

            toll.Interact(null);
            Assert.AreEqual(0.8f, memories.Integrity, 0.0001f, "A second Interact must not spend integrity again.");
            Assert.IsFalse(toll.CanInteract, "A paid toll must refuse further interaction.");
        }

        [Test]
        public void Toll_SetsItsFlagOnlyOncePaid()
        {
            var world = NewComponent<WorldState>("WorldState");
            NewMemoryManager();
            var toll = NewToll(0.1f, "TOLL_PAID", null);

            Assert.IsFalse(world.GetFlag("TOLL_PAID"));
            toll.Interact(null);
            Assert.IsTrue(world.GetFlag("TOLL_PAID"));
        }

        [Test]
        public void Toll_NeverBlocksPassageEvenAtZeroIntegrity()
        {
            var memories = NewMemoryManager();
            memories.ReduceIntegrity(1f); // drain it to zero first
            Assert.AreEqual(0f, memories.Integrity, 0.0001f);

            var toll = NewToll(0.2f, null, null);

            Assert.DoesNotThrow(() => toll.Interact(null));
            Assert.IsTrue(toll.Paid, "Having no integrity left to spend must not stop the toll from being paid.");
            Assert.AreEqual(0f, memories.Integrity, 0.0001f, "Integrity should clamp at zero, not go negative.");
        }

        [Test]
        public void Toll_WithNoMemoryManager_DoesNothingAndStaysUnpaid()
        {
            // No MemoryManager in this scene at all.
            var toll = NewToll(0.2f, "TOLL_PAID", null);

            Assert.DoesNotThrow(() => toll.Interact(null));
            Assert.IsFalse(toll.Paid, "Paying with no MemoryManager to charge would open the barrier for free.");
        }
    }
}

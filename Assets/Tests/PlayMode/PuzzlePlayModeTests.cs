using System.Collections;
using Game.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Play
{
    /// <summary>
    /// The first fire puzzle over real frames (SPEC.md section 26, TASK 012): a
    /// brazier's burn timer, and braziers plus a gate wired together through
    /// <see cref="PuzzleController"/> and <see cref="PuzzleGate"/> exactly as a scene
    /// would build them, rather than through <c>PuzzleController.Evaluate()</c>'s
    /// EditMode test seam.
    /// </summary>
    public class PuzzlePlayModeTests
    {
        private TestArena arena;

        [SetUp]
        public void SetUp()
        {
            arena = new TestArena();
            arena.EnsureWorldState();
        }

        [TearDown]
        public void TearDown()
        {
            arena.Dispose();
        }

        private FireBrazier NewBrazier(string name, float burnSeconds)
        {
            var go = arena.Track(new GameObject(name));
            go.SetActive(false);
            var collider = go.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            var brazier = go.AddComponent<FireBrazier>();
            brazier.ConfigureBrazier(burnSeconds);
            go.SetActive(true);
            return brazier;
        }

        private (PuzzleController controller, PuzzleGate gate) NewPuzzleWithGate(string id, string flag,
            params FireBrazier[] braziers)
        {
            var controllerGo = arena.Track(new GameObject("Puzzle_" + id));
            controllerGo.SetActive(false);
            var controller = controllerGo.AddComponent<PuzzleController>();
            controller.Configure(braziers, flag, id);
            controllerGo.SetActive(true);

            var gateGo = arena.Track(new GameObject("Gate_" + id));
            gateGo.SetActive(false);
            var gateCollider = gateGo.AddComponent<BoxCollider>();
            var gate = gateGo.AddComponent<PuzzleGate>();
            gate.Configure(id);
            gateGo.SetActive(true);

            return (controller, gate);
        }

        [UnityTest]
        public IEnumerator Brazier_LightingItMakesItSatisfiedAndRaisesChanged()
        {
            var brazier = NewBrazier("Brazier", burnSeconds: 0f);
            yield return null;

            var raised = 0;
            brazier.Changed += () => raised++;

            Assert.IsFalse(brazier.IsSatisfied);
            brazier.Light();

            Assert.IsTrue(brazier.IsLit);
            Assert.IsTrue(brazier.IsSatisfied);
            Assert.AreEqual(1, raised);
        }

        [UnityTest]
        public IEnumerator Brazier_BurnsOutOnItsOwnAfterItsDuration()
        {
            var brazier = NewBrazier("Brazier", burnSeconds: 0.2f);
            yield return null;

            brazier.Light();
            Assert.IsTrue(brazier.IsLit);

            yield return TestArena.Until(() => !brazier.IsLit, "the brazier to burn out", 2f);

            Assert.IsFalse(brazier.IsSatisfied);
        }

        [UnityTest]
        public IEnumerator Brazier_InteractingTwiceOnlyLightsItOnce()
        {
            var brazier = NewBrazier("Brazier", burnSeconds: 0f);
            yield return null;

            var raised = 0;
            brazier.Changed += () => raised++;

            brazier.Interact(null);
            Assert.IsFalse(brazier.CanInteract, "An already-lit brazier should refuse another interaction.");
            brazier.Interact(null);

            Assert.AreEqual(1, raised, "Interacting with an already-lit brazier lit it again.");
        }

        [UnityTest]
        public IEnumerator Puzzle_LightingEveryBrazierWithinTheWindowSolvesItAndOpensTheGate()
        {
            var a = NewBrazier("A", burnSeconds: 3f);
            var b = NewBrazier("B", burnSeconds: 3f);
            var c = NewBrazier("C", burnSeconds: 3f);
            var (controller, gate) = NewPuzzleWithGate("FIRE_PUZZLE", "FIRE_PUZZLE_SOLVED", a, b, c);
            yield return null;

            Assert.IsFalse(gate.IsOpen);

            a.Light();
            yield return null;
            b.Light();
            yield return null;
            c.Light();
            yield return null;

            Assert.IsTrue(controller.IsSolved);
            Assert.IsTrue(gate.IsOpen, "The gate did not open when the puzzle solved.");
            Assert.IsFalse(gate.GetComponent<Collider>().enabled, "The gate's collider should no longer block the player.");
        }

        [UnityTest]
        public IEnumerator Puzzle_LightingThemTooSlowlyNeverSolves()
        {
            var a = NewBrazier("A", burnSeconds: 0.15f);
            var b = NewBrazier("B", burnSeconds: 3f);
            var (controller, gate) = NewPuzzleWithGate("SLOW_PUZZLE", "SLOW_PUZZLE_SOLVED", a, b);
            yield return null;

            a.Light();
            yield return TestArena.Until(() => !a.IsLit, "the first brazier to burn out", 2f);

            b.Light();
            yield return null;

            Assert.IsFalse(controller.IsSolved, "The puzzle solved even though the first brazier had already gone out.");
            Assert.IsFalse(gate.IsOpen);
        }

        [UnityTest]
        public IEnumerator Puzzle_OnceSolvedStaysSolvedEvenAfterABrazierBurnsOut()
        {
            var a = NewBrazier("A", burnSeconds: 0.2f);
            var b = NewBrazier("B", burnSeconds: 0f);
            var (controller, gate) = NewPuzzleWithGate("LATCH_PUZZLE", "LATCH_PUZZLE_SOLVED", a, b);
            yield return null;

            a.Light();
            b.Light();
            yield return null;

            Assert.IsTrue(controller.IsSolved);
            Assert.IsTrue(gate.IsOpen);

            yield return TestArena.Until(() => !a.IsLit, "the brazier to burn out", 2f);

            Assert.IsTrue(controller.IsSolved, "The puzzle re-locked after a brazier burned out.");
            Assert.IsTrue(gate.IsOpen, "The gate closed again after a brazier burned out.");
        }
    }
}

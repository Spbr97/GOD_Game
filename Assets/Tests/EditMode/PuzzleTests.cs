using System;
using System.Collections.Generic;
using Game.Core;
using Game.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// The reusable puzzle framework (SPEC.md section 26, TASK 012): a controller that
    /// solves once every element is satisfied at once, and stays solved afterwards.
    ///
    /// Runs in an empty scene of its own for the same reason <c>AvarshaTests</c> does —
    /// <see cref="WorldState.Instance"/> resolves by searching loaded scenes.
    /// </summary>
    public class PuzzleTests
    {
        /// <summary>A minimal, hand-driven puzzle piece — no interaction, no timer, just a flag a test can flip.</summary>
        private class TestElement : MonoBehaviour, IPuzzleElement
        {
            public bool Satisfied;
            public bool IsSatisfied => Satisfied;
            public event Action Changed;

            public void Set(bool value)
            {
                Satisfied = value;
                Changed?.Invoke();
            }
        }

        private readonly List<GameObject> spawned = new();
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

            spawned.Clear();
            EventBus.Clear();
        }

        private T NewComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go.AddComponent<T>();
        }

        private TestElement NewElement(string name) => NewComponent<TestElement>(name);

        private PuzzleController NewPuzzle(string id, string flag, params TestElement[] elements)
        {
            var controller = NewComponent<PuzzleController>("Puzzle_" + id);
            controller.Configure(elements, flag, id);
            return controller;
        }

        [Test]
        public void Puzzle_SolvesOnlyWhenEveryElementIsSatisfiedAtOnce()
        {
            var a = NewElement("A");
            var b = NewElement("B");
            var puzzle = NewPuzzle("TEST_PUZZLE", "TEST_PUZZLE_SOLVED", a, b);

            a.Set(true);
            puzzle.Evaluate();
            Assert.IsFalse(puzzle.IsSolved, "Solved with only one of two elements satisfied.");

            b.Set(true);
            puzzle.Evaluate();
            Assert.IsTrue(puzzle.IsSolved);
        }

        [Test]
        public void Puzzle_SetsItsFlagAndPublishesSolvedOnce()
        {
            var world = NewComponent<WorldState>("WorldState");
            var a = NewElement("A");
            var puzzle = NewPuzzle("TEST_PUZZLE", "TEST_PUZZLE_SOLVED", a);

            var solvedCount = 0;
            EventBus.Subscribe<PuzzleSolvedEvent>(_ => solvedCount++);

            a.Set(true);
            puzzle.Evaluate();

            Assert.IsTrue(world.GetFlag("TEST_PUZZLE_SOLVED"));
            Assert.AreEqual(1, solvedCount);
        }

        [Test]
        public void Puzzle_StaysSolvedEvenIfAnElementLaterReverts()
        {
            var a = NewElement("A");
            var b = NewElement("B");
            var puzzle = NewPuzzle("TEST_PUZZLE", "TEST_PUZZLE_SOLVED", a, b);

            a.Set(true);
            b.Set(true);
            puzzle.Evaluate();
            Assert.IsTrue(puzzle.IsSolved);

            a.Set(false);
            puzzle.Evaluate();
            Assert.IsTrue(puzzle.IsSolved, "An element reverting should not re-lock an already-solved puzzle.");
        }

        [Test]
        public void Puzzle_WithNoElements_NeverSolves()
        {
            var puzzle = NewPuzzle("EMPTY", "EMPTY_SOLVED");

            puzzle.Evaluate();

            Assert.IsFalse(puzzle.IsSolved, "A puzzle with nothing to satisfy solved itself.");
        }

        [Test]
        public void Puzzle_IdFallsBackToTheGameObjectNameWhenUnset()
        {
            var puzzle = NewComponent<PuzzleController>("Puzzle_Unnamed");
            puzzle.Configure(Array.Empty<TestElement>(), null, null);

            Assert.AreEqual("Puzzle_Unnamed", puzzle.PuzzleId);
        }
    }
}

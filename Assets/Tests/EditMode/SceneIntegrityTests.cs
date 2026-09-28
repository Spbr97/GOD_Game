using Game.AI;
using Game.Combat;
using Game.Core;
using Game.DevTools;
using Game.Dialogue;
using Game.UI;
using Game.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Game.Tests
{
    public class SceneIntegrityTests
    {
        private SceneSetup[] previous;

        [SetUp]
        public void SaveOpenScenes() => previous = EditorSceneManager.GetSceneManagerSetup();

        [TearDown]
        public void RestoreOpenScenes()
        {
            // Batch-mode test runs can begin without any loaded scene.
            foreach (var setup in previous)
                if (setup.isLoaded && setup.isActive)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                    return;
                }
        }

        [TestCase("Assets/Scenes/MainMenu.unity")]
        [TestCase("Assets/Scenes/Test.unity")]
        [TestCase("Assets/Scenes/Avarsha.unity")]
        [TestCase("Assets/Scenes/Agniya.unity")]
        public void ShippedScene_OpensWithoutMissingScripts(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid());
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                    Assert.IsNotNull(component, $"Missing script in {path} under {root.name}");
        }

        [TestCase("Assets/Scenes/Test.unity")]
        [TestCase("Assets/Scenes/Avarsha.unity")]
        [TestCase("Assets/Scenes/Agniya.unity")]
        public void GameplayScene_HasPlayerBoundsAndUiWiring(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Assert.IsNotNull(Find<PlayerDeath>(scene));
            Assert.IsNotNull(Find<CheckpointManager>(scene));
            Assert.IsNotNull(Find<WorldBounds>(scene));
            Assert.IsNotNull(Find<PlayerBoundsGuard>(scene));
            Assert.IsNotNull(Find<DeviceWatcher>(scene));
            Assert.IsNotNull(Find<DebugConsole>(scene));
            Assert.IsNotNull(Find<Canvas>(scene));
            var map = Find<MapUI>(scene);
            Assert.IsNotNull(map);
            AssertReference(map, "mapRoot");
            AssertReference(map, "mapContent");
            var hud = Find<HudUI>(scene);
            Assert.IsNotNull(hud);
            AssertReference(hud, "healthFill");
            AssertReference(hud, "staminaFill");
            AssertReference(hud, "divineFill");
        }

        [Test]
        public void Avarsha_HasEncounterGuardsAndBakedNavigation()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Avarsha.unity", OpenSceneMode.Single);
            Assert.IsNotNull(Find<EnemyBoundsGuard>(scene));
            Assert.IsNotNull(Find<BossArena>(scene));
            Assert.Greater(NavMesh.CalculateTriangulation().vertices.Length, 0,
                "Avarsha has no baked NavMesh geometry.");
        }

        /// <summary>
        /// TASK 041. Agniya is a gameplay scene in its own right, so it needs everything
        /// one needs to run standalone: the managers that carry progression are scene
        /// singletons, and a scene that is missing one loses whatever it holds the moment
        /// the player walks in.
        /// </summary>
        [Test]
        public void Agniya_StandsOnItsOwnAsAGameplayScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Agniya.unity", OpenSceneMode.Single);

            Assert.IsNotNull(Find<Game.Save.SaveManager>(scene), "no SaveManager: nothing would carry progression in");
            Assert.IsNotNull(Find<WorldState>(scene));
            Assert.IsNotNull(Find<Game.Quests.QuestManager>(scene));
            Assert.IsNotNull(Find<Game.Memory.MemoryManager>(scene));
            Assert.IsNotNull(Find<Game.Inventory.InventoryManager>(scene));
            Assert.IsNotNull(Find<Game.Progression.SkillTreeManager>(scene));
            Assert.IsNotNull(Find<CheckpointManager>(scene));
            Assert.IsNotNull(Find<GameSceneManager>(scene), "no GameSceneManager: the way back out would not load");
            Assert.IsNotNull(Find<BossArena>(scene), "Agniya is where the boss fight is");
            Assert.Greater(NavMesh.CalculateTriangulation().vertices.Length, 0,
                "Agniya has no baked NavMesh geometry, so its enemies cannot move.");
        }

        /// <summary>
        /// The two halves of TASK 041's round trip, checked as content rather than as
        /// code: Avarsha must offer a way in and somewhere to come back to, and Agniya the
        /// mirror of both.
        /// </summary>
        [TestCase("Assets/Scenes/Avarsha.unity", "Agniya", "FromAvarsha", "FromAgniya")]
        [TestCase("Assets/Scenes/Agniya.unity", "Avarsha", "FromAgniya", "FromAvarsha")]
        public void GameplayScene_HasTheExitAndArrivalPointTheRoundTripNeeds(
            string path, string expectedTarget, string expectedSpawnThere, string arrivalHere)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            var exit = Find<SceneExit>(scene);
            Assert.IsNotNull(exit, $"{path} has no SceneExit.");
            Assert.AreEqual(expectedTarget, exit.TargetScene);
            Assert.AreEqual(expectedSpawnThere, exit.TargetSpawnId);

            Assert.IsNotNull(SceneSpawnPoint.Find(arrivalHere),
                $"{path} has no SceneSpawnPoint '{arrivalHere}' for the return trip to land on.");
        }

        /// <summary>
        /// Every exit's destination must be a scene the build actually contains, and its
        /// spawn id must exist in that scene. A string is the only thing that can cross a
        /// scene boundary, so a typo here is a door that opens onto nothing and there is
        /// nothing at author time to catch it but this.
        /// </summary>
        [Test]
        public void EverySceneExit_NamesASceneInTheBuildAndASpawnPointThatExists()
        {
            var gameplayScenes = new[] { "Assets/Scenes/Avarsha.unity", "Assets/Scenes/Agniya.unity" };
            var inBuild = new System.Collections.Generic.HashSet<string>();
            foreach (var entry in EditorBuildSettings.scenes)
            {
                if (entry.enabled)
                {
                    inBuild.Add(System.IO.Path.GetFileNameWithoutExtension(entry.path));
                }
            }

            // Collected first, because checking one means opening the scene it points at,
            // which closes the scene it was found in.
            var required = new System.Collections.Generic.List<(string From, string Scene, string Spawn)>();

            foreach (var path in gameplayScenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var exit in root.GetComponentsInChildren<SceneExit>(true))
                    {
                        Assert.IsNotEmpty(exit.TargetScene, $"{path}: '{exit.name}' names no destination.");
                        Assert.IsTrue(inBuild.Contains(exit.TargetScene),
                            $"{path}: '{exit.name}' leads to '{exit.TargetScene}', which is not an enabled scene in Build Settings.");
                        required.Add((path, exit.TargetScene, exit.TargetSpawnId));
                    }
                }
            }

            Assert.IsNotEmpty(required, "no SceneExit was found in any gameplay scene");

            foreach (var entry in required)
            {
                EditorSceneManager.OpenScene($"Assets/Scenes/{entry.Scene}.unity", OpenSceneMode.Single);
                Assert.IsNotNull(SceneSpawnPoint.Find(entry.Spawn),
                    $"{entry.From} sends the player to spawn '{entry.Spawn}' in '{entry.Scene}', which has no such point.");
            }
        }

        /// <summary>
        /// A scene's baked navigation must live in a saved asset, not in memory.
        ///
        /// <c>NavMeshSurface.BuildNavMesh()</c> produces a <c>NavMeshData</c> that is not
        /// attached to anything. It works perfectly for the rest of that Editor session,
        /// the scene saves without complaint, and every EditMode test that asks whether a
        /// NavMesh exists passes — because the live object is still there. The player
        /// build has nothing to serialize and ships a scene whose enemies cannot move.
        /// This happened during TASK 041's scene split and is only visible by asking for
        /// the asset path.
        /// </summary>
        [TestCase("Assets/Scenes/Avarsha.unity")]
        [TestCase("Assets/Scenes/Agniya.unity")]
        public void GameplayScene_BakedNavigationIsASavedAssetAndNotInMemoryOnly(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            var surface = Find<Unity.AI.Navigation.NavMeshSurface>(scene);
            Assert.IsNotNull(surface, $"{path} has no NavMeshSurface.");
            Assert.IsNotNull(surface.navMeshData, $"{path}'s NavMeshSurface has no baked data.");

            var assetPath = AssetDatabase.GetAssetPath(surface.navMeshData);
            Assert.IsNotEmpty(assetPath,
                $"{path}'s NavMesh is an unsaved in-memory object; it will not survive a player build.");
            Assert.Greater(NavMesh.CalculateTriangulation().vertices.Length, 0,
                $"{path} has no walkable NavMesh geometry.");
        }

        /// <summary>
        /// A bake that is saved is not the same as a bake that is *current*. TASK 041's
        /// in-memory NavMesh bug is caught by the test above, but its sibling is not: bake
        /// once, then move the floor, widen a corridor or drop an enemy into a room that
        /// did not exist at bake time, and the asset is still a perfectly valid saved
        /// NavMesh — of the scene as it used to be. Nothing complains, and the enemies in
        /// the new room stand still.
        ///
        /// Checked by asking whether the places navigation actually has to work are on the
        /// mesh: where the player arrives, where enemies stand, and where patrols walk. A
        /// stale bake fails here the moment the geometry under one of them changed.
        /// </summary>
        [TestCase("Assets/Scenes/Avarsha.unity")]
        [TestCase("Assets/Scenes/Agniya.unity")]
        public void GameplayScene_BakedNavigationIsCurrentWithItsGeometry(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var landmarks = new System.Collections.Generic.List<(string What, Vector3 Where)>();

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var spawn in root.GetComponentsInChildren<SceneSpawnPoint>(true))
                    landmarks.Add(($"spawn point '{spawn.SpawnId}'", spawn.Position));

                foreach (var enemy in root.GetComponentsInChildren<EnemyController>(true))
                    landmarks.Add(($"enemy '{enemy.name}'", enemy.transform.position));

                foreach (var route in root.GetComponentsInChildren<PatrolRoute>(true))
                    for (var i = 0; i < route.WaypointCount; i++)
                        landmarks.Add(($"waypoint {i} of patrol '{route.name}'", route.GetPosition(i)));
            }

            Assert.IsNotEmpty(landmarks, $"{path} has nothing that needs to navigate, so this proves nothing.");

            // Generous: the question is whether there is a mesh under this place at all,
            // not whether the author placed it to the centimetre.
            const float tolerance = 2f;

            foreach (var landmark in landmarks)
            {
                Assert.IsTrue(NavMesh.SamplePosition(landmark.Where, out _, tolerance, NavMesh.AllAreas),
                    $"{path}: {landmark.What} at {landmark.Where} has no NavMesh within {tolerance}m. "
                    + "Either it was moved off the walkable floor, or the geometry changed and the bake was "
                    + "never re-run. Re-bake the surface and save its asset.");
            }
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static void AssertReference(UnityEngine.Object component, string name)
        {
            var property = new SerializedObject(component).FindProperty(name);
            Assert.IsNotNull(property, $"{component.name} has no serialized field {name}.");
            Assert.IsNotNull(property.objectReferenceValue, $"{component.name}.{name} is unassigned.");
        }
        [Test]
        public void EdgeCase12_MissingAssetBundle_CannotBlockShippedContent()
        {
            Assert.IsEmpty(AssetDatabase.GetAllAssetBundleNames(),
                "A bundle was introduced without a loader and missing-bundle fallback test.");
        }

        [Test]
        public void EdgeCase28_RequiredNpcsCannotBeKilledBeforeQuestDialogue()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Avarsha.unity", OpenSceneMode.Single);
            var count = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var npc in root.GetComponentsInChildren<NpcInteractable>(true))
                {
                    count++;
                    Assert.IsNull(npc.GetComponentInParent<HealthComponent>(),
                        $"Required dialogue NPC {npc.name} can be killed before speaking.");
                }
            }
            Assert.Greater(count, 0, "Avarsha has no dialogue NPCs to protect.");
        }
    }
}
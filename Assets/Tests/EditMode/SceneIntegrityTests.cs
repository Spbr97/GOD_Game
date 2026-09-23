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
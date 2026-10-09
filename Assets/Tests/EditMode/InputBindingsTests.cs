using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace Game.Tests
{
    /// <summary>Checks the shipped input asset, which gameplay components share.</summary>
    public class InputBindingsTests
    {
        private const string AssetPath = "Assets/Input/PlayerControls.inputactions";

        private static InputAction GameplayAction(string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.IsNotNull(asset, "The shipped input asset is missing.");
            var action = asset.FindActionMap("Gameplay")?.FindAction(name);
            Assert.IsNotNull(action, $"The Gameplay action '{name}' is missing.");
            return action;
        }

        [Test]
        public void ControllerCanMoveAndSprint()
        {
            Assert.IsTrue(GameplayAction("Move").bindings.Any(binding =>
                binding.path == "<Gamepad>/leftStick" && !binding.isPartOfComposite),
                "WASD worked, but the controller had no movement binding.");
            Assert.IsTrue(GameplayAction("Sprint").bindings.Any(binding =>
                binding.path == "<Gamepad>/leftStickPress"),
                "The controller had no sprint binding.");
        }

        [Test]
        public void ControllerMapAndInteractUseDifferentButtons()
        {
            var interact = GameplayAction("Interact").bindings
                .Single(binding => binding.path.StartsWith("<Gamepad>/"));
            var map = GameplayAction("Map").bindings
                .Single(binding => binding.path.StartsWith("<Gamepad>/"));
            Assert.AreNotEqual(interact.path, map.path,
                "One D-pad press must not interact and open the map together.");
        }
    }
}

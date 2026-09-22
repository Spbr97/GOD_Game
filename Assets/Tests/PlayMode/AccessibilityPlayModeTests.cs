using System.Collections;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Tests.Play
{
    /// <summary>
    /// SPEC.md section 43 (TASK 017) against a live scene: aim/lock-on assistance
    /// actually widening acquisition, camera shake and the screen-effects flash both
    /// respecting their toggle, a telegraph's non-colour tell, and a control rebind
    /// surviving a save/load round trip through <see cref="SettingsManager"/>.
    /// </summary>
    public class AccessibilityPlayModeTests
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
        public IEnumerator LockOn_AimAssistWidensTheAcquisitionCone()
        {
            var settings = arena.SpawnSettingsManager();
            var player = arena.SpawnPlayer(Vector3.zero, withCombatInput: true);
            player.Root.transform.rotation = Quaternion.identity;

            // 80 degrees off dead ahead: outside the controller's base 70-degree half
            // angle, inside a 1.4x-widened one (98 degrees).
            var offset = Quaternion.Euler(0f, 80f, 0f) * Vector3.forward * 6f;
            arena.SpawnDummy("Candidate", offset);
            yield return null;

            settings.Current.AimAssistEnabled = false;
            Assert.IsFalse(player.LockOn.Acquire(), "The candidate is outside the base cone; assist is off.");

            settings.Current.AimAssistEnabled = true;
            Assert.IsTrue(player.LockOn.Acquire(), "Assist should widen the cone enough to reach an 80-degree candidate.");
        }

        [UnityTest]
        public IEnumerator PlayerCamera_ShakeIsSkippedWhenTheSettingIsOff()
        {
            var settings = arena.SpawnSettingsManager();
            var cameraGo = arena.Track(new GameObject("Camera", typeof(Camera)));
            cameraGo.SetActive(false);
            cameraGo.transform.position = new Vector3(0f, 0f, -5f);
            var targetGo = arena.Track(new GameObject("Target"));
            var camera = cameraGo.AddComponent<PlayerCamera>();

            // Inactive while wiring: AddComponent runs Awake immediately on an active
            // object, and PlayerCamera.Awake disables itself if target/inputActions are
            // still null (see TestArena's note on this exact gotcha elsewhere).
            var field = typeof(PlayerCamera).GetField("target", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(camera, targetGo.transform);
            var inputField = typeof(PlayerCamera).GetField("inputActions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            inputField.SetValue(camera, arena.BuildInputActions());
            cameraGo.SetActive(true);
            yield return null;

            yield return null; // let one LateUpdate settle the base position
            var basePosition = cameraGo.transform.position;

            settings.Current.CameraShakeEnabled = false;
            camera.Shake(1f, 5f);
            yield return null;
            Assert.AreEqual(basePosition, cameraGo.transform.position, "Shake should not move the camera when the setting is off.");

            settings.Current.CameraShakeEnabled = true;
            camera.Shake(1f, 5f);
            yield return null;
            Assert.AreNotEqual(basePosition, cameraGo.transform.position, "Shake should move the camera when the setting is on.");
        }

        [UnityTest]
        public IEnumerator ScreenEffectsUI_FlashesOnDamageUnlessTheSettingIsOff()
        {
            var settings = arena.SpawnSettingsManager();
            var player = arena.SpawnPlayer(Vector3.zero);

            var imageGo = arena.Track(new GameObject("Flash", typeof(RectTransform), typeof(UnityEngine.UI.Image)));
            imageGo.SetActive(false);
            var image = imageGo.GetComponent<UnityEngine.UI.Image>();
            var screenEffects = imageGo.AddComponent<Game.UI.ScreenEffectsUI>();

            // Inactive while wiring: AddComponent runs OnEnable immediately on an active
            // object, and ScreenEffectsUI.OnEnable's SetAlpha(0) would run before
            // Configure assigns flashImage, leaving the Image's default opaque colour
            // untouched (see TestArena's note on this exact gotcha elsewhere).
            screenEffects.Configure(image, player.Health);
            imageGo.SetActive(true);
            yield return null;

            settings.Current.ScreenEffectsEnabled = false;
            player.Health.TakeDamage(DamageData.Create(10f, null));
            yield return null;
            Assert.AreEqual(0f, image.color.a, 0.001f, "No flash should appear when the setting is off.");

            settings.Current.ScreenEffectsEnabled = true;
            player.Health.TakeDamage(DamageData.Create(10f, null));
            yield return null;
            Assert.Greater(image.color.a, 0f, "A flash should appear when the setting is on.");
        }

        [UnityTest]
        public IEnumerator EnemyCombatant_TelegraphPulsesScaleIndependentlyOfColour()
        {
            var archetype = arena.NewArchetype(telegraph: 0.3f);
            var enemy = arena.SpawnEnemy("Enemy", Vector3.zero, archetype);
            var bodyTransform = enemy.Root.transform.Find("Body");
            var originalScale = bodyTransform.localScale;

            enemy.Combatant.TryAttack();
            yield return TestArena.Until(() => enemy.Combatant.IsTelegraphing, "the telegraph to start");

            Assert.AreNotEqual(originalScale, bodyTransform.localScale,
                "The telegraph should pulse the body's scale as a non-colour tell (SPEC.md section 43).");

            yield return TestArena.Until(() => !enemy.Combatant.IsTelegraphing, "the telegraph to end", 2f);
            yield return null;

            Assert.AreEqual(originalScale, bodyTransform.localScale, "The pulse should restore the original scale once the telegraph ends.");
        }

        [UnityTest]
        public IEnumerator SettingsManager_BindingOverrideSurvivesASaveAndLoadOnAFreshAsset()
        {
            // This test is the one place in the suite that writes to the real
            // PlayerPrefs key SettingsManager.SaveBindingOverrides uses — back up
            // whatever a real Controls-screen remap may already have put there, the
            // same care SavePlayModeTests takes with the player's real save files.
            const string rebindPrefsKey = "InputBindingOverrides";
            var hadPriorValue = PlayerPrefs.HasKey(rebindPrefsKey);
            var priorValue = hadPriorValue ? PlayerPrefs.GetString(rebindPrefsKey) : null;

            try
            {
                yield return RunBindingOverrideRoundTrip();
            }
            finally
            {
                if (hadPriorValue)
                {
                    PlayerPrefs.SetString(rebindPrefsKey, priorValue);
                }
                else
                {
                    PlayerPrefs.DeleteKey(rebindPrefsKey);
                }

                PlayerPrefs.Save();
            }
        }

        private IEnumerator RunBindingOverrideRoundTrip()
        {
            // One asset throughout, not a second "fresh" one: Input System matches a
            // saved override back onto a binding by its serialized GUID, and a second
            // AddActionMap/AddAction call generates new random GUIDs that a real saved
            // file's stable ones would never actually collide with — a truly fresh
            // process restart reloads the *same* serialized asset, not a rebuilt one.
            // Clearing the override in memory before reloading it is what actually
            // proves the PlayerPrefs round trip rather than the object simply still
            // holding what it was never asked to forget.
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            arena.TrackAsset(asset);
            var map = asset.AddActionMap("Gameplay");
            var action = map.AddAction("TestAction", InputActionType.Button, binding: "<Keyboard>/e");

            var settings = arena.SpawnSettingsManager(asset);
            yield return null;

            action.ApplyBindingOverride(0, "<Keyboard>/q");
            settings.SaveBindingOverrides();

            action.RemoveBindingOverride(0);
            Assert.IsNull(action.bindings[0].overridePath, "Sanity check: the override should be cleared before reloading it.");

            settings.LoadBindingOverrides();

            Assert.AreEqual("<Keyboard>/q", action.bindings[0].overridePath,
                "Loading did not restore the rebound key from PlayerPrefs.");
        }
    }
}

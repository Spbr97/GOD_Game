using System.Collections;
using Game.Audio;
using Game.Combat;
using Game.Core;
using Game.VFX;
using Game.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Play
{
    /// <summary>
    /// TASK 018's placeholder VFX/audio/animation against real triggers: a brazier
    /// actually lighting, a boss's own presentation hooks, and the player's
    /// placeholder animator reacting to a real swing and a real death — proving the
    /// listeners are wired to the events they claim to react to, not just that the
    /// spawners work in isolation (that part is EditMode's <c>PresentationTests</c>).
    /// </summary>
    public class PresentationPlayModeTests
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
        public IEnumerator CombatVfx_SpawnsASparkOnAParry()
        {
            var listener = arena.Track(new GameObject("CombatVfx"));
            listener.AddComponent<CombatVfx>();

            var attacker = arena.Track(new GameObject("Attacker"));
            attacker.transform.position = new Vector3(2f, 0f, 3f);

            EventBus.Publish(new ParryEvent(null, attacker, true));
            yield return null;

            var spark = GameObject.Find("Vfx_Spark");
            Assert.IsNotNull(spark, "A parry should spawn a placeholder spark VFX.");
            Assert.AreEqual(attacker.transform.position, spark.transform.position);
        }

        [UnityTest]
        public IEnumerator CombatAudio_PlaysAToneOnGuardBroken()
        {
            var listener = arena.Track(new GameObject("CombatAudio"));
            listener.AddComponent<CombatAudio>();

            var defender = arena.Track(new GameObject("Defender"));

            EventBus.Publish(new GuardBrokenEvent(defender, 0.5f));
            yield return null;

            var sfx = GameObject.Find("Sfx_GuardBreak");
            Assert.IsNotNull(sfx, "A broken guard should play a placeholder guard-break tone.");
            Assert.IsTrue(sfx.GetComponent<AudioSource>().isPlaying);
        }

        [UnityTest]
        public IEnumerator FireBrazierVfx_SpawnsFireWhenTheBrazierLights()
        {
            var brazierGo = arena.Track(new GameObject("Brazier"));
            brazierGo.AddComponent<BoxCollider>();
            var brazier = brazierGo.AddComponent<FireBrazier>();
            brazierGo.AddComponent<FireBrazierVfx>();

            brazier.Light();
            yield return null;

            Assert.IsNotNull(GameObject.Find("Vfx_Fire"), "Lighting a brazier should spawn a placeholder fire VFX.");
        }

        [Test]
        public void BossPresentationHooks_PlayTransformationSpawnsVfxAndAudio()
        {
            var bossGo = arena.Track(new GameObject("Boss"));
            var hooks = bossGo.AddComponent<BossPresentationHooks>();

            hooks.PlayTransformation();

            Assert.IsNotNull(GameObject.Find("Vfx_BossTransformation"));
            Assert.IsNotNull(GameObject.Find("Sfx_BossPhaseTransition"));
        }

        [UnityTest]
        public IEnumerator PlaceholderAnimator_TiltsDuringASwingAndRelaxesAfter()
        {
            yield return Ground();

            var player = arena.SpawnPlayer(Vector3.zero, withWeapon: true, withCombatInput: true);
            var animator = player.Root.AddComponent<Game.Animation.PlaceholderAnimator>();

            player.Weapon.TrySwing(AttackType.Light);
            yield return TestArena.Until(() => player.Weapon.IsSwinging, "the swing to start", 1f);

            // Real time, not frame count, so the blend has time to move away from
            // rest regardless of how fast the editor is stepping frames.
            yield return new WaitForSeconds(0.2f);
            yield return null;

            var duringSwingTilt = NormalizeAngle(player.Root.transform.localRotation.eulerAngles.x);
            Assert.Greater(Mathf.Abs(duringSwingTilt), 0.5f,
                "The placeholder animator should tilt while the weapon is swinging.");

            yield return TestArena.Until(() => !player.Weapon.IsSwinging, "the swing to end", 3f);

            // Let the blend relax back towards rest. A frame count is not a fixed
            // amount of real time — the Slerp's convergence depends on Time.deltaTime,
            // not on how many frames elapsed — so wait real seconds instead.
            yield return new WaitForSeconds(1f);
            yield return null;

            var afterSwingTilt = NormalizeAngle(player.Root.transform.localRotation.eulerAngles.x);
            Assert.AreEqual(0f, afterSwingTilt, 1f, "The tilt should relax back to rest once the swing ends.");
        }

        [UnityTest]
        public IEnumerator PlaceholderAnimator_CollapsesOnPlayerDied()
        {
            var player = arena.SpawnPlayer(Vector3.zero);
            var animator = player.Root.AddComponent<Game.Animation.PlaceholderAnimator>();

            EventBus.Publish(new PlayerDiedEvent(player.Root));

            // A frame count is not a fixed amount of real time; see the note in
            // PlaceholderAnimator_TiltsDuringASwingAndRelaxesAfter.
            yield return new WaitForSeconds(0.5f);
            yield return null;

            var tiltX = NormalizeAngle(player.Root.transform.localRotation.eulerAngles.x);
            Assert.Greater(tiltX, 10f, "The placeholder animator should collapse the visual on death.");
        }

        private static float NormalizeAngle(float degrees)
        {
            return degrees > 180f ? degrees - 360f : degrees;
        }

        private IEnumerator Ground()
        {
            arena.BuildFloor();
            arena.BuildNavMesh();
            yield return null;
        }
    }
}

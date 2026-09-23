using System.Collections;
using Game.Combat;
using Game.Dialogue;
using Game.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Tests.Play
{
    public class ResolutionCombatPlayModeTests
    {
        private TestArena arena;

        [SetUp] public void SetUp() => arena = new TestArena();
        [TearDown] public void TearDown() => arena.Dispose();

        [UnityTest]
        public IEnumerator MovingHitbox_SweepsAcrossThinHurtboxOnce()
        {
            var owner = arena.Track(new GameObject("Attacker"));
            owner.SetActive(false);
            var volume = new GameObject("Moving hitbox");
            volume.transform.SetParent(owner.transform, false);
            volume.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            var box = volume.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = Vector3.one * 0.2f;
            var hitbox = volume.AddComponent<Hitbox>();
            hitbox.SetOwner(owner);
            owner.SetActive(true);
            var target = arena.SpawnDummy("Thin target", new Vector3(0f, 0f, 2f));
            yield return null;

            hitbox.Activate(DamageData.Create(10f, owner));
            volume.transform.position = new Vector3(0f, 0.6f, 4f);
            Physics.SyncTransforms();
            yield return null;

            Assert.AreEqual(50f, target.Health.CurrentHealth, 0.01f);
            yield return null;
            Assert.AreEqual(50f, target.Health.CurrentHealth, 0.01f,
                "The sweep and trigger callback applied one attack twice.");
        }

        [UnityTest]
        public IEnumerator LockOn_CannotAcquireThroughSolidWall()
        {
            var player = arena.SpawnPlayer(Vector3.zero, withCombatInput: true);
            var target = arena.SpawnDummy("Target", new Vector3(0f, 0f, 4f));
            var wall = arena.Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.SetPositionAndRotation(new Vector3(0f, 1f, 2f), Quaternion.identity);
            wall.transform.localScale = new Vector3(2f, 2f, 0.2f);
            Physics.SyncTransforms();
            yield return null;

            Assert.IsFalse(player.LockOn.Acquire());
            wall.SetActive(false);
            Physics.SyncTransforms();
            Assert.IsTrue(player.LockOn.Acquire());
            Assert.AreEqual(target.Health, player.LockOn.Target);
        }

        [UnityTest]
        public IEnumerator Interaction_CannotSelectThroughWall_AndHasNoSixteenColliderCap()
        {
            var player = arena.SpawnPlayer(Vector3.zero);
            var actions = arena.BuildInputActions();
            actions.FindActionMap("Gameplay").AddAction("Interact", InputActionType.Button);
            player.Root.SetActive(false);
            var interactor = player.Root.AddComponent<PlayerInteractor>();
            interactor.Configure(actions);
            player.Root.SetActive(true);

            for (var i = 0; i < 20; i++)
            {
                var decoy = arena.Track(new GameObject("Decoy" + i));
                decoy.transform.position = new Vector3(0f, 0f, 2f);
                decoy.AddComponent<SphereCollider>().isTrigger = true;
            }
            var npc = arena.Track(new GameObject("NPC"));
            npc.transform.position = new Vector3(0f, 0f, 2f);
            var collider = npc.AddComponent<SphereCollider>();
            collider.center = Vector3.up;
            collider.isTrigger = true;
            npc.AddComponent<NpcInteractable>();
            var wall = arena.Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = new Vector3(0f, 1f, 1f);
            wall.transform.localScale = new Vector3(2f, 2f, 0.2f);
            Physics.SyncTransforms();
            yield return null;
            Assert.IsNull(interactor.Current);

            wall.SetActive(false);
            Physics.SyncTransforms();
            yield return null;
            Assert.IsNotNull(interactor.Current,
                "An interactable after twenty unrelated colliders was never considered.");
        }
    }
}
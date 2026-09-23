using System.Collections;
using Game.Core;
using Game.Dialogue;
using Game.Inventory;
using Game.Quests;
using Game.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Play
{
    public class QuestAdapterPlayModeTests
    {
        private TestArena arena;
        [SetUp] public void SetUp() => arena = new TestArena();
        [TearDown] public void TearDown() => arena.Dispose();

        private QuestManager Start(string id, ObjectiveType type)
        {
            arena.EnsureWorldState();
            var quest = arena.TrackAsset(ScriptableObject.CreateInstance<QuestDefinition>());
            quest.Configure("Q_" + id, id, "",
                new[] { new QuestObjective { ObjectiveId = id, Description = id, Type = type } });
            var manager = arena.SpawnQuestManager(quest);
            Assert.IsTrue(manager.StartQuest(quest));
            return manager;
        }

        [UnityTest]
        public IEnumerator CollectItem_ReportsItsQuestObjectiveOnce()
        {
            var manager = Start("COLLECT", ObjectiveType.CollectItem);
            var item = arena.TrackAsset(ScriptableObject.CreateInstance<InventoryItem>());
            item.Configure("ITEM", "Item", ItemCategory.QuestItem);
            var inventoryGo = arena.Track(new GameObject("Inventory"));
            inventoryGo.SetActive(false);
            var inventory = inventoryGo.AddComponent<InventoryManager>();
            inventory.Configure(new[] { item });
            inventoryGo.SetActive(true);
            var pickupGo = arena.Track(new GameObject("Pickup"));
            pickupGo.AddComponent<BoxCollider>();
            var pickup = pickupGo.AddComponent<ItemPickup>();
            pickup.Configure(item, 1, "COLLECT");
            var player = arena.SpawnPlayer(Vector3.zero);
            yield return null;
            pickup.Interact(player.Root);
            pickup.Interact(player.Root);
            Assert.AreEqual(QuestStatus.Completed, manager.GetStatus("Q_COLLECT"));
            Assert.AreEqual(1, inventory.GetCount(item));
        }

        [UnityTest]
        public IEnumerator ChooseDialogue_ReportsTheSelectedChoiceObjective()
        {
            var manager = Start("CHOOSE", ObjectiveType.ChooseDialogue);
            var graph = arena.TrackAsset(ScriptableObject.CreateInstance<DialogueGraph>());
            graph.Configure("CHOICE_GRAPH", new[] { "START" }, new[]
            {
                new DialogueNode
                {
                    DialogueId = "START", Text = "Choose.",
                    Choices = new[] { new DialogueChoice { Text = "Yes", ObjectiveId = "CHOOSE" } }
                }
            });
            var runner = arena.Track(new GameObject("DialogueRunner")).AddComponent<DialogueRunner>();
            yield return null;
            Assert.IsTrue(runner.Begin(graph));
            Assert.IsTrue(runner.Choose(0));
            Assert.AreEqual(QuestStatus.Completed, manager.GetStatus("Q_CHOOSE"));
        }

        [UnityTest]
        public IEnumerator Survive_CompletesAfterLivingForDuration()
        {
            var manager = Start("SURVIVE", ObjectiveType.Survive);
            arena.SpawnPlayer(Vector3.zero);
            var objective = arena.Track(new GameObject("Survival clock")).AddComponent<SurviveObjective>();
            objective.Configure("SURVIVE", 0.1f);
            yield return TestArena.Until(() => manager.GetStatus("Q_SURVIVE") == QuestStatus.Completed,
                "the survival objective", 2f);
        }

        [UnityTest]
        public IEnumerator Escort_CompletesOnlyWhenAssignedActorReachesGoal()
        {
            var manager = Start("ESCORT", ObjectiveType.Escort);
            var actor = arena.Track(new GameObject("Escort"));
            actor.transform.position = new Vector3(-4f, 0f, 0f);
            var body = actor.AddComponent<Rigidbody>();
            body.isKinematic = true;
            actor.AddComponent<SphereCollider>();
            var stranger = arena.Track(new GameObject("Stranger"));
            stranger.AddComponent<SphereCollider>();
            var goalGo = arena.Track(new GameObject("Goal"));
            var volume = goalGo.AddComponent<BoxCollider>();
            volume.size = new Vector3(2f, 2f, 2f);
            var goal = goalGo.AddComponent<EscortGoal>();
            goal.Configure(actor.transform, "ESCORT");
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(QuestStatus.Active, manager.GetStatus("Q_ESCORT"));
            actor.transform.position = Vector3.zero;
            Physics.SyncTransforms();
            yield return TestArena.Until(() => manager.GetStatus("Q_ESCORT") == QuestStatus.Completed,
                "the assigned escort to enter the goal", 2f);
        }
    }
}

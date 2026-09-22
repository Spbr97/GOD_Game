using System.Collections;
using Game.Core;
using Game.Memory;
using Game.Quests;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.Play
{
    /// <summary>
    /// The Quest Log and Memory Archive (SPEC.md section 42, TASK 010) against a real
    /// <see cref="GameManager"/> pause state and real quest/memory managers — not just
    /// the data underneath them, which <c>AvarshaTests</c> already covers in EditMode.
    ///
    /// Drives the journal through its public methods (<c>Toggle</c>, <c>ShowMemories</c>)
    /// rather than a simulated keypress, the same way <c>LockOnController.Toggle</c> is
    /// tested elsewhere: the input wiring itself is the same proven pattern
    /// <see cref="Game.UI.PauseMenu"/> already uses.
    /// </summary>
    public class JournalPlayModeTests
    {
        private TestArena arena;
        private GameManager gameManager;
        private JournalUI journal;

        [SetUp]
        public void SetUp()
        {
            arena = new TestArena();
            arena.EnsureWorldState();

            var gmGo = arena.Track(new GameObject("GameManager"));
            gameManager = gmGo.AddComponent<GameManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            arena.Dispose();
        }

        private JournalUI BuildJournal()
        {
            var actions = arena.BuildInputActions();
            actions.FindActionMap("Gameplay").AddAction("Journal", InputActionType.Button);

            var canvasGo = arena.Track(new GameObject("Canvas", typeof(RectTransform)));
            canvasGo.SetActive(false);

            var root = new GameObject("JournalRoot", typeof(RectTransform));
            root.transform.SetParent(canvasGo.transform, false);

            var questsPanel = new GameObject("QuestsPanel", typeof(RectTransform));
            questsPanel.transform.SetParent(root.transform, false);
            var questsContent = questsPanel.GetComponent<RectTransform>();

            var memoriesPanel = new GameObject("MemoriesPanel", typeof(RectTransform));
            memoriesPanel.transform.SetParent(root.transform, false);
            var memoriesContent = memoriesPanel.GetComponent<RectTransform>();

            var journalUi = canvasGo.AddComponent<JournalUI>();
            var so = new UnityEditor.SerializedObject(journalUi);
            so.FindProperty("inputActions").objectReferenceValue = actions;
            so.FindProperty("journalRoot").objectReferenceValue = root;
            so.FindProperty("questsPanel").objectReferenceValue = questsPanel;
            so.FindProperty("memoriesPanel").objectReferenceValue = memoriesPanel;
            so.FindProperty("questsContent").objectReferenceValue = questsContent;
            so.FindProperty("memoriesContent").objectReferenceValue = memoriesContent;
            so.ApplyModifiedPropertiesWithoutUndo();

            canvasGo.SetActive(true);
            return journalUi;
        }

        [UnityTest]
        public IEnumerator Journal_OpensAndPausesThenClosesAndResumes()
        {
            journal = BuildJournal();
            yield return null;

            Assert.AreEqual(GameState.Playing, gameManager.CurrentState);
            Assert.IsFalse(journal.IsOpen);

            journal.Toggle();
            yield return null;

            Assert.IsTrue(journal.IsOpen, "Toggle did not open the journal.");
            Assert.AreEqual(GameState.Paused, gameManager.CurrentState);

            journal.Toggle();
            yield return null;

            Assert.IsFalse(journal.IsOpen, "A second toggle did not close the journal.");
            Assert.AreEqual(GameState.Playing, gameManager.CurrentState);
        }

        [UnityTest]
        public IEnumerator Journal_IgnoresTheOpenRequestWhileSomethingElseHasTheGamePaused()
        {
            journal = BuildJournal();
            gameManager.Pause();
            yield return null;

            journal.Toggle();
            yield return null;

            Assert.IsFalse(journal.IsOpen,
                "The journal opened over a pause that belongs to something else (e.g. the Pause menu or a conversation).");
        }

        [UnityTest]
        public IEnumerator Journal_ListsActiveQuestsWithObjectiveProgress()
        {
            journal = BuildJournal();

            var quest = arena.TrackAsset(ScriptableObject.CreateInstance<QuestDefinition>());
            quest.Configure("Q_JOURNAL", "A Logged Quest", "",
                new[]
                {
                    new QuestObjective { ObjectiveId = "STEP", Description = "Do the thing", RequiredCount = 2 }
                });
            var quests = arena.SpawnQuestManager(quest);
            quests.StartQuest("Q_JOURNAL");
            quests.ReportObjective("STEP");
            yield return null;

            journal.Toggle();
            yield return null;

            var content = journal.transform.Find("JournalRoot/QuestsPanel");
            Assert.IsNotNull(content, "Could not find the quests panel to inspect its rows.");
            Assert.Greater(content.childCount, 0, "No rows were built for an active quest.");

            var text = CombinedRowText(content);
            StringAssert.Contains("A Logged Quest", text);
            StringAssert.Contains("Do the thing", text);
            StringAssert.Contains("1/2", text);
        }

        [UnityTest]
        public IEnumerator Journal_MemoriesTabListsDiscoveredMemoriesAndCorruptSpendsIntegrity()
        {
            journal = BuildJournal();

            var memory = arena.TrackAsset(ScriptableObject.CreateInstance<MemoryFragment>());
            memory.Configure("MEM_JOURNAL", "A Logged Memory", "It sits in the archive.", "Nobody",
                MemoryCategory.Lost, MemoryImportance.Optional);
            var memories = arena.SpawnMemoryManager(memory);
            memories.Discover("MEM_JOURNAL");
            yield return null;

            journal.Toggle();
            journal.ShowMemories();
            yield return null;

            var memoriesPanel = journal.transform.Find("JournalRoot/MemoriesPanel");
            var text = CombinedRowText(memoriesPanel);
            StringAssert.Contains("A Logged Memory", text);

            var corruptButton = FindButtonLabelled(memoriesPanel, "Corrupt");
            Assert.IsNotNull(corruptButton, "No Corrupt button was built for a non-critical memory.");

            corruptButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(MemoryState.Corrupted, memories.GetState("MEM_JOURNAL"));
            Assert.Less(memories.Integrity, 1f, "Corrupting from the archive should have spent integrity.");
        }

        [UnityTest]
        public IEnumerator Journal_ACriticalMemory_HasNoCorruptButton()
        {
            journal = BuildJournal();

            var memory = arena.TrackAsset(ScriptableObject.CreateInstance<MemoryFragment>());
            memory.Configure("MEM_CRITICAL", "A Protected Memory", "Never lost.", "Nobody",
                MemoryCategory.Divine, MemoryImportance.Critical);
            var memories = arena.SpawnMemoryManager(memory);
            memories.Discover("MEM_CRITICAL");
            yield return null;

            journal.Toggle();
            journal.ShowMemories();
            yield return null;

            var memoriesPanel = journal.transform.Find("JournalRoot/MemoriesPanel");
            Assert.IsNull(FindButtonLabelled(memoriesPanel, "Corrupt"),
                "A critical memory must not offer the corruption its own protection would refuse.");
        }

        private static string CombinedRowText(Transform panel)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var text in panel.GetComponentsInChildren<Text>(true))
            {
                builder.Append(text.text).Append('\n');
            }

            return builder.ToString();
        }

        private static Button FindButtonLabelled(Transform panel, string label)
        {
            foreach (var button in panel.GetComponentsInChildren<Button>(true))
            {
                var text = button.GetComponentInChildren<Text>();
                if (text != null && text.text == label)
                {
                    return button;
                }
            }

            return null;
        }
    }
}

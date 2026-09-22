using System.Collections.Generic;
using Game.Core;
using Game.Memory;
using Game.Quests;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The Quest Log and Memory Archive (SPEC.md section 42's two screens), combined
    /// into one journal with a tab each. Opens on its own input action rather than
    /// sharing Pause's: both would otherwise race to read <see cref="GameManager"/>'s
    /// state after the other has already changed it in the same input event.
    ///
    /// Rows are built at runtime rather than laid out in the Editor, because the
    /// number of quests and memories changes as the game is played. There is no
    /// scroll view yet — a placeholder list simply extends past the panel once there
    /// are enough entries to overflow it (SPEC.md section 78; see KNOWN_ISSUES.md).
    /// </summary>
    public class JournalUI : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

        [Header("Panels")]
        [SerializeField] private GameObject journalRoot;
        [SerializeField] private GameObject questsPanel;
        [SerializeField] private GameObject memoriesPanel;

        [Header("Tabs")]
        [SerializeField] private Button questsTabButton;
        [SerializeField] private Button memoriesTabButton;

        [Header("Content roots")]
        [SerializeField] private RectTransform questsContent;
        [SerializeField] private RectTransform memoriesContent;

        [Tooltip("Vertical space one journal row takes, so rows stack without overlapping.")]
        [SerializeField] private float rowHeight = 30f;

        [Tooltip("Integrity spent corrupting one memory from this screen (SPEC.md section 20).")]
        [SerializeField] private float corruptionCost = 0.1f;

        private InputAction journalAction;
        private readonly List<GameObject> spawnedRows = new();

        // Lazy rather than a field initializer: Resources.GetBuiltinResource refuses to
        // run during a MonoBehaviour's construction, which a static field initializer
        // counts as the moment anything adds the first JournalUI to a GameObject.
        private static Font cachedFont;
        private static Font BuiltinFont => cachedFont != null ? cachedFont : (cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (journalRoot != null)
            {
                journalRoot.SetActive(false);
            }

            if (inputActions == null)
            {
                GameLogger.LogError(LogCategory.UI, "No InputActionAsset assigned to JournalUI.", this);
                enabled = false;
                return;
            }

            journalAction = inputActions.FindActionMap("Gameplay")?.FindAction("Journal");

            questsTabButton?.onClick.AddListener(ShowQuests);
            memoriesTabButton?.onClick.AddListener(ShowMemories);
        }

        private void OnEnable()
        {
            journalAction?.Enable();
            if (journalAction != null)
            {
                journalAction.performed += OnJournalPressed;
            }
        }

        private void OnDisable()
        {
            if (journalAction != null)
            {
                journalAction.performed -= OnJournalPressed;
            }

            journalAction?.Disable();
        }

        private void OnJournalPressed(InputAction.CallbackContext context) => Toggle();

        /// <summary>
        /// Opens or closes the journal. Public so a test can drive it directly rather
        /// than simulating a keypress through the Input System, matching
        /// <see cref="Game.Combat.LockOnController.Toggle"/>'s test seam.
        /// </summary>
        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                Open();
            }
        }

        private void Open()
        {
            IsOpen = true;
            GameManager.Instance.Pause();

            if (journalRoot != null)
            {
                journalRoot.SetActive(true);
            }

            ShowQuests();

            // SPEC.md section 43: a gamepad has no cursor, so opening a panel with
            // nothing selected leaves it with no way to move focus onto a button at all.
            if (EventSystem.current != null && questsTabButton != null)
            {
                EventSystem.current.SetSelectedGameObject(questsTabButton.gameObject);
            }
        }

        private void Close()
        {
            IsOpen = false;

            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            {
                GameManager.Instance.Resume();
            }

            if (journalRoot != null)
            {
                journalRoot.SetActive(false);
            }
        }

        /// <summary>Switches to the Quest Log tab. Public: wired to the tab button, and a test seam.</summary>
        public void ShowQuests()
        {
            SetActiveIfAssigned(questsPanel, true);
            SetActiveIfAssigned(memoriesPanel, false);
            RefreshQuests();
        }

        /// <summary>Switches to the Memory Archive tab. Public: wired to the tab button, and a test seam.</summary>
        public void ShowMemories()
        {
            SetActiveIfAssigned(questsPanel, false);
            SetActiveIfAssigned(memoriesPanel, true);
            RefreshMemories();
        }

        // -------------------------------------------------------------------- quests

        private void RefreshQuests()
        {
            ClearRows();

            if (questsContent == null)
            {
                return;
            }

            var quests = QuestManager.Instance;
            if (quests == null)
            {
                AddRow(questsContent, 0, "No quest log exists in this scene.", Color.grey);
                return;
            }

            var row = 0;

            foreach (var progress in quests.ActiveQuests.Values)
            {
                row = AddQuestEntry(progress, row);
            }

            foreach (var progress in quests.FinishedQuests.Values)
            {
                row = AddQuestEntry(progress, row);
            }

            if (row == 0)
            {
                AddRow(questsContent, row, "No quests yet.", Color.grey);
            }
        }

        private int AddQuestEntry(QuestProgress progress, int row)
        {
            var statusColour = progress.Status switch
            {
                QuestStatus.Completed => new Color(0.5f, 0.9f, 0.5f),
                QuestStatus.Failed => new Color(0.9f, 0.4f, 0.4f),
                _ => Color.white
            };

            row = AddRow(questsContent, row, $"{progress.Definition.Title}  [{progress.Status}]", statusColour, bold: true);

            var objectives = progress.Definition.Objectives;
            if (objectives != null)
            {
                for (var i = 0; i < objectives.Count; i++)
                {
                    var objective = objectives[i];
                    if (objective == null || objective.Hidden)
                    {
                        continue;
                    }

                    var complete = progress.IsObjectiveComplete(objective.ObjectiveId);
                    var required = Mathf.Max(1, objective.RequiredCount);
                    var countText = required > 1 ? $"  ({progress.GetCount(objective.ObjectiveId)}/{required})" : string.Empty;
                    var mark = complete ? "[x] " : "[ ] ";

                    row = AddRow(questsContent, row, $"    {mark}{objective.Description}{countText}",
                        complete ? new Color(0.6f, 0.6f, 0.6f) : Color.white);
                }
            }

            return row + 1;
        }

        // ------------------------------------------------------------------- memories

        private void RefreshMemories()
        {
            ClearRows();

            if (memoriesContent == null)
            {
                return;
            }

            var memories = MemoryManager.Instance;
            if (memories == null)
            {
                AddRow(memoriesContent, 0, "No memory archive exists in this scene.", Color.grey);
                return;
            }

            var row = AddRow(memoriesContent, 0, $"Memory Integrity: {memories.Integrity * 100f:0}%", Color.white, bold: true);

            if (memories.DiscoveredCount == 0)
            {
                AddRow(memoriesContent, row, "No memories discovered yet.", Color.grey);
                return;
            }

            foreach (var memory in memories.Discovered)
            {
                var state = memories.GetState(memory.MemoryId);
                row = AddRow(memoriesContent, row, $"{memory.Title}  —  {memory.Category}, {state}", TintFor(state), bold: true);
                row = AddRow(memoriesContent, row, $"    {memory.Description}", Color.grey);

                if (!memory.IsProtected)
                {
                    row = AddCorruptButton(memoriesContent, row, memory);
                }

                row++;
            }
        }

        private int AddCorruptButton(RectTransform parent, int row, MemoryFragment memory)
        {
            var rect = RowRect(parent, row, 160f, 22f);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.35f, 0.1f, 0.1f, 0.9f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => CorruptMemory(memory));

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rect, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<Text>();
            label.font = BuiltinFont;
            label.fontSize = 16;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = "Corrupt";
            label.raycastTarget = false;

            spawnedRows.Add(rect.gameObject);
            return row + 1;
        }

        private void CorruptMemory(MemoryFragment memory)
        {
            MemoryManager.Instance?.Corrupt(memory, corruptionCost);
            RefreshMemories();
        }

        private static Color TintFor(MemoryState state)
        {
            return state switch
            {
                MemoryState.Corrupted => new Color(0.75f, 0.35f, 0.85f),
                MemoryState.Forgotten => new Color(0.55f, 0.55f, 0.55f),
                MemoryState.FalseMemory => new Color(0.85f, 0.55f, 0.2f),
                MemoryState.Restored => new Color(0.5f, 0.85f, 0.95f),
                _ => new Color(0.95f, 0.85f, 0.5f)
            };
        }

        // -------------------------------------------------------------------- rows

        private int AddRow(RectTransform parent, int row, string text, Color colour, bool bold = false)
        {
            var rect = RowRect(parent, row, -1f, rowHeight);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = BuiltinFont;
            label.fontSize = bold ? 20 : 18;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = colour;
            label.text = text;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;

            spawnedRows.Add(rect.gameObject);
            return row + 1;
        }

        private RectTransform RowRect(RectTransform parent, int row, float width, float height)
        {
            var go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(width < 0f ? 1f : 0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -row * rowHeight);
            rect.sizeDelta = width < 0f ? new Vector2(0f, height) : new Vector2(width, height);
            return rect;
        }

        private void ClearRows()
        {
            for (var i = 0; i < spawnedRows.Count; i++)
            {
                if (spawnedRows[i] != null)
                {
                    Destroy(spawnedRows[i]);
                }
            }

            spawnedRows.Clear();
        }

        private static void SetActiveIfAssigned(GameObject go, bool active)
        {
            if (go != null)
            {
                go.SetActive(active);
            }
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(InputActionAsset actions)
        {
            inputActions = actions;
        }
    }
}

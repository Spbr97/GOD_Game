using System.Collections.Generic;
using Game.Core;
using Game.Inventory;
using Game.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The Inventory and Skill Tree screens (SPEC.md section 42), combined into one
    /// panel with a tab each — the same shape <see cref="JournalUI"/> already uses for
    /// the Quest Log and Memory Archive, down to the row-building and font handling,
    /// for the same reasons: the lists change size as the game is played, and opening
    /// on its own input action avoids racing <see cref="PauseMenu"/>/<see cref="JournalUI"/>
    /// for <see cref="GameManager"/>'s state in the same input event.
    /// </summary>
    public class ProgressionUI : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

        [Header("Panels")]
        [SerializeField] private GameObject progressionRoot;
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private GameObject skillsPanel;

        [Header("Tabs")]
        [SerializeField] private Button inventoryTabButton;
        [SerializeField] private Button skillsTabButton;

        [Header("Content roots")]
        [SerializeField] private RectTransform inventoryContent;
        [SerializeField] private RectTransform skillsContent;

        [Tooltip("Vertical space one row takes, so rows stack without overlapping.")]
        [SerializeField] private float rowHeight = 30f;

        private InputAction progressionAction;
        private readonly List<GameObject> spawnedRows = new();

        // See JournalUI.BuiltinFont for why this is lazy rather than a field initializer.
        private static Font cachedFont;
        private static Font BuiltinFont => cachedFont != null ? cachedFont : (cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (progressionRoot != null)
            {
                progressionRoot.SetActive(false);
            }

            if (inputActions == null)
            {
                GameLogger.LogError(LogCategory.UI, "No InputActionAsset assigned to ProgressionUI.", this);
                enabled = false;
                return;
            }

            progressionAction = inputActions.FindActionMap("Gameplay")?.FindAction("Progression");

            inventoryTabButton?.onClick.AddListener(ShowInventory);
            skillsTabButton?.onClick.AddListener(ShowSkills);
        }

        private void OnEnable()
        {
            progressionAction?.Enable();
            if (progressionAction != null)
            {
                progressionAction.performed += OnProgressionPressed;
            }

            EventBus.Subscribe<SkillBonusesChangedEvent>(OnSkillBonusesChanged);
            EventBus.Subscribe<InventoryChangedEvent>(OnInventoryChanged);
        }

        private void OnDisable()
        {
            if (progressionAction != null)
            {
                progressionAction.performed -= OnProgressionPressed;
            }

            progressionAction?.Disable();

            EventBus.Unsubscribe<SkillBonusesChangedEvent>(OnSkillBonusesChanged);
            EventBus.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);
        }

        private void OnProgressionPressed(InputAction.CallbackContext context) => Toggle();

        private void OnSkillBonusesChanged(SkillBonusesChangedEvent changed)
        {
            if (IsOpen && skillsPanel != null && skillsPanel.activeSelf)
            {
                RefreshSkills();
            }
        }

        private void OnInventoryChanged(InventoryChangedEvent changed)
        {
            if (IsOpen && inventoryPanel != null && inventoryPanel.activeSelf)
            {
                RefreshInventory();
            }
        }

        /// <summary>Opens or closes the panel. Public so a test can drive it directly rather than a simulated keypress.</summary>
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

            if (progressionRoot != null)
            {
                progressionRoot.SetActive(true);
            }

            ShowInventory();

            // See JournalUI.Open for why: a gamepad has no cursor to move onto a button.
            if (EventSystem.current != null && inventoryTabButton != null)
            {
                EventSystem.current.SetSelectedGameObject(inventoryTabButton.gameObject);
            }
        }

        private void Close()
        {
            IsOpen = false;

            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            {
                GameManager.Instance.Resume();
            }

            if (progressionRoot != null)
            {
                progressionRoot.SetActive(false);
            }
        }

        /// <summary>Switches to the Inventory tab. Public: wired to the tab button, and a test seam.</summary>
        public void ShowInventory()
        {
            SetActiveIfAssigned(inventoryPanel, true);
            SetActiveIfAssigned(skillsPanel, false);
            RefreshInventory();
        }

        /// <summary>Switches to the Skill Tree tab. Public: wired to the tab button, and a test seam.</summary>
        public void ShowSkills()
        {
            SetActiveIfAssigned(inventoryPanel, false);
            SetActiveIfAssigned(skillsPanel, true);
            RefreshSkills();
        }

        // ----------------------------------------------------------------- inventory

        private void RefreshInventory()
        {
            ClearRows();

            if (inventoryContent == null)
            {
                return;
            }

            var inventory = InventoryManager.Instance;
            if (inventory == null)
            {
                AddRow(inventoryContent, 0, "No inventory exists in this scene.", Color.grey);
                return;
            }

            var row = 0;
            var any = false;

            foreach (ItemCategory category in System.Enum.GetValues(typeof(ItemCategory)))
            {
                var wroteHeader = false;

                foreach (var itemId in inventory.HeldItemIds)
                {
                    var item = inventory.Find(itemId);
                    if (item == null || item.Category != category)
                    {
                        continue;
                    }

                    if (!wroteHeader)
                    {
                        row = AddRow(inventoryContent, row, category.ToString(), Color.white, bold: true);
                        wroteHeader = true;
                    }

                    var count = inventory.GetCount(item);
                    var countText = item.Stackable && count != 1 ? $"  x{count}" : string.Empty;
                    row = AddRow(inventoryContent, row, $"    {item.DisplayName}{countText}", Color.white);
                    any = true;
                }
            }

            if (!any)
            {
                AddRow(inventoryContent, row, "Nothing carried yet.", Color.grey);
            }
        }

        // --------------------------------------------------------------------- skills

        private void RefreshSkills()
        {
            ClearRows();

            if (skillsContent == null)
            {
                return;
            }

            var skills = SkillTreeManager.Instance;
            if (skills == null)
            {
                AddRow(skillsContent, 0, "No skill tree exists in this scene.", Color.grey);
                return;
            }

            var row = AddRow(skillsContent, 0, $"Skill Points: {skills.AvailablePoints}", Color.white, bold: true);

            foreach (SkillBranch branch in System.Enum.GetValues(typeof(SkillBranch)))
            {
                row = AddRow(skillsContent, row, branch.ToString(), Color.white, bold: true);
                var wroteAny = false;

                foreach (var skillId in BranchSkillIds(skills, branch))
                {
                    wroteAny = true;
                    row = AddSkillEntry(skills, skillId, row);
                }

                if (!wroteAny)
                {
                    row = AddRow(skillsContent, row, "    (none)", Color.grey);
                }

                row++;
            }
        }

        private static IEnumerable<string> BranchSkillIds(SkillTreeManager skills, SkillBranch branch)
        {
            foreach (var id in skills.CatalogueIds)
            {
                var skill = skills.Find(id);
                if (skill != null && skill.Branch == branch)
                {
                    yield return id;
                }
            }
        }

        private int AddSkillEntry(SkillTreeManager skills, string skillId, int row)
        {
            var skill = skills.Find(skillId);
            if (skill == null)
            {
                return row;
            }

            var unlocked = skills.IsUnlocked(skillId);
            var canUnlock = skills.CanUnlock(skillId);
            var status = unlocked ? "[unlocked]" : $"[{skill.Cost} pt]";
            var colour = unlocked ? new Color(0.5f, 0.9f, 0.5f) : (canUnlock ? Color.white : Color.grey);

            row = AddRow(skillsContent, row, $"    {skill.DisplayName}  {status}", colour);
            row = AddRow(skillsContent, row, $"        {skill.Description}", Color.grey);

            if (!unlocked)
            {
                row = AddUnlockButton(skillsContent, row, skill, canUnlock);
            }

            return row;
        }

        private int AddUnlockButton(RectTransform parent, int row, SkillDefinition skill, bool canUnlock)
        {
            var rect = RowRect(parent, row, 120f, 22f);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = canUnlock ? new Color(0.15f, 0.35f, 0.15f, 0.9f) : new Color(0.2f, 0.2f, 0.2f, 0.6f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = canUnlock;
            button.onClick.AddListener(() => UnlockSkill(skill.SkillId));

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rect, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<Text>();
            label.font = BuiltinFont;
            label.fontSize = 15;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = "Unlock";
            label.raycastTarget = false;

            spawnedRows.Add(rect.gameObject);
            return row + 1;
        }

        private void UnlockSkill(string skillId)
        {
            SkillTreeManager.Instance?.Unlock(skillId);
            RefreshSkills();
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

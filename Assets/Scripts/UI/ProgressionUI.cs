using System.Collections.Generic;
using Game.Core;
using Game.Inventory;
using Game.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Core.Localization;

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
                ShowUnavailable(inventoryContent, "inventory");
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
                        row = AddRow(inventoryContent, row, category.ToString(), UiTheme.Heading, bold: true);
                        wroteHeader = true;
                    }

                    var count = inventory.GetCount(item);
                    var countText = item.Stackable && count != 1 ? Strings.Format(StringKeys.ProgressionItemCount, count) : string.Empty;
                    row = AddRow(inventoryContent, row, $"    {item.DisplayName}{countText}", UiTheme.TextPrimary);
                    any = true;
                }
            }

            if (!any)
            {
                AddRow(inventoryContent, row, Strings.Get(StringKeys.ProgressionNoItems), UiTheme.TextMuted);
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
                ShowUnavailable(skillsContent, "skill tree");
                return;
            }

            var row = AddRow(skillsContent, 0, Strings.Format(StringKeys.ProgressionSkillPoints, skills.AvailablePoints),
                UiTheme.TextPrimary, bold: true);

            // Where the number came from. A player looking at "Skill Points: 0" has no way
            // to know that quests and bosses are what grant them, and a number with no
            // visible source reads as a system that is not working rather than as one
            // waiting to be earned (KNOWN_ISSUES.md, TASK 016).
            row = AddRow(skillsContent, row, Strings.Get(StringKeys.ProgressionPointsSource), UiTheme.TextMuted);
            row++;

            foreach (SkillBranch branch in System.Enum.GetValues(typeof(SkillBranch)))
            {
                row = AddRow(skillsContent, row, branch.ToString(), UiTheme.Heading, bold: true);
                var wroteAny = false;

                foreach (var skillId in BranchSkillIds(skills, branch))
                {
                    wroteAny = true;
                    row = AddSkillEntry(skills, skillId, row);
                }

                if (!wroteAny)
                {
                    row = AddRow(skillsContent, row,
                        "    " + Strings.Get(StringKeys.ProgressionBranchEmpty), UiTheme.TextMuted);
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
            var status = unlocked
                ? Strings.Get(StringKeys.ProgressionUnlocked)
                : Strings.Format(StringKeys.ProgressionCost, skill.Cost);
            var colour = unlocked ? UiTheme.Positive : (canUnlock ? UiTheme.TextPrimary : UiTheme.TextMuted);

            row = AddRow(skillsContent, row, Strings.Format(StringKeys.ProgressionSkillRow, skill.DisplayName, status), colour);
            row = AddRow(skillsContent, row, $"        {skill.Description}", UiTheme.TextMuted);

            if (!unlocked)
            {
                // Why not, before the button that cannot be pressed. A greyed-out Unlock
                // on its own says "no" without saying whether the answer is "not yet" or
                // "not enough", and those are two different things for a player deciding
                // what to do next (KNOWN_ISSUES.md, TASK 016).
                var reason = UnlockBlockedReason(skills, skill);
                if (reason != null)
                {
                    row = AddRow(skillsContent, row, "        " + reason, UiTheme.Caution);
                }

                row = AddUnlockButton(skillsContent, row, skill, canUnlock);
            }

            return row;
        }

        /// <summary>
        /// Why this skill cannot be unlocked, or null when it can be.
        ///
        /// Both answers are a "no" and they are not the same no: "needs two more points"
        /// is a reason to go and play, and "needs Astra Focus first" is a reason to look
        /// further up the branch. A single greyed-out button says neither, which is the
        /// gap KNOWN_ISSUES.md recorded under TASK 016.
        ///
        /// Points are reported before the prerequisite when both are missing. A player who
        /// cannot afford a skill they are also not eligible for is further away than the
        /// prerequisite alone suggests, and the cheaper fact to act on is the count.
        /// </summary>
        private static string UnlockBlockedReason(SkillTreeManager skills, SkillDefinition skill)
        {
            if (skills == null || skill == null || skills.CanUnlock(skill.SkillId))
            {
                return null;
            }

            var shortfall = skill.Cost - skills.AvailablePoints;
            if (shortfall > 0)
            {
                return Strings.Format(StringKeys.ProgressionNeedPoints, shortfall);
            }

            if (!string.IsNullOrEmpty(skill.PrerequisiteId) && !skills.IsUnlocked(skill.PrerequisiteId))
            {
                var prerequisite = skills.Find(skill.PrerequisiteId);
                return Strings.Format(StringKeys.ProgressionNeedPrerequisite,
                    prerequisite != null ? prerequisite.DisplayName : skill.PrerequisiteId);
            }

            // CanUnlock said no for a reason this method does not know about. Saying
            // nothing is better than inventing a reason the player would then act on.
            return null;
        }

        /// <summary>
        /// A screen whose system is not in the scene. The player gets one short sentence
        /// and the reason goes to the log: a sentence about scene wiring is not something
        /// to hand a translator, and not something a player can act on either.
        /// </summary>
        private void ShowUnavailable(RectTransform parent, string missing)
        {
            // A warning, not an error. In a shipped scene this is a wiring fault, and
            // SceneIntegrityTests already fails the build for it — but a test arena
            // deliberately builds half a scene, and a screen that degrades to one honest
            // sentence is working as designed, not failing.
            GameLogger.LogWarning(LogCategory.UI,
                $"No {missing} exists in this scene; the screen has nothing to show.", this);
            AddRow(parent, 0, Strings.Get(StringKeys.UiUnavailable), UiTheme.TextMuted);
        }

        private int AddUnlockButton(RectTransform parent, int row, SkillDefinition skill, bool canUnlock)
        {
            var rect = RowRect(parent, row, 120f, 22f);
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            UiTheme.Apply(button, confirm: true);
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
            UiTheme.Apply(label, UiTheme.TextRole.Small);
            label.alignment = TextAnchor.MiddleCenter;
            label.text = Strings.Get(StringKeys.ProgressionUnlockButton);
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
            UiTheme.Apply(label, bold ? UiTheme.TextRole.Heading : UiTheme.TextRole.Body);
            label.alignment = TextAnchor.MiddleLeft;
            // After the role: a row's colour says whether the thing is owned, affordable
            // or out of reach, which the role knows nothing about.
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

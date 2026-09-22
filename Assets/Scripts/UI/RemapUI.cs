using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Interactive controller/keyboard remapping (SPEC.md section 43) for the Main
    /// Menu's Controls screen (section 42). Rows are built at runtime, the same
    /// reason and shape as <see cref="JournalUI"/>'s — the same action always has the
    /// same two bindings (no <c>InputControlScheme</c>s are defined on the asset, so
    /// "keyboard" and "gamepad" are read off each binding's path rather than a scheme
    /// name), but listing them still beats hand-laying twenty-two rows in the Editor.
    ///
    /// Rebinds are saved through <see cref="SettingsManager.SaveBindingOverrides"/> onto
    /// the one shared <see cref="InputActionAsset"/> every gameplay controller reads,
    /// so a rebind here takes effect immediately without a scene reload.
    /// </summary>
    public class RemapUI : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private RectTransform content;
        [SerializeField] private Button resetButton;

        [Tooltip("Vertical space one row takes, so rows stack without overlapping.")]
        [SerializeField] private float rowHeight = 30f;

        [Tooltip("Gameplay actions offered for remapping, by name. Move/Look/Pause are left out: a composite axis and the menu's own escape hatch are poor candidates for interactive rebinding.")]
        [SerializeField]
        private string[] remappableActions =
        {
            "Jump", "Sprint", "LightAttack", "HeavyAttack", "Dodge",
            "Interact", "Guard", "LockOn", "Ability", "Journal", "Progression", "Skip"
        };

        // See JournalUI.BuiltinFont for why this is lazy rather than a field initializer.
        private static Font cachedFont;
        private static Font BuiltinFont => cachedFont != null ? cachedFont : (cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        private readonly List<GameObject> spawnedRows = new();
        private InputActionRebindingExtensions.RebindingOperation activeRebind;

        private void Awake()
        {
            resetButton?.onClick.AddListener(ResetToDefaults);
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnDisable()
        {
            CancelActiveRebind();
        }

        /// <summary>Rebuilds every row from the asset's current bindings. Public: a test seam and the reset button's own refresh.</summary>
        public void Refresh()
        {
            CancelActiveRebind();
            ClearRows();

            if (inputActions == null || content == null)
            {
                return;
            }

            var map = inputActions.FindActionMap("Gameplay");
            if (map == null)
            {
                return;
            }

            var row = 0;
            foreach (var actionName in remappableActions)
            {
                var action = map.FindAction(actionName);
                if (action == null)
                {
                    continue;
                }

                row = AddLabelRow(action.name, row);

                for (var i = 0; i < action.bindings.Count; i++)
                {
                    if (action.bindings[i].isComposite || action.bindings[i].isPartOfComposite)
                    {
                        continue;
                    }

                    row = AddBindingRow(action, i, row);
                }

                row++;
            }
        }

        public void ResetToDefaults()
        {
            SettingsManager.Instance?.ResetBindingOverrides();
            Refresh();
        }

        private void StartRebind(InputAction action, int bindingIndex, Text bindingLabel)
        {
            CancelActiveRebind();

            bindingLabel.text = "Press any key or button...";
            action.Disable();

            activeRebind = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("Mouse/position")
                .WithControlsExcluding("Mouse/delta")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(_ => FinishRebind(action, bindingIndex, bindingLabel))
                .OnCancel(_ => FinishRebind(action, bindingIndex, bindingLabel))
                .Start();
        }

        private void FinishRebind(InputAction action, int bindingIndex, Text bindingLabel)
        {
            activeRebind?.Dispose();
            activeRebind = null;
            action.Enable();

            bindingLabel.text = action.GetBindingDisplayString(bindingIndex);
            SettingsManager.Instance?.SaveBindingOverrides();
        }

        private void CancelActiveRebind()
        {
            if (activeRebind == null)
            {
                return;
            }

            var operation = activeRebind;
            activeRebind = null;
            operation.Cancel();
            operation.Dispose();
        }

        // -------------------------------------------------------------------- rows

        private int AddLabelRow(string actionName, int row)
        {
            var rect = RowRect(content, row, -1f, rowHeight);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = BuiltinFont;
            label.fontSize = 20;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = new Color(0.95f, 0.82f, 0.45f);
            label.text = SplitCamelCase(actionName);
            label.raycastTarget = false;

            spawnedRows.Add(rect.gameObject);
            return row + 1;
        }

        private int AddBindingRow(InputAction action, int bindingIndex, int row)
        {
            var rowRect = RowRect(content, row, -1f, rowHeight);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rowRect, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(20f, 0f);
            labelRect.offsetMax = new Vector2(-160f, 0f);
            var label = labelGo.AddComponent<Text>();
            label.font = BuiltinFont;
            label.fontSize = 16;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.white;
            label.text = action.GetBindingDisplayString(bindingIndex);
            label.raycastTarget = false;

            var buttonGo = new GameObject("RebindButton", typeof(RectTransform));
            buttonGo.transform.SetParent(rowRect, false);
            var buttonRect = (RectTransform)buttonGo.transform;
            buttonRect.anchorMin = new Vector2(1f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-10f, 0f);
            buttonRect.sizeDelta = new Vector2(140f, 24f);
            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);
            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(() => StartRebind(action, bindingIndex, label));

            var buttonLabelGo = new GameObject("Label", typeof(RectTransform));
            buttonLabelGo.transform.SetParent(buttonGo.transform, false);
            var buttonLabelRect = (RectTransform)buttonLabelGo.transform;
            buttonLabelRect.anchorMin = Vector2.zero;
            buttonLabelRect.anchorMax = Vector2.one;
            buttonLabelRect.offsetMin = Vector2.zero;
            buttonLabelRect.offsetMax = Vector2.zero;
            var buttonLabel = buttonLabelGo.AddComponent<Text>();
            buttonLabel.font = BuiltinFont;
            buttonLabel.fontSize = 14;
            buttonLabel.alignment = TextAnchor.MiddleCenter;
            buttonLabel.color = Color.white;
            buttonLabel.text = "Rebind";
            buttonLabel.raycastTarget = false;

            spawnedRows.Add(rowRect.gameObject);
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

        private static string SplitCamelCase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var result = new System.Text.StringBuilder();
            result.Append(value[0]);
            for (var i = 1; i < value.Length; i++)
            {
                if (char.IsUpper(value[i]) && !char.IsUpper(value[i - 1]))
                {
                    result.Append(' ');
                }

                result.Append(value[i]);
            }

            return result.ToString();
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(InputActionAsset actions, RectTransform contentRoot, string[] actionsToOffer = null)
        {
            inputActions = actions;
            content = contentRoot;
            if (actionsToOffer != null)
            {
                remappableActions = actionsToOffer;
            }
        }
    }
}

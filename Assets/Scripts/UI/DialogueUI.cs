using System.Collections.Generic;
using Game.Core;
using Game.Dialogue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Renders whatever <see cref="DialogueRunner"/> is showing (SPEC.md sections 22
    /// and 42, TASK 003).
    ///
    /// Presentation only: it reads dialogue events and calls Advance/Choose, and holds
    /// no conversation state of its own, so the runner stays testable without a canvas.
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text speakerLabel;
        [SerializeField] private Text bodyLabel;
        [SerializeField] private Text continueHint;

        [Tooltip("Parent the choice buttons are created under.")]
        [SerializeField] private RectTransform choiceContainer;

        [Tooltip("Button cloned for each choice. Kept inactive in the scene.")]
        [SerializeField] private Button choiceButtonTemplate;

        [SerializeField] private InputActionAsset inputActions;

        [Tooltip("Ignore advance input for this long after a node appears, so one press does not skip two lines.")]
        [SerializeField] private float advanceCooldown = 0.15f;

        private readonly List<Button> spawnedChoices = new();
        private InputAction advanceAction;
        private float advanceAllowedAt;

        private void Awake()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }

            if (choiceButtonTemplate != null)
            {
                choiceButtonTemplate.gameObject.SetActive(false);
            }

            if (inputActions != null)
            {
                advanceAction = inputActions.FindActionMap("Gameplay")?.FindAction("Interact");
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Subscribe<DialogueNodeShownEvent>(OnNodeShown);
            EventBus.Subscribe<DialogueCompletedEvent>(OnDialogueCompleted);
            advanceAction?.Enable();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Unsubscribe<DialogueNodeShownEvent>(OnNodeShown);
            EventBus.Unsubscribe<DialogueCompletedEvent>(OnDialogueCompleted);
            ClearChoices();
        }

        private void Update()
        {
            var runner = DialogueRunner.Instance;
            if (runner == null || !runner.IsRunning || runner.CurrentNode.HasChoices)
            {
                return;
            }

            if (Time.unscaledTime < advanceAllowedAt)
            {
                return;
            }

            if (advanceAction != null && advanceAction.WasPressedThisFrame())
            {
                runner.Advance();
            }
        }

        private void OnDialogueStarted(DialogueStartedEvent started)
        {
            if (panel != null)
            {
                panel.SetActive(true);
            }
        }

        private void OnNodeShown(DialogueNodeShownEvent shown)
        {
            var node = shown.Node;
            advanceAllowedAt = Time.unscaledTime + advanceCooldown;

            if (speakerLabel != null)
            {
                var speaker = string.IsNullOrEmpty(node.Speaker)
                    ? DialogueRunner.Instance != null ? DialogueRunner.Instance.DefaultSpeaker : null
                    : node.Speaker;
                speakerLabel.text = speaker ?? string.Empty;
            }

            if (bodyLabel != null)
            {
                bodyLabel.text = node.SubtitleText;
            }

            BuildChoices(node);

            if (continueHint != null)
            {
                continueHint.gameObject.SetActive(!node.HasChoices);
            }
        }

        private void OnDialogueCompleted(DialogueCompletedEvent completed)
        {
            ClearChoices();

            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void BuildChoices(DialogueNode node)
        {
            ClearChoices();

            if (!node.HasChoices || choiceButtonTemplate == null || choiceContainer == null)
            {
                return;
            }

            for (var i = 0; i < node.Choices.Length; i++)
            {
                var choice = node.Choices[i];
                if (!DialogueRunner.IsChoiceAvailable(choice))
                {
                    continue;
                }

                var index = i;
                var button = Instantiate(choiceButtonTemplate, choiceContainer);
                button.gameObject.SetActive(true);

                var label = button.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = choice.Text;
                }

                button.onClick.AddListener(() => DialogueRunner.Instance?.Choose(index));
                spawnedChoices.Add(button);
            }

            // A node with choices but none currently available would trap the player,
            // because there is nothing to press and advance is refused.
            if (spawnedChoices.Count == 0)
            {
                GameLogger.LogFallback(
                    LogCategory.Dialogue,
                    $"node '{node.DialogueId}' offered no available choices",
                    "DialogueUI.BuildChoices",
                    "every choice was hidden by its flag requirements",
                    "the conversation is ended so the player is not stuck",
                    this);
                DialogueRunner.Instance?.Stop();
            }
        }

        private void ClearChoices()
        {
            for (var i = 0; i < spawnedChoices.Count; i++)
            {
                if (spawnedChoices[i] != null)
                {
                    Destroy(spawnedChoices[i].gameObject);
                }
            }

            spawnedChoices.Clear();
        }
    }
}

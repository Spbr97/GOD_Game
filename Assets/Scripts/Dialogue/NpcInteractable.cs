using Game.Core;
using Game.World;
using UnityEngine;

namespace Game.Dialogue
{
    /// <summary>
    /// An NPC the player can speak to (SPEC.md TASK 003). It owns no conversation
    /// logic — it picks the graph and hands it to <see cref="DialogueRunner"/> — so
    /// adding a character is authoring a ScriptableObject, not writing a script.
    /// </summary>
    public class NpcInteractable : Interactable
    {
        [Header("NPC")]
        [SerializeField] private string npcName = "Villager";
        [SerializeField] private DialogueGraph dialogue;

        [Tooltip("Flag set the first time this NPC is spoken to. Optional.")]
        [SerializeField] private string firstSpokenFlag;

        [Tooltip("Turn to face the player while talking.")]
        [SerializeField] private bool faceSpeaker = true;

        public string NpcName => string.IsNullOrEmpty(npcName) ? DisplayName : npcName;
        public DialogueGraph Dialogue => dialogue;

        public override string Prompt => $"Speak to {NpcName}";

        public override bool CanInteract
        {
            get
            {
                if (dialogue == null)
                {
                    return false;
                }

                // Talking to a second NPC mid-conversation would be refused by the
                // runner anyway; hiding the prompt says so before the player presses.
                if (DialogueRunner.Instance != null && DialogueRunner.Instance.IsRunning)
                {
                    return false;
                }

                return base.CanInteract;
            }
        }

        public override void Interact(GameObject interactor)
        {
            if (DialogueRunner.Instance == null)
            {
                GameLogger.LogFallback(
                    LogCategory.Dialogue,
                    $"could not start dialogue with {NpcName}",
                    $"NpcInteractable on '{name}'",
                    "no DialogueRunner exists in the scene",
                    "the interaction is ignored; the player keeps control",
                    this);
                return;
            }

            if (faceSpeaker && interactor != null)
            {
                var toPlayer = interactor.transform.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(toPlayer);
                }
            }

            if (DialogueRunner.Instance.Begin(dialogue, NpcName) && !string.IsNullOrEmpty(firstSpokenFlag))
            {
                SetFlag(firstSpokenFlag);
            }
        }

        /// <summary>Test and tooling seam for wiring this NPC without the Inspector.</summary>
        public void Configure(string characterName, DialogueGraph graph, string spokenFlag = null)
        {
            npcName = characterName;
            dialogue = graph;
            firstSpokenFlag = spokenFlag;
        }
    }
}

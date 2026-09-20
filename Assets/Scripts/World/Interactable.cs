using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Raised when the player's current interaction target changes, so UI can show and
    /// hide the prompt without polling. A null <see cref="Target"/> means nothing is
    /// in range.
    /// </summary>
    public readonly struct InteractionTargetChangedEvent
    {
        public readonly Interactable Target;

        public InteractionTargetChangedEvent(Interactable target)
        {
            Target = target;
        }
    }

    /// <summary>
    /// Anything the player can press Interact on (SPEC.md section 27). NPCs, memory
    /// fragments and doors all derive from this so <see cref="PlayerInteractor"/> needs
    /// to know about exactly one type rather than one per content kind.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class Interactable : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private string promptVerb = "Interact";
        [SerializeField] private string displayName = "";

        [Tooltip("How close the player must be, measured from this object's position.")]
        [SerializeField] private float interactionRange = 2.5f;

        [Tooltip("Flags that must all be set before this can be interacted with. Leave empty for always.")]
        [SerializeField] private string[] requiredFlags;

        [Tooltip("Flags that block interaction while set. Used to retire an interaction once it is spent.")]
        [SerializeField] private string[] blockingFlags;

        public float InteractionRange => interactionRange;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        /// <summary>The line shown on the interaction prompt, e.g. "Speak to Queen Amara".</summary>
        public virtual string Prompt => $"{promptVerb} {DisplayName}";

        /// <summary>
        /// Whether the player may interact right now. Overrides should call
        /// <c>base.CanInteract</c> so the flag gates keep applying.
        /// </summary>
        public virtual bool CanInteract
        {
            get
            {
                if (!isActiveAndEnabled)
                {
                    return false;
                }

                var state = WorldState.Instance;
                if (state == null)
                {
                    // No world state in the scene is a setup error, not a reason to
                    // make the world uninteractable; ungated interactions still work.
                    return (requiredFlags == null || requiredFlags.Length == 0)
                           && (blockingFlags == null || blockingFlags.Length == 0);
                }

                return state.HasAllFlags(requiredFlags) && state.HasNoneOfFlags(blockingFlags);
            }
        }

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        /// <summary>Called by <see cref="PlayerInteractor"/>. Implementations do the actual work.</summary>
        public abstract void Interact(GameObject interactor);

        /// <summary>Test and tooling seam for the shared interaction fields.</summary>
        public void ConfigureInteraction(string verb, string label, float range,
            string[] required = null, string[] blocking = null)
        {
            promptVerb = verb;
            displayName = label;
            interactionRange = range;
            requiredFlags = required;
            blockingFlags = blocking;
        }

        protected void SetFlag(string flag, bool value = true)
        {
            if (string.IsNullOrEmpty(flag))
            {
                return;
            }

            if (WorldState.Instance == null)
            {
                GameLogger.LogFallback(
                    LogCategory.Game,
                    $"could not set flag '{flag}'",
                    $"{name} ({GetType().Name})",
                    "no WorldState exists in the scene",
                    "the flag is dropped; anything gated on it stays unavailable",
                    this);
                return;
            }

            WorldState.Instance.SetFlag(flag, value);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}

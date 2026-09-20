using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.World
{
    /// <summary>
    /// Finds the best interactable near the player and runs it on the Interact action
    /// (SPEC.md section 27, TASK 003).
    ///
    /// Selection is by angle-weighted distance rather than nearest-wins, because in a
    /// crowded scene the nearest object is often behind the player. Facing the thing
    /// you mean to talk to should be enough to select it.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

        [Tooltip("Widest search radius. Each interactable's own range still applies.")]
        [SerializeField] private float searchRadius = 4f;

        [SerializeField] private LayerMask interactableLayers = ~0;

        [Tooltip("How strongly facing the target is preferred over being close to it.")]
        [Range(0f, 1f)]
        [SerializeField] private float facingWeight = 0.6f;

        private readonly Collider[] candidates = new Collider[16];
        private InputAction interactAction;
        private Interactable current;

        /// <summary>The interactable the player would activate right now, or null.</summary>
        public Interactable Current => current;

        /// <summary>Set while dialogue or a cutscene owns input, so Interact does not fire through the UI.</summary>
        public bool InteractionSuspended { get; set; }

        private void Awake()
        {
            if (inputActions == null)
            {
                GameLogger.LogError(LogCategory.Player, "No InputActionAsset assigned to PlayerInteractor.", this);
                enabled = false;
                return;
            }

            interactAction = inputActions.FindActionMap("Gameplay")?.FindAction("Interact");

            if (interactAction == null)
            {
                GameLogger.LogFallback(
                    LogCategory.Player,
                    "could not find the Interact action",
                    "PlayerInteractor.Awake",
                    "the Gameplay action map has no action named 'Interact'",
                    "interaction is disabled; the rest of the player still works",
                    this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            interactAction?.Enable();
            if (interactAction != null)
            {
                interactAction.performed += OnInteractPressed;
            }
        }

        private void OnDisable()
        {
            if (interactAction != null)
            {
                interactAction.performed -= OnInteractPressed;
            }

            interactAction?.Disable();
            SetCurrent(null);
        }

        private void Update()
        {
            SetCurrent(InteractionSuspended ? null : FindBest());
        }

        private void OnInteractPressed(InputAction.CallbackContext context)
        {
            TryInteract();
        }

        /// <summary>Runs the current interactable. Public so tests and debug tools can drive it.</summary>
        public bool TryInteract()
        {
            if (InteractionSuspended || current == null || !current.CanInteract)
            {
                return false;
            }

            GameLogger.Log(LogCategory.Player, $"Interacted with '{current.DisplayName}'.", this);
            current.Interact(gameObject);
            return true;
        }

        private Interactable FindBest()
        {
            var origin = transform.position;
            var count = Physics.OverlapSphereNonAlloc(
                origin, searchRadius, candidates, interactableLayers, QueryTriggerInteraction.Collide);

            Interactable best = null;
            var bestScore = float.MaxValue;
            var forward = transform.forward;

            for (var i = 0; i < count; i++)
            {
                var candidate = candidates[i] != null ? candidates[i].GetComponentInParent<Interactable>() : null;
                if (candidate == null || !candidate.CanInteract)
                {
                    continue;
                }

                var offset = candidate.transform.position - origin;
                offset.y = 0f;
                var distance = offset.magnitude;
                if (distance > candidate.InteractionRange)
                {
                    continue;
                }

                // A target directly ahead scores its raw distance; one directly behind
                // scores as if it were twice as far. Ties still fall to the closer one.
                var facing = distance > 0.001f ? Vector3.Dot(forward, offset / distance) : 1f;
                var score = distance * (1f + facingWeight * (1f - facing));

                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private void SetCurrent(Interactable next)
        {
            if (ReferenceEquals(current, next))
            {
                return;
            }

            current = next;
            EventBus.Publish(new InteractionTargetChangedEvent(next));
        }
    }
}

using Game.Core;
using Game.World;
using UnityEngine;

namespace Game.Memory
{
    /// <summary>
    /// A memory fragment sitting in the world, collected by interacting with it
    /// (SPEC.md sections 19 and 27, TASK 003).
    ///
    /// The object stays in the scene after collection rather than being destroyed, so
    /// a memory cannot be lost to a destroyed object before the grant is recorded; it
    /// just stops being interactable.
    /// </summary>
    public class MemoryPickup : Interactable
    {
        [Header("Memory")]
        [SerializeField] private MemoryFragment memory;

        [Tooltip("Renderer tinted with the memory's colour and hidden once collected.")]
        [SerializeField] private Renderer visual;

        [Tooltip("Bobbing height, so the fragment reads as an object of interest without VFX.")]
        [SerializeField] private float bobHeight = 0.15f;

        [SerializeField] private float bobSpeed = 1.5f;
        [SerializeField] private float spinDegreesPerSecond = 40f;

        private Vector3 restPosition;
        private MaterialPropertyBlock propertyBlock;

        public MemoryFragment Memory => memory;
        public bool Collected { get; private set; }

        public override string Prompt => memory != null ? $"Recall {memory.Title}" : "Recall memory";

        public override bool CanInteract => !Collected && memory != null && base.CanInteract;

        private void Awake()
        {
            restPosition = transform.position;

            if (visual == null)
            {
                visual = GetComponentInChildren<Renderer>();
            }

            ApplyTint();
        }

        private void Update()
        {
            if (Collected)
            {
                return;
            }

            var bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = restPosition + new Vector3(0f, bob, 0f);
            transform.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.World);
        }

        public override void Interact(GameObject interactor)
        {
            if (Collected || memory == null)
            {
                return;
            }

            if (MemoryManager.Instance == null)
            {
                GameLogger.LogFallback(
                    LogCategory.Memory,
                    $"could not collect memory '{memory.MemoryId}'",
                    $"MemoryPickup on '{name}'",
                    "no MemoryManager exists in the scene",
                    "the fragment stays collectable so the memory is not lost",
                    this);
                return;
            }

            // Marked collected on a successful grant only. If the manager rejects it
            // (already discovered), the object still retires, because leaving a
            // permanently inert prompt in the world is worse than a no-op.
            MemoryManager.Instance.Discover(memory);
            Collected = true;

            if (visual != null)
            {
                visual.enabled = false;
            }

            transform.position = restPosition;
        }

        private void ApplyTint()
        {
            if (visual == null || memory == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            visual.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", memory.VisualTint);
            visual.SetPropertyBlock(propertyBlock);
        }

        /// <summary>Test and tooling seam for wiring the pickup without the Inspector.</summary>
        public void Configure(MemoryFragment fragment)
        {
            memory = fragment;
            if (visual == null)
            {
                visual = GetComponentInChildren<Renderer>();
            }

            ApplyTint();
        }
    }
}

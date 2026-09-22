using Game.Audio;
using Game.Core;
using Game.VFX;
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

        [Tooltip("Optional. If set, collecting this fragment survives a save (SPEC.md TASK 009) — a load will not un-collect it.")]
        [SerializeField] private SaveIdentity identity;

        private Vector3 restPosition;
        private MaterialPropertyBlock propertyBlock;

        public MemoryFragment Memory => memory;
        public bool Collected { get; private set; }

        public override string Prompt => memory != null ? $"Recall {memory.Title}" : "Recall memory";

        public override bool CanInteract => !Collected && memory != null && base.CanInteract;

        private void Awake()
        {
            restPosition = transform.position;

            if (identity == null)
            {
                identity = GetComponent<SaveIdentity>();
            }

            if (visual == null)
            {
                visual = GetComponentInChildren<Renderer>();
            }

            ApplyTint();
        }

        private void OnEnable()
        {
            if (identity == null)
            {
                return;
            }

            EventBus.Subscribe<WorldFlagChangedEvent>(OnWorldFlagChanged);

            // See EnemyHealth.OnEnable for why both a check-now and a subscription are
            // needed: this covers a save already applied before this pickup existed.
            if (WorldObjectState.IsMarked(WorldObjectState.CollectedFlag(identity.Id)))
            {
                ApplyRestoredCollection();
            }
        }

        private void OnDisable()
        {
            if (identity != null)
            {
                EventBus.Unsubscribe<WorldFlagChangedEvent>(OnWorldFlagChanged);
            }
        }

        private void OnWorldFlagChanged(WorldFlagChangedEvent changed)
        {
            if (changed.Value && identity != null && changed.Flag == WorldObjectState.CollectedFlag(identity.Id))
            {
                ApplyRestoredCollection();
            }
        }

        /// <summary>
        /// Applies a remembered collection from a save directly, without calling
        /// <see cref="MemoryManager.Discover"/> — the save already restored that grant
        /// through <c>MemoryStates</c>, and discovering it again would be a duplicate
        /// (SPEC.md section 56: no duplicate rewards).
        ///
        /// Guarded by <c>Collected</c> for the same reason <c>EnemyHealth</c> guards on
        /// <c>IsDead</c>: <see cref="Interact"/> marking the flag publishes the very
        /// event this method listens for, so an ordinary in-session collection would
        /// otherwise call back into itself. Harmless here since both paths set the same
        /// state, but the guard keeps the two paths honestly separate.
        /// </summary>
        private void ApplyRestoredCollection()
        {
            if (Collected)
            {
                return;
            }

            Collected = true;

            if (visual != null)
            {
                visual.enabled = false;
            }

            transform.position = restPosition;
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

            // Cosmetic only, and only on a live collection — a save's restore replays
            // no VFX/audio, the same way it replays no other one-off effect.
            VfxSpawner.Spawn(VfxKind.MemoryFragment, transform.position);
            SfxSpawner.Play(SfxKind.MemoryDiscovered, transform.position);

            // Set before marking the flag: Mark publishes the event this object also
            // listens for, and Collected is what ApplyRestoredCollection's guard checks
            // to tell an ordinary collection apart from a genuine restore.
            Collected = true;

            if (identity != null)
            {
                WorldObjectState.Mark(WorldObjectState.CollectedFlag(identity.Id));
            }

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

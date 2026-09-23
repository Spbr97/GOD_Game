using Game.Core;
using Game.Inventory;
using Game.Quests;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A generic inventory item sitting in the world (SPEC.md section 28, TASK 016) —
    /// the same role <see cref="Game.Memory.MemoryPickup"/> plays for memories, kept
    /// separate because an <see cref="InventoryItem"/> is a different catalogue with
    /// different categories, not because the interaction differs.
    /// </summary>
    public class ItemPickup : Interactable
    {
        [SerializeField] private InventoryItem item;
        [SerializeField] private int amount = 1;
        [SerializeField] private string objectiveIdOnCollect;

        [Tooltip("Renderer tinted with the item's colour and hidden once collected.")]
        [SerializeField] private Renderer visual;

        [Tooltip("Optional. If set, collecting this survives a save (SPEC.md TASK 009) — a load will not un-collect it.")]
        [SerializeField] private SaveIdentity identity;

        private MaterialPropertyBlock propertyBlock;

        public bool Collected { get; private set; }

        public override string Prompt => item != null ? $"Take {item.DisplayName}" : "Take item";

        public override bool CanInteract => !Collected && item != null && base.CanInteract;

        private void Awake()
        {
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

            // See EnemyHealth.OnEnable for why both a check-now and a subscription are needed.
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
        /// Applies a remembered collection directly, without calling
        /// <see cref="InventoryManager.Add"/> again — the save already restored the
        /// held count, and adding again would grant a duplicate (SPEC.md section 56).
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
        }

        public override void Interact(GameObject interactor)
        {
            if (Collected || item == null)
            {
                return;
            }

            if (InventoryManager.Instance == null)
            {
                GameLogger.LogFallback(
                    LogCategory.Game,
                    $"could not collect item '{item.ItemId}'",
                    $"ItemPickup on '{name}'",
                    "no InventoryManager exists in the scene",
                    "the pickup stays collectable so the item is not lost",
                    this);
                return;
            }

            InventoryManager.Instance.Add(item, amount);
            if (!string.IsNullOrEmpty(objectiveIdOnCollect))
            {
                QuestManager.Instance?.ReportObjective(objectiveIdOnCollect);
            }
            Collected = true;

            if (identity != null)
            {
                WorldObjectState.Mark(WorldObjectState.CollectedFlag(identity.Id));
            }

            if (visual != null)
            {
                visual.enabled = false;
            }
        }

        private void ApplyTint()
        {
            if (visual == null || item == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            visual.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", item.Tint);
            visual.SetPropertyBlock(propertyBlock);
        }

        /// <summary>Test and tooling seam for wiring the pickup without the Inspector.</summary>
        public void Configure(InventoryItem inventoryItem, int itemAmount = 1, string objectiveId = null)
        {
            item = inventoryItem;
            amount = itemAmount;
            objectiveIdOnCollect = objectiveId;
            if (visual == null)
            {
                visual = GetComponentInChildren<Renderer>();
            }

            ApplyTint();
        }
    }
}

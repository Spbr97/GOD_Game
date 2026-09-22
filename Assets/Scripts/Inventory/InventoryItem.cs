using UnityEngine;

namespace Game.Inventory
{
    /// <summary>The six categories from SPEC.md section 28.</summary>
    public enum ItemCategory
    {
        Weapon,
        DivineMark,
        MemoryFragment,
        QuestItem,
        Lore,
        Consumable
    }

    /// <summary>
    /// One kind of item (SPEC.md section 28), as data — the same authored-ScriptableObject
    /// shape as <see cref="Game.Memory.MemoryFragment"/> and <see cref="Game.AI.EnemyArchetype"/>.
    ///
    /// Section 28 asks for a simple inventory with no item clutter, so this holds only
    /// what the UI and <see cref="InventoryManager"/> actually need — no weight, no
    /// rarity tiers, no equipment slots, none of which the game has a use for yet.
    /// </summary>
    [CreateAssetMenu(fileName = "Item_", menuName = "God Game/Inventory Item")]
    public class InventoryItem : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName = "Untitled Item";

        [TextArea(2, 4)]
        [SerializeField] private string description;

        [SerializeField] private ItemCategory category;

        [Tooltip("Whether picking up more than one raises a count, or each is its own notable thing.")]
        [SerializeField] private bool stackable = true;

        [Tooltip("Placeholder stand-in for an icon (SPEC.md section 78).")]
        [SerializeField] private Color tint = Color.white;

        [Tooltip("Health restored when used, for Consumable items. Zero for anything else.")]
        [SerializeField] private float healAmount;

        public string ItemId => string.IsNullOrEmpty(itemId) ? name : itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public ItemCategory Category => category;
        public bool Stackable => stackable;
        public Color Tint => tint;
        public float HealAmount => healAmount;

        /// <summary>Test and tooling seam for authoring without the Inspector.</summary>
        public void Configure(string id, string label, ItemCategory itemCategory, bool canStack = true, float heal = 0f)
        {
            itemId = id;
            displayName = label;
            category = itemCategory;
            stackable = canStack;
            healAmount = heal;
        }
    }
}

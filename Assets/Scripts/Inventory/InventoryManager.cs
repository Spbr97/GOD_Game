using System.Collections.Generic;
using Game.Combat;
using Game.Core;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// What the player is carrying (SPEC.md section 28, TASK 016). Scene-scoped like
    /// <see cref="Game.Quests.QuestManager"/>/<see cref="Game.Memory.MemoryManager"/>
    /// (see ARCHITECTURE.md's "Manager lookup"), restored fresh by
    /// <see cref="Game.Save.SaveManager.Apply"/> on every scene load, and filling
    /// <c>SaveData.Inventory</c> (reserved since the save format was written).
    ///
    /// Deliberately not one entry per physical pickup: a count per <see cref="InventoryItem"/>
    /// id is section 28's "must be simple, avoid item clutter" in code, not just in the UI.
    ///
    /// Automatically grants a Divine Mark whenever a Divine-category memory is
    /// discovered (SPEC.md Act II's "obtain divine marks") — reacting to
    /// <see cref="Game.Memory.MemoryDiscoveredEvent"/>'s payload rather than any
    /// content asset naming a specific god, so a later temple's own divine memory
    /// grants one for free.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        private static InventoryManager instance;

        /// <summary>Resolved on first access; see <see cref="SceneSingleton"/>.</summary>
        public static InventoryManager Instance => SceneSingleton.Resolve(ref instance);

        [Tooltip("Every item that exists, so it can be resolved by id from save data and the UI.")]
        [SerializeField] private InventoryItem[] catalogue;

        [Tooltip("Granted on discovering a Divine-category memory. Optional.")]
        [SerializeField] private InventoryItem divineMarkItem;

        [Tooltip("Granted once when this manager first wakes — a new game's starting loadout. A save's own restore overwrites this, so it never duplicates a returning player's items.")]
        [SerializeField] private InventoryItem[] startingItems;

        private readonly Dictionary<string, InventoryItem> byId = new();
        private readonly Dictionary<string, int> counts = new();

        /// <summary>Every item id currently held with a positive count, for the UI to list.</summary>
        public IEnumerable<string> HeldItemIds
        {
            get
            {
                foreach (var pair in counts)
                {
                    if (pair.Value > 0)
                    {
                        yield return pair.Key;
                    }
                }
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                GameLogger.LogWarning(LogCategory.Game, "A second InventoryManager was destroyed.", this);
                Destroy(this);
                return;
            }

            instance = this;
            BuildCatalogue();
            GrantStartingItems();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<Game.Memory.MemoryDiscoveredEvent>(OnMemoryDiscovered);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<Game.Memory.MemoryDiscoveredEvent>(OnMemoryDiscovered);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public InventoryItem Find(string itemId)
        {
            return !string.IsNullOrEmpty(itemId) && byId.TryGetValue(itemId, out var item) ? item : null;
        }

        public int GetCount(InventoryItem item)
        {
            return item != null && counts.TryGetValue(item.ItemId, out var count) ? count : 0;
        }

        /// <summary>Adds one or more of an item. Register it in the catalogue first, or it cannot be resolved back from a save.</summary>
        public void Add(InventoryItem item, int amount = 1)
        {
            if (item == null || amount <= 0)
            {
                return;
            }

            Register(item);
            var next = GetCount(item) + (item.Stackable ? amount : 1);
            counts[item.ItemId] = next;
            EventBus.Publish(new InventoryChangedEvent(item, next));
        }

        /// <summary>Removes up to <paramref name="amount"/>, clamped at zero. Returns how many were actually removed.</summary>
        public int Remove(InventoryItem item, int amount = 1)
        {
            if (item == null || amount <= 0)
            {
                return 0;
            }

            var current = GetCount(item);
            var removed = Mathf.Min(current, amount);
            if (removed <= 0)
            {
                return 0;
            }

            var next = current - removed;
            counts[item.ItemId] = next;
            EventBus.Publish(new InventoryChangedEvent(item, next));
            return removed;
        }

        /// <summary>
        /// Consumes one and heals the player (SPEC.md section 28's Consumables).
        /// Refuses if none are held, or the item is not actually consumable.
        /// </summary>
        public bool UseConsumable(InventoryItem item)
        {
            if (item == null || item.Category != ItemCategory.Consumable || GetCount(item) <= 0)
            {
                return false;
            }

            var player = Object.FindAnyObjectByType<PlayerDeath>(FindObjectsInactive.Include);
            var health = player != null ? player.GetComponent<HealthComponent>() : null;

            if (health == null)
            {
                GameLogger.LogFallback(
                    LogCategory.Game,
                    $"could not use consumable '{item.ItemId}'",
                    "InventoryManager.UseConsumable",
                    "no player HealthComponent found in the scene",
                    "the item is kept rather than spent for no effect",
                    this);
                return false;
            }

            if (item.HealAmount > 0f)
            {
                health.Heal(item.HealAmount);
            }

            Remove(item, 1);
            return true;
        }

        private void OnMemoryDiscovered(Game.Memory.MemoryDiscoveredEvent discovered)
        {
            if (divineMarkItem != null && discovered.Memory != null
                && discovered.Memory.Category == Game.Memory.MemoryCategory.Divine)
            {
                Add(divineMarkItem);
            }
        }

        private void Register(InventoryItem item)
        {
            if (item != null)
            {
                byId[item.ItemId] = item;
            }
        }

        private void GrantStartingItems()
        {
            if (startingItems == null)
            {
                return;
            }

            for (var i = 0; i < startingItems.Length; i++)
            {
                Add(startingItems[i]);
            }
        }

        private void BuildCatalogue()
        {
            byId.Clear();
            if (catalogue == null)
            {
                return;
            }

            for (var i = 0; i < catalogue.Length; i++)
            {
                Register(catalogue[i]);
            }
        }

        /// <summary>Every held entry as "id:count", for <see cref="Game.Save.SaveManager.Capture"/>.</summary>
        public List<string> CaptureEntries()
        {
            var entries = new List<string>();
            foreach (var pair in counts)
            {
                if (pair.Value > 0)
                {
                    entries.Add($"{pair.Key}:{pair.Value}");
                }
            }

            return entries;
        }

        /// <summary>
        /// Puts held counts back where a save left them. Unknown ids (an item removed
        /// from the catalogue since the save was written) are dropped rather than
        /// failing the whole load, the same tolerance <see cref="Game.Quests.QuestManager.RestoreQuest"/>
        /// and <see cref="Game.Memory.MemoryManager.RestoreState"/> show for their own ids.
        /// </summary>
        public void RestoreEntries(IEnumerable<string> entries)
        {
            counts.Clear();
            if (entries == null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry))
                {
                    continue;
                }

                var separator = entry.IndexOf(':');
                if (separator <= 0 || separator == entry.Length - 1)
                {
                    continue;
                }

                var id = entry.Substring(0, separator);
                if (Find(id) == null || !int.TryParse(entry.Substring(separator + 1), out var count) || count <= 0)
                {
                    continue;
                }

                counts[id] = count;
            }
        }

        /// <summary>Test and tooling seam for supplying the catalogue without the Inspector.</summary>
        public void Configure(InventoryItem[] items, InventoryItem divineMark = null)
        {
            catalogue = items;
            divineMarkItem = divineMark;
            BuildCatalogue();
        }
    }
}

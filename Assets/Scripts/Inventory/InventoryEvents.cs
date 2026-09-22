namespace Game.Inventory
{
    /// <summary>Raised whenever an item's held count changes, so UI does no polling.</summary>
    public readonly struct InventoryChangedEvent
    {
        public readonly InventoryItem Item;
        public readonly int NewCount;

        public InventoryChangedEvent(InventoryItem item, int newCount)
        {
            Item = item;
            NewCount = newCount;
        }
    }
}

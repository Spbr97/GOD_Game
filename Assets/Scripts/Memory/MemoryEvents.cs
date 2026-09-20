namespace Game.Memory
{
    /// <summary>EventBus payloads for memories (SPEC.md section 49).</summary>
    public readonly struct MemoryDiscoveredEvent
    {
        public readonly MemoryFragment Memory;

        public MemoryDiscoveredEvent(MemoryFragment memory)
        {
            Memory = memory;
        }
    }

    public readonly struct MemoryStateChangedEvent
    {
        public readonly MemoryFragment Memory;
        public readonly MemoryState PreviousState;
        public readonly MemoryState NewState;

        public MemoryStateChangedEvent(MemoryFragment memory, MemoryState previousState, MemoryState newState)
        {
            Memory = memory;
            PreviousState = previousState;
            NewState = newState;
        }
    }

    /// <summary>Raised when overall memory integrity changes (SPEC.md section 20).</summary>
    public readonly struct MemoryIntegrityChangedEvent
    {
        public readonly float Integrity;

        public MemoryIntegrityChangedEvent(float integrity)
        {
            Integrity = integrity;
        }
    }
}

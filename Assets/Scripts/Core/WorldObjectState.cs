namespace Game.Core
{
    /// <summary>
    /// Per-object save state layered on top of <see cref="WorldState"/>'s flat flags
    /// (SPEC.md TASK 009). An enemy's death or a pickup's collection becomes an
    /// ordinary namespaced world flag, so it round-trips through a save via the same
    /// mechanism story flags already use — no change to <c>SaveData</c>'s shape, and no
    /// new save-file version.
    ///
    /// Restoring one of these flags must never replay the event that originally set it
    /// (a load replays no quest, memory or damage events, per <c>SaveManager</c>'s own
    /// doc comment). A caller that finds a flag already set applies the remembered
    /// state directly — hide the pickup, disable the corpse — rather than calling back
    /// through <c>Interact</c> or <c>HealthComponent.Kill</c>.
    /// </summary>
    public static class WorldObjectState
    {
        public static string DeadFlag(string saveId) => $"OBJ_DEAD_{saveId}";

        public static string CollectedFlag(string saveId) => $"OBJ_COLLECTED_{saveId}";

        public static string PaidFlag(string saveId) => $"OBJ_PAID_{saveId}";

        public static bool IsMarked(string flag) => WorldState.Instance != null && WorldState.Instance.GetFlag(flag);

        public static void Mark(string flag)
        {
            WorldState.Instance?.SetFlag(flag);
        }
    }
}

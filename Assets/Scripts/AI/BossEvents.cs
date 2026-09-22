using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Boss payloads published on <see cref="Game.Core.EventBus"/> (SPEC.md section 18,
    /// section 30's <c>OnBossStarted</c>/<c>OnBossDefeated</c> examples), so the HUD's
    /// boss health bar, music and later the cinematic system can react without
    /// referencing <see cref="BossController"/> directly.
    /// </summary>
    public readonly struct BossEncounterStartedEvent
    {
        public readonly GameObject Boss;
        public readonly string BossId;
        public readonly string DisplayName;

        public BossEncounterStartedEvent(GameObject boss, string bossId, string displayName)
        {
            Boss = boss;
            BossId = bossId;
            DisplayName = displayName;
        }
    }

    /// <summary>Raised whenever the boss crosses into phase 2 or phase 3.</summary>
    public readonly struct BossPhaseChangedEvent
    {
        public readonly GameObject Boss;
        public readonly int Phase;

        public BossPhaseChangedEvent(GameObject boss, int phase)
        {
            Boss = boss;
            Phase = phase;
        }
    }

    /// <summary>
    /// Raised when a live encounter ended without a defeat, because the player left
    /// the arena and the boss was put back (SPEC.md section 54's edge case 7,
    /// section 55's "important boss arenas must reset safely"). Distinct from
    /// <see cref="BossDefeatedEvent"/> on purpose: the HUD hides the same bar either
    /// way, but nothing that celebrates a victory should fire for a walked-away fight.
    /// </summary>
    public readonly struct BossEncounterResetEvent
    {
        public readonly GameObject Boss;
        public readonly string BossId;

        public BossEncounterResetEvent(GameObject boss, string bossId)
        {
            Boss = boss;
            BossId = bossId;
        }
    }

    /// <summary>Raised once when the boss dies, and again (without replaying the victory hooks) when a save restores a prior defeat.</summary>
    public readonly struct BossDefeatedEvent
    {
        public readonly GameObject Boss;
        public readonly string BossId;

        public BossDefeatedEvent(GameObject boss, string bossId)
        {
            Boss = boss;
            BossId = bossId;
        }
    }
}

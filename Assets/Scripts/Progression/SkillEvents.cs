namespace Game.Progression
{
    /// <summary>Raised when a skill is newly unlocked (not on a save restoring an already-unlocked one).</summary>
    public readonly struct SkillUnlockedEvent
    {
        public readonly SkillDefinition Skill;

        public SkillUnlockedEvent(SkillDefinition skill)
        {
            Skill = skill;
        }
    }

    /// <summary>
    /// Raised whenever the set of unlocked skills changes for any reason — a live
    /// unlock or a save restoring a whole set at once. Anything that needs to
    /// recompute a total from scratch (see <see cref="Game.Player.PlayerProgressionStats"/>)
    /// listens for this rather than <see cref="SkillUnlockedEvent"/>, which only fires
    /// per skill and only on a live unlock.
    /// </summary>
    public readonly struct SkillBonusesChangedEvent
    {
    }
}

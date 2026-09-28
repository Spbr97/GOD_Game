namespace Game.Combat.Abilities
{
    /// <summary>
    /// What a divine ability actually does (TASK 043).
    ///
    /// The split is deliberate: <see cref="DivineAbilityController"/> owns the parts
    /// every ability shares — is it unlocked, can it be paid for, is it off cooldown, has
    /// it been remembered — and an effect owns only the part that is unique to it. A
    /// temple in TASK 045-050 adds one <see cref="DivineAbilityDefinition"/> asset and one
    /// component implementing this, and gets the rest for free.
    ///
    /// <see cref="Perform"/> is called only after the cost has been paid and the cooldown
    /// started, so an effect never has to check any of that, and cannot forget to.
    /// </summary>
    public interface IDivineAbilityEffect
    {
        /// <summary>The <see cref="DivineAbilityDefinition.AbilityId"/> this implements.</summary>
        string AbilityId { get; }

        /// <summary>
        /// False when this effect cannot run right now for a reason only it knows — mid-
        /// swing, already dashing, underwater. Checked before the cost is taken, so a
        /// refusal never charges the player.
        /// </summary>
        bool CanPerform { get; }

        /// <summary>
        /// Runs the ability. The cost is already spent and the cooldown already started.
        /// </summary>
        void Perform(DivineAbilityDefinition definition);
    }
}

using UnityEngine;

namespace Game.Combat.Abilities
{
    /// <summary>
    /// Raised when any divine ability is used (TASK 043).
    ///
    /// One event for all seven rather than one per ability, so a listener that reacts to
    /// "the player spent divine power" — audio, VFX, the memory cost in SPEC.md section
    /// 20 — subscribes once and never changes again when a temple adds another.
    /// <see cref="AbilityId"/> is there for the listeners that do need to tell them apart.
    /// </summary>
    public readonly struct DivineAbilityUsedEvent
    {
        public readonly GameObject User;
        public readonly string AbilityId;

        /// <summary>How many times this ability has been used this run. SPEC.md section 20's "repeated use" costs read this.</summary>
        public readonly int TotalUses;

        public DivineAbilityUsedEvent(GameObject user, string abilityId, int totalUses)
        {
            User = user;
            AbilityId = abilityId;
            TotalUses = totalUses;
        }
    }

    /// <summary>
    /// Raised when a use was refused, with the reason. Exists so the HUD can say *why*
    /// nothing happened — a button that silently does nothing is the most common way a
    /// player concludes an ability is broken.
    /// </summary>
    public readonly struct DivineAbilityRefusedEvent
    {
        public readonly string AbilityId;
        public readonly DivineAbilityRefusal Reason;

        public DivineAbilityRefusedEvent(string abilityId, DivineAbilityRefusal reason)
        {
            AbilityId = abilityId;
            Reason = reason;
        }
    }

    public enum DivineAbilityRefusal
    {
        /// <summary>No such ability is registered on this user.</summary>
        Unknown,

        /// <summary>The temple that grants it has not been finished.</summary>
        Locked,

        /// <summary>Still cooling down.</summary>
        OnCooldown,

        /// <summary>Not enough divine energy.</summary>
        NotEnoughEnergy,

        /// <summary>The ability is registered but nothing implements it.</summary>
        NoEffect,

        /// <summary>Something else is running — a cutscene, a death, another ability.</summary>
        Busy
    }
}

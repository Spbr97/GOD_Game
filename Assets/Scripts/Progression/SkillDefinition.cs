using UnityEngine;

namespace Game.Progression
{
    /// <summary>The four branches from SPEC.md section 30.</summary>
    public enum SkillBranch
    {
        Warrior,
        Guardian,
        Divine,
        Memory
    }

    /// <summary>
    /// What one unlocked skill actually changes. A flat, data-driven set rather than
    /// one subclass per skill — <see cref="SkillTreeManager.GetBonus"/> just sums the
    /// values of every unlocked skill with a given type, so a new skill is a new
    /// <see cref="SkillDefinition"/> asset, not a new class.
    ///
    /// **All twelve are read by a system** as of TASK 043. Where each one lands:
    ///
    /// <list type="table">
    /// <item><term>AttackDamageMultiplier</term><description><c>WeaponController</c> — outgoing damage.</description></item>
    /// <item><term>ComboWindowBonusSeconds</term><description><c>CombatController</c> — the <c>ComboTracker</c>'s window, in seconds.</description></item>
    /// <item><term>ParryWindowMultiplier</term><description><c>GuardController.ScaledParryWindow</c>, and the perfect window with it.</description></item>
    /// <item><term>MaxHealthBonus</term><description><c>PlayerProgressionStats</c>.</description></item>
    /// <item><term>BlockReductionBonus</term><description><c>GuardController.ScaledStaminaPerDamageBlocked</c>.</description></item>
    /// <item><term>IncomingDamageMultiplier</term><description><c>Hurtbox</c>, for <c>Faction.Player</c> only.</description></item>
    /// <item><term>AbilityDashSpeedMultiplier</term><description><c>CombatController.ScaledAbilityDashSpeed</c>.</description></item>
    /// <item><term>AbilityCooldownMultiplier</term><description><c>CombatController.ScaledAbilityCooldown</c>.</description></item>
    /// <item><term>MaxDivineEnergyBonus</term><description><c>PlayerProgressionStats</c>.</description></item>
    /// <item><term>MemoryDetectionRangeMultiplier</term><description><c>MemoryPickup.InteractionRange</c>.</description></item>
    /// <item><term>EmberStepForgetRestoreMultiplier</term><description><c>MemoryManager.ScaledForgetSeconds</c>.</description></item>
    /// <item><term>MemoryCorruptionCostMultiplier</term><description><c>MemoryManager.Corrupt</c>.</description></item>
    /// </list>
    ///
    /// **Direction lives in the value, not in the reader.** A skill that should make
    /// something smaller carries a negative <see cref="SkillDefinition.EffectValue"/>,
    /// and every multiplier call site applies <c>1 + bonus</c> through
    /// <see cref="SkillTreeManager.Scale"/>. The single exception is
    /// <see cref="BlockReductionBonus"/>, which is named for the reduction it grants and
    /// so reads as <c>1 - bonus</c>; its own call site says so.
    /// </summary>
    public enum SkillEffectType
    {
        AttackDamageMultiplier,
        ComboWindowBonusSeconds,
        ParryWindowMultiplier,
        MaxHealthBonus,
        BlockReductionBonus,
        IncomingDamageMultiplier,
        AbilityDashSpeedMultiplier,
        AbilityCooldownMultiplier,
        MaxDivineEnergyBonus,
        MemoryDetectionRangeMultiplier,
        EmberStepForgetRestoreMultiplier,
        MemoryCorruptionCostMultiplier
    }

    /// <summary>
    /// One node in the skill tree (SPEC.md section 30), as data. <see cref="EffectValue"/>
    /// is interpreted by whatever reads <see cref="SkillEffectType"/> — a "Multiplier"
    /// type reads it as a bonus fraction added to a base of 1, a "Bonus" type as a flat
    /// addition, matching each call site's own doc comment.
    /// </summary>
    [CreateAssetMenu(fileName = "Skill_", menuName = "God Game/Skill Definition")]
    public class SkillDefinition : ScriptableObject
    {
        [SerializeField] private string skillId;
        [SerializeField] private string displayName = "Untitled Skill";

        [TextArea(2, 4)]
        [SerializeField] private string description;

        [SerializeField] private SkillBranch branch;

        [Tooltip("Skill points required to unlock this node.")]
        [Min(0)]
        [SerializeField] private int cost = 1;

        [Tooltip("Optional. Another skill's id that must already be unlocked.")]
        [SerializeField] private string prerequisiteId;

        [SerializeField] private SkillEffectType effectType;
        [SerializeField] private float effectValue;

        public string SkillId => string.IsNullOrEmpty(skillId) ? name : skillId;
        public string DisplayName => displayName;
        public string Description => description;
        public SkillBranch Branch => branch;
        public int Cost => cost;
        public string PrerequisiteId => prerequisiteId;
        public SkillEffectType EffectType => effectType;
        public float EffectValue => effectValue;

        /// <summary>Test and tooling seam for authoring without the Inspector.</summary>
        public void Configure(string id, string label, SkillBranch skillBranch, int skillCost,
            SkillEffectType type, float value, string prerequisite = null)
        {
            skillId = id;
            displayName = label;
            branch = skillBranch;
            cost = skillCost;
            effectType = type;
            effectValue = value;
            prerequisiteId = prerequisite;
        }
    }
}

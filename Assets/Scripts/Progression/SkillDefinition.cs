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
    /// Only <see cref="AttackDamageMultiplier"/>, <see cref="MaxHealthBonus"/>,
    /// <see cref="MaxDivineEnergyBonus"/> and <see cref="MemoryCorruptionCostMultiplier"/>
    /// are read by a system today — one per branch, proving the mechanism end to end.
    /// The rest exist so the tree has all twelve of section 30's named upgrades and can
    /// be unlocked, persisted and shown correctly; see KNOWN_ISSUES.md for wiring the
    /// remaining eight into their systems.
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

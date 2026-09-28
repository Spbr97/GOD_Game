using Game.Core;
using UnityEngine;

namespace Game.Combat.Abilities
{
    /// <summary>
    /// One divine ability, as data (SPEC.md sections 8 and 16, TASK 043).
    ///
    /// The seven temples each grant one. Before this existed, Ember Step was six
    /// serialized fields on <see cref="CombatController"/> and a hard-coded flag name —
    /// workable for one ability, and the wrong shape for seven, because each new one
    /// would have arrived as another six fields and another copy of the same
    /// unlock/cost/cooldown checks, each free to drift from the others.
    ///
    /// What an ability *does* is a <see cref="IDivineAbilityEffect"/> component. What it
    /// costs, when it is available and how it is remembered is this asset plus
    /// <see cref="DivineAbilityController"/>, and that half is written once.
    /// </summary>
    [CreateAssetMenu(fileName = "Ability_", menuName = "God Game/Divine Ability")]
    public class DivineAbilityDefinition : ScriptableObject
    {
        [Tooltip("Stable id. Matched to the IDivineAbilityEffect that performs it, and used in the save.")]
        [SerializeField] private string abilityId = "ABILITY_UNNAMED";

        [SerializeField] private string displayName = "Unnamed Ability";

        [TextArea(2, 4)]
        [SerializeField] private string description;

        [Tooltip("Which temple grants it. Narrative only; nothing reads this to gate anything.")]
        [SerializeField] private string grantedBy;

        [Header("Unlock")]
        [Tooltip("Must be unlocked before it can be used at all. Off for an ability the player starts with.")]
        [SerializeField] private bool requiresUnlock = true;

        [Tooltip("Optional. The world flag that unlocks it. Blank means ABILITY_UNLOCKED_<id>.")]
        [SerializeField] private string unlockFlag;

        [Header("Cost and cooldown")]
        [Tooltip("Divine energy spent per use. Abilities cost energy, not stamina — that is what separates them from a dodge.")]
        [Min(0f)]
        [SerializeField] private float energyCost = 20f;

        [Tooltip("Seconds before it can be used again, before the Divine branch's cooldown skill.")]
        [Min(0f)]
        [SerializeField] private float cooldownSeconds = 3f;

        [Tooltip("How long the effect runs. The effect itself decides what happens during it.")]
        [Min(0f)]
        [SerializeField] private float durationSeconds = 0.25f;

        public string AbilityId => string.IsNullOrEmpty(abilityId) ? name : abilityId;

        public string DisplayName => displayName;

        public string Description => description;

        public string GrantedBy => grantedBy;

        public bool RequiresUnlock => requiresUnlock;

        /// <summary>
        /// The world flag that unlocks this. Defaults to <c>ABILITY_UNLOCKED_&lt;id&gt;</c>,
        /// which is the name <c>QuestReward.AbilityUnlock</c> already writes — so granting
        /// an ability as a quest reward needs no extra wiring, and an author who leaves
        /// this blank gets the working default rather than a silent never-unlocks.
        /// </summary>
        public string UnlockFlag =>
            string.IsNullOrEmpty(unlockFlag) ? "ABILITY_UNLOCKED_" + AbilityId : unlockFlag;

        public float EnergyCost => energyCost;

        public float DurationSeconds => durationSeconds;

        /// <summary>The cooldown before skills. <see cref="ScaledCooldown"/> is what the game uses.</summary>
        public float BaseCooldownSeconds => cooldownSeconds;

        /// <summary>
        /// The cooldown after the Divine branch's cooldown skill (SPEC.md section 30).
        /// <c>DIVINE_COOLDOWN</c> carries -0.2, so one point makes every ability 20%
        /// faster to come back — one rule for all seven rather than per-ability wiring.
        /// </summary>
        public float ScaledCooldown => cooldownSeconds
            * Game.Progression.SkillTreeManager.Scale(
                Game.Progression.SkillEffectType.AbilityCooldownMultiplier);

        /// <summary>True when the world says the player has been granted this.</summary>
        public bool IsUnlocked =>
            !requiresUnlock || (WorldState.Instance != null && WorldState.Instance.GetFlag(UnlockFlag));

        /// <summary>Test and tooling seam.</summary>
        public void Configure(string id, float cost, float cooldown, float duration = 0.25f,
            bool needsUnlock = true, string flag = null, string label = null)
        {
            abilityId = id;
            energyCost = cost;
            cooldownSeconds = cooldown;
            durationSeconds = duration;
            requiresUnlock = needsUnlock;
            unlockFlag = flag;
            displayName = label ?? id;
        }
    }
}

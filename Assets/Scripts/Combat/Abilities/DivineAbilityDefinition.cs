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

        [Header("Memory cost (SPEC.md section 20)")]
        [Tooltip("Whether using this ability costs memory at all. Off by default: only designated abilities pay one (TASK 040).")]
        [SerializeField] private bool costsMemory;

        [Tooltip("Overall memory integrity spent every use. Only read when Costs Memory is on.")]
        [Min(0f)]
        [SerializeField] private float memoryIntegrityCostPerUse = 0.02f;

        [Tooltip("Every this many uses, one Optional memory the player knows is temporarily forgotten. Zero disables it.")]
        [Min(0)]
        [SerializeField] private int usesPerForgottenMemory = 3;

        public string AbilityId => string.IsNullOrEmpty(abilityId) ? name : abilityId;


        [Tooltip("Optional key into the shared string table, e.g. \"ability.ember_step\". Blank means this asset is not localized and shows the text typed above. See LocalizedContent.")]
        [SerializeField] private string localizationKey;

        /// <summary>The key this asset's text is translated under, or empty when it is not localized.</summary>
        public string LocalizationKey => localizationKey;

        /// <summary>Test and tooling seam: points this asset's text at a string-table key.</summary>
        public void ConfigureLocalizationKey(string key) => localizationKey = key;

        public string DisplayName => Game.Core.Localization.LocalizedContent.Text(localizationKey, "name", displayName);

        public string Description => Game.Core.Localization.LocalizedContent.Text(localizationKey, "description", description);

        /// <summary>The text an author typed, untranslated. For the Inspector and for validation.</summary>
        public string AuthoredDisplayName => displayName;

        public string AuthoredDescription => description;

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

        /// <summary>
        /// Whether this ability costs the player memory to use.
        ///
        /// **Off by default, and deliberately per-ability** (TASK 040 decision). SPEC.md
        /// section 20 gives Ember Step a memory cost, and the tempting generalisation was
        /// to charge every <see cref="DivineAbilityUsedEvent"/> the same way — one rule,
        /// no per-ability wiring. That was rejected: a cost every ability pays is a tax,
        /// and a tax is not a characterisation. Ember Step burning memory is a statement
        /// about fire and about what the player is trading away, and it stops meaning
        /// anything if the water temple's ability does it too.
        ///
        /// So each ability states its own cost, and most will state none.
        /// </summary>
        public bool CostsMemory => costsMemory;

        /// <summary>Integrity spent per use, or zero when this ability costs no memory.</summary>
        public float MemoryIntegrityCostPerUse => costsMemory ? memoryIntegrityCostPerUse : 0f;

        /// <summary>Uses per temporarily forgotten memory, or zero when this ability costs no memory.</summary>
        public int UsesPerForgottenMemory => costsMemory ? usesPerForgottenMemory : 0;

        /// <summary>Test and tooling seam for the memory cost.</summary>
        public void ConfigureMemoryCost(bool costs, float integrityPerUse = 0.02f, int usesPerForget = 3)
        {
            costsMemory = costs;
            memoryIntegrityCostPerUse = integrityPerUse;
            usesPerForgottenMemory = usesPerForget;
        }

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

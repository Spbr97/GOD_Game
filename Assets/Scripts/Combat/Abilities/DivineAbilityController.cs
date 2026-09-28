using System.Collections.Generic;
using Game.Core;
using Game.Save;
using UnityEngine;

namespace Game.Combat.Abilities
{
    /// <summary>
    /// The one place a divine ability is unlocked, paid for, put on cooldown and
    /// remembered (SPEC.md sections 8, 16 and 31; TASK 043).
    ///
    /// This is the contract TASK 045–050 build on. A temple's ability is a
    /// <see cref="DivineAbilityDefinition"/> asset and a component implementing
    /// <see cref="IDivineAbilityEffect"/>; everything else — the unlock flag, the energy
    /// cost, the cooldown and the skill that shortens it, the event other systems react
    /// to, the save — happens here and is written once.
    ///
    /// **Order matters and is fixed.** Unlocked, then the effect's own readiness, then
    /// cooldown, then cost. Cost last so a refusal for any other reason never charges the
    /// player, which is the bug this ordering exists to make impossible.
    ///
    /// It is an <see cref="ISaveParticipant"/> rather than a field on <c>SaveData</c>:
    /// use counts and remaining cooldowns are this system's business, the save shape does
    /// not have to grow a field per ability, and a temple added later needs no save
    /// migration.
    /// </summary>
    [DisallowMultipleComponent]
    public class DivineAbilityController : MonoBehaviour, ISaveParticipant
    {
        [Tooltip("Every ability this character can ever use. Being listed here does not mean it is unlocked.")]
        [SerializeField] private List<DivineAbilityDefinition> abilities = new();

        [Tooltip("Where uses are paid from. Found on this GameObject if left unset.")]
        [SerializeField] private DivineEnergyComponent divineEnergy;

        private readonly Dictionary<string, float> readyAt = new();
        private readonly Dictionary<string, int> useCounts = new();
        private readonly List<IDivineAbilityEffect> effects = new();

        public string SaveKey => "DIVINE_ABILITIES";

        /// <summary>Every ability registered here, unlocked or not.</summary>
        public IReadOnlyList<DivineAbilityDefinition> Abilities => abilities;

        private void Awake()
        {
            if (divineEnergy == null)
            {
                divineEnergy = GetComponent<DivineEnergyComponent>();
            }

            RefreshEffects();
        }

        /// <summary>
        /// Re-scans this GameObject and its children for ability effects. Called in Awake
        /// and exposed because an effect can be added at runtime — a temple reward that
        /// attaches its component when the boss dies.
        /// </summary>
        public void RefreshEffects()
        {
            effects.Clear();

            var behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IDivineAbilityEffect effect && !string.IsNullOrEmpty(effect.AbilityId))
                {
                    effects.Add(effect);
                }
            }
        }

        /// <summary>Adds an ability at runtime, or replaces the definition of one already registered.</summary>
        public void Register(DivineAbilityDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            for (var i = 0; i < abilities.Count; i++)
            {
                if (abilities[i] != null && abilities[i].AbilityId == definition.AbilityId)
                {
                    abilities[i] = definition;
                    return;
                }
            }

            abilities.Add(definition);
        }

        public DivineAbilityDefinition Find(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId))
            {
                return null;
            }

            for (var i = 0; i < abilities.Count; i++)
            {
                if (abilities[i] != null && abilities[i].AbilityId == abilityId)
                {
                    return abilities[i];
                }
            }

            return null;
        }

        /// <summary>Seconds until this can be used again; zero when it is ready now.</summary>
        public float CooldownRemaining(string abilityId) =>
            readyAt.TryGetValue(abilityId, out var at) ? Mathf.Max(0f, at - Time.time) : 0f;

        /// <summary>How many times this has been used this run. SPEC.md section 20's repeated-use costs read this.</summary>
        public int UseCount(string abilityId) => useCounts.TryGetValue(abilityId, out var count) ? count : 0;

        /// <summary>
        /// Whether a use would be accepted right now, and why not if it would not. Used by
        /// the HUD to grey a button, and by <see cref="TryUse"/> itself, so the two can
        /// never disagree about what is allowed.
        /// </summary>
        public bool CanUse(string abilityId, out DivineAbilityRefusal refusal)
        {
            refusal = DivineAbilityRefusal.Unknown;

            var definition = Find(abilityId);
            if (definition == null)
            {
                return false;
            }

            if (!definition.IsUnlocked)
            {
                refusal = DivineAbilityRefusal.Locked;
                return false;
            }

            var effect = FindEffect(abilityId);
            if (effect == null)
            {
                refusal = DivineAbilityRefusal.NoEffect;
                return false;
            }

            if (!effect.CanPerform)
            {
                refusal = DivineAbilityRefusal.Busy;
                return false;
            }

            if (CooldownRemaining(abilityId) > 0f)
            {
                refusal = DivineAbilityRefusal.OnCooldown;
                return false;
            }

            if (divineEnergy != null && divineEnergy.CurrentEnergy < definition.EnergyCost)
            {
                refusal = DivineAbilityRefusal.NotEnoughEnergy;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Uses an ability. Returns false, having published a
        /// <see cref="DivineAbilityRefusedEvent"/>, when it could not — and in that case
        /// nothing has been spent and no cooldown has started.
        /// </summary>
        public bool TryUse(string abilityId)
        {
            if (!CanUse(abilityId, out var refusal))
            {
                EventBus.Publish(new DivineAbilityRefusedEvent(abilityId, refusal));
                return false;
            }

            var definition = Find(abilityId);

            // Checked by CanUse, but taken here: between the check and the spend nothing
            // runs, and TrySpend is still the one thing that may say no.
            if (divineEnergy != null && !divineEnergy.TrySpend(definition.EnergyCost))
            {
                EventBus.Publish(new DivineAbilityRefusedEvent(abilityId, DivineAbilityRefusal.NotEnoughEnergy));
                return false;
            }

            readyAt[abilityId] = Time.time + definition.ScaledCooldown;
            useCounts[abilityId] = UseCount(abilityId) + 1;

            FindEffect(abilityId).Perform(definition);

            GameLogger.Log(LogCategory.Combat,
                $"{name} used '{abilityId}' (use #{useCounts[abilityId]}).", this);
            EventBus.Publish(new DivineAbilityUsedEvent(gameObject, abilityId, useCounts[abilityId]));
            return true;
        }

        private IDivineAbilityEffect FindEffect(string abilityId)
        {
            for (var i = 0; i < effects.Count; i++)
            {
                // Destroyed components linger in the list as null-equal Unity objects.
                if (effects[i] is MonoBehaviour behaviour && behaviour == null)
                {
                    continue;
                }

                if (effects[i].AbilityId == abilityId)
                {
                    return effects[i];
                }
            }

            return null;
        }

        // ---------------------------------------------------------------------- saving

        [System.Serializable]
        private class Payload
        {
            public List<string> Ids = new();
            public List<int> Uses = new();

            /// <summary>Seconds still to wait, not an absolute time: <c>Time.time</c> restarts with the scene.</summary>
            public List<float> CooldownsRemaining = new();
        }

        public string CaptureJson()
        {
            var payload = new Payload();

            foreach (var pair in useCounts)
            {
                payload.Ids.Add(pair.Key);
                payload.Uses.Add(pair.Value);
                payload.CooldownsRemaining.Add(CooldownRemaining(pair.Key));
            }

            return JsonUtility.ToJson(payload);
        }

        public void RestoreJson(string json)
        {
            useCounts.Clear();
            readyAt.Clear();

            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            var payload = JsonUtility.FromJson<Payload>(json);
            if (payload?.Ids == null)
            {
                return;
            }

            for (var i = 0; i < payload.Ids.Count; i++)
            {
                var id = payload.Ids[i];
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                if (i < payload.Uses.Count)
                {
                    useCounts[id] = payload.Uses[i];
                }

                if (i < payload.CooldownsRemaining.Count && payload.CooldownsRemaining[i] > 0f)
                {
                    readyAt[id] = Time.time + payload.CooldownsRemaining[i];
                }
            }
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(DivineEnergyComponent energy, params DivineAbilityDefinition[] definitions)
        {
            divineEnergy = energy != null ? energy : GetComponent<DivineEnergyComponent>();
            abilities = new List<DivineAbilityDefinition>(definitions ?? System.Array.Empty<DivineAbilityDefinition>());
            readyAt.Clear();
            useCounts.Clear();
            RefreshEffects();
        }
    }
}

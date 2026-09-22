using Game.Combat;
using Game.Core;
using Game.Progression;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Applies the skill tree's stat bonuses (SPEC.md section 30's Guardian and Divine
    /// branches, TASK 016) to this player's <see cref="HealthComponent"/> and
    /// <see cref="DivineEnergyComponent"/>.
    ///
    /// Deliberately not logic those components own themselves: <see cref="HealthComponent"/>
    /// and <see cref="DivineEnergyComponent"/> predate the skill tree and are shared
    /// with every enemy, which has no skills. Recomputes from the base captured at
    /// <see cref="Awake"/> every time, rather than adding deltas incrementally, so
    /// applying it twice (a live unlock, then a save's restore replaying the same
    /// unlocked set) can never double-count.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public class PlayerProgressionStats : MonoBehaviour
    {
        private HealthComponent health;
        private DivineEnergyComponent divineEnergy;
        private float baseMaxHealth;
        private float baseMaxDivineEnergy;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            divineEnergy = GetComponent<DivineEnergyComponent>();

            // Captured before anything has had a chance to raise the cap, so recomputing
            // later is always "base + current bonus", never "already-boosted + bonus".
            baseMaxHealth = health.MaxHealth;
            baseMaxDivineEnergy = divineEnergy != null ? divineEnergy.MaxEnergy : 0f;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<SkillBonusesChangedEvent>(OnBonusesChanged);
            ApplyAll();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SkillBonusesChangedEvent>(OnBonusesChanged);
        }

        private void OnBonusesChanged(SkillBonusesChangedEvent changed) => ApplyAll();

        private void ApplyAll()
        {
            var skills = SkillTreeManager.Instance;
            var healthBonus = skills != null ? skills.GetBonus(SkillEffectType.MaxHealthBonus) : 0f;
            health.SetMaxHealth(baseMaxHealth + healthBonus);

            if (divineEnergy != null)
            {
                var energyBonus = skills != null ? skills.GetBonus(SkillEffectType.MaxDivineEnergyBonus) : 0f;
                divineEnergy.SetMaxEnergy(baseMaxDivineEnergy + energyBonus);
            }
        }

        /// <summary>Test and tooling seam: sets the bases <see cref="Awake"/> would otherwise have captured.</summary>
        public void Configure(HealthComponent healthComponent, DivineEnergyComponent divineEnergyComponent)
        {
            health = healthComponent;
            divineEnergy = divineEnergyComponent;
            baseMaxHealth = health.MaxHealth;
            baseMaxDivineEnergy = divineEnergy != null ? divineEnergy.MaxEnergy : 0f;
        }
    }
}

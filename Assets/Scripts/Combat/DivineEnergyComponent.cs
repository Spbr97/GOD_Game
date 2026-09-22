using System;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The resource divine abilities will spend (SPEC.md sections 13 and 15). It
    /// exists now because a perfect parry has to restore "a small amount of divine
    /// energy" and there was nothing to restore. No ability spends it yet; that
    /// arrives with the first temple (SPEC.md section 60, Phase 3).
    ///
    /// Starts empty rather than full: it is earned through play, not given.
    /// </summary>
    public class DivineEnergyComponent : MonoBehaviour
    {
        [SerializeField] private float maxEnergy = 100f;

        public float MaxEnergy => maxEnergy;
        public float CurrentEnergy { get; private set; }
        public float EnergyFraction => maxEnergy > 0f ? CurrentEnergy / maxEnergy : 0f;

        /// <summary>Raised with the new value after any change.</summary>
        public event Action<float> Changed;

        public void Gain(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            CurrentEnergy = Mathf.Min(maxEnergy, CurrentEnergy + amount);
            Changed?.Invoke(CurrentEnergy);
        }

        public bool HasEnergy(float amount)
        {
            return CurrentEnergy >= amount;
        }

        /// <summary>Spends if affordable; otherwise spends nothing and returns false.</summary>
        public bool TrySpend(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (CurrentEnergy < amount)
            {
                return false;
            }

            CurrentEnergy -= amount;
            Changed?.Invoke(CurrentEnergy);
            return true;
        }

        /// <summary>Puts energy at an exact value, for restoring a save.</summary>
        public void RestoreTo(float value)
        {
            CurrentEnergy = Mathf.Clamp(value, 0f, maxEnergy);
            Changed?.Invoke(CurrentEnergy);
        }

        /// <summary>Editor/test seam.</summary>
        public void Configure(float newMax, float startingEnergy = 0f)
        {
            maxEnergy = Mathf.Max(1f, newMax);
            CurrentEnergy = Mathf.Clamp(startingEnergy, 0f, maxEnergy);
        }

        /// <summary>
        /// Recomputes max energy to an exact value (TASK 016's Divine skill bonus).
        /// Never raises current energy — see <see cref="HealthComponent.SetMaxHealth"/>
        /// for why a cap change is not also a free top-up, and why calling this
        /// repeatedly with the same total is a safe no-op.
        /// </summary>
        public void SetMaxEnergy(float newMax)
        {
            maxEnergy = Mathf.Max(1f, newMax);
            CurrentEnergy = Mathf.Min(CurrentEnergy, maxEnergy);
        }
    }
}

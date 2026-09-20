using System;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Stamina pool gating attacks and dodges (SPEC.md sections 13 and 15, TASK 002).
    /// Regeneration pauses for <see cref="regenDelay"/> after every spend so trading
    /// stamina has a real cost.
    /// </summary>
    public class StaminaComponent : MonoBehaviour
    {
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float regenPerSecond = 25f;
        [SerializeField] private float regenDelay = 0.6f;

        private float regenBlockedUntil;

        public float MaxStamina => maxStamina;
        public float CurrentStamina { get; private set; }
        public float StaminaFraction => maxStamina > 0f ? CurrentStamina / maxStamina : 0f;

        public event Action<float> Spent;

        private void Awake()
        {
            CurrentStamina = maxStamina;
        }

        private void Update()
        {
            Regenerate(Time.deltaTime, Time.time);
        }

        public bool HasStamina(float amount)
        {
            return CurrentStamina >= amount;
        }

        /// <summary>
        /// Spends stamina if there is enough. Returns false and spends nothing
        /// otherwise, so the caller can reject the action outright rather than
        /// starting an attack it cannot pay for.
        /// </summary>
        public bool TrySpend(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (CurrentStamina < amount)
            {
                return false;
            }

            CurrentStamina -= amount;
            regenBlockedUntil = Time.time + regenDelay;
            Spent?.Invoke(amount);
            return true;
        }

        public void Restore(float amount)
        {
            CurrentStamina = Mathf.Clamp(CurrentStamina + amount, 0f, maxStamina);
        }

        public void ResetStamina()
        {
            CurrentStamina = maxStamina;
            regenBlockedUntil = 0f;
        }

        /// <summary>Puts stamina at an exact value, for restoring a save. See HealthComponent.RestoreTo.</summary>
        public void RestoreTo(float value)
        {
            CurrentStamina = Mathf.Clamp(value, 0f, maxStamina);
            regenBlockedUntil = 0f;
        }

        /// <summary>Time is passed in rather than read, so tests can step it deterministically.</summary>
        public void Regenerate(float deltaTime, float now)
        {
            if (now < regenBlockedUntil || CurrentStamina >= maxStamina)
            {
                return;
            }

            CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + regenPerSecond * deltaTime);
        }

        /// <summary>Editor/test seam.</summary>
        public void Configure(float newMax, float newRegenPerSecond, float newRegenDelay)
        {
            maxStamina = Mathf.Max(1f, newMax);
            regenPerSecond = newRegenPerSecond;
            regenDelay = newRegenDelay;
            CurrentStamina = maxStamina;
        }
    }
}

using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Health pool shared by the player and every enemy (SPEC.md TASK 002).
    ///
    /// Damage is funnelled through <see cref="TakeDamage"/>, which is the single
    /// place that enforces the "no duplicate damage events" rule: a swing's
    /// <see cref="DamageData.AttackId"/> is remembered and a repeat of the same id
    /// is rejected. Callers can therefore fire optimistically from several
    /// colliders without coordinating.
    /// </summary>
    public class HealthComponent : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private bool destroyOnDeath;

        /// <summary>How many recent attack ids are remembered for deduplication.</summary>
        private const int AttackHistorySize = 16;

        private readonly HashSet<int> recentAttackIds = new();
        private readonly Queue<int> attackIdOrder = new();

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }

        /// <summary>Normalized 0..1, for health bars. Zero max health reads as 0.</summary>
        public float HealthFraction => maxHealth > 0f ? CurrentHealth / maxHealth : 0f;

        /// <summary>Set by dodge i-frames and by death, so nothing lands on a corpse.</summary>
        public bool IsInvulnerable { get; set; }

        /// <summary>Raised after health has already been reduced.</summary>
        public event Action<DamageData> Damaged;

        /// <summary>Raised once, on the transition to dead.</summary>
        public event Action<DamageData> Died;

        public event Action<float> Healed;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        /// <summary>
        /// Applies damage. Returns true only when health actually changed, so callers
        /// can decide whether to play a hit reaction.
        /// </summary>
        public bool TakeDamage(DamageData damage)
        {
            if (IsDead || IsInvulnerable)
            {
                return false;
            }

            if (damage.Amount <= 0f)
            {
                return false;
            }

            if (damage.Source == gameObject)
            {
                return false;
            }

            if (!RegisterAttack(damage.AttackId))
            {
                return false;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage.Amount);
            GameLogger.Log(LogCategory.Combat, $"{name} took {damage.Amount:0.#} {damage.Type} damage ({CurrentHealth:0.#}/{maxHealth:0.#}).", this);

            Damaged?.Invoke(damage);
            EventBus.Publish(new DamageAppliedEvent(gameObject, damage, CurrentHealth));

            if (CurrentHealth <= 0f)
            {
                Kill(damage);
            }

            return true;
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            Healed?.Invoke(amount);
        }

        /// <summary>Kills outright, bypassing the invulnerability check (scripted deaths, void planes).</summary>
        public void Kill(DamageData killingBlow)
        {
            if (IsDead)
            {
                return;
            }

            IsDead = true;
            CurrentHealth = 0f;
            IsInvulnerable = true;

            GameLogger.Log(LogCategory.Combat, $"{name} died.", this);
            Died?.Invoke(killingBlow);
            EventBus.Publish(new EntityDiedEvent(gameObject, killingBlow));

            if (destroyOnDeath)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Restores to full and clears the dead flag. Used by checkpoint respawn.
        /// Attack history is cleared too, so a respawned entity is not immune to an
        /// attack id it happened to see before dying.
        /// </summary>
        public void ResetHealth()
        {
            CurrentHealth = maxHealth;
            IsDead = false;
            IsInvulnerable = false;
            recentAttackIds.Clear();
            attackIdOrder.Clear();
        }

        /// <summary>
        /// Puts health at an exact value without going through damage or healing, for
        /// restoring a save. Deliberately separate from <see cref="TakeDamage"/>: a load
        /// is not an injury, and routing it through damage would publish hit events and
        /// could trigger the death path on the frame the game comes back.
        /// </summary>
        public void RestoreTo(float value, bool dead = false)
        {
            CurrentHealth = Mathf.Clamp(value, 0f, maxHealth);
            IsDead = dead;
            IsInvulnerable = dead;
            recentAttackIds.Clear();
            attackIdOrder.Clear();
        }

        /// <summary>Editor/test seam for setting up an entity before Awake would run.</summary>
        public void Configure(float newMaxHealth)
        {
            maxHealth = Mathf.Max(1f, newMaxHealth);
            CurrentHealth = maxHealth;
        }

        /// <summary>
        /// Returns false when this attack id has already been applied. Ids of 0 are
        /// treated as "untracked" and always allowed, for damage sources that do not
        /// come from a swing (fall damage, hazards).
        /// </summary>
        private bool RegisterAttack(int attackId)
        {
            if (attackId == 0)
            {
                return true;
            }

            if (!recentAttackIds.Add(attackId))
            {
                return false;
            }

            attackIdOrder.Enqueue(attackId);
            if (attackIdOrder.Count > AttackHistorySize)
            {
                recentAttackIds.Remove(attackIdOrder.Dequeue());
            }

            return true;
        }
    }
}

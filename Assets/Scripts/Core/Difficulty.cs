using UnityEngine;

namespace Game.Core
{
    /// <summary>The four modes from SPEC.md section 44.</summary>
    public enum DifficultyMode
    {
        Story,
        Normal,
        Warrior,
        Mythic
    }

    /// <summary>
    /// What a difficulty mode actually changes. SPEC.md sections 15 and 44 are
    /// explicit that difficulty must move behaviour and timing windows rather than
    /// simply multiply enemy health, so there is deliberately no health multiplier
    /// in here.
    /// </summary>
    public readonly struct DifficultyModifiers
    {
        /// <summary>Scales damage enemies deal to the player.</summary>
        public readonly float EnemyDamage;

        /// <summary>
        /// Scales the telegraph (windup) of an enemy attack. Above 1 means a longer,
        /// more readable wind-up; below 1 means less time to react.
        /// </summary>
        public readonly float EnemyTelegraph;

        /// <summary>Scales the pause between enemy attacks. Below 1 means more pressure.</summary>
        public readonly float EnemyAttackCooldown;

        /// <summary>
        /// Scales how many enemies in a group may attack at once, via
        /// <see cref="Game.AI.EnemyGroup"/>. Below 1 rounds down to at least one.
        /// </summary>
        public readonly float GroupAggression;

        /// <summary>Scales player-side reaction windows (dodge i-frames, and parry once it exists).</summary>
        public readonly float PlayerTimingWindow;

        public DifficultyModifiers(float enemyDamage, float enemyTelegraph, float enemyAttackCooldown,
            float groupAggression, float playerTimingWindow)
        {
            EnemyDamage = enemyDamage;
            EnemyTelegraph = enemyTelegraph;
            EnemyAttackCooldown = enemyAttackCooldown;
            GroupAggression = groupAggression;
            PlayerTimingWindow = playerTimingWindow;
        }
    }

    /// <summary>
    /// The current difficulty, readable from anywhere without scene wiring.
    ///
    /// This is static rather than a manager component because every enemy reads it
    /// every time it attacks, and an enemy that spawns before a manager's Awake must
    /// still get a correct answer. <see cref="SettingsManager"/> owns persisting it.
    /// </summary>
    public static class Difficulty
    {
        private static DifficultyMode current = DifficultyMode.Normal;

        public static DifficultyMode Current => current;

        public static DifficultyModifiers Modifiers { get; private set; } = For(DifficultyMode.Normal);

        public static void Set(DifficultyMode mode)
        {
            if (current == mode)
            {
                return;
            }

            current = mode;
            Modifiers = For(mode);
            GameLogger.Log(LogCategory.Game, $"Difficulty set to {mode}.");
        }

        /// <summary>Restores the default. Tests call this so one test cannot leak into the next.</summary>
        public static void Reset()
        {
            current = DifficultyMode.Normal;
            Modifiers = For(DifficultyMode.Normal);
        }

        public static DifficultyModifiers For(DifficultyMode mode)
        {
            return mode switch
            {
                //                                    damage  telegraph  cooldown  group  player window
                DifficultyMode.Story => new DifficultyModifiers(0.6f, 1.35f, 1.4f, 0.5f, 1.3f),
                DifficultyMode.Warrior => new DifficultyModifiers(1.3f, 0.8f, 0.8f, 1.5f, 0.85f),
                DifficultyMode.Mythic => new DifficultyModifiers(1.6f, 0.65f, 0.6f, 2f, 0.7f),
                _ => new DifficultyModifiers(1f, 1f, 1f, 1f, 1f)
            };
        }

        /// <summary>
        /// Applies the group-aggression modifier to a base simultaneous-attacker count.
        /// Always leaves at least one attacker, or an encounter on Story would stall
        /// with nobody permitted to swing.
        /// </summary>
        public static int ScaleAttackerCount(int baseCount)
        {
            return Mathf.Max(1, Mathf.RoundToInt(baseCount * Modifiers.GroupAggression));
        }
    }
}

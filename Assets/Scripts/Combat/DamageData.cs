using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Who an attack is willing to hurt. A <see cref="Hitbox"/> refuses a
    /// <see cref="Hurtbox"/> of its own faction, which is what stops two enemies
    /// swinging at the same player from killing each other.
    ///
    /// <see cref="Neutral"/> is the default and opts out of the check entirely: it
    /// hits, and is hit by, everything. Anything that has not been given a faction
    /// therefore behaves exactly as it did before factions existed.
    /// </summary>
    public enum Faction
    {
        Neutral,
        Player,
        Hostile
    }

    public enum DamageType
    {
        Physical,
        Divine,
        Elemental,
        Environmental
    }

    /// <summary>
    /// One damage application (SPEC.md TASK 002). Passed by value so a hitbox can
    /// build a template once per swing and hand copies to every hurtbox it touches.
    ///
    /// <see cref="AttackId"/> is the deduplication key: every swing gets a fresh id
    /// from <see cref="NextAttackId"/>, and a <see cref="HealthComponent"/> refuses
    /// an id it has already processed. That is what keeps a single swing from
    /// damaging an enemy twice when it has several overlapping hurtbox colliders.
    /// </summary>
    public struct DamageData
    {
        public float Amount;
        public DamageType Type;

        /// <summary>The attacking entity's root object. Used to avoid self-damage.</summary>
        public GameObject Source;

        public Vector3 HitPoint;

        /// <summary>Normalized attacker-to-victim direction, for knockback and hit reactions.</summary>
        public Vector3 Direction;

        /// <summary>Unique per swing. See the class summary.</summary>
        public int AttackId;

        /// <summary>Bypasses blocking once blocking exists; ignored for now.</summary>
        public bool Unblockable;

        private static int attackIdCounter;

        /// <summary>Allocates the next swing id. Never returns 0, which means "no id".</summary>
        public static int NextAttackId()
        {
            attackIdCounter++;
            if (attackIdCounter == 0)
            {
                attackIdCounter = 1;
            }

            return attackIdCounter;
        }

        public static DamageData Create(float amount, GameObject source, DamageType type = DamageType.Physical)
        {
            return new DamageData
            {
                Amount = amount,
                Type = type,
                Source = source,
                HitPoint = source != null ? source.transform.position : Vector3.zero,
                Direction = source != null ? source.transform.forward : Vector3.forward,
                AttackId = NextAttackId(),
                Unblockable = false
            };
        }
    }
}

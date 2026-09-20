using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Combat payloads published on <see cref="Game.Core.EventBus"/> so UI, audio and
    /// VFX can react without referencing combat components directly (SPEC.md section 49).
    /// </summary>
    public readonly struct DamageAppliedEvent
    {
        public readonly GameObject Victim;
        public readonly DamageData Damage;
        public readonly float RemainingHealth;

        public DamageAppliedEvent(GameObject victim, DamageData damage, float remainingHealth)
        {
            Victim = victim;
            Damage = damage;
            RemainingHealth = remainingHealth;
        }
    }

    public readonly struct EntityDiedEvent
    {
        public readonly GameObject Entity;
        public readonly DamageData KillingBlow;

        public EntityDiedEvent(GameObject entity, DamageData killingBlow)
        {
            Entity = entity;
            KillingBlow = killingBlow;
        }
    }

    public readonly struct PlayerDiedEvent
    {
        public readonly GameObject Player;

        public PlayerDiedEvent(GameObject player)
        {
            Player = player;
        }
    }

    public readonly struct PlayerRespawnedEvent
    {
        public readonly GameObject Player;
        public readonly Vector3 Position;

        public PlayerRespawnedEvent(GameObject player, Vector3 position)
        {
            Player = player;
            Position = position;
        }
    }

    public readonly struct CheckpointActivatedEvent
    {
        public readonly Checkpoint Checkpoint;

        public CheckpointActivatedEvent(Checkpoint checkpoint)
        {
            Checkpoint = checkpoint;
        }
    }
}

namespace Game.Combat
{
    /// <summary>Raised when the player parries (SPEC.md section 15). <see cref="Game.AI.EnemyStagger"/> listens and staggers the attacker.</summary>
    public readonly struct ParryEvent
    {
        public readonly GameObject Defender;
        public readonly GameObject Attacker;
        public readonly bool Perfect;

        /// <summary>How long the attacker should reel. Zero means "use the attacker's own stagger duration".</summary>
        public readonly float StaggerDuration;

        public ParryEvent(GameObject defender, GameObject attacker, bool perfect, float staggerDuration = 0f)
        {
            Defender = defender;
            Attacker = attacker;
            Perfect = perfect;
            StaggerDuration = staggerDuration;
        }
    }

    /// <summary>Raised when a block absorbs a hit.</summary>
    public readonly struct AttackBlockedEvent
    {
        public readonly GameObject Defender;
        public readonly DamageData Damage;

        public AttackBlockedEvent(GameObject defender, DamageData damage)
        {
            Defender = defender;
            Damage = damage;
        }
    }

    /// <summary>Raised when a block fails: out of stamina, or hit by an unblockable attack.</summary>
    public readonly struct GuardBrokenEvent
    {
        public readonly GameObject Defender;
        public readonly float StunDuration;

        public GuardBrokenEvent(GameObject defender, float stunDuration)
        {
            Defender = defender;
            StunDuration = stunDuration;
        }
    }

    /// <summary>Raised when the lock-on target changes. <see cref="Target"/> is null on release.</summary>
    public readonly struct LockOnChangedEvent
    {
        public readonly GameObject Player;
        public readonly GameObject Target;

        public LockOnChangedEvent(GameObject player, GameObject target)
        {
            Player = player;
            Target = target;
        }
    }

    /// <summary>Raised when a swing completes a named chain (SPEC.md section 14).</summary>
    public readonly struct ComboPerformedEvent
    {
        public readonly GameObject Player;
        public readonly string ChainName;
        public readonly float DamageMultiplier;

        public ComboPerformedEvent(GameObject player, string chainName, float damageMultiplier)
        {
            Player = player;
            ChainName = chainName;
            DamageMultiplier = damageMultiplier;
        }
    }

    /// <summary>Raised when a finisher starts against a weakened enemy.</summary>
    public readonly struct FinisherStartedEvent
    {
        public readonly GameObject Player;
        public readonly GameObject Target;

        public FinisherStartedEvent(GameObject player, GameObject target)
        {
            Player = player;
            Target = target;
        }
    }
}

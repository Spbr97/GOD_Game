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

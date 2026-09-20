using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// AI payloads published on <see cref="Game.Core.EventBus"/> so UI, audio and the
    /// encounter logic can react without referencing <see cref="EnemyController"/>
    /// directly (SPEC.md section 49).
    /// </summary>
    public readonly struct EnemyStateChangedEvent
    {
        public readonly GameObject Enemy;
        public readonly EnemyState From;
        public readonly EnemyState To;

        public EnemyStateChangedEvent(GameObject enemy, EnemyState from, EnemyState to)
        {
            Enemy = enemy;
            From = from;
            To = to;
        }
    }

    /// <summary>Raised when an enemy first acquires a target, for music stings and alert audio.</summary>
    public readonly struct EnemyAlertedEvent
    {
        public readonly GameObject Enemy;
        public readonly GameObject Target;

        public EnemyAlertedEvent(GameObject enemy, GameObject target)
        {
            Enemy = enemy;
            Target = target;
        }
    }

    /// <summary>Raised when poise breaks (SPEC.md section 16 hit reactions).</summary>
    public readonly struct EnemyStaggeredEvent
    {
        public readonly GameObject Enemy;
        public readonly float Duration;

        public EnemyStaggeredEvent(GameObject enemy, float duration)
        {
            Enemy = enemy;
            Duration = duration;
        }
    }

    /// <summary>
    /// Raised when an enemy gives up and heads home, so an encounter can reset itself
    /// (SPEC.md section 55: arenas must reset safely).
    /// </summary>
    public readonly struct EnemyLostTargetEvent
    {
        public readonly GameObject Enemy;

        public EnemyLostTargetEvent(GameObject enemy)
        {
            Enemy = enemy;
        }
    }
}

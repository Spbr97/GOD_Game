namespace Game.AI
{
    /// <summary>
    /// The behaviours SPEC.md section 17 requires, one enum value each.
    ///
    /// The order matters only for readability. Transitions are decided in
    /// <see cref="EnemyController"/>; nothing should infer priority from the
    /// numeric value.
    /// </summary>
    public enum EnemyState
    {
        /// <summary>Standing at home with no patrol route.</summary>
        Idle,

        /// <summary>Walking a <see cref="PatrolRoute"/>.</summary>
        Patrol,

        /// <summary>Heard something and is moving to look, without having seen the target.</summary>
        Investigate,

        /// <summary>Target is visible; closing distance.</summary>
        Chase,

        /// <summary>In range and swinging, or waiting out an attack cooldown in range.</summary>
        Attack,

        /// <summary>Backing away — low health, or yielding its attack slot to another enemy.</summary>
        Retreat,

        /// <summary>Poise broken; cannot act (SPEC.md section 16 hit reactions).</summary>
        Stagger,

        /// <summary>Lost the target; sweeping around its last known position.</summary>
        Search,

        /// <summary>Given up and walking back to its spawn point.</summary>
        ReturnHome,

        /// <summary>Health reached zero. Terminal — nothing transitions out of this.</summary>
        Dead
    }
}

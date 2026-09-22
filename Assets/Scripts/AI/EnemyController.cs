using Game.Combat;
using Game.Core;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Everything an enemy senses on one tick, gathered before any decision is made.
    ///
    /// The state machine reads this struct and nothing else, which is what lets
    /// <see cref="EnemyController.Decide"/> be a pure function that tests can drive
    /// without a scene, a NavMesh or a physics step.
    /// </summary>
    public readonly struct EnemySenses
    {
        public readonly bool CanSeeTarget;

        /// <summary>Seconds since the target was last seen; <see cref="float.MaxValue"/> if never.</summary>
        public readonly float TimeSinceSeen;

        public readonly float DistanceToTarget;
        public readonly float DistanceFromHome;
        public readonly float HealthFraction;
        public readonly bool IsDead;
        public readonly bool IsStaggered;

        /// <summary>A noise or an ally's alert is pending investigation.</summary>
        public readonly bool HasDisturbance;

        /// <summary>This enemy currently holds one of its group's attack slots.</summary>
        public readonly bool HasAttackSlot;

        /// <summary>False once a low-health retreat has already been spent at this health.</summary>
        public readonly bool CanRetreat;

        public EnemySenses(bool canSeeTarget, float timeSinceSeen, float distanceToTarget, float distanceFromHome,
            float healthFraction, bool isDead, bool isStaggered, bool hasDisturbance, bool hasAttackSlot, bool canRetreat)
        {
            CanSeeTarget = canSeeTarget;
            TimeSinceSeen = timeSinceSeen;
            DistanceToTarget = distanceToTarget;
            DistanceFromHome = distanceFromHome;
            HealthFraction = healthFraction;
            IsDead = isDead;
            IsStaggered = isStaggered;
            HasDisturbance = hasDisturbance;
            HasAttackSlot = hasAttackSlot;
            CanRetreat = canRetreat;
        }
    }

    /// <summary>
    /// The enemy state machine (SPEC.md section 17): patrol, investigate, chase,
    /// attack, retreat, stagger, search, return-to-home, group coordination, death.
    ///
    /// It owns transitions and per-state behaviour only. Seeing is
    /// <see cref="EnemyPerception"/>, moving is <see cref="EnemyNavigator"/>, swinging
    /// is <see cref="EnemyCombatant"/>, reeling is <see cref="EnemyStagger"/>, and
    /// health is the shared <see cref="HealthComponent"/> from TASK 002. Splitting it
    /// this way is what keeps <see cref="Decide"/> testable.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public class EnemyController : MonoBehaviour
    {
        [SerializeField] private EnemyArchetype archetype;
        [SerializeField] private EnemyPerception perception;
        [SerializeField] private EnemyNavigator navigator;
        [SerializeField] private EnemyCombatant combatant;
        [SerializeField] private EnemyStagger stagger;
        [SerializeField] private EnemyGroup group;
        [SerializeField] private PatrolRoute patrolRoute;

        [Tooltip("How close to home counts as home.")]
        [SerializeField] private float homeArrivalDistance = 1.5f;

        [Tooltip("Tints the body to the archetype colour on start, as placeholder visual identity.")]
        [SerializeField] private bool applyArchetypeTint = true;

        private HealthComponent health;
        private Vector3 home;
        private Quaternion homeRotation;

        private float stateEnteredAt;
        private int patrolIndex;
        private float patrolWaitUntil;
        private Vector3 searchPoint;
        private bool hasDisturbance;
        private bool canRetreat = true;
        private bool alertedGroup;
        private float speedMultiplier = 1f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public EnemyState State { get; private set; } = EnemyState.Idle;

        public EnemyArchetype Archetype => archetype;

        /// <summary>Where this enemy spawned, and where it returns when it gives up.</summary>
        public Vector3 Home => home;

        /// <summary>Seconds spent in the current state.</summary>
        public float TimeInState => Time.time - stateEnteredAt;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();

            // Plain null checks rather than ??=: a missing serialized reference is a
            // Unity "fake null", which the null-coalescing operator does not catch.
            if (perception == null) { perception = GetComponent<EnemyPerception>(); }
            if (navigator == null) { navigator = GetComponent<EnemyNavigator>(); }
            if (combatant == null) { combatant = GetComponent<EnemyCombatant>(); }
            if (stagger == null) { stagger = GetComponent<EnemyStagger>(); }
            if (group == null) { group = GetComponentInParent<EnemyGroup>(); }

            home = transform.position;
            homeRotation = transform.rotation;

            if (archetype != null)
            {
                health.Configure(archetype.MaxHealth);

                if (applyArchetypeTint)
                {
                    ApplyTint(archetype.BodyTint);
                }
            }
            else
            {
                GameLogger.LogFallback(
                    LogCategory.AI,
                    "EnemyController has no archetype",
                    $"{name} in scene {gameObject.scene.name}",
                    "no EnemyArchetype assigned",
                    "the enemy stands idle and never acts",
                    this);
            }
        }

        private void OnEnable()
        {
            group?.Register(this);

            if (health != null)
            {
                health.Damaged += HandleDamaged;
                health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            group?.Unregister(this);

            if (health != null)
            {
                health.Damaged -= HandleDamaged;
                health.Died -= HandleDied;
            }
        }

        private void Update()
        {
            if (archetype == null || State == EnemyState.Dead)
            {
                return;
            }

            var senses = Sense();
            var next = Decide(State, senses, archetype, TimeInState, patrolRoute != null, homeArrivalDistance);

            if (next != State)
            {
                Transition(next);
            }

            Act(senses);
        }

        /// <summary>
        /// The whole transition table, as a pure function. Priority runs top to
        /// bottom: death, then stagger, then the leash, then combat, then the
        /// give-up ladder of search → investigate → return home → patrol.
        /// </summary>
        public static EnemyState Decide(EnemyState current, in EnemySenses senses, EnemyArchetype archetype,
            float timeInState, bool hasPatrolRoute, float homeArrivalDistance = 1.5f)
        {
            if (senses.IsDead || current == EnemyState.Dead)
            {
                return EnemyState.Dead;
            }

            if (senses.IsStaggered)
            {
                return EnemyState.Stagger;
            }

            var hasTarget = senses.CanSeeTarget || senses.TimeSinceSeen <= archetype.LoseTargetAfter;

            if (hasTarget)
            {
                // Anti-cheese (SPEC.md section 56): aggro cannot follow the player
                // across the map. The leash outranks the target.
                if (senses.DistanceFromHome > archetype.LeashRange)
                {
                    return EnemyState.ReturnHome;
                }

                if (senses.CanRetreat
                    && archetype.RetreatHealthFraction > 0f
                    && senses.HealthFraction <= archetype.RetreatHealthFraction
                    && (current != EnemyState.Retreat || timeInState < archetype.RetreatDuration))
                {
                    return EnemyState.Retreat;
                }

                // Without a group slot the enemy closes but does not swing, which is
                // what turns a mob into a rotation instead of a simultaneous pile-on.
                if (senses.DistanceToTarget <= archetype.AttackRange && senses.HasAttackSlot)
                {
                    return EnemyState.Attack;
                }

                return EnemyState.Chase;
            }

            // Target lost. Sweep the last known position before giving up.
            if (senses.TimeSinceSeen < float.MaxValue)
            {
                if (current == EnemyState.Search)
                {
                    return timeInState < archetype.SearchDuration ? EnemyState.Search : EnemyState.ReturnHome;
                }

                if (current is EnemyState.Chase or EnemyState.Attack or EnemyState.Retreat or EnemyState.Stagger)
                {
                    return EnemyState.Search;
                }
            }

            if (senses.HasDisturbance)
            {
                if (current == EnemyState.Investigate)
                {
                    return timeInState < archetype.InvestigateDuration
                        ? EnemyState.Investigate
                        : EnemyState.ReturnHome;
                }

                return EnemyState.Investigate;
            }

            // An enemy already walking its beat is not "away from home" — its route is
            // where it belongs. Without this exception a patrolling enemy flips to
            // ReturnHome the moment it steps past homeArrivalDistance, then back to
            // Patrol when it gets back, and thrashes between two destinations without
            // ever moving.
            if (current == EnemyState.Patrol && hasPatrolRoute)
            {
                return EnemyState.Patrol;
            }

            if (senses.DistanceFromHome > homeArrivalDistance)
            {
                return EnemyState.ReturnHome;
            }

            return hasPatrolRoute ? EnemyState.Patrol : EnemyState.Idle;
        }

        /// <summary>
        /// Sends this enemy to look at a position without it having seen anything.
        /// Called by <see cref="EnemyGroup.BroadcastAlert"/> and when damaged from
        /// out of sight.
        /// </summary>
        public void NotifyDisturbance(Vector3 position)
        {
            if (State == EnemyState.Dead)
            {
                return;
            }

            hasDisturbance = true;
            perception?.ReportDisturbance(position);
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(EnemyArchetype enemyArchetype, PatrolRoute route = null, EnemyGroup enemyGroup = null)
        {
            archetype = enemyArchetype;
            patrolRoute = route;
            group = enemyGroup;
        }

        /// <summary>
        /// Scales every movement speed this enemy sets on itself, from now on. Used by
        /// <see cref="BossController"/> to make later phases more dangerous through pace
        /// rather than health or damage (SPEC.md section 18).
        /// </summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = Mathf.Max(0.1f, multiplier);

            // Re-apply immediately so a phase change is felt on the spot rather than
            // waiting for the next state transition to pick up the new pace.
            switch (State)
            {
                case EnemyState.Chase:
                case EnemyState.Retreat:
                case EnemyState.Search:
                case EnemyState.Investigate:
                    navigator?.SetSpeed(archetype.ChaseSpeed * speedMultiplier);
                    break;

                case EnemyState.Patrol:
                case EnemyState.ReturnHome:
                    navigator?.SetSpeed(archetype.PatrolSpeed * speedMultiplier);
                    break;
            }
        }

        /// <summary>Public seam for a boss's supernatural-transformation tint (SPEC.md section 18 phase 3).</summary>
        public void ApplyPhaseTint(Color colour)
        {
            ApplyTint(colour);
        }

        /// <summary>
        /// Puts this enemy back exactly where it was authored, having forgotten
        /// everything: position, rotation, pace, tint and target.
        ///
        /// Used by <see cref="BossArena"/> when the player walks out of a fight
        /// (SPEC.md section 55's "important boss arenas must reset safely") and by
        /// <see cref="Game.World.EnemyBoundsGuard"/> when one falls through the floor.
        /// Health is deliberately not touched here — this class does not own it, and
        /// the two callers want different answers.
        /// </summary>
        public void ResetToHome()
        {
            if (State == EnemyState.Dead)
            {
                return;
            }

            navigator?.Stop();

            // Warp rather than move: the navigator owns this transform while its agent
            // is on a mesh, and a plain assignment is silently undone next frame.
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.Warp(home);
            }
            else
            {
                transform.position = home;
            }

            transform.rotation = homeRotation;

            perception?.Forget();
            hasDisturbance = false;

            SetSpeedMultiplier(1f);

            if (archetype != null && applyArchetypeTint)
            {
                ApplyTint(archetype.BodyTint);
            }

            Transition(patrolRoute != null ? EnemyState.Patrol : EnemyState.Idle);
        }

        private EnemySenses Sense()
        {
            var targetPosition = perception != null && perception.Target != null
                ? perception.Target.position
                : transform.position + Vector3.forward * 1000f;

            var distanceToTarget = Vector3.Distance(transform.position, targetPosition);
            var healthFraction = health != null ? health.HealthFraction : 1f;

            if (archetype != null && healthFraction > archetype.RetreatHealthFraction)
            {
                // Healing (or simply never having been hurt) re-arms the retreat.
                canRetreat = true;
            }

            return new EnemySenses(
                canSeeTarget: perception != null && perception.HasLineOfSight,
                timeSinceSeen: perception != null ? perception.TimeSinceSeen : float.MaxValue,
                distanceToTarget: distanceToTarget,
                distanceFromHome: Vector3.Distance(transform.position, home),
                healthFraction: healthFraction,
                isDead: health != null && health.IsDead,
                isStaggered: stagger != null && stagger.IsStaggered,
                hasDisturbance: hasDisturbance,
                hasAttackSlot: group == null || group.HoldsAttackSlot(this),
                canRetreat: canRetreat);
        }

        private void Transition(EnemyState next)
        {
            var previous = State;
            OnExit(previous);

            State = next;
            stateEnteredAt = Time.time;

            OnEnter(next, previous);

            EventBus.Publish(new EnemyStateChangedEvent(gameObject, previous, next));
        }

        private void OnExit(EnemyState state)
        {
            switch (state)
            {
                case EnemyState.Attack:
                    group?.ReleaseAttackSlot(this);
                    break;

                case EnemyState.Retreat:
                    // One retreat per dip below the threshold; Sense() re-arms it if
                    // health ever climbs back. Otherwise the enemy would retreat, come
                    // back, and immediately retreat again forever.
                    canRetreat = false;
                    break;

                case EnemyState.Investigate:
                    hasDisturbance = false;
                    break;
            }
        }

        private void OnEnter(EnemyState state, EnemyState previous)
        {
            switch (state)
            {
                case EnemyState.Chase:
                    navigator?.SetSpeed(archetype.ChaseSpeed * speedMultiplier);

                    if (!alertedGroup)
                    {
                        alertedGroup = true;
                        group?.BroadcastAlert(this, transform.position);
                        EventBus.Publish(new EnemyAlertedEvent(gameObject,
                            perception != null && perception.Target != null ? perception.Target.gameObject : null));
                    }

                    break;

                case EnemyState.Attack:
                    navigator?.Stop();
                    break;

                case EnemyState.Stagger:
                    combatant?.CancelAttack();
                    navigator?.Stop();
                    group?.ReleaseAttackSlot(this);
                    break;

                case EnemyState.Retreat:
                case EnemyState.Search:
                case EnemyState.Investigate:
                    navigator?.SetSpeed(archetype.ChaseSpeed * speedMultiplier);
                    if (state == EnemyState.Search)
                    {
                        searchPoint = perception != null ? perception.LastKnownPosition : transform.position;
                    }

                    break;

                case EnemyState.ReturnHome:
                    navigator?.SetSpeed(archetype.PatrolSpeed * speedMultiplier);
                    alertedGroup = false;
                    hasDisturbance = false;
                    perception?.Forget();
                    EventBus.Publish(new EnemyLostTargetEvent(gameObject));
                    break;

                case EnemyState.Patrol:
                    navigator?.SetSpeed(archetype.PatrolSpeed * speedMultiplier);
                    if (patrolRoute != null && previous != EnemyState.Patrol)
                    {
                        patrolIndex = patrolRoute.NearestIndex(transform.position);
                    }

                    break;

                case EnemyState.Idle:
                    navigator?.Stop();
                    break;

                case EnemyState.Dead:
                    combatant?.CancelAttack();
                    navigator?.Stop();
                    group?.ReleaseAttackSlot(this);
                    break;
            }
        }

        private void Act(in EnemySenses senses)
        {
            switch (State)
            {
                case EnemyState.Idle:
                    navigator?.FaceTowards(transform.position + homeRotation * Vector3.forward, archetype.TurnSpeedDegrees);
                    break;

                case EnemyState.Patrol:
                    TickPatrol();
                    break;

                case EnemyState.Investigate:
                    MoveTo(perception != null ? perception.LastKnownPosition : home);
                    break;

                case EnemyState.Chase:
                    TickChase(senses);
                    break;

                case EnemyState.Attack:
                    TickAttack();
                    break;

                case EnemyState.Retreat:
                    TickRetreat();
                    break;

                case EnemyState.Search:
                    TickSearch();
                    break;

                case EnemyState.ReturnHome:
                    MoveTo(home);
                    break;

                case EnemyState.Stagger:
                    // Reeling: no movement, no swing, no facing. That window is the reward.
                    break;
            }
        }

        private void TickPatrol()
        {
            if (patrolRoute == null || patrolRoute.WaypointCount == 0)
            {
                return;
            }

            if (Time.time < patrolWaitUntil)
            {
                return;
            }

            var waypoint = patrolRoute.GetPosition(patrolIndex);

            if (navigator != null && navigator.HasArrived && navigator.Destination == waypoint)
            {
                patrolIndex = patrolRoute.NextIndex(patrolIndex);
                patrolWaitUntil = Time.time + patrolRoute.WaitAtWaypoint;
                return;
            }

            MoveTo(waypoint);
        }

        private void TickChase(in EnemySenses senses)
        {
            var destination = perception != null ? perception.LastKnownPosition : home;

            // In range but without a slot: hold and face, rather than shoving into the
            // player's back while an ally swings.
            if (senses.DistanceToTarget <= archetype.AttackRange && !senses.HasAttackSlot)
            {
                navigator?.Stop();
                navigator?.FaceTowards(destination, archetype.TurnSpeedDegrees);
                group?.TryClaimAttackSlot(this);
                return;
            }

            group?.TryClaimAttackSlot(this);
            MoveTo(destination);
        }

        private void TickAttack()
        {
            var target = perception != null && perception.Target != null
                ? perception.Target.position
                : transform.position + transform.forward;

            // Face the target during the telegraph but not during the active frames,
            // so a dodge to the side actually beats the swing.
            if (combatant == null || !combatant.IsHitboxOpen)
            {
                navigator?.FaceTowards(target, archetype.TurnSpeedDegrees);
            }

            combatant?.TryAttack();
        }

        private void TickRetreat()
        {
            var away = transform.position - (perception != null && perception.Target != null
                ? perception.Target.position
                : home);
            away.y = 0f;

            if (away.sqrMagnitude < 0.0001f)
            {
                away = -transform.forward;
            }

            MoveTo(transform.position + away.normalized * archetype.AttackRange * 2f);
        }

        private void TickSearch()
        {
            if (navigator == null || !navigator.HasArrived)
            {
                MoveTo(searchPoint);
                return;
            }

            // Pick a new point around the last known position. Random rather than a
            // fixed pattern so a player cannot memorise the sweep.
            var offset = Random.insideUnitCircle * archetype.SearchRadius;
            var origin = perception != null ? perception.LastKnownPosition : home;
            searchPoint = origin + new Vector3(offset.x, 0f, offset.y);
            MoveTo(searchPoint);
        }

        private void MoveTo(Vector3 destination)
        {
            if (navigator == null)
            {
                return;
            }

            if (!navigator.SetDestination(destination))
            {
                // SPEC.md section 50 navigation failure, final step: return to patrol.
                hasDisturbance = false;
                perception?.Forget();
                Transition(patrolRoute != null ? EnemyState.Patrol : EnemyState.Idle);
            }
        }

        private void HandleDamaged(DamageData damage)
        {
            if (State == EnemyState.Dead)
            {
                return;
            }

            // Being hit from behind is a lead, not free vision: the enemy turns to
            // look rather than instantly acquiring an unseen attacker.
            if (damage.Source != null)
            {
                NotifyDisturbance(damage.Source.transform.position);
            }

            group?.BroadcastAlert(this, transform.position);
        }

        private void HandleDied(DamageData killingBlow)
        {
            Transition(EnemyState.Dead);
        }

        private void ApplyTint(Color colour)
        {
            var block = new MaterialPropertyBlock();
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, colour);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}

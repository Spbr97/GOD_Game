using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Combat
{
    /// <summary>
    /// Lock-on targeting (SPEC.md section 13, TASK 007). Picks the hostile the
    /// player most plausibly means — nearest to the centre of view, then nearest by
    /// distance — and points both the player and the camera at it until it dies,
    /// leaves range, or the player lets go.
    ///
    /// Candidates are anything with a <see cref="HealthComponent"/> and a hurtbox
    /// that is not on the player's side, so training dummies are lockable and
    /// friendly NPCs (which have no health) are not. Nothing here knows what an
    /// enemy is; that keeps Combat from depending on AI.
    ///
    /// Player facing goes through <see cref="Game.Player.PlayerController.FacingTarget"/>
    /// and the camera through <see cref="Game.Player.PlayerCamera.LookTarget"/>, the
    /// same one-way arrangement as the dodge impulse (ARCHITECTURE.md).
    /// </summary>
    public class LockOnController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

        [Tooltip("Furthest an enemy can be to acquire it.")]
        [SerializeField] private float acquireRange = 15f;

        [Tooltip("The lock drops once the target is further than this. Wider than acquire so it does not flicker at the edge.")]
        [SerializeField] private float breakRange = 20f;

        [Tooltip("Half-angle in degrees around the view direction inside which an enemy can be acquired.")]
        [SerializeField] private float acquireHalfAngle = 70f;

        [Tooltip("Weight of angle versus distance when ranking candidates. Higher prefers what is in front over what is close.")]
        [SerializeField] private float angleWeight = 0.05f;

        private InputAction lockOnAction;
        private Game.Player.PlayerController locomotion;
        private Game.Player.PlayerCamera playerCamera;
        private HealthComponent ownHealth;

        /// <summary>The locked entity's health, or null when not locked.</summary>
        public HealthComponent Target { get; private set; }

        public bool IsLocked => Target != null;

        public Transform TargetTransform => Target != null ? Target.transform : null;

        private void Awake()
        {
            locomotion = GetComponent<Game.Player.PlayerController>();
            ownHealth = GetComponent<HealthComponent>();

            if (inputActions != null)
            {
                lockOnAction = inputActions.FindActionMap("Gameplay")?.FindAction("LockOn");
            }
        }

        private void OnEnable()
        {
            lockOnAction?.Enable();
        }

        private void OnDisable()
        {
            lockOnAction?.Disable();
            Release();
        }

        private void Update()
        {
            if (lockOnAction != null && lockOnAction.WasPressedThisFrame())
            {
                Toggle();
            }

            if (Target == null)
            {
                return;
            }

            if (Target.IsDead || !Target.gameObject.activeInHierarchy
                || Vector3.Distance(transform.position, Target.transform.position) > breakRange)
            {
                Release();
            }
        }

        /// <summary>Locks on to the best candidate, or releases if already locked.</summary>
        public void Toggle()
        {
            if (IsLocked)
            {
                Release();
            }
            else
            {
                Acquire();
            }
        }

        /// <summary>Locks on to the best candidate in view. Returns false when there is none.</summary>
        public bool Acquire()
        {
            var candidate = FindBestCandidate();
            if (candidate == null)
            {
                return false;
            }

            SetTarget(candidate);
            return true;
        }

        /// <summary>Locks on to a specific entity. For tests and scripted encounters.</summary>
        public void LockTo(HealthComponent target)
        {
            if (target == null || target.IsDead)
            {
                Release();
                return;
            }

            SetTarget(target);
        }

        public void Release()
        {
            if (Target == null)
            {
                return;
            }

            Target = null;
            Apply(null);
            GameLogger.Log(LogCategory.Combat, $"{name} released lock-on.", this);
            EventBus.Publish(new LockOnChangedEvent(gameObject, null));
        }

        /// <summary>Test seam. Must be called before Awake for the input to bind.</summary>
        public void Configure(InputActionAsset actions, float range = 15f, float dropRange = 20f)
        {
            inputActions = actions;
            acquireRange = range;
            breakRange = dropRange;
        }

        private void SetTarget(HealthComponent target)
        {
            if (Target == target)
            {
                return;
            }

            Target = target;
            Apply(target.transform);
            GameLogger.Log(LogCategory.Combat, $"{name} locked on to {target.name}.", this);
            EventBus.Publish(new LockOnChangedEvent(gameObject, target.gameObject));
        }

        private void Apply(Transform target)
        {
            if (locomotion != null)
            {
                locomotion.FacingTarget = target;
            }

            if (playerCamera == null && Camera.main != null)
            {
                playerCamera = Camera.main.GetComponent<Game.Player.PlayerCamera>();
            }

            if (playerCamera != null)
            {
                playerCamera.LookTarget = target;
            }
        }

        private HealthComponent FindBestCandidate()
        {
            var origin = transform.position;
            var viewForward = Camera.main != null ? Camera.main.transform.forward : transform.forward;
            viewForward.y = 0f;
            if (viewForward.sqrMagnitude < 0.0001f)
            {
                viewForward = transform.forward;
            }

            viewForward.Normalize();

            HealthComponent best = null;
            var bestScore = float.PositiveInfinity;

            foreach (var hurtbox in FindObjectsByType<Hurtbox>())
            {
                var health = hurtbox.Health;
                if (health == null || health == ownHealth || health.IsDead || hurtbox.Faction == Faction.Player)
                {
                    continue;
                }

                var offset = health.transform.position - origin;
                offset.y = 0f;
                var distance = offset.magnitude;
                if (distance > acquireRange || distance < 0.0001f)
                {
                    continue;
                }

                var angle = Vector3.Angle(viewForward, offset);
                if (angle > acquireHalfAngle)
                {
                    continue;
                }

                var score = distance + angle * angleWeight * acquireRange;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = health;
                }
            }

            return best;
        }
    }
}

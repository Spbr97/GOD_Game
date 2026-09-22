using Game.Core;
using Game.Memory;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A one-time toll paid in memory integrity rather than an item (SPEC.md Phase 3's
    /// "memory cost", TASK 015) — the temple itself demanding a price distinct from
    /// Ember Step's per-use drain (TASK 011), which is a cost of using an ability, not
    /// of entering a place.
    ///
    /// Deliberately not a hard gate with no alternative: <see cref="MemoryManager.ReduceIntegrity"/>
    /// clamps at zero rather than refusing, so paying is always possible regardless of
    /// how much integrity the player has already spent (SPEC.md section 55's
    /// anti-softlock rule against a cost that can leave a required action permanently
    /// unavailable).
    /// </summary>
    public class MemoryToll : Interactable
    {
        [Range(0f, 1f)]
        [SerializeField] private float integrityCost = 0.15f;

        [Tooltip("Set once the toll is paid. Optional.")]
        [SerializeField] private string paidFlag;

        [Tooltip("Made inactive once the toll is paid — the barrier this toll guards.")]
        [SerializeField] private GameObject barrier;

        [Tooltip("Optional. If set, paying the toll survives a save (SPEC.md TASK 009) and does not charge integrity twice on restore.")]
        [SerializeField] private SaveIdentity identity;

        public bool Paid { get; private set; }

        public override string Prompt => Paid
            ? "The way is open"
            : $"Offer a memory to pass ({integrityCost:P0} integrity)";

        public override bool CanInteract => !Paid && base.CanInteract;

        private void Awake()
        {
            if (identity == null)
            {
                identity = GetComponent<SaveIdentity>();
            }
        }

        private void OnEnable()
        {
            if (identity == null)
            {
                return;
            }

            EventBus.Subscribe<WorldFlagChangedEvent>(OnWorldFlagChanged);

            // See EnemyHealth.OnEnable for why both a check-now and a subscription are
            // needed: this covers a save already applied before this toll existed.
            if (WorldObjectState.IsMarked(WorldObjectState.PaidFlag(identity.Id)))
            {
                ApplyRestoredPayment();
            }
        }

        private void OnDisable()
        {
            if (identity != null)
            {
                EventBus.Unsubscribe<WorldFlagChangedEvent>(OnWorldFlagChanged);
            }
        }

        private void OnWorldFlagChanged(WorldFlagChangedEvent changed)
        {
            if (changed.Value && identity != null && changed.Flag == WorldObjectState.PaidFlag(identity.Id))
            {
                ApplyRestoredPayment();
            }
        }

        /// <summary>
        /// Applies a remembered payment directly, without spending integrity again —
        /// the save already captured the reduced integrity, so charging it a second
        /// time here would spend it twice. Guarded by <c>Paid</c> for the same
        /// self-notification reason as <see cref="Game.Combat.EnemyHealth.ApplyRestoredDeath"/>.
        /// </summary>
        private void ApplyRestoredPayment()
        {
            if (Paid)
            {
                return;
            }

            Paid = true;
            OpenBarrier();
        }

        public override void Interact(GameObject interactor)
        {
            if (Paid)
            {
                return;
            }

            if (MemoryManager.Instance == null)
            {
                GameLogger.LogFallback(
                    LogCategory.Memory,
                    $"could not pay the memory toll '{name}'",
                    $"MemoryToll on '{name}'",
                    "no MemoryManager exists in the scene",
                    "the toll stays unpaid so the barrier is not opened for free",
                    this);
                return;
            }

            MemoryManager.Instance.ReduceIntegrity(integrityCost);
            Paid = true;
            OpenBarrier();

            if (identity != null)
            {
                WorldObjectState.Mark(WorldObjectState.PaidFlag(identity.Id));
            }

            if (!string.IsNullOrEmpty(paidFlag))
            {
                SetFlag(paidFlag);
            }
        }

        private void OpenBarrier()
        {
            if (barrier != null)
            {
                barrier.SetActive(false);
            }
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(float cost, string flagOnPaid, GameObject barrierObject)
        {
            integrityCost = Mathf.Clamp01(cost);
            paidFlag = flagOnPaid;
            barrier = barrierObject;
        }
    }
}

using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A blocking collider that opens permanently once its puzzle is solved (SPEC.md
    /// section 26). Decoupled from <see cref="PuzzleController"/> the same way
    /// <see cref="Combat.Checkpoint"/> and <see cref="Combat.CheckpointManager"/> are:
    /// matched by an id string through <see cref="PuzzleSolvedEvent"/> rather than a
    /// direct reference, so either can be authored without the other existing yet.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PuzzleGate : MonoBehaviour
    {
        [Tooltip("Must match the PuzzleController's puzzle id.")]
        [SerializeField] private string puzzleId;

        [Tooltip("Optional. Hidden (rather than just made non-solid) once open.")]
        [SerializeField] private GameObject visual;

        [Tooltip("Optional. If set, being open survives a save on the door's own terms, without the puzzle having to re-announce itself (TASK 041).")]
        [SerializeField] private SaveIdentity identity;

        [Tooltip("Tick for a door the player cannot finish the game without passing. Checked by ProgressionRecovery after a load.")]
        [SerializeField] private bool essential;

        public bool IsOpen { get; private set; }

        public string PuzzleId => puzzleId;

        public bool Essential => essential;

        /// <summary>The stable id this door's open state is recorded against, or null when it records none.</summary>
        public string SaveId => identity != null ? identity.Id : null;

        private void Awake()
        {
            if (identity == null)
            {
                identity = GetComponent<SaveIdentity>();
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PuzzleSolvedEvent>(OnPuzzleSolved);
            EventBus.Subscribe<WorldFlagChangedEvent>(OnWorldFlagChanged);

            // Two paths into the same state, for the same reason ItemPickup and MemoryToll
            // need both: a save applied before this door's OnEnable ran is already in
            // WorldState and publishes nothing further, so it has to be read now.
            if (identity != null && WorldObjectState.IsMarked(WorldObjectState.OpenedFlag(identity.Id)))
            {
                Open(record: false);
            }
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PuzzleSolvedEvent>(OnPuzzleSolved);
            EventBus.Unsubscribe<WorldFlagChangedEvent>(OnWorldFlagChanged);
        }

        private void OnPuzzleSolved(PuzzleSolvedEvent solved)
        {
            if (solved.PuzzleId == puzzleId)
            {
                Open();
            }
        }

        private void OnWorldFlagChanged(WorldFlagChangedEvent changed)
        {
            if (changed.Value && identity != null && changed.Flag == WorldObjectState.OpenedFlag(identity.Id))
            {
                Open(record: false);
            }
        }

        /// <summary>
        /// Opens the door now. Public so <c>ProgressionRecovery</c> can free a door that a
        /// save says should be open, and so a test does not have to stage a whole puzzle.
        /// Idempotent.
        /// </summary>
        public void Open(bool record = true)
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;

            var blocker = GetComponent<Collider>();
            if (blocker != null)
            {
                blocker.enabled = false;
            }

            if (visual != null)
            {
                visual.SetActive(false);
            }

            if (record && identity != null)
            {
                WorldObjectState.Mark(WorldObjectState.OpenedFlag(identity.Id));
            }

            GameLogger.Log(LogCategory.Game, $"Gate for puzzle '{puzzleId}' opened.", this);
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(string id, SaveIdentity saveIdentity = null, bool isEssential = false)
        {
            puzzleId = id;
            essential = isEssential;

            if (saveIdentity != null)
            {
                identity = saveIdentity;
            }
            else if (identity == null)
            {
                identity = GetComponent<SaveIdentity>();
            }
        }
    }
}

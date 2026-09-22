using Game.Audio;
using Game.Core;
using Game.Quests;
using Game.VFX;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Watches a set of <see cref="IPuzzleElement"/>s and solves the puzzle the moment
    /// every one of them is satisfied at the same time (SPEC.md section 26: the player
    /// should be able to see why it solved, not guess a hidden combination — each
    /// element's own state is the only rule).
    ///
    /// Solving is permanent: once every element has been satisfied simultaneously, the
    /// puzzle stays solved even if an element later reverts (a brazier burning out does
    /// not re-lock a gate already opened). This matches <see cref="Combat.Checkpoint"/>'s
    /// activation idempotency rather than inventing a re-lock rule SPEC.md never asks for.
    /// </summary>
    public class PuzzleController : MonoBehaviour
    {
        [Tooltip("Every piece that must be satisfied at once to solve this puzzle. Each must implement IPuzzleElement.")]
        [SerializeField] private MonoBehaviour[] elements;

        [Tooltip("World flag set once solved, and what a save restores this from.")]
        [SerializeField] private string solvedFlag;

        [Tooltip("Objective reported once solved. Optional.")]
        [SerializeField] private string objectiveIdOnSolve;

        [Tooltip("Identifies this puzzle in PuzzleSolvedEvent. Falls back to the GameObject's name.")]
        [SerializeField] private string puzzleId;

        private IPuzzleElement[] pieces;

        public bool IsSolved { get; private set; }
        public string PuzzleId => string.IsNullOrEmpty(puzzleId) ? name : puzzleId;

        private void Awake()
        {
            Resolve();
        }

        private void OnEnable()
        {
            Resolve();

            for (var i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] != null)
                {
                    pieces[i].Changed += Evaluate;
                }
            }

            EventBus.Subscribe<WorldFlagChangedEvent>(OnWorldFlagChanged);

            // Covers a save applied before this object's own OnEnable ran; the ordinary
            // live-solve path goes through Evaluate below instead.
            if (!IsSolved && !string.IsNullOrEmpty(solvedFlag)
                && WorldState.Instance != null && WorldState.Instance.GetFlag(solvedFlag))
            {
                ApplyRestoredSolve();
            }

            Evaluate();
        }

        private void OnDisable()
        {
            if (pieces != null)
            {
                for (var i = 0; i < pieces.Length; i++)
                {
                    if (pieces[i] != null)
                    {
                        pieces[i].Changed -= Evaluate;
                    }
                }
            }

            EventBus.Unsubscribe<WorldFlagChangedEvent>(OnWorldFlagChanged);
        }

        private void OnWorldFlagChanged(WorldFlagChangedEvent changed)
        {
            if (changed.Value && !IsSolved && !string.IsNullOrEmpty(solvedFlag) && changed.Flag == solvedFlag)
            {
                ApplyRestoredSolve();
            }
        }

        private void Resolve()
        {
            if (pieces != null)
            {
                return;
            }

            pieces = new IPuzzleElement[elements?.Length ?? 0];
            for (var i = 0; i < pieces.Length; i++)
            {
                pieces[i] = elements[i] as IPuzzleElement;
                if (pieces[i] == null && elements[i] != null)
                {
                    GameLogger.LogError(LogCategory.Game,
                        $"'{elements[i].name}' is listed in {name}'s puzzle but does not implement IPuzzleElement.", this);
                }
            }
        }

        /// <summary>Re-checks every element now. Public so a test can drive it without waiting for OnEnable's event wiring.</summary>
        public void Evaluate()
        {
            if (IsSolved || pieces == null || pieces.Length == 0)
            {
                return;
            }

            for (var i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null || !pieces[i].IsSatisfied)
                {
                    return;
                }
            }

            Solve();
        }

        private void Solve()
        {
            IsSolved = true;
            GameLogger.Log(LogCategory.Game, $"Puzzle '{PuzzleId}' solved.", this);

            if (!string.IsNullOrEmpty(solvedFlag))
            {
                WorldState.Instance?.SetFlag(solvedFlag);
            }

            if (!string.IsNullOrEmpty(objectiveIdOnSolve))
            {
                QuestManager.Instance?.ReportObjective(objectiveIdOnSolve);
            }

            // Cosmetic only, and only on a live solve (SPEC.md section 36's "glowing
            // symbols") — a restored solve replays no VFX/audio, the same way
            // MemoryPickup's restore path replays none either.
            VfxSpawner.Spawn(VfxKind.Glyph, transform.position);
            SfxSpawner.Play(SfxKind.PuzzleSolved, transform.position);

            EventBus.Publish(new PuzzleSolvedEvent(PuzzleId));
        }

        /// <summary>
        /// Applies a remembered solve from a save directly: marks solved and tells any
        /// <see cref="PuzzleGate"/> to open, without replaying <c>SetFlag</c> (already
        /// true) or the quest report (SaveManager's own restore already put that quest
        /// where the save left it).
        /// </summary>
        private void ApplyRestoredSolve()
        {
            IsSolved = true;
            EventBus.Publish(new PuzzleSolvedEvent(PuzzleId));
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(MonoBehaviour[] puzzleElements, string flag, string id, string objective = null)
        {
            elements = puzzleElements;
            solvedFlag = flag;
            puzzleId = id;
            objectiveIdOnSolve = objective;
            pieces = null;
            Resolve();
        }
    }
}

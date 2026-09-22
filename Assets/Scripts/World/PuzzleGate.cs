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

        public bool IsOpen { get; private set; }

        private void OnEnable()
        {
            EventBus.Subscribe<PuzzleSolvedEvent>(OnPuzzleSolved);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PuzzleSolvedEvent>(OnPuzzleSolved);
        }

        private void OnPuzzleSolved(PuzzleSolvedEvent solved)
        {
            if (solved.PuzzleId == puzzleId)
            {
                Open();
            }
        }

        private void Open()
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

            GameLogger.Log(LogCategory.Game, $"Gate for puzzle '{puzzleId}' opened.", this);
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(string id)
        {
            puzzleId = id;
        }
    }
}

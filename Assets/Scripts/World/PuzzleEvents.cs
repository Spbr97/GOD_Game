namespace Game.World
{
    /// <summary>Raised once, the moment a puzzle's elements are all satisfied at once (SPEC.md section 26).</summary>
    public readonly struct PuzzleSolvedEvent
    {
        public readonly string PuzzleId;

        public PuzzleSolvedEvent(string puzzleId)
        {
            PuzzleId = puzzleId;
        }
    }
}

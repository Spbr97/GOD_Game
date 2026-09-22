using System;

namespace Game.World
{
    /// <summary>
    /// One piece of a puzzle (SPEC.md section 26). A brazier, a pressure plate, a
    /// mirror, whatever a later temple invents — <see cref="PuzzleController"/> only
    /// needs to know whether a piece is currently satisfied and when that changes, not
    /// what kind of piece it is. This is the "reusable puzzle pieces so later temples
    /// add mechanics, not systems" the roadmap asks for: a new puzzle category is a new
    /// class implementing this interface, not a new controller.
    /// </summary>
    public interface IPuzzleElement
    {
        bool IsSatisfied { get; }

        /// <summary>Raised whenever <see cref="IsSatisfied"/> may have changed.</summary>
        event Action Changed;
    }
}

namespace Cascade.UI;

/// <summary>
/// Interface for undoable operations. Implementations capture the state
/// needed to execute and reverse an operation.
/// </summary>
public interface IUndoCommand
{
    /// <summary>
    /// Human-readable description of the operation
    /// (e.g., "Delete paragraph", "Change font to Bold").
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Executes the operation. Called on first execution and on Redo.
    /// </summary>
    void Execute();

    /// <summary>
    /// Reverses the operation. Called on Undo.
    /// </summary>
    void Undo();
}

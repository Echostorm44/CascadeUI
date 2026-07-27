namespace Cascade.UI;

/// <summary>
/// Extends <see cref="IUndoCommand"/> for commands that can merge with the
/// previous command on the undo stack. Useful for operations like consecutive
/// character typing that should form a single undo step.
/// </summary>
public interface IMergeableCommand : IUndoCommand
{
    /// <summary>
    /// Returns <c>true</c> if this command can merge with the <paramref name="previous"/>
    /// command. If true, <see cref="Merge"/> is called instead of pushing a new undo step.
    /// </summary>
    bool CanMerge(IUndoCommand previous);

    /// <summary>
    /// Merges this command into <paramref name="previous"/>. The previous command
    /// is modified in place. This command is discarded.
    /// </summary>
    void Merge(IUndoCommand previous);
}

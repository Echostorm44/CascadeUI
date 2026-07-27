namespace Cascade.UI;

/// <summary>
/// Convenience factory for creating simple lambda-based <see cref="IUndoCommand"/>
/// implementations without a dedicated class.
/// </summary>
public static class UndoCommand
{
    /// <summary>
    /// Creates a command from execute and undo delegates.
    /// </summary>
    /// <param name="description">Human-readable description of the operation.</param>
    /// <param name="execute">Action to execute the operation (and redo).</param>
    /// <param name="undo">Action to reverse the operation.</param>
    public static IUndoCommand Create(string description, Action execute, Action undo)
    {
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(undo);
        return new LambdaCommand(description, execute, undo);
    }

    /// <summary>
    /// Creates a command from a property value change. Captures the current value
    /// before executing, then swaps between old and new on undo/redo.
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="description">Human-readable description of the change.</param>
    /// <param name="getter">Retrieves the current property value.</param>
    /// <param name="setter">Sets the property to a new value.</param>
    /// <param name="newValue">The new value to apply.</param>
    public static IUndoCommand PropertyChange<T>(
        string description,
        Func<T> getter,
        Action<T> setter,
        T newValue)
    {
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(setter);

        var oldValue = getter();
        return new LambdaCommand(
            description,
            () => setter(newValue),
            () => setter(oldValue));
    }

    private sealed class LambdaCommand : IUndoCommand
    {
        private readonly Action executeAction;
        private readonly Action undoAction;

        public string Description { get; }

        internal LambdaCommand(string description, Action execute, Action undo)
        {
            Description = description;
            executeAction = execute;
            undoAction = undo;
        }

        public void Execute()
        {
            executeAction();
        }

        public void Undo()
        {
            undoAction();
        }
    }
}

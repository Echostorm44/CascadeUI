namespace Cascade.UI;

/// <summary>
/// A command history with undo/redo operations. Provides reactive signal
/// properties (<see cref="CanUndo"/>, <see cref="CanRedo"/>, etc.) that
/// update UI automatically when the stack state changes.
/// </summary>
public class UndoStack
{
    private readonly int maxDepth;
    private readonly List<IUndoCommand> undoList = [];
    private readonly List<IUndoCommand> redoList = [];
    private readonly SignalSource canUndoSource = new("UndoStack.CanUndo");
    private readonly SignalSource canRedoSource = new("UndoStack.CanRedo");
    private readonly SignalSource undoDescriptionSource = new("UndoStack.UndoDescription");
    private readonly SignalSource redoDescriptionSource = new("UndoStack.RedoDescription");
    private readonly SignalSource isDirtySource = new("UndoStack.IsDirty");
    private int? savedIndex;
    private int batchDepth;
    private List<IUndoCommand>? batchCommands;

    /// <summary>
    /// Creates a new <see cref="UndoStack"/> with an optional maximum depth.
    /// </summary>
    /// <param name="maxDepth">
    /// Maximum number of commands on the undo stack. Oldest commands are
    /// discarded when the limit is exceeded. Default: unlimited.
    /// </param>
    public UndoStack(int maxDepth = int.MaxValue)
    {
        this.maxDepth = maxDepth;
    }

    /// <summary>
    /// Executes a command and pushes it onto the undo stack.
    /// Clears the redo stack (forward history is lost on new action).
    /// Supports <see cref="IMergeableCommand"/> merging with the previous command.
    /// </summary>
    public void Execute(IUndoCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (batchDepth > 0)
        {
            command.Execute();
            batchCommands!.Add(command);
            return;
        }

        command.Execute();

        // Try merge with the previous command
        if (command is IMergeableCommand mergeable && undoList.Count > 0)
        {
            var previous = undoList[^1];
            if (mergeable.CanMerge(previous))
            {
                mergeable.Merge(previous);
                redoList.Clear();

                // Merge modified the top command — invalidate saved point if at or past current
                if (savedIndex.HasValue && savedIndex.Value >= undoList.Count)
                {
                    savedIndex = null;
                }

                NotifyStateChanged();
                return;
            }
        }

        var countBefore = undoList.Count;
        undoList.Add(command);
        redoList.Clear();

        // Saved point was in redo direction — now unreachable
        if (savedIndex.HasValue && savedIndex.Value > countBefore)
        {
            savedIndex = null;
        }

        EnforceMaxDepth();
        NotifyStateChanged();
    }

    /// <summary>
    /// Undoes the most recent command. No-op if <see cref="CanUndo"/> is <c>false</c>.
    /// </summary>
    public void Undo()
    {
        if (undoList.Count == 0)
        {
            return;
        }

        var command = undoList[^1];
        undoList.RemoveAt(undoList.Count - 1);
        command.Undo();
        redoList.Add(command);
        NotifyStateChanged();
    }

    /// <summary>
    /// Redoes the most recently undone command. No-op if <see cref="CanRedo"/> is <c>false</c>.
    /// </summary>
    public void Redo()
    {
        if (redoList.Count == 0)
        {
            return;
        }

        var command = redoList[^1];
        redoList.RemoveAt(redoList.Count - 1);
        command.Execute();
        undoList.Add(command);
        EnforceMaxDepth();
        NotifyStateChanged();
    }

    /// <summary>
    /// True when there is at least one command to undo. Reactive — reading in
    /// <c>Render()</c> subscribes to changes.
    /// </summary>
    [Signal]
    public bool CanUndo
    {
        get
        {
            SignalTracker.RecordRead(canUndoSource);
            return undoList.Count > 0;
        }
    }

    /// <summary>
    /// True when there is at least one command to redo. Reactive — reading in
    /// <c>Render()</c> subscribes to changes.
    /// </summary>
    [Signal]
    public bool CanRedo
    {
        get
        {
            SignalTracker.RecordRead(canRedoSource);
            return redoList.Count > 0;
        }
    }

    /// <summary>
    /// Human-readable description of the next undo operation.
    /// Empty string if <see cref="CanUndo"/> is <c>false</c>.
    /// Reactive — reading in <c>Render()</c> subscribes to changes.
    /// </summary>
    [Signal]
    public string UndoDescription
    {
        get
        {
            SignalTracker.RecordRead(undoDescriptionSource);
            return undoList.Count > 0 ? undoList[^1].Description : string.Empty;
        }
    }

    /// <summary>
    /// Human-readable description of the next redo operation.
    /// Empty string if <see cref="CanRedo"/> is <c>false</c>.
    /// Reactive — reading in <c>Render()</c> subscribes to changes.
    /// </summary>
    [Signal]
    public string RedoDescription
    {
        get
        {
            SignalTracker.RecordRead(redoDescriptionSource);
            return redoList.Count > 0 ? redoList[^1].Description : string.Empty;
        }
    }

    /// <summary>
    /// Number of commands on the undo stack.
    /// </summary>
    public int UndoCount
    {
        get => undoList.Count;
    }

    /// <summary>
    /// Number of commands on the redo stack.
    /// </summary>
    public int RedoCount
    {
        get => redoList.Count;
    }

    /// <summary>
    /// Clears both undo and redo stacks.
    /// </summary>
    public void Clear()
    {
        undoList.Clear();
        redoList.Clear();
        savedIndex = null;
        NotifyStateChanged();
    }

    /// <summary>
    /// Begins a batch operation. All commands executed before the returned
    /// <see cref="IDisposable"/> is disposed are grouped into a single undo step.
    /// Batches can nest — only the outermost batch creates an undo step.
    /// </summary>
    /// <param name="description">Description for the batched undo step.</param>
    public IDisposable BeginBatch(string description)
    {
        if (batchDepth == 0)
        {
            batchCommands = [];
        }

        batchDepth++;
        return new BatchScope(this, description);
    }

    /// <summary>
    /// Marks the current state as the "saved" point. <see cref="IsDirty"/> will
    /// be <c>false</c> until the stack moves away from this point.
    /// </summary>
    public void MarkSaved()
    {
        savedIndex = undoList.Count;
        SignalTracker.NotifyWrite(isDirtySource);
    }

    /// <summary>
    /// True when the current state differs from the last <see cref="MarkSaved"/> point.
    /// Correctly handles undo-past-save. Reactive — reading in <c>Render()</c>
    /// subscribes to changes.
    /// </summary>
    [Signal]
    public bool IsDirty
    {
        get
        {
            SignalTracker.RecordRead(isDirtySource);

            if (!savedIndex.HasValue)
            {
                return undoList.Count > 0;
            }

            return undoList.Count != savedIndex.Value;
        }
    }

    private void EnforceMaxDepth()
    {
        while (undoList.Count > maxDepth)
        {
            undoList.RemoveAt(0);

            if (savedIndex.HasValue)
            {
                savedIndex = savedIndex.Value - 1;
                if (savedIndex.Value < 0)
                {
                    savedIndex = null;
                }
            }
        }
    }

    private void NotifyStateChanged()
    {
        SignalTracker.NotifyWrite(canUndoSource);
        SignalTracker.NotifyWrite(canRedoSource);
        SignalTracker.NotifyWrite(undoDescriptionSource);
        SignalTracker.NotifyWrite(redoDescriptionSource);
        SignalTracker.NotifyWrite(isDirtySource);
    }

    private void EndBatch(string description)
    {
        batchDepth--;
        if (batchDepth > 0)
        {
            return;
        }

        var commands = batchCommands!;
        batchCommands = null;

        if (commands.Count == 0)
        {
            return;
        }

        var batchCommand = new BatchCommand(description, commands);
        var countBefore = undoList.Count;
        undoList.Add(batchCommand);
        redoList.Clear();

        if (savedIndex.HasValue && savedIndex.Value > countBefore)
        {
            savedIndex = null;
        }

        EnforceMaxDepth();
        NotifyStateChanged();
    }

    private sealed class BatchScope : IDisposable
    {
        private readonly UndoStack stack;
        private readonly string description;
        private bool disposed;

        internal BatchScope(UndoStack stack, string description)
        {
            this.stack = stack;
            this.description = description;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            stack.EndBatch(description);
        }
    }

    private sealed class BatchCommand : IUndoCommand
    {
        private readonly List<IUndoCommand> commands;

        public string Description { get; }

        internal BatchCommand(string description, List<IUndoCommand> commands)
        {
            Description = description;
            this.commands = commands;
        }

        public void Execute()
        {
            foreach (var command in commands)
            {
                command.Execute();
            }
        }

        public void Undo()
        {
            for (var i = commands.Count - 1; i >= 0; i--)
            {
                commands[i].Undo();
            }
        }
    }
}

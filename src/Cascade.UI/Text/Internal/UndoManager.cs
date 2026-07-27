namespace Cascade.UI;

/// <summary>
/// Undo/redo stack for text editing operations. Records each edit as an
/// undoable operation and merges consecutive character typing into single
/// undo groups. Groups break on pause (&gt; 300 ms), whitespace insertion,
/// deletion, or cursor jumps. Memory-bounded via configurable max depth.
/// </summary>
internal sealed class UndoManager
{
    /// <summary>
    /// A single edit operation that can be undone/redone.
    /// </summary>
    internal readonly record struct EditOperation(
        int Offset,
        string RemovedText,
        string InsertedText,
        TextSelection SelectionBefore,
        TextSelection SelectionAfter
    );

    /// <summary>
    /// A group of related operations that undo/redo as a unit.
    /// </summary>
    internal sealed class UndoGroup
    {
        internal readonly List<EditOperation> Operations = new();
        internal long TimestampTicks;
    }

    const long MergeTimeoutTicks = 300;

    readonly List<UndoGroup> undoStack = new();
    readonly List<UndoGroup> redoStack = new();
    readonly int maxDepth;

    UndoGroup? openGroup;

    internal UndoManager(int maxDepth = 1000)
    {
        this.maxDepth = maxDepth;
    }

    internal bool CanUndo => openGroup != null || undoStack.Count > 0;
    internal bool CanRedo => redoStack.Count > 0;

    /// <summary>Records an edit, merging it into the current group when appropriate.</summary>
    internal void RecordEdit(EditOperation op)
    {
        // Any new edit invalidates the redo stack
        redoStack.Clear();

        if (openGroup != null && CanMerge(openGroup, op))
        {
            MergeInto(openGroup, op);
            return;
        }

        SealOpenGroup();
        openGroup = new UndoGroup { TimestampTicks = Environment.TickCount64 };
        openGroup.Operations.Add(op);
    }

    /// <summary>Forces the current open group to be sealed so the next edit starts fresh.</summary>
    internal void BreakGroup()
    {
        SealOpenGroup();
    }

    /// <summary>Pops the most recent undo group.</summary>
    internal bool TryUndo(out UndoGroup? group)
    {
        SealOpenGroup();
        if (undoStack.Count == 0)
        {
            group = null;
            return false;
        }

        group = undoStack[^1];
        undoStack.RemoveAt(undoStack.Count - 1);
        redoStack.Add(group);
        return true;
    }

    /// <summary>Pops the most recent redo group.</summary>
    internal bool TryRedo(out UndoGroup? group)
    {
        if (redoStack.Count == 0)
        {
            group = null;
            return false;
        }

        group = redoStack[^1];
        redoStack.RemoveAt(redoStack.Count - 1);
        undoStack.Add(group);
        return true;
    }

    /// <summary>Clears all undo/redo history.</summary>
    internal void Clear()
    {
        undoStack.Clear();
        redoStack.Clear();
        openGroup = null;
    }

    // ── Private helpers ─────────────────────────────────────────────────

    void SealOpenGroup()
    {
        if (openGroup == null)
        {
            return;
        }

        undoStack.Add(openGroup);
        openGroup = null;

        // Enforce max depth by removing the oldest groups
        while (undoStack.Count > maxDepth)
        {
            undoStack.RemoveAt(0);
        }
    }

    static bool CanMerge(UndoGroup group, EditOperation op)
    {
        if (group.Operations.Count == 0)
        {
            return false;
        }

        // Time check: break group after 300ms pause
        long elapsed = Environment.TickCount64 - group.TimestampTicks;
        if (elapsed > MergeTimeoutTicks)
        {
            return false;
        }

        // Only merge single-character inserts (regular typing)
        if (op.InsertedText.Length != 1)
        {
            return false;
        }

        // Deletions always start a new group
        if (op.RemovedText.Length > 0)
        {
            return false;
        }

        // Whitespace insertion breaks the group
        if (char.IsWhiteSpace(op.InsertedText[0]))
        {
            return false;
        }

        // The previous operation must also be a pure insert
        var last = group.Operations[^1];
        if (last.RemovedText.Length > 0)
        {
            return false;
        }

        // Must be adjacent (cursor jump = new group)
        int expectedOffset = last.Offset + last.InsertedText.Length;
        if (op.Offset != expectedOffset)
        {
            return false;
        }

        return true;
    }

    static void MergeInto(UndoGroup group, EditOperation op)
    {
        var last = group.Operations[^1];
        group.Operations[^1] = last with
        {
            InsertedText = last.InsertedText + op.InsertedText,
            SelectionAfter = op.SelectionAfter,
        };
        group.TimestampTicks = Environment.TickCount64;
    }
}

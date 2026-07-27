namespace Cascade.UI;

/// <summary>
/// The stateful text editing coordinator. Holds a document and selection,
/// responds to editing commands, and coordinates IME composition. Does not
/// know about pixels, focus, or visual rendering — those are the control's
/// responsibility.
/// </summary>
/// <remarks>
/// This is a pure C# class with no platform dependencies. Platform-specific
/// behavior (IME, OS text services) is injected via <see cref="IPlatformTextInput"/>.
/// The engine is fully testable without a window, GPU, or running application.
/// </remarks>
public sealed class TextEditingEngine
{
    readonly TextEditingOptions options;
    readonly UndoManager undoManager;

    TextDocument document;
    TextSelection selection;
    bool isReadOnly;
    bool isUndoRedoing;

    // IME composition state
    TextComposition? activeComposition;
    int compositionAnchor; // document offset where composition text starts

    // ── Construction ────────────────────────────────────────────────────

    /// <summary>Creates a new editing engine for the given document and options.</summary>
    public TextEditingEngine(TextDocument document, TextEditingOptions options)
    {
        this.document = document;
        this.options = options;
        this.undoManager = new UndoManager();
        this.selection = TextSelection.Collapsed(0);
    }

    // ── State ───────────────────────────────────────────────────────────

    /// <summary>The backing document being edited.</summary>
    public TextDocument Document
    {
        get { return document; }
    }

    /// <summary>The current selection (collapsed = caret, expanded = highlighted range).</summary>
    public TextSelection Selection
    {
        get { return selection; }
        set { SetSelection(value); }
    }

    /// <summary>When true, the engine rejects all editing commands.</summary>
    public bool IsReadOnly
    {
        get { return isReadOnly; }
        set { isReadOnly = value; }
    }

    // ── IME state (read-only to consumers) ──────────────────────────────

    /// <summary>True when an IME composition session is active.</summary>
    public bool IsComposing
    {
        get { return activeComposition != null; }
    }

    /// <summary>The current IME composition, or null when not composing.</summary>
    public TextComposition? ActiveComposition
    {
        get { return activeComposition; }
    }

    // ── Text editing commands ───────────────────────────────────────────

    /// <summary>Inserts text at the current selection, replacing any selected text.</summary>
    public void InsertText(ReadOnlySpan<char> text)
    {
        if (isReadOnly || IsComposing)
        {
            return;
        }

        var effectiveText = text;
        char[]? truncatedBuffer = null;

        // MaxLength enforcement
        if (options.MaxLength > 0 && !text.IsEmpty)
        {
            int currentLen = document.Length - selection.Length;
            int allowed = options.MaxLength - currentLen;
            if (allowed <= 0 && selection.IsCollapsed)
            {
                return;
            }
            if (text.Length > allowed && allowed > 0)
            {
                truncatedBuffer = new char[allowed];
                text.Slice(0, allowed).CopyTo(truncatedBuffer);
                effectiveText = truncatedBuffer;
            }
            else if (allowed <= 0)
            {
                effectiveText = ReadOnlySpan<char>.Empty;
            }
        }

        if (effectiveText.IsEmpty && selection.IsCollapsed)
        {
            return;
        }

        string removedText = selection.IsCollapsed ? "" : GetSelectedText();
        var selBefore = selection;
        int editOffset = selection.Start;

        // Apply edit
        if (selection.IsCollapsed)
        {
            if (!effectiveText.IsEmpty)
            {
                // Overwrite mode: replace character at caret instead of inserting
                if (options.OverwriteMode && selection.Focus.Offset < document.Length)
                {
                    int overwriteLen = Math.Min(effectiveText.Length,
                        document.Length - selection.Focus.Offset);
                    string overwritten = document.Slice(editOffset, overwriteLen).ToString();
                    document.Replace(editOffset, overwriteLen, effectiveText);
                    removedText = overwritten;
                }
                else
                {
                    document.Insert(editOffset, effectiveText);
                }
            }
        }
        else
        {
            document.Replace(editOffset, selection.Length, effectiveText);
        }

        int newOffset = editOffset + effectiveText.Length;
        var selAfter = TextSelection.Collapsed(newOffset);

        if (!isUndoRedoing)
        {
            undoManager.RecordEdit(new UndoManager.EditOperation(
                editOffset, removedText, new string(effectiveText), selBefore, selAfter));
        }

        selection = selAfter;
        OnTextChanged(new TextChangeEvent(editOffset, removedText.Length,
            effectiveText.Length, effectiveText.ToArray()));
        OnSelectionChanged(selection);
    }

    /// <summary>Deletes the character before the caret (Backspace).</summary>
    public void DeleteBackward()
    {
        if (isReadOnly || IsComposing)
        {
            return;
        }

        if (!selection.IsCollapsed)
        {
            InsertText(ReadOnlySpan<char>.Empty);
            return;
        }

        int pos = selection.Focus.Offset;
        if (pos == 0)
        {
            return;
        }

        int deleteLen = PreviousCharLength(pos);
        DeleteRangeInternal(pos - deleteLen, deleteLen);
    }

    /// <summary>Deletes the character after the caret (Delete key).</summary>
    public void DeleteForward()
    {
        if (isReadOnly || IsComposing)
        {
            return;
        }

        if (!selection.IsCollapsed)
        {
            InsertText(ReadOnlySpan<char>.Empty);
            return;
        }

        int pos = selection.Focus.Offset;
        if (pos >= document.Length)
        {
            return;
        }

        int deleteLen = NextCharLength(pos);
        DeleteRangeInternal(pos, deleteLen);
    }

    /// <summary>Deletes the word before the caret (Ctrl+Backspace / Option+Delete).</summary>
    public void DeleteWordBackward()
    {
        if (isReadOnly || IsComposing)
        {
            return;
        }

        if (!selection.IsCollapsed)
        {
            InsertText(ReadOnlySpan<char>.Empty);
            return;
        }

        int pos = selection.Focus.Offset;
        int wordStart = WordBoundary.FindWordStart(document, pos);
        if (wordStart < pos)
        {
            undoManager.BreakGroup();
            DeleteRangeInternal(wordStart, pos - wordStart);
        }
    }

    /// <summary>Deletes the word after the caret (Ctrl+Delete / Option+Fn+Delete).</summary>
    public void DeleteWordForward()
    {
        if (isReadOnly || IsComposing)
        {
            return;
        }

        if (!selection.IsCollapsed)
        {
            InsertText(ReadOnlySpan<char>.Empty);
            return;
        }

        int pos = selection.Focus.Offset;
        int wordEnd = WordBoundary.FindWordEnd(document, pos);
        if (wordEnd > pos)
        {
            undoManager.BreakGroup();
            DeleteRangeInternal(pos, wordEnd - pos);
        }
    }

    /// <summary>Deletes from the caret to the start of the line (Cmd+Backspace on macOS).</summary>
    public void DeleteToLineStart()
    {
        if (isReadOnly || IsComposing)
        {
            return;
        }

        if (!selection.IsCollapsed)
        {
            InsertText(ReadOnlySpan<char>.Empty);
            return;
        }

        int pos = selection.Focus.Offset;
        int lineStart = GetLineStartOffset(pos);
        if (lineStart < pos)
        {
            undoManager.BreakGroup();
            DeleteRangeInternal(lineStart, pos - lineStart);
        }
    }

    /// <summary>Deletes from the caret to the end of the line (Cmd+Delete on macOS / Ctrl+K).</summary>
    public void DeleteToLineEnd()
    {
        if (isReadOnly || IsComposing)
        {
            return;
        }

        if (!selection.IsCollapsed)
        {
            InsertText(ReadOnlySpan<char>.Empty);
            return;
        }

        int pos = selection.Focus.Offset;
        int lineEnd = GetLineEndOffset(pos);
        if (lineEnd > pos)
        {
            undoManager.BreakGroup();
            DeleteRangeInternal(pos, lineEnd - pos);
        }
    }

    // ── Selection ───────────────────────────────────────────────────────

    /// <summary>Selects all text in the document.</summary>
    public void SelectAll()
    {
        SetSelection(TextSelection.All(document));
    }

    /// <summary>Selects the word at the given document offset (double-click).</summary>
    public void SelectWord(int offset)
    {
        var (start, end) = WordBoundary.WordAt(document, offset);
        SetSelection(new TextSelection(new TextPosition(start), new TextPosition(end)));
    }

    /// <summary>Selects the entire line at the given index (triple-click in TextArea).</summary>
    public void SelectLine(int lineIndex)
    {
        if (lineIndex < 0 || lineIndex >= document.LineCount)
        {
            return;
        }

        var line = document.GetLine(lineIndex);
        SetSelection(new TextSelection(
            new TextPosition(line.Start),
            new TextPosition(line.Start + line.LengthWithTerminator)));
    }

    // ── Cursor movement ─────────────────────────────────────────────────

    /// <summary>
    /// Moves the caret in the specified direction. When <paramref name="extendSelection"/>
    /// is true (Shift held), the anchor stays fixed and the focus moves, extending the selection.
    /// </summary>
    public void MoveCaret(CaretMovement movement, bool extendSelection)
        => MoveCaret(movement, extendSelection, null);

    /// <summary>
    /// Moves the caret. When <paramref name="layout"/> is supplied, the visual
    /// arrow keys (<see cref="CaretMovement.Left"/>/<see cref="CaretMovement.Right"/>)
    /// move by <em>visual</em> position so they read correctly in RTL and mixed
    /// (bidi) text — Left always moves the caret leftward on screen, which inside an
    /// RTL run is the logically-next character. Without a layout (or for logical
    /// movements like <see cref="CaretMovement.PreviousCharacter"/>) movement stays
    /// purely logical, preserving the existing behaviour.
    /// </summary>
    public void MoveCaret(CaretMovement movement, bool extendSelection, TextLayoutResult? layout)
    {
        // For Left/Right without extend: collapse selection to the visually-correct
        // edge. In RTL text the visual-left edge is the logical end, so collapse by
        // visual side when a layout is available.
        if (!extendSelection && !selection.IsCollapsed)
        {
            if (movement is CaretMovement.Left or CaretMovement.PreviousCharacter)
            {
                SetSelection(TextSelection.Collapsed(CollapseEdge(movement, layout, toLeft: true)));
                return;
            }

            if (movement is CaretMovement.Right or CaretMovement.NextCharacter)
            {
                SetSelection(TextSelection.Collapsed(CollapseEdge(movement, layout, toLeft: false)));
                return;
            }
        }

        int focus = selection.Focus.Offset;
        int newOffset =
            layout is not null && movement is CaretMovement.Left or CaretMovement.Right
                ? ComputeVisualOffset(movement == CaretMovement.Left, focus, layout)
                : ComputeNewOffset(movement, focus);

        if (extendSelection)
        {
            SetSelection(new TextSelection(selection.Anchor, new TextPosition(newOffset)));
        }
        else
        {
            SetSelection(TextSelection.Collapsed(newOffset));
        }
    }

    /// <summary>
    /// Chooses the offset to collapse a selection onto for a visual Left/Right move:
    /// the visually-left or visually-right end of the selection. Falls back to the
    /// logical start/end when no layout is available.
    /// </summary>
    int CollapseEdge(CaretMovement movement, TextLayoutResult? layout, bool toLeft)
    {
        if (layout is null)
        {
            return toLeft ? selection.Start : selection.End;
        }
        var startInfo = layout.GetCaretInfo(selection.Start);
        var endInfo = layout.GetCaretInfo(selection.End);
        // Pick the endpoint with the smaller (left) / larger (right) screen X.
        bool startIsLeft = startInfo.X <= endInfo.X;
        return toLeft
            ? (startIsLeft ? selection.Start : selection.End)
            : (startIsLeft ? selection.End : selection.Start);
    }

    /// <summary>
    /// Visual Left/Right: step to the adjacent caret stop on the focus line, or
    /// cross to the neighbouring line at the line's visual edge.
    /// </summary>
    int ComputeVisualOffset(bool left, int focus, TextLayoutResult layout)
    {
        if (layout.Lines.Count == 0)
        {
            return ComputeNewOffset(left ? CaretMovement.Left : CaretMovement.Right, focus);
        }

        int lineIdx = layout.GetLineIndexForOffset(focus);
        var line = layout.Lines[lineIdx];

        int neighbor = VisualCaret.VisualNeighbor(line, focus, left);
        if (neighbor >= 0)
        {
            return neighbor;
        }

        // At the line's visual edge: continue onto the adjacent line.
        if (left && lineIdx > 0)
        {
            var prev = layout.Lines[lineIdx - 1];
            return prev.TextStart + prev.TextLength;
        }
        if (!left && lineIdx < layout.Lines.Count - 1)
        {
            return layout.Lines[lineIdx + 1].TextStart;
        }
        return focus; // document visual edge — no move
    }

    // ── Clipboard ───────────────────────────────────────────────────────

    /// <summary>Returns the currently selected text, or an empty string if collapsed.</summary>
    public string GetSelectedText()
    {
        if (selection.IsCollapsed)
        {
            return "";
        }
        return document.Slice(selection.Start, selection.Length).ToString();
    }

    /// <summary>Copies the selection to the clipboard and deletes it.</summary>
    public void Cut()
    {
        if (isReadOnly || selection.IsCollapsed)
        {
            return;
        }

        undoManager.BreakGroup();
        InsertText(ReadOnlySpan<char>.Empty);
    }

    /// <summary>Copies the selection to the clipboard.</summary>
    public void Copy()
    {
        // The engine does not own clipboard access. The control reads
        // GetSelectedText() and places it on the OS clipboard.
        // This method exists for API completeness; clipboard interaction
        // is the control's responsibility via GetSelectedText().
        _ = selection;
    }

    /// <summary>Inserts text from the clipboard, replacing any selection.</summary>
    public void Paste(string text)
    {
        if (isReadOnly)
        {
            return;
        }

        undoManager.BreakGroup();
        InsertText(text.AsSpan());
        undoManager.BreakGroup();
    }

    // ── Undo/redo ───────────────────────────────────────────────────────

    /// <summary>Undoes the last editing operation and restores the prior selection.</summary>
    public void Undo()
    {
        if (!undoManager.TryUndo(out var group) || group == null)
        {
            return;
        }

        isUndoRedoing = true;
        try
        {
            // Apply operations in reverse
            for (int i = group.Operations.Count - 1; i >= 0; i--)
            {
                var op = group.Operations[i];
                if (op.InsertedText.Length > 0)
                {
                    document.Delete(op.Offset, op.InsertedText.Length);
                }
                if (op.RemovedText.Length > 0)
                {
                    document.Insert(op.Offset, op.RemovedText.AsSpan());
                }
            }

            selection = group.Operations[0].SelectionBefore;
            OnTextChanged(default);
            OnSelectionChanged(selection);
        }
        finally
        {
            isUndoRedoing = false;
        }
    }

    /// <summary>Redoes the last undone editing operation.</summary>
    public void Redo()
    {
        if (!undoManager.TryRedo(out var group) || group == null)
        {
            return;
        }

        isUndoRedoing = true;
        try
        {
            foreach (var op in group.Operations)
            {
                if (op.RemovedText.Length > 0)
                {
                    document.Delete(op.Offset, op.RemovedText.Length);
                }
                if (op.InsertedText.Length > 0)
                {
                    document.Insert(op.Offset, op.InsertedText.AsSpan());
                }
            }

            selection = group.Operations[^1].SelectionAfter;
            OnTextChanged(default);
            OnSelectionChanged(selection);
        }
        finally
        {
            isUndoRedoing = false;
        }
    }

    /// <summary>True when there are operations available to undo.</summary>
    public bool CanUndo
    {
        get { return undoManager.CanUndo; }
    }

    /// <summary>True when there are operations available to redo.</summary>
    public bool CanRedo
    {
        get { return undoManager.CanRedo; }
    }

    // ── IME coordination (called by platform adapter, not by controls) ──

    /// <summary>Enters composition mode with the given initial composition state.</summary>
    public void BeginComposition(TextComposition composition)
    {
        if (isReadOnly)
        {
            return;
        }

        // Record anchor before composition begins
        compositionAnchor = selection.IsCollapsed
            ? selection.Focus.Offset
            : selection.Start;

        // Delete any selected text first
        if (!selection.IsCollapsed)
        {
            undoManager.BreakGroup();
            string removed = GetSelectedText();
            document.Delete(selection.Start, selection.Length);
            selection = TextSelection.Collapsed(selection.Start);
            compositionAnchor = selection.Focus.Offset;
        }

        // Insert provisional composition text
        if (composition.Text.Length > 0)
        {
            document.Insert(compositionAnchor, composition.Text.AsSpan());
        }

        activeComposition = composition;
        OnCompositionChanged(activeComposition);
    }

    /// <summary>Updates the active composition with new text, cursor, or segments.</summary>
    public void UpdateComposition(TextComposition composition)
    {
        if (!IsComposing)
        {
            return;
        }

        // Remove old composition text
        int oldLen = activeComposition!.Value.Text.Length;
        if (oldLen > 0)
        {
            document.Delete(compositionAnchor, oldLen);
        }

        // Insert new composition text
        if (composition.Text.Length > 0)
        {
            document.Insert(compositionAnchor, composition.Text.AsSpan());
        }

        activeComposition = composition;
        selection = TextSelection.Collapsed(compositionAnchor + composition.CursorOffset);
        OnCompositionChanged(activeComposition);
    }

    /// <summary>Commits the composition, inserting the final text into the document.</summary>
    public void CommitComposition(string committedText)
    {
        if (!IsComposing)
        {
            return;
        }

        // Remove provisional composition text
        int oldLen = activeComposition!.Value.Text.Length;
        if (oldLen > 0)
        {
            document.Delete(compositionAnchor, oldLen);
        }

        // Insert committed text
        var selBefore = TextSelection.Collapsed(compositionAnchor);
        if (committedText.Length > 0)
        {
            document.Insert(compositionAnchor, committedText.AsSpan());
        }

        int newOffset = compositionAnchor + committedText.Length;
        var selAfter = TextSelection.Collapsed(newOffset);

        // Record as undoable operation
        undoManager.RecordEdit(new UndoManager.EditOperation(
            compositionAnchor, "", committedText, selBefore, selAfter));

        activeComposition = null;
        selection = selAfter;

        OnCompositionChanged(null);
        OnTextChanged(new TextChangeEvent(compositionAnchor, 0,
            committedText.Length, committedText.AsMemory()));
        OnSelectionChanged(selection);
    }

    /// <summary>Cancels the active composition, removing provisional text.</summary>
    public void CancelComposition()
    {
        if (!IsComposing)
        {
            return;
        }

        // Remove provisional composition text
        int oldLen = activeComposition!.Value.Text.Length;
        if (oldLen > 0)
        {
            document.Delete(compositionAnchor, oldLen);
        }

        activeComposition = null;
        selection = TextSelection.Collapsed(compositionAnchor);

        OnCompositionChanged(null);
        OnSelectionChanged(selection);
    }

    // ── Events ──────────────────────────────────────────────────────────

    /// <summary>Raised after any text modification.</summary>
    public event Action<TextChangeEvent>? TextChanged;

    /// <summary>Raised after the selection or caret position changes.</summary>
    public event Action<TextSelection>? SelectionChanged;

    /// <summary>Raised when composition state changes (begin, update, commit, cancel).</summary>
    public event Action<TextComposition?>? CompositionChanged;

    // ── Query (used by controls for rendering) ──────────────────────────

    /// <summary>
    /// Computes the selection rectangles for the current selection against
    /// the given text layout. Returns one rect per visual line in the selection.
    /// </summary>
    public IReadOnlyList<TextSelectionRect> GetSelectionRects(TextLayoutResult layout)
    {
        var rects = new List<TextSelectionRect>();

        if (selection.IsCollapsed || layout.Lines.Count == 0)
        {
            return rects;
        }

        int selStart = selection.Start;
        int selEnd = selection.End;

        for (int i = 0; i < layout.Lines.Count; i++)
        {
            var line = layout.Lines[i];
            int lineStart = line.TextStart;
            int lineEnd = lineStart + line.TextLength;

            if (lineEnd <= selStart || lineStart >= selEnd)
            {
                continue;
            }

            int overlapStart = Math.Max(selStart, lineStart);
            int overlapEnd = Math.Min(selEnd, lineEnd);

            // WP-3531: a logical selection range maps to one or more *visual* spans
            // (one in pure-LTR/RTL text, several when it straddles bidi runs). Build
            // a rect per contiguous visual span from the selected glyphs' X-intervals
            // so RTL/mixed selections don't highlight the gaps between runs.
            var intervals = new List<(float A, float B)>();
            foreach (var g in line.Glyphs)
            {
                if (g.ClusterIndex >= overlapStart && g.ClusterIndex < overlapEnd)
                {
                    intervals.Add((g.X, g.X + g.AdvanceWidth));
                }
            }

            if (intervals.Count == 0)
            {
                // No glyphs in range (e.g. selecting only trailing whitespace): fall
                // back to a zero-to-caret span so the line still shows selection.
                float sx = VisualCaret.XForOffset(line, overlapStart);
                float ex = VisualCaret.XForOffset(line, overlapEnd);
                if (ex < sx) { (sx, ex) = (ex, sx); }
                if (ex > sx) { rects.Add(new TextSelectionRect(line.X + sx, line.Y, ex - sx, line.Height, i)); }
                continue;
            }

            intervals.Sort(static (p, q) => p.A.CompareTo(q.A));
            float curA = intervals[0].A, curB = intervals[0].B;
            for (int k = 1; k < intervals.Count; k++)
            {
                if (intervals[k].A <= curB + 0.5f)
                {
                    curB = Math.Max(curB, intervals[k].B);
                }
                else
                {
                    rects.Add(new TextSelectionRect(line.X + curA, line.Y, curB - curA, line.Height, i));
                    curA = intervals[k].A;
                    curB = intervals[k].B;
                }
            }
            rects.Add(new TextSelectionRect(line.X + curA, line.Y, curB - curA, line.Height, i));
        }

        return rects;
    }

    /// <summary>
    /// Computes the caret rendering information for the current selection
    /// against the given text layout.
    /// </summary>
    public CaretInfo GetCaretInfo(TextLayoutResult layout)
    {
        return layout.GetCaretInfo(selection.Focus.Offset, selection.Focus.Affinity);
    }

    // ── Bidirectional text ──────────────────────────────────────────────

    /// <summary>
    /// Returns the base paragraph direction for the given line, as determined
    /// by the Unicode Bidirectional Algorithm.
    /// </summary>
    public TextDirection GetParagraphDirection(int lineIndex)
    {
        if (lineIndex < 0 || lineIndex >= document.LineCount)
        {
            return TextDirection.LeftToRight;
        }

        var line = document.GetLine(lineIndex);
        int end = line.Start + line.Length;

        // Find first strong directional character (UAX #9 rule P2/P3)
        for (int i = line.Start; i < end; i++)
        {
            char ch = document[i];
            if (IsRtlChar(ch))
            {
                return TextDirection.RightToLeft;
            }
            if (IsLtrStrongChar(ch))
            {
                return TextDirection.LeftToRight;
            }
        }

        return TextDirection.LeftToRight;
    }

    // ── Private implementation ──────────────────────────────────────────

    void SetSelection(TextSelection newSelection)
    {
        int docLen = document.Length;
        int clampedAnchor = Math.Clamp(newSelection.Anchor.Offset, 0, docLen);
        int clampedFocus = Math.Clamp(newSelection.Focus.Offset, 0, docLen);

        var clamped = new TextSelection(
            new TextPosition(clampedAnchor, newSelection.Anchor.Affinity),
            new TextPosition(clampedFocus, newSelection.Focus.Affinity));

        if (selection != clamped)
        {
            selection = clamped;
            OnSelectionChanged(selection);
        }
    }

    void DeleteRangeInternal(int offset, int length)
    {
        string removedText = document.Slice(offset, length).ToString();
        var selBefore = selection;

        document.Delete(offset, length);

        var selAfter = TextSelection.Collapsed(offset);

        if (!isUndoRedoing)
        {
            undoManager.RecordEdit(new UndoManager.EditOperation(
                offset, removedText, "", selBefore, selAfter));
        }

        selection = selAfter;
        OnTextChanged(new TextChangeEvent(offset, length, 0, ReadOnlyMemory<char>.Empty));
        OnSelectionChanged(selection);
    }

    int ComputeNewOffset(CaretMovement movement, int focus)
    {
        return movement switch
        {
            CaretMovement.Left or
            CaretMovement.PreviousCharacter => PreviousCharOffset(focus),

            CaretMovement.Right or
            CaretMovement.NextCharacter => NextCharOffset(focus),

            CaretMovement.PreviousWord => WordBoundary.FindWordStart(document, focus),
            CaretMovement.NextWord     => WordBoundary.FindWordEnd(document, focus),

            CaretMovement.LineStart => GetLineStartOffset(focus),
            CaretMovement.LineEnd   => GetLineEndOffset(focus),

            CaretMovement.PreviousLine => MoveToLine(focus, -1),
            CaretMovement.NextLine     => MoveToLine(focus, +1),

            CaretMovement.PreviousParagraph => WordBoundary.FindParagraphStart(document, focus),
            CaretMovement.NextParagraph     => WordBoundary.FindParagraphEnd(document, focus),

            CaretMovement.DocumentStart => 0,
            CaretMovement.DocumentEnd   => document.Length,

            CaretMovement.PageUp   => 0,
            CaretMovement.PageDown => document.Length,

            _ => focus,
        };
    }

    int PreviousCharOffset(int offset)
    {
        if (offset <= 0)
        {
            return 0;
        }

        return offset - PreviousCharLength(offset);
    }

    int NextCharOffset(int offset)
    {
        if (offset >= document.Length)
        {
            return document.Length;
        }

        return offset + NextCharLength(offset);
    }

    int PreviousCharLength(int offset)
    {
        if (offset <= 0)
        {
            return 0;
        }

        if (offset >= 2 &&
            char.IsLowSurrogate(document[offset - 1]) &&
            char.IsHighSurrogate(document[offset - 2]))
        {
            return 2;
        }

        // Handle \r\n as a single unit
        if (offset >= 2 && document[offset - 1] == '\n' && document[offset - 2] == '\r')
        {
            return 2;
        }

        return 1;
    }

    int NextCharLength(int offset)
    {
        if (offset >= document.Length)
        {
            return 0;
        }

        if (char.IsHighSurrogate(document[offset]) &&
            offset + 1 < document.Length &&
            char.IsLowSurrogate(document[offset + 1]))
        {
            return 2;
        }

        // Handle \r\n as a single unit
        if (document[offset] == '\r' &&
            offset + 1 < document.Length &&
            document[offset + 1] == '\n')
        {
            return 2;
        }

        return 1;
    }

    int GetLineStartOffset(int offset)
    {
        int lineIndex = document.GetLineIndexFromPosition(offset);
        return document.GetLine(lineIndex).Start;
    }

    int GetLineEndOffset(int offset)
    {
        int lineIndex = document.GetLineIndexFromPosition(offset);
        var line = document.GetLine(lineIndex);
        return line.Start + line.Length;
    }

    int MoveToLine(int focus, int delta)
    {
        int currentLine = document.GetLineIndexFromPosition(focus);
        int targetLine = currentLine + delta;

        if (targetLine < 0)
        {
            return 0;
        }

        if (targetLine >= document.LineCount)
        {
            return document.Length;
        }

        var current = document.GetLine(currentLine);
        int column = focus - current.Start;

        var target = document.GetLine(targetLine);
        return target.Start + Math.Min(column, target.Length);
    }

    static bool IsRtlChar(char ch)
    {
        // Arabic
        if (ch >= '\u0600' && ch <= '\u06FF') { return true; }
        // Hebrew
        if (ch >= '\u0590' && ch <= '\u05FF') { return true; }
        // Arabic Supplement
        if (ch >= '\u0750' && ch <= '\u077F') { return true; }
        // Arabic Extended-A
        if (ch >= '\u08A0' && ch <= '\u08FF') { return true; }
        return false;
    }

    static bool IsLtrStrongChar(char ch)
    {
        // Latin, Greek, Cyrillic, and most other scripts are LTR
        if (ch >= 'A' && ch <= 'Z') { return true; }
        if (ch >= 'a' && ch <= 'z') { return true; }
        if (ch >= '\u00C0' && ch <= '\u024F') { return true; }
        return false;
    }

    void OnTextChanged(TextChangeEvent e)
    {
        TextChanged?.Invoke(e);
    }

    void OnSelectionChanged(TextSelection sel)
    {
        SelectionChanged?.Invoke(sel);
    }

    void OnCompositionChanged(TextComposition? comp)
    {
        CompositionChanged?.Invoke(comp);
    }
}

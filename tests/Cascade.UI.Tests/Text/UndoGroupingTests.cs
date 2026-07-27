namespace Cascade.UI.Tests;

/// <summary>
/// Tests for undo grouping — consecutive typing merges into single undo groups,
/// with breaks on pause, whitespace, deletion, and cursor jumps.
/// </summary>
public class UndoGroupingTests
{
    static TextEditingEngine CreateEngine(string initialText = "")
    {
        return new TextEditingEngine(
            new TextDocument(initialText),
            new TextEditingOptions());
    }

    // ── Basic undo/redo ─────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task Undo_RevertsLastEdit()
    {
        var engine = CreateEngine();
        engine.InsertText("Hello".AsSpan());
        engine.Undo();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("");
    }

    [TUnit.Core.Test]
    public async Task Redo_ReappliesUndoneEdit()
    {
        var engine = CreateEngine();
        engine.InsertText("Hello".AsSpan());
        engine.Undo();
        engine.Redo();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello");
    }

    [TUnit.Core.Test]
    public async Task Undo_RestoresSelection()
    {
        var engine = CreateEngine("Hello");
        engine.Selection = TextSelection.Collapsed(5);
        engine.InsertText(" World".AsSpan());

        engine.Undo();

        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(5);
    }

    [TUnit.Core.Test]
    public async Task CanUndo_TrueAfterEdit()
    {
        var engine = CreateEngine();
        engine.InsertText("A".AsSpan());
        await TUnit.Assertions.Assert.That(engine.CanUndo).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task CanUndo_FalseWhenEmpty()
    {
        var engine = CreateEngine();
        await TUnit.Assertions.Assert.That(engine.CanUndo).IsFalse();
    }

    [TUnit.Core.Test]
    public async Task CanRedo_TrueAfterUndo()
    {
        var engine = CreateEngine();
        engine.InsertText("A".AsSpan());
        engine.Undo();
        await TUnit.Assertions.Assert.That(engine.CanRedo).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task CanRedo_FalseAfterNewEdit()
    {
        var engine = CreateEngine();
        engine.InsertText("A".AsSpan());
        engine.Undo();
        engine.InsertText("B".AsSpan());
        await TUnit.Assertions.Assert.That(engine.CanRedo).IsFalse();
    }

    // ── Consecutive typing merges ───────────────────────────────────────

    [TUnit.Core.Test]
    public async Task ConsecutiveTyping_MergesIntoSingleUndo()
    {
        var engine = CreateEngine();

        // Type characters rapidly (within merge window)
        engine.InsertText("H".AsSpan());
        engine.InsertText("e".AsSpan());
        engine.InsertText("l".AsSpan());
        engine.InsertText("l".AsSpan());
        engine.InsertText("o".AsSpan());

        // Single undo should revert all characters
        engine.Undo();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("");
    }

    [TUnit.Core.Test]
    public async Task ConsecutiveTyping_Redo_RestoresAllCharacters()
    {
        var engine = CreateEngine();
        engine.InsertText("H".AsSpan());
        engine.InsertText("e".AsSpan());
        engine.InsertText("l".AsSpan());
        engine.InsertText("l".AsSpan());
        engine.InsertText("o".AsSpan());

        engine.Undo();
        engine.Redo();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello");
    }

    // ── Whitespace breaks group ─────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task WhitespaceInsertion_BreaksUndoGroup()
    {
        var engine = CreateEngine();
        engine.InsertText("H".AsSpan());
        engine.InsertText("i".AsSpan());
        engine.InsertText(" ".AsSpan()); // whitespace breaks group

        // Undo should revert just the space
        engine.Undo();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hi");

        // Another undo should revert "Hi"
        engine.Undo();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("");
    }

    // ── Deletion breaks group ───────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task Deletion_BreaksUndoGroup()
    {
        var engine = CreateEngine("Hello");
        engine.Selection = TextSelection.Collapsed(5);

        engine.InsertText("X".AsSpan());
        engine.InsertText("Y".AsSpan());
        engine.DeleteBackward(); // deletion breaks group

        // Undo should revert the deletion
        engine.Undo();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("HelloXY");
    }

    // ── Cursor jump breaks group ────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task CursorJump_BreaksUndoGroup()
    {
        var engine = CreateEngine("Hello World");
        engine.Selection = TextSelection.Collapsed(5);

        engine.InsertText("A".AsSpan());
        engine.InsertText("B".AsSpan());

        // Jump cursor to a non-adjacent position
        engine.Selection = TextSelection.Collapsed(0);
        engine.InsertText("X".AsSpan());

        // Undo should revert "X" at position 0
        engine.Undo();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("HelloAB World");

        // Second undo should revert "AB"
        engine.Undo();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello World");
    }

    // ── Redo stack clears on new edit after undo ────────────────────────

    [TUnit.Core.Test]
    public async Task NewEditAfterUndo_ClearsRedoStack()
    {
        var engine = CreateEngine();
        engine.InsertText("Hello".AsSpan());
        engine.Undo();

        // New edit should clear redo
        engine.InsertText("World".AsSpan());
        await TUnit.Assertions.Assert.That(engine.CanRedo).IsFalse();
    }

    // ── Multi-character insert is its own group ─────────────────────────

    [TUnit.Core.Test]
    public async Task MultiCharacterInsert_DoesNotMerge()
    {
        var engine = CreateEngine();
        engine.InsertText("A".AsSpan());
        engine.InsertText("BC".AsSpan()); // multi-char = new group

        engine.Undo();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("A");
    }

    // ── Paste creates separate undo group ───────────────────────────────

    [TUnit.Core.Test]
    public async Task Paste_CreatesNewUndoGroup()
    {
        var engine = CreateEngine("Hello");
        engine.Selection = TextSelection.Collapsed(5);

        engine.InsertText("A".AsSpan());
        engine.Paste(" World");

        engine.Undo();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("HelloA");
    }

    // ── Multiple undo/redo cycles ───────────────────────────────────────

    [TUnit.Core.Test]
    public async Task MultipleUndoRedo_WorksCorrectly()
    {
        var engine = CreateEngine();
        engine.InsertText("First".AsSpan());
        engine.InsertText(" ".AsSpan());
        engine.InsertText("Second".AsSpan());

        engine.Undo(); // undo "Second"
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("First ");

        engine.Undo(); // undo " "
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("First");

        engine.Redo(); // redo " "
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("First ");

        engine.Redo(); // redo "Second"
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("First Second");
    }

    // ── Undo of delete-with-selection ────────────────────────────────────

    [TUnit.Core.Test]
    public async Task Undo_SelectionDelete_RestoresText()
    {
        var engine = CreateEngine("Hello World");
        engine.Selection = new TextSelection(new TextPosition(5), new TextPosition(11));
        engine.InsertText(ReadOnlySpan<char>.Empty);

        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello");

        engine.Undo();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello World");
    }

    // ── Undo of replace ─────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task Undo_Replace_RestoresOriginalText()
    {
        var engine = CreateEngine("Hello World");
        engine.Selection = new TextSelection(new TextPosition(6), new TextPosition(11));
        engine.InsertText("Cascade".AsSpan());

        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello Cascade");

        engine.Undo();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello World");
    }
}

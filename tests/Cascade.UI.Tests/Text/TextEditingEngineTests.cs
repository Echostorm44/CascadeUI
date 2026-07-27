namespace Cascade.UI.Tests;

/// <summary>
/// Tests for <see cref="TextEditingEngine"/> — all editing commands,
/// selection management, and caret movement.
/// </summary>
public class TextEditingEngineTests
{
    static TextEditingEngine CreateEngine(string initialText = "",
        TextEditingOptions? options = null)
    {
        var doc = new TextDocument(initialText);
        return new TextEditingEngine(doc, options ?? new TextEditingOptions());
    }

    // ── InsertText ──────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task InsertText_AtEmptyDocument_InsertsText()
    {
        var engine = CreateEngine();
        engine.InsertText("Hello".AsSpan());
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello");
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(5);
    }

    [TUnit.Core.Test]
    public async Task InsertText_ReplacesSelection()
    {
        var engine = CreateEngine("Hello World");
        engine.Selection = new TextSelection(new TextPosition(6), new TextPosition(11));
        engine.InsertText("Cascade".AsSpan());
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello Cascade");
    }

    [TUnit.Core.Test]
    public async Task InsertText_WhenReadOnly_DoesNothing()
    {
        var engine = CreateEngine("Test");
        engine.IsReadOnly = true;
        engine.InsertText("X".AsSpan());
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Test");
    }

    [TUnit.Core.Test]
    public async Task InsertText_FiresTextChangedEvent()
    {
        var engine = CreateEngine();
        bool fired = false;
        engine.TextChanged += _ => fired = true;
        engine.InsertText("A".AsSpan());
        await TUnit.Assertions.Assert.That(fired).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task InsertText_MaxLength_TruncatesInsert()
    {
        var engine = CreateEngine("AB", new TextEditingOptions { MaxLength = 5 });
        engine.Selection = TextSelection.Collapsed(2);
        engine.InsertText("CDEFGH".AsSpan());
        await TUnit.Assertions.Assert.That(engine.Document.Length).IsEqualTo(5);
    }

    // ── Delete backward/forward ─────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task DeleteBackward_RemovesPreviousCharacter()
    {
        var engine = CreateEngine("Hello");
        engine.Selection = TextSelection.Collapsed(5);
        engine.DeleteBackward();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hell");
    }

    [TUnit.Core.Test]
    public async Task DeleteBackward_AtStart_DoesNothing()
    {
        var engine = CreateEngine("Hello");
        engine.Selection = TextSelection.Collapsed(0);
        engine.DeleteBackward();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello");
    }

    [TUnit.Core.Test]
    public async Task DeleteBackward_WithSelection_DeletesSelection()
    {
        var engine = CreateEngine("Hello World");
        engine.Selection = new TextSelection(new TextPosition(5), new TextPosition(11));
        engine.DeleteBackward();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello");
    }

    [TUnit.Core.Test]
    public async Task DeleteForward_RemovesNextCharacter()
    {
        var engine = CreateEngine("Hello");
        engine.Selection = TextSelection.Collapsed(0);
        engine.DeleteForward();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("ello");
    }

    [TUnit.Core.Test]
    public async Task DeleteForward_AtEnd_DoesNothing()
    {
        var engine = CreateEngine("Hello");
        engine.Selection = TextSelection.Collapsed(5);
        engine.DeleteForward();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello");
    }

    // ── DeleteWord ──────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task DeleteWordBackward_DeletesWord()
    {
        var engine = CreateEngine("Hello World");
        engine.Selection = TextSelection.Collapsed(11);
        engine.DeleteWordBackward();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello ");
    }

    [TUnit.Core.Test]
    public async Task DeleteWordForward_DeletesWord()
    {
        var engine = CreateEngine("Hello World");
        engine.Selection = TextSelection.Collapsed(6);
        engine.DeleteWordForward();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello ");
    }

    // ── DeleteToLine ────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task DeleteToLineStart_DeletesFromCaretToLineStart()
    {
        var engine = CreateEngine("Hello World");
        engine.Selection = TextSelection.Collapsed(5);
        engine.DeleteToLineStart();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo(" World");
    }

    [TUnit.Core.Test]
    public async Task DeleteToLineEnd_DeletesFromCaretToLineEnd()
    {
        var engine = CreateEngine("Hello World");
        engine.Selection = TextSelection.Collapsed(5);
        engine.DeleteToLineEnd();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello");
    }

    // ── SelectAll ───────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task SelectAll_SelectsEntireDocument()
    {
        var engine = CreateEngine("Hello World");
        engine.SelectAll();
        await TUnit.Assertions.Assert.That(engine.Selection.Start).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(engine.Selection.End).IsEqualTo(11);
        await TUnit.Assertions.Assert.That(engine.Selection.IsCollapsed).IsFalse();
    }

    // ── SelectWord ──────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task SelectWord_SelectsWordAtOffset()
    {
        var engine = CreateEngine("Hello World");
        engine.SelectWord(7);
        await TUnit.Assertions.Assert.That(engine.Selection.Start).IsEqualTo(6);
        await TUnit.Assertions.Assert.That(engine.Selection.End).IsEqualTo(11);
    }

    // ── SelectLine ──────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task SelectLine_SelectsEntireLine()
    {
        var engine = CreateEngine("Line1\nLine2\nLine3");
        engine.SelectLine(1);
        await TUnit.Assertions.Assert.That(engine.Selection.Start).IsEqualTo(6);
        await TUnit.Assertions.Assert.That(engine.Selection.End).IsEqualTo(12);
    }

    // ── Caret movement ──────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task MoveCaret_Left_MovesOneCharacter()
    {
        var engine = CreateEngine("Hello");
        engine.Selection = TextSelection.Collapsed(3);
        engine.MoveCaret(CaretMovement.Left, false);
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(2);
    }

    [TUnit.Core.Test]
    public async Task MoveCaret_Right_MovesOneCharacter()
    {
        var engine = CreateEngine("Hello");
        engine.Selection = TextSelection.Collapsed(3);
        engine.MoveCaret(CaretMovement.Right, false);
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(4);
    }

    [TUnit.Core.Test]
    public async Task MoveCaret_Left_WithSelection_CollapsesToStart()
    {
        var engine = CreateEngine("Hello World");
        engine.Selection = new TextSelection(new TextPosition(2), new TextPosition(8));
        engine.MoveCaret(CaretMovement.Left, false);
        await TUnit.Assertions.Assert.That(engine.Selection.IsCollapsed).IsTrue();
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(2);
    }

    [TUnit.Core.Test]
    public async Task MoveCaret_Right_WithSelection_CollapsesToEnd()
    {
        var engine = CreateEngine("Hello World");
        engine.Selection = new TextSelection(new TextPosition(2), new TextPosition(8));
        engine.MoveCaret(CaretMovement.Right, false);
        await TUnit.Assertions.Assert.That(engine.Selection.IsCollapsed).IsTrue();
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(8);
    }

    [TUnit.Core.Test]
    public async Task MoveCaret_Left_ExtendSelection_ExtendsFocus()
    {
        var engine = CreateEngine("Hello");
        engine.Selection = TextSelection.Collapsed(3);
        engine.MoveCaret(CaretMovement.Left, true);
        await TUnit.Assertions.Assert.That(engine.Selection.Anchor.Offset).IsEqualTo(3);
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(2);
        await TUnit.Assertions.Assert.That(engine.Selection.IsCollapsed).IsFalse();
    }

    [TUnit.Core.Test]
    public async Task MoveCaret_PreviousWord_JumpsToWordStart()
    {
        var engine = CreateEngine("Hello World");
        engine.Selection = TextSelection.Collapsed(11);
        engine.MoveCaret(CaretMovement.PreviousWord, false);
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(6);
    }

    [TUnit.Core.Test]
    public async Task MoveCaret_NextWord_JumpsToWordEnd()
    {
        var engine = CreateEngine("Hello World");
        engine.Selection = TextSelection.Collapsed(0);
        engine.MoveCaret(CaretMovement.NextWord, false);
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(5);
    }

    [TUnit.Core.Test]
    public async Task MoveCaret_LineStart_GoesToLineStart()
    {
        var engine = CreateEngine("Hello\nWorld");
        engine.Selection = TextSelection.Collapsed(9);
        engine.MoveCaret(CaretMovement.LineStart, false);
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(6);
    }

    [TUnit.Core.Test]
    public async Task MoveCaret_LineEnd_GoesToLineEnd()
    {
        var engine = CreateEngine("Hello\nWorld");
        engine.Selection = TextSelection.Collapsed(6);
        engine.MoveCaret(CaretMovement.LineEnd, false);
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(11);
    }

    [TUnit.Core.Test]
    public async Task MoveCaret_DocumentStart_GoesToZero()
    {
        var engine = CreateEngine("Hello World");
        engine.Selection = TextSelection.Collapsed(5);
        engine.MoveCaret(CaretMovement.DocumentStart, false);
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task MoveCaret_DocumentEnd_GoesToEnd()
    {
        var engine = CreateEngine("Hello World");
        engine.Selection = TextSelection.Collapsed(0);
        engine.MoveCaret(CaretMovement.DocumentEnd, false);
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(11);
    }

    [TUnit.Core.Test]
    public async Task MoveCaret_PreviousLine_MovesToPreviousLine()
    {
        var engine = CreateEngine("Hello\nWorld\nTest");
        engine.Selection = TextSelection.Collapsed(9);
        engine.MoveCaret(CaretMovement.PreviousLine, false);
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(3);
    }

    [TUnit.Core.Test]
    public async Task MoveCaret_NextLine_MovesToNextLine()
    {
        var engine = CreateEngine("Hello\nWorld\nTest");
        engine.Selection = TextSelection.Collapsed(3);
        engine.MoveCaret(CaretMovement.NextLine, false);
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(9);
    }

    [TUnit.Core.Test]
    public async Task MoveCaret_PreviousParagraph_MovesToParagraphStart()
    {
        var engine = CreateEngine("Hello\nWorld\nTest");
        engine.Selection = TextSelection.Collapsed(9);
        engine.MoveCaret(CaretMovement.PreviousParagraph, false);
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(6);
    }

    [TUnit.Core.Test]
    public async Task MoveCaret_NextParagraph_MovesToParagraphEnd()
    {
        var engine = CreateEngine("Hello\nWorld\nTest");
        engine.Selection = TextSelection.Collapsed(6);
        engine.MoveCaret(CaretMovement.NextParagraph, false);
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(12);
    }

    // ── Clipboard ───────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task GetSelectedText_ReturnsSelectedRange()
    {
        var engine = CreateEngine("Hello World");
        engine.Selection = new TextSelection(new TextPosition(6), new TextPosition(11));
        await TUnit.Assertions.Assert.That(engine.GetSelectedText()).IsEqualTo("World");
    }

    [TUnit.Core.Test]
    public async Task GetSelectedText_Collapsed_ReturnsEmpty()
    {
        var engine = CreateEngine("Hello");
        engine.Selection = TextSelection.Collapsed(3);
        await TUnit.Assertions.Assert.That(engine.GetSelectedText()).IsEqualTo("");
    }

    [TUnit.Core.Test]
    public async Task Cut_RemovesSelectedText()
    {
        var engine = CreateEngine("Hello World");
        engine.Selection = new TextSelection(new TextPosition(5), new TextPosition(11));
        engine.Cut();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello");
    }

    [TUnit.Core.Test]
    public async Task Paste_InsertsTextAtCaret()
    {
        var engine = CreateEngine("Hello");
        engine.Selection = TextSelection.Collapsed(5);
        engine.Paste(" World");
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello World");
    }

    [TUnit.Core.Test]
    public async Task Paste_ReplacesSelection()
    {
        var engine = CreateEngine("Hello World");
        engine.Selection = new TextSelection(new TextPosition(6), new TextPosition(11));
        engine.Paste("Cascade");
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello Cascade");
    }

    // ── Surrogate pair handling ──────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task DeleteBackward_SurrogatePair_DeletesBothCodeUnits()
    {
        // 😀 is U+1F600, represented as surrogate pair \uD83D\uDE00
        var engine = CreateEngine("A\U0001F600B");
        engine.Selection = TextSelection.Collapsed(3); // after the emoji
        engine.DeleteBackward();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("AB");
    }

    [TUnit.Core.Test]
    public async Task DeleteForward_SurrogatePair_DeletesBothCodeUnits()
    {
        var engine = CreateEngine("A\U0001F600B");
        engine.Selection = TextSelection.Collapsed(1); // before the emoji
        engine.DeleteForward();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("AB");
    }

    [TUnit.Core.Test]
    public async Task MoveCaret_Right_SurrogatePair_SkipsBothCodeUnits()
    {
        var engine = CreateEngine("A\U0001F600B");
        engine.Selection = TextSelection.Collapsed(1); // before emoji
        engine.MoveCaret(CaretMovement.Right, false);
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(3);
    }

    // ── CRLF handling ───────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task DeleteBackward_CRLF_DeletesBothChars()
    {
        var engine = CreateEngine("Line1\r\nLine2");
        engine.Selection = TextSelection.Collapsed(7); // after \r\n
        engine.DeleteBackward();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Line1Line2");
    }
}

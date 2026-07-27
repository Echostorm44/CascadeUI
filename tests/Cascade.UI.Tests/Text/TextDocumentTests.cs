namespace Cascade.UI.Tests;

/// <summary>
/// Tests for <see cref="TextDocument"/> — insert, delete, replace, line
/// tracking, and automatic string-to-PieceTable promotion.
/// </summary>
public class TextDocumentTests
{
    // ── Construction ────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task EmptyDocument_HasZeroLength()
    {
        var doc = new TextDocument();
        await TUnit.Assertions.Assert.That(doc.Length).IsEqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task StringConstructor_SetsContent()
    {
        var doc = new TextDocument("Hello");
        await TUnit.Assertions.Assert.That(doc.Length).IsEqualTo(5);
        await TUnit.Assertions.Assert.That(doc.ToString()).IsEqualTo("Hello");
    }

    [TUnit.Core.Test]
    public async Task SpanConstructor_SetsContent()
    {
        ReadOnlySpan<char> text = "World".AsSpan();
        var doc = new TextDocument(text);
        await TUnit.Assertions.Assert.That(doc.ToString()).IsEqualTo("World");
    }

    // ── Indexer ─────────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task Indexer_ReturnsCorrectCharacters()
    {
        var doc = new TextDocument("ABC");
        await TUnit.Assertions.Assert.That(doc[0]).IsEqualTo('A');
        await TUnit.Assertions.Assert.That(doc[1]).IsEqualTo('B');
        await TUnit.Assertions.Assert.That(doc[2]).IsEqualTo('C');
    }

    // ── Slice ───────────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task Slice_ReturnsSubstring()
    {
        var doc = new TextDocument("Hello World");
        var slice = doc.Slice(6, 5);
        await TUnit.Assertions.Assert.That(slice.ToString()).IsEqualTo("World");
    }

    [TUnit.Core.Test]
    public async Task Slice_EmptyLength_ReturnsEmpty()
    {
        var doc = new TextDocument("Test");
        var slice = doc.Slice(2, 0);
        await TUnit.Assertions.Assert.That(slice.Length).IsEqualTo(0);
    }

    // ── Insert ──────────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task Insert_AtStart_PrependsText()
    {
        var doc = new TextDocument("World");
        doc.Insert(0, "Hello ".AsSpan());
        await TUnit.Assertions.Assert.That(doc.ToString()).IsEqualTo("Hello World");
    }

    [TUnit.Core.Test]
    public async Task Insert_AtEnd_AppendsText()
    {
        var doc = new TextDocument("Hello");
        doc.Insert(5, " World".AsSpan());
        await TUnit.Assertions.Assert.That(doc.ToString()).IsEqualTo("Hello World");
    }

    [TUnit.Core.Test]
    public async Task Insert_InMiddle_SplitsText()
    {
        var doc = new TextDocument("HWorld");
        doc.Insert(1, "ello ".AsSpan());
        await TUnit.Assertions.Assert.That(doc.ToString()).IsEqualTo("Hello World");
    }

    [TUnit.Core.Test]
    public async Task Insert_Empty_NoChange()
    {
        var doc = new TextDocument("Test");
        doc.Insert(2, ReadOnlySpan<char>.Empty);
        await TUnit.Assertions.Assert.That(doc.ToString()).IsEqualTo("Test");
    }

    [TUnit.Core.Test]
    public async Task Insert_FiresChangedEvent()
    {
        var doc = new TextDocument("AB");
        TextChangeEvent? received = null;
        doc.Changed += e => received = e;

        doc.Insert(1, "X".AsSpan());

        await TUnit.Assertions.Assert.That(received).IsNotNull();
        await TUnit.Assertions.Assert.That(received!.Value.Offset).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(received!.Value.OldLength).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(received!.Value.NewLength).IsEqualTo(1);
    }

    // ── Delete ──────────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task Delete_FromStart_RemovesPrefix()
    {
        var doc = new TextDocument("Hello World");
        doc.Delete(0, 6);
        await TUnit.Assertions.Assert.That(doc.ToString()).IsEqualTo("World");
    }

    [TUnit.Core.Test]
    public async Task Delete_FromEnd_RemovesSuffix()
    {
        var doc = new TextDocument("Hello World");
        doc.Delete(5, 6);
        await TUnit.Assertions.Assert.That(doc.ToString()).IsEqualTo("Hello");
    }

    [TUnit.Core.Test]
    public async Task Delete_InMiddle_RemovesRange()
    {
        var doc = new TextDocument("Hello Beautiful World");
        doc.Delete(5, 10);
        await TUnit.Assertions.Assert.That(doc.ToString()).IsEqualTo("Hello World");
    }

    [TUnit.Core.Test]
    public async Task Delete_ZeroLength_NoChange()
    {
        var doc = new TextDocument("Test");
        doc.Delete(2, 0);
        await TUnit.Assertions.Assert.That(doc.ToString()).IsEqualTo("Test");
    }

    // ── Replace ─────────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task Replace_SwapsText()
    {
        var doc = new TextDocument("Hello World");
        doc.Replace(6, 5, "Cascade".AsSpan());
        await TUnit.Assertions.Assert.That(doc.ToString()).IsEqualTo("Hello Cascade");
    }

    [TUnit.Core.Test]
    public async Task Replace_WithEmpty_DeletesRange()
    {
        var doc = new TextDocument("Hello Beautiful World");
        doc.Replace(5, 10, ReadOnlySpan<char>.Empty);
        await TUnit.Assertions.Assert.That(doc.ToString()).IsEqualTo("Hello World");
    }

    [TUnit.Core.Test]
    public async Task Replace_ZeroLength_InsertsText()
    {
        var doc = new TextDocument("HelloWorld");
        doc.Replace(5, 0, " ".AsSpan());
        await TUnit.Assertions.Assert.That(doc.ToString()).IsEqualTo("Hello World");
    }

    // ── Line tracking ───────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task LineCount_SingleLine_IsOne()
    {
        var doc = new TextDocument("Hello World");
        await TUnit.Assertions.Assert.That(doc.LineCount).IsEqualTo(1);
    }

    [TUnit.Core.Test]
    public async Task LineCount_MultipleLinesLF()
    {
        var doc = new TextDocument("Line1\nLine2\nLine3");
        await TUnit.Assertions.Assert.That(doc.LineCount).IsEqualTo(3);
    }

    [TUnit.Core.Test]
    public async Task LineCount_MultiplLinesCRLF()
    {
        var doc = new TextDocument("Line1\r\nLine2\r\nLine3");
        await TUnit.Assertions.Assert.That(doc.LineCount).IsEqualTo(3);
    }

    [TUnit.Core.Test]
    public async Task LineCount_TrailingNewline()
    {
        var doc = new TextDocument("Line1\nLine2\n");
        await TUnit.Assertions.Assert.That(doc.LineCount).IsEqualTo(3);
    }

    [TUnit.Core.Test]
    public async Task GetLine_ReturnsCorrectInfo()
    {
        var doc = new TextDocument("Hello\nWorld\nTest");

        var line0 = doc.GetLine(0);
        await TUnit.Assertions.Assert.That(line0.Start).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(line0.Length).IsEqualTo(5);
        await TUnit.Assertions.Assert.That(line0.LengthWithTerminator).IsEqualTo(6);
        await TUnit.Assertions.Assert.That(line0.LineIndex).IsEqualTo(0);

        var line1 = doc.GetLine(1);
        await TUnit.Assertions.Assert.That(line1.Start).IsEqualTo(6);
        await TUnit.Assertions.Assert.That(line1.Length).IsEqualTo(5);

        var line2 = doc.GetLine(2);
        await TUnit.Assertions.Assert.That(line2.Start).IsEqualTo(12);
        await TUnit.Assertions.Assert.That(line2.Length).IsEqualTo(4);
        await TUnit.Assertions.Assert.That(line2.LengthWithTerminator).IsEqualTo(4);
    }

    [TUnit.Core.Test]
    public async Task GetLineIndexFromPosition_ReturnsCorrectIndex()
    {
        var doc = new TextDocument("Hello\nWorld\nTest");

        await TUnit.Assertions.Assert.That(doc.GetLineIndexFromPosition(0)).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(doc.GetLineIndexFromPosition(3)).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(doc.GetLineIndexFromPosition(6)).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(doc.GetLineIndexFromPosition(12)).IsEqualTo(2);
    }

    [TUnit.Core.Test]
    public async Task GetLineIndexFromPosition_AfterEdit_RebuildsCache()
    {
        var doc = new TextDocument("Hello\nWorld");
        await TUnit.Assertions.Assert.That(doc.LineCount).IsEqualTo(2);

        doc.Insert(5, "\nMiddle".AsSpan());
        await TUnit.Assertions.Assert.That(doc.LineCount).IsEqualTo(3);
        await TUnit.Assertions.Assert.That(doc.GetLineIndexFromPosition(6)).IsEqualTo(1);
    }

    // ── String-to-PieceTable promotion ──────────────────────────────────

    [TUnit.Core.Test]
    public async Task SmallDocument_UsesStringBacking()
    {
        var doc = new TextDocument("Small text");
        doc.Insert(5, " extra".AsSpan());
        await TUnit.Assertions.Assert.That(doc.ToString()).IsEqualTo("Small extra text");
        await TUnit.Assertions.Assert.That(doc.Length).IsEqualTo(16);
    }

    [TUnit.Core.Test]
    public async Task LargeDocument_PromotesToPieceTable()
    {
        var doc = new TextDocument("Start ");

        // Insert enough text to exceed the 4096 threshold
        var largeText = new string('X', 5000);
        doc.Insert(6, largeText.AsSpan());

        await TUnit.Assertions.Assert.That(doc.Length).IsEqualTo(5006);
        await TUnit.Assertions.Assert.That(doc[0]).IsEqualTo('S');
        await TUnit.Assertions.Assert.That(doc[6]).IsEqualTo('X');
    }

    [TUnit.Core.Test]
    public async Task LargeDocument_InsertDeleteStillWorks()
    {
        var largeText = new string('A', 5000);
        var doc = new TextDocument(largeText);

        doc.Insert(2500, "INSERTED".AsSpan());
        await TUnit.Assertions.Assert.That(doc.Length).IsEqualTo(5008);
        await TUnit.Assertions.Assert.That(doc.Slice(2500, 8).ToString()).IsEqualTo("INSERTED");

        doc.Delete(2500, 8);
        await TUnit.Assertions.Assert.That(doc.Length).IsEqualTo(5000);
        await TUnit.Assertions.Assert.That(doc.ToString()).IsEqualTo(largeText);
    }

    [TUnit.Core.Test]
    public async Task LargeDocument_SliceWorks()
    {
        var text = new string('B', 5000);
        var doc = new TextDocument(text);
        doc.Insert(100, "XYZ".AsSpan());

        var slice = doc.Slice(99, 5);
        await TUnit.Assertions.Assert.That(slice.ToString()).IsEqualTo("BXYZB");
    }

    // ── Multiple edits ──────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task MultipleInserts_BuildUpText()
    {
        var doc = new TextDocument();
        doc.Insert(0, "H".AsSpan());
        doc.Insert(1, "e".AsSpan());
        doc.Insert(2, "l".AsSpan());
        doc.Insert(3, "l".AsSpan());
        doc.Insert(4, "o".AsSpan());

        await TUnit.Assertions.Assert.That(doc.ToString()).IsEqualTo("Hello");
    }

    [TUnit.Core.Test]
    public async Task EmptyDocument_LineCountIsOne()
    {
        var doc = new TextDocument();
        await TUnit.Assertions.Assert.That(doc.LineCount).IsEqualTo(1);
    }

    [TUnit.Core.Test]
    public async Task EmptyDocument_GetLine_ReturnsEmptyLine()
    {
        var doc = new TextDocument();
        var line = doc.GetLine(0);
        await TUnit.Assertions.Assert.That(line.Start).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(line.Length).IsEqualTo(0);
    }
}

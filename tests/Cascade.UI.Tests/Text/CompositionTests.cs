namespace Cascade.UI.Tests;

/// <summary>
/// Tests for the IME composition lifecycle — begin, update, commit, cancel.
/// </summary>
public class CompositionTests
{
    static TextEditingEngine CreateEngine(string initialText = "")
    {
        return new TextEditingEngine(
            new TextDocument(initialText),
            new TextEditingOptions());
    }

    static TextComposition MakeComposition(string text, int cursor = 0)
    {
        return new TextComposition(text, cursor, Array.Empty<CompositionSegment>());
    }

    // ── Begin composition ───────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task BeginComposition_SetsIsComposing()
    {
        var engine = CreateEngine("Hello ");
        engine.Selection = TextSelection.Collapsed(6);

        engine.BeginComposition(MakeComposition("にほ"));

        await TUnit.Assertions.Assert.That(engine.IsComposing).IsTrue();
        await TUnit.Assertions.Assert.That(engine.ActiveComposition).IsNotNull();
    }

    [TUnit.Core.Test]
    public async Task BeginComposition_InsertsProvisionalText()
    {
        var engine = CreateEngine("Hello ");
        engine.Selection = TextSelection.Collapsed(6);

        engine.BeginComposition(MakeComposition("にほ"));

        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello にほ");
    }

    [TUnit.Core.Test]
    public async Task BeginComposition_FiresCompositionChanged()
    {
        var engine = CreateEngine();
        bool fired = false;
        engine.CompositionChanged += _ => fired = true;

        engine.BeginComposition(MakeComposition("test"));
        await TUnit.Assertions.Assert.That(fired).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task BeginComposition_WhenReadOnly_DoesNothing()
    {
        var engine = CreateEngine("Test");
        engine.IsReadOnly = true;

        engine.BeginComposition(MakeComposition("X"));
        await TUnit.Assertions.Assert.That(engine.IsComposing).IsFalse();
    }

    // ── Update composition ──────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task UpdateComposition_ReplacesProvisionalText()
    {
        var engine = CreateEngine("Hello ");
        engine.Selection = TextSelection.Collapsed(6);

        engine.BeginComposition(MakeComposition("にほ"));
        engine.UpdateComposition(MakeComposition("にほん"));

        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello にほん");
    }

    [TUnit.Core.Test]
    public async Task UpdateComposition_UpdatesCursorPosition()
    {
        var engine = CreateEngine("A");
        engine.Selection = TextSelection.Collapsed(1);

        engine.BeginComposition(MakeComposition("xy", 1));
        engine.UpdateComposition(MakeComposition("xyz", 2));

        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(3);
    }

    [TUnit.Core.Test]
    public async Task UpdateComposition_WhenNotComposing_DoesNothing()
    {
        var engine = CreateEngine("Test");
        engine.UpdateComposition(MakeComposition("X"));
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Test");
    }

    // ── Commit composition ──────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task CommitComposition_ReplacesProvisionalWithFinal()
    {
        var engine = CreateEngine("Hello ");
        engine.Selection = TextSelection.Collapsed(6);

        engine.BeginComposition(MakeComposition("にほんご"));
        engine.CommitComposition("日本語");

        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello 日本語");
        await TUnit.Assertions.Assert.That(engine.IsComposing).IsFalse();
    }

    [TUnit.Core.Test]
    public async Task CommitComposition_MovesCaretAfterCommittedText()
    {
        var engine = CreateEngine("");
        engine.BeginComposition(MakeComposition("test"));
        engine.CommitComposition("final");

        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(5);
        await TUnit.Assertions.Assert.That(engine.Selection.IsCollapsed).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task CommitComposition_IsUndoable()
    {
        var engine = CreateEngine("A");
        engine.Selection = TextSelection.Collapsed(1);

        engine.BeginComposition(MakeComposition("temp"));
        engine.CommitComposition("final");

        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Afinal");

        engine.Undo();
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("A");
    }

    [TUnit.Core.Test]
    public async Task CommitComposition_WhenNotComposing_DoesNothing()
    {
        var engine = CreateEngine("Test");
        engine.CommitComposition("X");
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Test");
    }

    // ── Cancel composition ──────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task CancelComposition_RemovesProvisionalText()
    {
        var engine = CreateEngine("Hello ");
        engine.Selection = TextSelection.Collapsed(6);

        engine.BeginComposition(MakeComposition("にほんご"));
        engine.CancelComposition();

        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hello ");
        await TUnit.Assertions.Assert.That(engine.IsComposing).IsFalse();
    }

    [TUnit.Core.Test]
    public async Task CancelComposition_RestoresCaretToAnchor()
    {
        var engine = CreateEngine("Hello ");
        engine.Selection = TextSelection.Collapsed(6);

        engine.BeginComposition(MakeComposition("test"));
        engine.CancelComposition();

        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(6);
    }

    [TUnit.Core.Test]
    public async Task CancelComposition_FiresCompositionChangedNull()
    {
        var engine = CreateEngine();
        TextComposition? lastValue = new TextComposition("dummy", 0, Array.Empty<CompositionSegment>());
        engine.CompositionChanged += c => lastValue = c;

        engine.BeginComposition(MakeComposition("test"));
        engine.CancelComposition();

        await TUnit.Assertions.Assert.That(lastValue).IsNull();
    }

    // ── InsertText during composition ────────────────────────────────────

    [TUnit.Core.Test]
    public async Task InsertText_DuringComposition_IsRejected()
    {
        var engine = CreateEngine("AB");
        engine.Selection = TextSelection.Collapsed(2);

        engine.BeginComposition(MakeComposition("temp"));
        engine.InsertText("X".AsSpan());

        // InsertText should be rejected while composing
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("ABtemp");
    }

    // ── Composition with existing selection ──────────────────────────────

    [TUnit.Core.Test]
    public async Task BeginComposition_WithSelection_DeletesSelectionFirst()
    {
        var engine = CreateEngine("Hello World");
        engine.Selection = new TextSelection(new TextPosition(5), new TextPosition(11));

        engine.BeginComposition(MakeComposition("test"));

        // "Hello" + "test" = "Hellotest"
        await TUnit.Assertions.Assert.That(engine.Document.ToString()).IsEqualTo("Hellotest");
    }

    // ── Composition segments ────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task Composition_PreservesSegments()
    {
        var segments = new CompositionSegment[]
        {
            new(0, 2, CompositionSegmentStyle.TargetConverted),
            new(2, 2, CompositionSegmentStyle.Converted),
        };
        var comp = new TextComposition("にほんご", 2, segments);

        var engine = CreateEngine();
        engine.BeginComposition(comp);

        await TUnit.Assertions.Assert.That(engine.ActiveComposition!.Value.Segments.Count).IsEqualTo(2);
        await TUnit.Assertions.Assert.That(engine.ActiveComposition!.Value.Segments[0].Style)
            .IsEqualTo(CompositionSegmentStyle.TargetConverted);
    }
}

namespace Cascade.UI.Tests;

/// <summary>
/// Tests for the <see cref="TextSelection"/> anchor/focus model —
/// forward/backward selections, collapsed state, and normalization.
/// </summary>
public class SelectionTests
{
    // ── Collapsed selection ──────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task Collapsed_CreatesZeroLengthSelection()
    {
        var sel = TextSelection.Collapsed(5);
        await TUnit.Assertions.Assert.That(sel.IsCollapsed).IsTrue();
        await TUnit.Assertions.Assert.That(sel.Start).IsEqualTo(5);
        await TUnit.Assertions.Assert.That(sel.End).IsEqualTo(5);
        await TUnit.Assertions.Assert.That(sel.Length).IsEqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task Collapsed_FromPosition_PreservesAffinity()
    {
        var pos = new TextPosition(3, TextAffinity.Upstream);
        var sel = TextSelection.Collapsed(pos);
        await TUnit.Assertions.Assert.That(sel.Anchor.Affinity).IsEqualTo(TextAffinity.Upstream);
        await TUnit.Assertions.Assert.That(sel.Focus.Affinity).IsEqualTo(TextAffinity.Upstream);
    }

    // ── Forward selection ────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task ForwardSelection_AnchorBeforeFocus()
    {
        var sel = new TextSelection(new TextPosition(2), new TextPosition(8));
        await TUnit.Assertions.Assert.That(sel.IsForward).IsTrue();
        await TUnit.Assertions.Assert.That(sel.Start).IsEqualTo(2);
        await TUnit.Assertions.Assert.That(sel.End).IsEqualTo(8);
        await TUnit.Assertions.Assert.That(sel.Length).IsEqualTo(6);
        await TUnit.Assertions.Assert.That(sel.IsCollapsed).IsFalse();
    }

    // ── Backward selection ───────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task BackwardSelection_AnchorAfterFocus()
    {
        var sel = new TextSelection(new TextPosition(8), new TextPosition(2));
        await TUnit.Assertions.Assert.That(sel.IsForward).IsFalse();
        await TUnit.Assertions.Assert.That(sel.Start).IsEqualTo(2);
        await TUnit.Assertions.Assert.That(sel.End).IsEqualTo(8);
        await TUnit.Assertions.Assert.That(sel.Length).IsEqualTo(6);
    }

    // ── All selection ────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task All_SelectsEntireDocument()
    {
        var doc = new TextDocument("Hello World");
        var sel = TextSelection.All(doc);
        await TUnit.Assertions.Assert.That(sel.Start).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(sel.End).IsEqualTo(11);
        await TUnit.Assertions.Assert.That(sel.IsForward).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task All_EmptyDocument_IsCollapsed()
    {
        var doc = new TextDocument();
        var sel = TextSelection.All(doc);
        await TUnit.Assertions.Assert.That(sel.IsCollapsed).IsTrue();
    }

    // ── Engine selection behavior ────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task SelectionChanged_FiresOnSelectionChange()
    {
        var engine = new TextEditingEngine(new TextDocument("Hello"), new TextEditingOptions());
        TextSelection? lastSelection = null;
        engine.SelectionChanged += s => lastSelection = s;

        engine.Selection = TextSelection.Collapsed(3);
        await TUnit.Assertions.Assert.That(lastSelection).IsNotNull();
        await TUnit.Assertions.Assert.That(lastSelection!.Value.Focus.Offset).IsEqualTo(3);
    }

    [TUnit.Core.Test]
    public async Task Selection_ClampedToDocumentLength()
    {
        var engine = new TextEditingEngine(new TextDocument("Hi"), new TextEditingOptions());
        engine.Selection = TextSelection.Collapsed(100);
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(2);
    }

    [TUnit.Core.Test]
    public async Task Selection_ClampedToZero()
    {
        var engine = new TextEditingEngine(new TextDocument("Hi"), new TextEditingOptions());
        engine.Selection = TextSelection.Collapsed(-5);
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task ExtendSelection_Shift_Left_ExpandsBackward()
    {
        var engine = new TextEditingEngine(new TextDocument("Hello"), new TextEditingOptions());
        engine.Selection = TextSelection.Collapsed(3);

        engine.MoveCaret(CaretMovement.Left, true);
        engine.MoveCaret(CaretMovement.Left, true);

        await TUnit.Assertions.Assert.That(engine.Selection.Anchor.Offset).IsEqualTo(3);
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(engine.Selection.Length).IsEqualTo(2);
    }

    [TUnit.Core.Test]
    public async Task ExtendSelection_Shift_Right_ExpandsForward()
    {
        var engine = new TextEditingEngine(new TextDocument("Hello"), new TextEditingOptions());
        engine.Selection = TextSelection.Collapsed(1);

        engine.MoveCaret(CaretMovement.Right, true);
        engine.MoveCaret(CaretMovement.Right, true);

        await TUnit.Assertions.Assert.That(engine.Selection.Anchor.Offset).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(engine.Selection.Focus.Offset).IsEqualTo(3);
    }

    [TUnit.Core.Test]
    public async Task TextPosition_Zero_HasOffsetZero()
    {
        await TUnit.Assertions.Assert.That(TextPosition.Zero.Offset).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(TextPosition.Zero.Affinity).IsEqualTo(TextAffinity.Downstream);
    }
}

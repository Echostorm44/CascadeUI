namespace Cascade.UI.Tests;

/// <summary>
/// WP-3531: visual-order caret movement, geometry and selection in RTL / mixed
/// (bidi) text. The visual arrow keys must move the caret by screen position, so
/// inside an RTL run Left moves to the logically-next character. Uses the real
/// layout pipeline (HarfBuzz + Inter primary + system-font fallback for Hebrew).
/// </summary>
public class BidiCaretTests
{
    private const string Hebrew = "שלום"; // logical ש=0 ל=1 ו=2 ם=3

    private static string InterPath()
    {
        string p = System.IO.Path.Combine(AppContext.BaseDirectory, "fonts", "Inter-Regular.ttf");
        if (!File.Exists(p))
        {
            throw new FileNotFoundException(p);
        }
        return p;
    }

    private static TextLayoutResult Layout(string text)
    {
        var opts = new TextLayoutOptions
        {
            FontPath = InterPath(),
            FontSize = 24f,
            MaxWidth = 4000f,
            MaxHeight = 1000f,
            MaxLines = 1,
            LineHeightMultiplier = 1f,
        };
        return TextLayoutEngine.Layout(text, opts);
    }

    private static TextEditingEngine Engine(string text)
        => new(new TextDocument(text), new TextEditingOptions());

    private static int Move(string text, int from, CaretMovement movement)
    {
        var engine = Engine(text);
        var layout = Layout(text);
        engine.Selection = TextSelection.Collapsed(from);
        engine.MoveCaret(movement, false, layout);
        return engine.Selection.Focus.Offset;
    }

    // ── Pure RTL ────────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task PureRtl_VisualLeft_MovesToLogicallyNextChar()
    {
        // In RTL, pressing Left moves the caret leftward on screen = the next
        // logical character. From the start (offset 0, visually rightmost) Left
        // should walk 0→1→2→3→4.
        int o = 0;
        foreach (int expected in new[] { 1, 2, 3, 4 })
        {
            o = Move(Hebrew, o, CaretMovement.Left);
            await TUnit.Assertions.Assert.That(o).IsEqualTo(expected);
        }
    }

    [TUnit.Core.Test]
    public async Task PureRtl_VisualRight_MovesToLogicallyPreviousChar()
    {
        // From the logical end (offset 4, visually leftmost) Right walks 4→3→2→1→0.
        int o = 4;
        foreach (int expected in new[] { 3, 2, 1, 0 })
        {
            o = Move(Hebrew, o, CaretMovement.Right);
            await TUnit.Assertions.Assert.That(o).IsEqualTo(expected);
        }
    }

    [TUnit.Core.Test]
    public async Task PureRtl_AtVisualEdges_DoesNotMove()
    {
        // Right from offset 0 (visually rightmost) and Left from offset 4
        // (visually leftmost) are at the document's visual edges — no movement.
        await TUnit.Assertions.Assert.That(Move(Hebrew, 0, CaretMovement.Right)).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(Move(Hebrew, 4, CaretMovement.Left)).IsEqualTo(4);
    }

    [TUnit.Core.Test]
    public async Task PureRtl_CaretGeometry_StartIsRightmost_DirectionIsRtl()
    {
        var layout = Layout(Hebrew);
        var start = layout.GetCaretInfo(0); // before ש (logical first)
        var end = layout.GetCaretInfo(4);   // after ם (logical last)

        // The logical start sits to the right of the logical end in RTL.
        await TUnit.Assertions.Assert.That(start.X).IsGreaterThan(end.X);
        await TUnit.Assertions.Assert.That(start.Direction).IsEqualTo(TextDirection.RightToLeft);
    }

    // ── LTR is unaffected ───────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task PureLtr_VisualMatchesLogical()
    {
        await TUnit.Assertions.Assert.That(Move("abc", 1, CaretMovement.Left)).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(Move("abc", 1, CaretMovement.Right)).IsEqualTo(2);

        var info = Layout("abc").GetCaretInfo(1);
        await TUnit.Assertions.Assert.That(info.Direction).IsEqualTo(TextDirection.LeftToRight);
    }

    // ── Mixed direction ─────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task Mixed_CaretDirection_FollowsTheRun()
    {
        // "abc" + שלום : Latin clusters 0-2 (LTR), Hebrew clusters 3-6 (RTL).
        var layout = Layout("abc" + Hebrew);
        await TUnit.Assertions.Assert.That(layout.GetCaretInfo(1).Direction).IsEqualTo(TextDirection.LeftToRight);
        await TUnit.Assertions.Assert.That(layout.GetCaretInfo(4).Direction).IsEqualTo(TextDirection.RightToLeft);
    }

    [TUnit.Core.Test]
    public async Task Mixed_VisualRight_VisitsEveryCaretPosition_MonotonicInX()
    {
        // Walking Right from the visual-left edge must visit all N+1 caret positions
        // with non-decreasing screen X, even across the LTR↔RTL boundary.
        const string text = "ab" + Hebrew; // 6 chars → 7 caret positions
        var layout = Layout(text);

        // Find the visually-leftmost caret offset to start from.
        int start = 0;
        float minX = float.MaxValue;
        for (int o = 0; o <= text.Length; o++)
        {
            float x = layout.GetCaretInfo(o).X;
            if (x < minX) { minX = x; start = o; }
        }

        var engine = Engine(text);
        engine.Selection = TextSelection.Collapsed(start);
        var visited = new HashSet<int> { start };
        float prevX = layout.GetCaretInfo(start).X;

        for (int step = 0; step < text.Length; step++)
        {
            engine.MoveCaret(CaretMovement.Right, false, layout);
            int o = engine.Selection.Focus.Offset;
            visited.Add(o);
            float x = layout.GetCaretInfo(o).X;
            await TUnit.Assertions.Assert.That(x).IsGreaterThanOrEqualTo(prevX - 0.01f);
            prevX = x;
        }

        await TUnit.Assertions.Assert.That(visited.Count).IsEqualTo(text.Length + 1);
    }

    // ── Selection geometry ──────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task PureRtl_Selection_IsOneContiguousVisualRect()
    {
        // A contiguous logical selection within one RTL run is one visual span.
        var engine = Engine(Hebrew);
        var layout = Layout(Hebrew);
        engine.Selection = new TextSelection(new TextPosition(1), new TextPosition(3));

        var rects = engine.GetSelectionRects(layout);
        await TUnit.Assertions.Assert.That(rects.Count).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(rects[0].Width).IsGreaterThan(0f);
    }

    [TUnit.Core.Test]
    public async Task Mixed_FullSelection_CoversAllRunsWithPositiveWidth()
    {
        const string text = "ab" + Hebrew;
        var engine = Engine(text);
        var layout = Layout(text);
        engine.Selection = new TextSelection(new TextPosition(0), new TextPosition(text.Length));

        var rects = engine.GetSelectionRects(layout);
        await TUnit.Assertions.Assert.That(rects.Count).IsGreaterThanOrEqualTo(1);
        foreach (var r in rects)
        {
            await TUnit.Assertions.Assert.That(r.Width).IsGreaterThan(0f);
        }
    }
}

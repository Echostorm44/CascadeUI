namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// Text in a box sized to it must draw whole. Layout sums a label's measured width with its
/// control's padding and icon, the painter subtracts them back, and float rounding can leave the
/// slot a few millionths of a pixel narrower than the text: an icon Button measured "Copy" at
/// 56.312504, was laid out 113.3125 wide, gave the label a 56.3125 slot and painted "Co…".
/// <see cref="NodePainter.PlaceText"/> treats an overrun within
/// <see cref="NodePainter.TextFitTolerance"/> as fitting.
/// </summary>
public class TextFitToleranceTests
{
    private const float ButtonFontSize = 17f;

    private static string InterPath => System.IO.Path.Combine(AppContext.BaseDirectory, "fonts", "InterVariable.ttf");

    [Test]
    public async Task IconButtonSlot_FromTheReport_DrawsCopyWhole()
    {
        // The exact numbers from the report: Inter at 17px, the label slot the painter computed.
        float measured = MeasureNoWrap("Copy");
        await Assert.That(measured).IsGreaterThan(56.3125f).Because("the report's slot must be narrower than the text for this test to mean anything");

        var drawn = Draw("Copy", slotWidth: 56.3125f);

        await Assert.That(drawn).IsEqualTo("Copy");
    }

    [Test]
    [Arguments("Copy")]
    [Arguments("Rename clip")]
    [Arguments("Show details")]
    [Arguments("Paste and keep window open")]
    public async Task Text_InASlotNarrowerByFloatError_DrawsWhole(string text)
    {
        float measured = MeasureNoWrap(text);

        var drawn = Draw(text, slotWidth: BitDecrement(measured));

        await Assert.That(drawn).IsEqualTo(text);
    }

    [Test]
    public async Task Text_ThatReallyOverflows_IsStillEllipsised()
    {
        float measured = MeasureNoWrap("Show details");

        var drawn = Draw("Show details", slotWidth: measured - 1f);

        await Assert.That(drawn).EndsWith("…");
    }

    [Test]
    public async Task WrappingText_InASlotNarrowerByFloatError_StaysOnOneLine()
    {
        float measured = MeasureNoWrap("Show details");

        var placed = NodePainter.PlaceText("Show details", new Rect(0f, 0f, BitDecrement(measured), 40f), 0f,
            ButtonFontSize, InterPath, maxLines: 0, TextOverflow.Clip, noWrap: false, lineHeight: 0f);

        await Assert.That(placed).IsNotNull();
        await Assert.That(placed!.Value.SingleLine).IsTrue();
        var layout = TextLayoutEngine.Layout("Show details",
            placed.Value.DrawOptions(InterPath, ButtonFontSize, TextAlignment.Start, TextOverflow.Clip, 0, false, 0f));
        await Assert.That(layout.Lines.Count).IsEqualTo(1);
    }

    // ── Fixture ──────────────────────────────────────────────────────

    private static float MeasureNoWrap(string text)
    {
        return TextLayoutEngine.Layout(text, new TextLayoutOptions
        {
            FontPath = InterPath,
            FontSize = ButtonFontSize,
            MaxWidth = float.PositiveInfinity,
        }).BoundingBox.Width;
    }

    private static float BitDecrement(float value)
    {
        return MathF.BitDecrement(value);
    }

    /// <summary>
    /// Places <paramref name="text"/> in a single-line, ellipsising slot as the painter does and
    /// returns what the draw call would show: the text, or a prefix of it ending in "…".
    /// </summary>
    private static string Draw(string text, float slotWidth)
    {
        var placed = NodePainter.PlaceText(text, new Rect(0f, 0f, slotWidth, 40f), 0f, ButtonFontSize, InterPath,
            maxLines: 1, TextOverflow.Ellipsis, noWrap: true, lineHeight: 0f)
            ?? throw new InvalidOperationException("PlaceText found no room.");
        var layout = TextLayoutEngine.Layout(text,
            placed.DrawOptions(InterPath, ButtonFontSize, TextAlignment.Start, TextOverflow.Ellipsis, 1, true, 0f));
        var line = layout.Lines[0];
        return line.TextLength >= text.Length ? text : text[..line.TextLength] + "…";
    }
}

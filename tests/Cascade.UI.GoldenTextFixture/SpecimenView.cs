namespace Cascade.UI.GoldenTextFixture;

/// <summary>
/// Renders one specimen page of the golden-text suite through the full
/// Cascade pipeline: <see cref="DrawContext.DrawText"/> → HarfBuzz shaping →
/// EtchBackend glyph ops → GPU glyph instances — the exact path the
/// 2026-06-10 malformed-letters bug lived in. Static content: every presented
/// frame is identical, so a screenshot at any moment is the golden candidate.
///
/// DPI neutralization: the canvas scales by <c>PageScale / PixelRatio</c>, so
/// one specimen unit maps to exactly PageScale device pixels regardless of
/// the machine's OS DPI. The suite captures the device-pixel region
/// (0, 0, SheetWidth × PageScale, SheetHeight × PageScale), which is
/// machine-independent. True fractional-DPI *window* behavior is explicitly
/// out of scope here (WP-3511 item 1) — see the suite README.
/// </summary>
internal sealed class SpecimenView : Component
{
    private readonly PageSpec page;
    private bool debugDumped;

    public SpecimenView()
    {
        string pageId = Environment.GetEnvironmentVariable("CASCADE_GOLDEN_PAGE") ?? "regular-dark-100";
        page = PageSpec.Parse(pageId);
    }

    protected override Node Render()
    {
        return CanvasFactory.Canvas(Size.Fill, DrawSheet);
    }

    private void DrawSheet(DrawContext ctx, Size size)
    {
        if (Environment.GetEnvironmentVariable("CASCADE_GOLDEN_DEBUG") == "1" && !debugDumped)
        {
            debugDumped = true;
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "golden-debug.txt"),
                $"PixelRatio={ctx.PixelRatio} CanvasSize={size.Width}x{size.Height} PageScale={page.Scale}");
        }

        float deviceScale = page.Scale / ctx.PixelRatio;
        using var scale = ctx.PushScale(deviceScale, deviceScale);

        ctx.DrawRect(new Rect(0, 0, PageSpec.SheetWidth, PageSpec.SheetHeight), page.Background);

        float y = 14f;
        foreach ((float fontSize, string text) in page.LinesForPage())
        {
            ctx.DrawText(text, 8f, y, fontSize, page.Foreground, page.FontPath);
            y += fontSize * 1.3f + 2f;
        }
    }
}

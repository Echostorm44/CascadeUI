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

    // Shadow parity sheet (ShadowParityTests): black shadows on white, compared pixel-by-pixel
    // against Etch's analytic ShadowShape.Coverage. Keep in sync with ShadowParityTests.Shapes.
    private static void DrawShadowSheet(DrawContext ctx)
    {
        ctx.DrawRect(new Rect(0, 0, PageSpec.SheetWidth, PageSpec.SheetHeight), new ColorValue("#FFFFFF"));
        var black = new ColorValue("#000000");
        ctx.DrawBlurredRoundedRect(new Rect(40, 40, 160, 120), black, radius: 0f, blurSigma: 8f);
        ctx.DrawBlurredRoundedRect(new Rect(260, 40, 160, 120), black.Opacity(0.6f), radius: 24f, blurSigma: 12f);
        ctx.DrawBlurredRoundedRect(new Rect(490, 50, 100, 100), black, radius: 50f, blurSigma: 6f);
        using (ctx.PushClip(new Rect(0, 220, 170, 220)))
        {
            ctx.DrawBlurredRoundedRect(new Rect(40, 250, 260, 150), black, radius: 12f, blurSigma: 10f);
        }
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

        // Colour fidelity sheet (ShadowParityTests.DarkColors_RenderExactly).
        if (Environment.GetEnvironmentVariable("CASCADE_GOLDEN_SCENE") == "colors")
        {
            ctx.DrawRect(new Rect(0, 0, PageSpec.SheetWidth, PageSpec.SheetHeight), new ColorValue("#FFFFFF"));
            string[] darks = ["#050505", "#0A0A0A", "#101010", "#1E1E1E", "#404040"];
            for (int i = 0; i < darks.Length; i++)
            {
                ctx.DrawRect(new Rect(20 + i * 60, 20, 50, 50), new ColorValue(darks[i]));
            }
            ctx.DrawRect(new Rect(20, 100, 50, 50), new ColorValue("#000000").Opacity(0.5f));
            ctx.DrawRect(new Rect(80, 100, 50, 50), new ColorValue("#000000").Opacity(0.99f));
            ctx.DrawRect(new Rect(140, 100, 50, 50), new ColorValue("#000000").Opacity(0.996f));
            return;
        }

        if (Environment.GetEnvironmentVariable("CASCADE_GOLDEN_SCENE") == "shadow")
        {
            DrawShadowSheet(ctx);
            return;
        }

        ctx.DrawRect(new Rect(0, 0, PageSpec.SheetWidth, PageSpec.SheetHeight), page.Background);

        float y = 14f;
        foreach ((float fontSize, string text) in page.LinesForPage())
        {
            ctx.DrawText(text, 8f, y, fontSize, page.Foreground, page.FontPath);
            y += fontSize * 1.3f + 2f;
        }
    }
}

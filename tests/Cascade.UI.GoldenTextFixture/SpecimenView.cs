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
    private static readonly ImageSource Cornflower = SolidImage(100, 149, 237);
    private static readonly ImageSource Orange = SolidImage(255, 165, 0);
    private static readonly ImageSource Charcoal = SolidImage(30, 30, 30);

    private static ImageSource SolidImage(byte r, byte g, byte b)
    {
        byte[] rgba = new byte[8 * 8 * 4];
        for (int i = 0; i < rgba.Length; i += 4)
        {
            rgba[i] = r;
            rgba[i + 1] = g;
            rgba[i + 2] = b;
            rgba[i + 3] = 255;
        }
        return ImageSource.FromBytes(rgba, 8, 8);
    }

    private readonly PageSpec page;
    private bool debugDumped;

    public SpecimenView()
    {
        string pageId = Environment.GetEnvironmentVariable("CASCADE_GOLDEN_PAGE") ?? "regular-dark-100";
        page = PageSpec.Parse(pageId);
    }

    protected override Node Render()
    {
        if (Environment.GetEnvironmentVariable("CASCADE_GOLDEN_SCENE") == "zorder-layer")
        {
            return PaintOrderLayerScene();
        }
        return CanvasFactory.Canvas(Size.Fill, DrawSheet);
    }

    // Paint order across a retained layer (PaintOrderTests): a ScrollView — whose content is
    // composited from a retained layer — with an opaque panel stacked over its right part. The
    // test runs this at CASCADE_FORCE_DPI=96 so logical units are device pixels. Keep in sync with
    // PaintOrderTests.OpaquePanelOverScrollView_HidesItsContent.
    private static Node PaintOrderLayerScene()
    {
        var rows = new Node[8];
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = new Row(spacing: 8, crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                new Image(Cornflower).Size(40, 40),
                new Label("MMMMMMMMMMMM").FontSize(32).Color(new ColorValue("#000000")),
                new Image(Orange).Size(40, 40),
            ]).Height(52);
        }

        return new Stack(
            new ScrollView(new Column(children: rows))
                .Size(PageSpec.SheetWidth, PageSpec.SheetHeight)
                .Background(new ColorValue("#FFFFFF")),
            new Spacer()
                .Size(300, 400)
                .Background(new ColorValue("#FF0000"))
                .TranslateX(320)
                .TranslateY(20));
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

    // Paint-order sheet (PaintOrderTests): text, images and shapes must composite in the order
    // they were painted, whatever their kind. Keep the rects in sync with PaintOrderTests.
    private static void DrawPaintOrderSheet(DrawContext ctx, string fontPath)
    {
        var black = new ColorValue("#000000");
        var white = new ColorValue("#FFFFFF");
        ctx.DrawRect(new Rect(0, 0, PageSpec.SheetWidth, PageSpec.SheetHeight), white);

        // A: text, then an opaque rect over its right part.
        ctx.DrawText("MMMMMMMM", 20, 20, 40, black, fontPath);
        ctx.DrawRect(new Rect(160, 10, 200, 80), new ColorValue("#FF0000"));

        // B: an image, then an opaque rect over its right part.
        ctx.DrawImage(Cornflower, new Rect(20, 110, 160, 80));
        ctx.DrawRect(new Rect(100, 110, 160, 80), new ColorValue("#008000"));

        // C: a rect, then text over it.
        ctx.DrawRect(new Rect(20, 210, 300, 80), new ColorValue("#0000FF"));
        ctx.DrawText("MMMMMM", 30, 220, 40, white, fontPath);

        // D: text, then an image over its right part.
        ctx.DrawText("MMMMMMMM", 20, 320, 40, black, fontPath);
        ctx.DrawImage(Orange, new Rect(160, 310, 200, 80));

        // E: an image, then text over it.
        ctx.DrawImage(Charcoal, new Rect(380, 210, 240, 80));
        ctx.DrawText("MMMMM", 390, 220, 40, white, fontPath);
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

        // Render-parity sheets (tests/Cascade.UI.GoldenText RenderParityTests).
        string? sceneName = Environment.GetEnvironmentVariable("CASCADE_GOLDEN_SCENE");
        if (sceneName is not null && ParityScenes.TryDraw(sceneName, ctx, page.FontPath))
        {
            return;
        }

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

        // Image fidelity (ShadowParityTests.ImagePixels_RenderUnchanged): an image is drawn with the
        // same sRGB bytes it was given.
        if (Environment.GetEnvironmentVariable("CASCADE_GOLDEN_SCENE") == "images")
        {
            ctx.DrawRect(new Rect(0, 0, PageSpec.SheetWidth, PageSpec.SheetHeight), new ColorValue("#FFFFFF"));
            ctx.DrawImage(Cornflower, new Rect(20, 20, 60, 60));
            ctx.DrawImage(Orange, new Rect(100, 20, 60, 60));
            ctx.DrawImage(Charcoal, new Rect(180, 20, 60, 60));
            return;
        }

        if (Environment.GetEnvironmentVariable("CASCADE_GOLDEN_SCENE") == "shadow")
        {
            DrawShadowSheet(ctx);
            return;
        }

        if (Environment.GetEnvironmentVariable("CASCADE_GOLDEN_SCENE") == "zorder")
        {
            DrawPaintOrderSheet(ctx, page.FontPath);
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

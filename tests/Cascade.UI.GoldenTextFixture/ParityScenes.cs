namespace Cascade.UI.GoldenTextFixture;

/// <summary>
/// Render-parity sheets: one scene per primitive family, drawn in specimen units
/// (<see cref="PageSpec.SheetWidth"/> × <see cref="PageSpec.SheetHeight"/>) under the page's
/// DPI-neutralizing scale. The render-parity suite captures each scene on the GPU presenter and
/// on the CPU renderer and compares them; the GPU captures also serve as before/after snapshots
/// when the renderer is refactored. Every scene is static, so any presented frame is the capture.
/// </summary>
internal static class ParityScenes
{
    /// <summary>Scene names accepted by <see cref="TryDraw"/> (CASCADE_GOLDEN_SCENE=parity-&lt;name&gt;).</summary>
    public static readonly string[] Names =
    [
        "shapes", "strokes", "paths", "gradients", "clips", "opacity", "images", "layers", "blur", "ui", "text",
    ];

    private static readonly ColorValue White = new("#FFFFFF");
    private static readonly ColorValue Black = new("#000000");
    private static readonly ColorValue Paper = new("#F4F1EA");
    private static readonly ColorValue Ink = new("#1C1C1E");
    private static readonly ColorValue Blue = new("#0A84FF");
    private static readonly ColorValue Orange = new("#FF9F0A");
    private static readonly ColorValue Green = new("#30D158");
    private static readonly ColorValue Red = new("#FF375F");
    private static readonly ColorValue Purple = new("#BF5AF2");
    private static readonly ColorValue Yellow = new("#FFD60A");
    private static readonly ColorValue Teal = new("#64D2FF");
    private static readonly ColorValue Slate = new("#2C2C2E");
    private static readonly ColorValue Gray = new("#8E8E93");

    private static readonly ImageSource CheckerImage = BuildChecker();
    private static readonly ImageSource AlphaRampImage = BuildAlphaRamp();
    private static readonly ImageSource PhotoImage = BuildPhoto();

    /// <summary>Draws the named parity scene; false when <paramref name="scene"/> is not a parity scene.</summary>
    public static bool TryDraw(string scene, DrawContext ctx, string fontPath)
    {
        switch (scene)
        {
            case "parity-shapes":
                DrawShapes(ctx);
                return true;
            case "parity-strokes":
                DrawStrokes(ctx);
                return true;
            case "parity-paths":
                DrawPaths(ctx);
                return true;
            case "parity-gradients":
                DrawGradients(ctx);
                return true;
            case "parity-clips":
                DrawClips(ctx, fontPath);
                return true;
            case "parity-opacity":
                DrawOpacity(ctx, fontPath);
                return true;
            case "parity-images":
                DrawImages(ctx);
                return true;
            case "parity-layers":
                DrawLayers(ctx, fontPath);
                return true;
            case "parity-blur":
                DrawBlur(ctx, fontPath);
                return true;
            case "parity-ui":
                DrawUi(ctx, fontPath);
                return true;
            case "parity-text":
                DrawText(ctx, fontPath);
                return true;
            default:
                return false;
        }
    }

    private static void Background(DrawContext ctx, ColorValue color)
    {
        ctx.DrawRect(new Rect(0, 0, PageSpec.SheetWidth, PageSpec.SheetHeight), color);
    }

    // Solid fills: pixel-aligned and fractional rects, rounded rects across radii, circles,
    // ellipses, sectors and donuts, plus translucent overlaps.
    private static void DrawShapes(DrawContext ctx)
    {
        Background(ctx, Paper);
        ctx.DrawRect(new Rect(10, 10, 60, 40), Blue);
        ctx.DrawRect(new Rect(80.5f, 10.25f, 60.4f, 40.6f), Orange);
        ctx.DrawRect(new Rect(150, 10, 0.5f, 40), Ink);
        ctx.DrawRect(new Rect(160, 10, 1.5f, 40), Ink);
        ctx.DrawRect(new Rect(170, 10, 60, 1), Ink);
        ctx.DrawRect(new Rect(170, 20.5f, 60, 1), Ink);
        ctx.DrawRect(new Rect(240, 10, 70, 40), Green, radius: 4);
        ctx.DrawRect(new Rect(320, 10, 70, 40), Red, radius: 12);
        ctx.DrawRect(new Rect(400, 10, 70, 40), Purple, radius: 20);
        ctx.DrawRect(new Rect(480, 10.5f, 70.3f, 39.4f), Teal, radius: 7.5f);
        ctx.DrawRect(new Rect(560, 10, 40, 40), Yellow, radius: 20);

        ctx.DrawCircle(new Point(40, 100), 30, Blue);
        ctx.DrawCircle(new Point(110.5f, 100.25f), 20.6f, Orange);
        ctx.DrawCircle(new Point(160, 100), 3, Ink);
        ctx.DrawCircle(new Point(175, 100), 1.2f, Ink);
        ctx.DrawEllipse(new Rect(200, 70, 120, 60), Green);
        ctx.DrawEllipse(new Rect(330, 70, 40, 90), Red);

        float start = -MathF.PI / 2f;
        float[] sweeps = [1.1f, 0.9f, 2.0f, 2.283f];
        ColorValue[] colors = [Blue, Orange, Green, Red];
        for (int i = 0; i < sweeps.Length; i++)
        {
            ctx.DrawSector(new Point(440, 110), 45, 0, start, sweeps[i], colors[i]);
            ctx.DrawSector(new Point(550, 110), 45, 26, start, sweeps[i], colors[i]);
            start += sweeps[i];
        }

        ctx.DrawRect(new Rect(20, 180, 180, 120), Blue.Opacity(0.6f), radius: 16);
        ctx.DrawRect(new Rect(90, 220, 180, 120), Red.Opacity(0.6f), radius: 16);
        ctx.DrawCircle(new Point(120, 260), 50, Yellow.Opacity(0.5f));
        ctx.DrawRect(new Rect(300, 180, 320, 240), Ink, radius: 24);
        for (int i = 0; i < 8; i++)
        {
            float x = 320 + i * 37.3f;
            ctx.DrawRect(new Rect(x, 200 + i * 3.7f, 28, 180 - i * 13.1f), colors[i % 4], radius: 3 + i);
        }
    }

    // Strokes: rect and rounded-rect borders, circle rings, lines with every cap, polylines with
    // every join, arcs, hairlines and thick translucent strokes.
    private static void DrawStrokes(DrawContext ctx)
    {
        Background(ctx, White);
        ctx.DrawRect(new Rect(10, 10, 80, 50), stroke: new Stroke(Ink, 1));
        ctx.DrawRect(new Rect(100.5f, 10.5f, 80, 50), stroke: new Stroke(Blue, 2.5f));
        ctx.DrawRect(new Rect(190, 10, 80, 50), stroke: new Stroke(Red, 6));
        ctx.DrawRect(new Rect(280, 10, 80, 50), stroke: new Stroke(Green, 1), radius: 8);
        ctx.DrawRect(new Rect(370, 10, 80, 50), stroke: new Stroke(Purple, 4), radius: 14);
        ctx.DrawRect(new Rect(460, 10, 80, 50), Yellow, new Stroke(Ink, 2), radius: 10);
        ctx.DrawRect(new Rect(550, 10, 80, 50), Teal.Opacity(0.5f), new Stroke(Ink.Opacity(0.5f), 8), radius: 6);

        ctx.DrawCircle(new Point(40, 110), 28, stroke: new Stroke(Blue, 2));
        ctx.DrawCircle(new Point(110, 110), 28, Orange, new Stroke(Ink, 5));
        ctx.DrawCircle(new Point(180, 110), 28, stroke: new Stroke(Red.Opacity(0.5f), 12));

        StrokeCap[] caps = [StrokeCap.Butt, StrokeCap.Round, StrokeCap.Square];
        for (int i = 0; i < caps.Length; i++)
        {
            float y = 90 + i * 22;
            ctx.DrawLine(new Point(240, y), new Point(330, y), new Stroke(Ink, 10, caps[i], StrokeJoin.Miter));
            ctx.DrawLine(new Point(350, y - 8), new Point(440, y + 8), new Stroke(Blue, 6, caps[i], StrokeJoin.Miter));
        }
        ctx.DrawLine(new Point(460, 85), new Point(620, 85), new Stroke(Ink, 1));
        ctx.DrawLine(new Point(460, 95.5f), new Point(620, 95.5f), new Stroke(Ink, 1));
        ctx.DrawLine(new Point(460, 105), new Point(620, 135), new Stroke(Red, 1.5f));
        ctx.DrawLine(new Point(460, 140), new Point(620, 140), new Stroke(Green, 0.5f));

        StrokeJoin[] joins = [StrokeJoin.Miter, StrokeJoin.Round, StrokeJoin.Bevel];
        for (int i = 0; i < joins.Length; i++)
        {
            float x = 20 + i * 140;
            var zigzag = new PathBuilder()
                .MoveTo(new Point(x, 230))
                .LineTo(new Point(x + 30, 170))
                .LineTo(new Point(x + 60, 230))
                .LineTo(new Point(x + 90, 180))
                .LineTo(new Point(x + 120, 200))
                .Build();
            ctx.DrawPath(zigzag, stroke: new Stroke(Purple, 9, StrokeCap.Butt, joins[i]));
            var check = new PathBuilder()
                .MoveTo(new Point(x + 10, 270))
                .LineTo(new Point(x + 40, 300))
                .LineTo(new Point(x + 100, 245))
                .Build();
            ctx.DrawPath(check, stroke: new Stroke(Blue.Opacity(0.55f), 12, StrokeCap.Round, joins[i]));
        }

        ctx.DrawArc(new Point(470, 270), 50, Angle.Degrees(200), Angle.Degrees(250), new Stroke(Orange, 8, StrokeCap.Butt, StrokeJoin.Miter));
        ctx.DrawArc(new Point(470, 270), 32, Angle.Degrees(-30), Angle.Degrees(300), new Stroke(Teal, 6, StrokeCap.Round, StrokeJoin.Round));
        ctx.DrawArc(new Point(580, 270), 40, Angle.Degrees(0), Angle.Degrees(-120), new Stroke(Red, 3, StrokeCap.Square, StrokeJoin.Miter));

        var wave = new PathBuilder()
            .MoveTo(new Point(20, 380))
            .CubicTo(new Point(80, 320), new Point(140, 440), new Point(200, 380))
            .QuadTo(new Point(250, 330), new Point(300, 390))
            .Build();
        ctx.DrawPath(wave, stroke: new Stroke(Ink, 3, StrokeCap.Round, StrokeJoin.Round));
        ctx.DrawPath(wave, stroke: new Stroke(Green, 1));
        var dashed = new PathBuilder()
            .MoveTo(new Point(330, 360))
            .LineTo(new Point(620, 360))
            .LineTo(new Point(620, 420))
            .LineTo(new Point(330, 420))
            .Close()
            .Build();
        ctx.DrawPath(dashed, stroke: new Stroke(Ink, 2, StrokeCap.Butt, StrokeJoin.Miter, new DashPattern(8, 5)));
    }

    // Arbitrary path fills: polygons, self-intersecting stars, holes (two subpaths), curves and
    // icon-like glyph shapes, transformed (rotated/scaled) fills.
    private static void DrawPaths(DrawContext ctx)
    {
        Background(ctx, Paper);
        var triangle = new PathBuilder()
            .MoveTo(new Point(20, 90))
            .LineTo(new Point(70, 10))
            .LineTo(new Point(120, 90))
            .Close()
            .Build();
        ctx.DrawPath(triangle, Red);

        var chevron = new PathBuilder()
            .MoveTo(new Point(140, 20))
            .LineTo(new Point(170, 50))
            .LineTo(new Point(140, 80))
            .LineTo(new Point(130, 70))
            .LineTo(new Point(150, 50))
            .LineTo(new Point(130, 30))
            .Close()
            .Build();
        ctx.DrawPath(chevron, Ink);

        ctx.DrawPath(Path.Star(new Point(240, 50), 42, 18, 5), Orange);
        ctx.DrawPath(Path.Star(new Point(340, 50), 42, 30, 7), Blue.Opacity(0.7f), new Stroke(Ink, 1.5f));
        ctx.DrawPath(Path.RegularPolygon(new Point(440, 50), 40, 6), Green);

        var crossed = new PathBuilder()
            .MoveTo(new Point(500, 10))
            .LineTo(new Point(620, 90))
            .LineTo(new Point(620, 10))
            .LineTo(new Point(500, 90))
            .Close()
            .Build();
        ctx.DrawPath(crossed, Purple);

        var ring = new PathBuilder()
            .MoveTo(new Point(20, 120)).LineTo(new Point(140, 120)).LineTo(new Point(140, 220)).LineTo(new Point(20, 220)).Close()
            .MoveTo(new Point(50, 150)).LineTo(new Point(50, 190)).LineTo(new Point(110, 190)).LineTo(new Point(110, 150)).Close()
            .Build();
        ctx.DrawPath(ring, Teal);

        var blob = new PathBuilder()
            .MoveTo(new Point(180, 170))
            .CubicTo(new Point(180, 110), new Point(300, 110), new Point(300, 170))
            .CubicTo(new Point(300, 240), new Point(230, 200), new Point(220, 230))
            .QuadTo(new Point(170, 250), new Point(180, 170))
            .Close()
            .Build();
        ctx.DrawPath(blob, Yellow, new Stroke(Ink, 2, StrokeCap.Round, StrokeJoin.Round));

        using (ctx.PushTranslate(400, 180))
        using (ctx.PushRotate(Angle.Degrees(23)))
        {
            ctx.DrawRect(new Rect(-50, -30, 100, 60), Blue);
            ctx.DrawRect(new Rect(-40, -20, 80, 40), Orange, radius: 10);
            ctx.DrawPath(Path.Star(new Point(0, 0), 18, 8, 5), White);
        }
        using (ctx.PushTranslate(540, 180))
        using (ctx.PushScale(1.7f, 0.8f))
        {
            ctx.DrawPath(Path.Star(new Point(0, 0), 30, 12, 6), Red);
        }

        var area = new PathBuilder().MoveTo(new Point(20, 420));
        float[] values = [0.3f, 0.5f, 0.42f, 0.7f, 0.62f, 0.85f, 0.55f, 0.75f];
        for (int i = 0; i < values.Length; i++)
        {
            area.LineTo(new Point(20 + i * 85, 420 - values[i] * 150));
        }
        area.LineTo(new Point(615, 420)).Close();
        ctx.DrawPath(area.Build(), Blue.Opacity(0.35f));
        var line = new PathBuilder().MoveTo(new Point(20, 420 - values[0] * 150));
        for (int i = 1; i < values.Length; i++)
        {
            line.LineTo(new Point(20 + i * 85, 420 - values[i] * 150));
        }
        ctx.DrawPath(line.Build(), stroke: new Stroke(Blue, 2.5f, StrokeCap.Round, StrokeJoin.Round));
    }

    // Gradients: angled multi-stop linear, explicit two-point linear, radial and sweep fills on
    // rects, rounded rects and arbitrary paths; a gradient with a transparent stop.
    private static void DrawGradients(DrawContext ctx)
    {
        Background(ctx, White);
        var rainbow = new[]
        {
            new GradientStop(0f, Red),
            new GradientStop(0.3f, Yellow),
            new GradientStop(0.55f, Green),
            new GradientStop(0.8f, Blue),
            new GradientStop(1f, Purple),
        };
        ctx.DrawRect(new Rect(10, 10, 300, 60), Gradient.Linear(Angle.Degrees(0), rainbow));
        ctx.DrawRect(new Rect(330, 10, 300, 60), Gradient.Linear(Angle.Degrees(90), new GradientStop(0f, Black), new GradientStop(1f, White)), radius: 12);
        ctx.DrawRect(new Rect(10, 90, 200, 120), Gradient.Linear(Angle.Degrees(35), rainbow), radius: 20);
        ctx.DrawRect(new Rect(230, 90, 200, 120),
            Gradient.Linear(new Point(250, 100), new Point(400, 200), new GradientStop(0f, Blue), new GradientStop(1f, Orange)));
        ctx.DrawRect(new Rect(450, 90, 180, 120),
            Gradient.Radial(new Point(540, 150), 90, new GradientStop(0f, White), new GradientStop(0.5f, Teal), new GradientStop(1f, Slate)), radius: 8);
        ctx.DrawRect(new Rect(10, 230, 200, 200),
            Gradient.Sweep(new Point(110, 330), Angle.Degrees(-90), rainbow), radius: 100);
        ctx.DrawRect(new Rect(230, 230, 200, 90),
            Gradient.Linear(Angle.Degrees(0), new GradientStop(0f, Red), new GradientStop(1f, Red.Opacity(0f))));
        ctx.DrawRect(new Rect(230, 330, 200, 100),
            Gradient.Linear(Angle.Degrees(180), new GradientStop(0f, Green), new GradientStop(0.5f, Green), new GradientStop(0.5f, Ink), new GradientStop(1f, Ink)), radius: 6);
        ctx.DrawPath(Path.Star(new Point(540, 330), 90, 40, 5),
            Gradient.Radial(new Point(540, 330), 90, new GradientStop(0f, Yellow), new GradientStop(1f, Red)));
    }

    // Clips: rect clip cutting a rounded card (the edge must stay square), rounded clips around
    // an image, rects and text, a circular path clip, nested and rotated clips.
    private static void DrawClips(DrawContext ctx, string fontPath)
    {
        Background(ctx, Paper);
        using (ctx.PushClip(new Rect(10, 10, 140, 90)))
        {
            ctx.DrawRect(new Rect(30, 30, 180, 120), Blue, radius: 24);
            ctx.DrawRect(new Rect(-20, 70, 100, 60), Orange, new Stroke(Ink, 3), radius: 16);
        }
        using (ctx.PushClip(new Rect(170.5f, 10.5f, 120.3f, 90.6f)))
        {
            ctx.DrawRect(new Rect(150, 0, 200, 200), Green);
            ctx.DrawText("Clipped text runs past", 175, 60, 20, Ink, fontPath);
        }
        using (ctx.PushRoundedClip(new Rect(310, 10, 150, 110), 30))
        {
            ctx.DrawImage(PhotoImage, new Rect(300, 0, 170, 130));
            ctx.DrawRect(new Rect(300, 80, 170, 50), Black.Opacity(0.5f));
            ctx.DrawText("Rounded clip", 320, 90, 18, White, fontPath);
        }
        using (ctx.PushClip(Path.Circle(new Point(545, 65), 55)))
        {
            ctx.DrawRect(new Rect(480, 0, 140, 140), Red);
            for (int i = 0; i < 7; i++)
            {
                ctx.DrawRect(new Rect(480 + i * 20, 0, 10, 140), Yellow);
            }
        }

        using (ctx.PushClip(new Rect(10, 140, 300, 140)))
        using (ctx.PushRoundedClip(new Rect(40, 160, 300, 100), 40))
        {
            ctx.DrawRect(new Rect(0, 140, 400, 160), Purple);
            ctx.DrawCircle(new Point(60, 210), 50, Teal);
            ctx.DrawText("Nested clips", 120, 190, 22, White, fontPath);
        }

        using (ctx.PushTranslate(470, 220))
        using (ctx.PushRotate(Angle.Degrees(30)))
        using (ctx.PushClip(new Rect(-60, -40, 120, 80)))
        {
            ctx.DrawRect(new Rect(-100, -100, 200, 200), Orange);
            ctx.DrawCircle(new Point(0, 0), 50, Blue);
        }

        using (ctx.PushClip(new Rect(10, 300, 620, 130)))
        {
            for (int i = 0; i < 6; i++)
            {
                float x = -40 + i * 120;
                ctx.DrawBlurredRoundedRect(new Rect(x, 330, 100, 120), Black.Opacity(0.4f), 12, 8);
                ctx.DrawRect(new Rect(x, 320, 100, 120), White, new Stroke(Gray, 1), radius: 12);
                ctx.DrawText($"Card {i}", x + 12, 340, 16, Ink, fontPath);
            }
        }
    }

    // Opacity: an opacity scope over overlapping shapes, text and an image; translucent and gray
    // text over coloured backgrounds; colour emoji at full and reduced opacity.
    private static void DrawOpacity(DrawContext ctx, string fontPath)
    {
        Background(ctx, White);
        using (ctx.PushOpacity(0.5f))
        {
            ctx.DrawRect(new Rect(20, 20, 160, 100), Blue, radius: 12);
            ctx.DrawRect(new Rect(90, 60, 160, 100), Red, radius: 12);
            ctx.DrawText("Faded text", 30, 30, 24, Ink, fontPath);
            ctx.DrawImage(PhotoImage, new Rect(270, 20, 120, 120));
        }
        ctx.DrawImage(PhotoImage, new Rect(410, 20, 120, 120), opacity: 0.35f);

        ColorValue[] backgrounds = [White, Black, Blue, Yellow, Slate, new ColorValue("#7F7F7F")];
        ColorValue[] inks = [Black, White, Gray, new ColorValue("#3A3A3C"), Red, Ink.Opacity(0.5f), White.Opacity(0.6f)];
        for (int b = 0; b < backgrounds.Length; b++)
        {
            float y = 180 + b * 42;
            ctx.DrawRect(new Rect(10, y, 620, 40), backgrounds[b]);
            for (int i = 0; i < inks.Length; i++)
            {
                ctx.DrawText("Aa%", 18 + i * 88, y + 8, 20, inks[i], fontPath);
            }
        }
    }

    // Images: magnified (bilinear) and minified, straight-alpha edges, rotated, source-rect
    // sub-images, and opacity.
    private static void DrawImages(DrawContext ctx)
    {
        Background(ctx, Paper);
        ctx.DrawImage(CheckerImage, new Rect(10, 10, 128, 128));
        ctx.DrawImage(CheckerImage, new Rect(150, 10, 37, 37));
        ctx.DrawImage(AlphaRampImage, new Rect(200, 10, 200, 60));
        ctx.DrawRect(new Rect(200, 80, 200, 60), Ink);
        ctx.DrawImage(AlphaRampImage, new Rect(200, 80, 200, 60));
        ctx.DrawImage(PhotoImage, new Rect(420, 10, 200, 130));
        ctx.DrawImage(PhotoImage, new Rect(16, 16, 16, 16), new Rect(10, 160, 120, 120));
        using (ctx.PushTranslate(260, 260))
        using (ctx.PushRotate(Angle.Degrees(-18)))
        {
            ctx.DrawImage(PhotoImage, new Rect(-80, -60, 160, 120));
        }
        ctx.DrawImage(PhotoImage, new Rect(420, 160, 200, 130), opacity: 0.5f);
        ctx.DrawImage(CheckerImage, new Rect(10.5f, 300.25f, 100.3f, 100.6f));
    }

    // Retained layers: content captured once into a layer and composited at a scroll offset
    // inside a viewport clip, then a second composite at reduced opacity, then a panel over it.
    private static void DrawLayers(DrawContext ctx, string fontPath)
    {
        Background(ctx, White);
        ulong handle = ctx.NextLayerHandle();
        using (ctx.PushLayerTexture(handle, 300, 800))
        {
            for (int i = 0; i < 12; i++)
            {
                float y = 20 + i * 64;
                ctx.DrawBlurredRoundedRect(new Rect(30, y + 3, 260, 52), Black.Opacity(0.3f), 10, 4);
                ctx.DrawRect(new Rect(30, y, 260, 52), i % 2 == 0 ? Paper : White, new Stroke(Gray, 1), radius: 10);
                ctx.DrawImage(CheckerImage, new Rect(40, y + 10, 32, 32));
                ctx.DrawText($"Row {i} retained layer", 84, y + 14, 18, Ink, fontPath);
                using (ctx.PushClip(new Rect(230, y + 8, 50, 36)))
                {
                    ctx.DrawCircle(new Point(255, y + 26), 24, i % 3 == 0 ? Red : Blue);
                }
            }
        }

        using (ctx.PushClip(new Rect(20, 20, 300, 400)))
        {
            ctx.DrawLayerTexture(handle, 0, -137.5f);
        }
        using (ctx.PushClip(new Rect(330, 20, 300, 400)))
        {
            ctx.DrawLayerTexture(handle, 310, -300, opacity: 0.6f);
        }
        ctx.DrawRect(new Rect(250, 300, 160, 100), Orange, radius: 14);
        ctx.DrawText("Over layer", 265, 335, 20, White, fontPath);
    }

    // Backdrop blur: frosted panels over high-contrast content, with and without tint, plus one
    // inside a retained layer.
    private static void DrawBlur(DrawContext ctx, string fontPath)
    {
        Background(ctx, White);
        ColorValue[] stripes = [Red, Orange, Yellow, Green, Teal, Blue, Purple];
        for (int i = 0; i < 28; i++)
        {
            ctx.DrawRect(new Rect(i * 23, 0, 12, 440), stripes[i % stripes.Length]);
        }
        ctx.DrawText("BACKDROP", 40, 60, 64, Ink, fontPath);
        ctx.DrawBackdropBlur(new Rect(30, 30, 260, 160), White.Opacity(0.35f), 24, 10);
        ctx.DrawText("Frosted", 60, 90, 28, Ink, fontPath);
        ctx.DrawBackdropBlur(new Rect(330, 40, 260, 140), Black.Opacity(0f), 0, 4);

        ulong handle = ctx.NextLayerHandle();
        using (ctx.PushLayerTexture(handle, 600, 300))
        {
            ctx.DrawText("Layer content under glass", 50, 230, 30, Black, fontPath);
            ctx.DrawBackdropBlur(new Rect(40, 250, 300, 120), Blue.Opacity(0.25f), 18, 6);
            ctx.DrawText("Blur in layer", 70, 290, 22, White, fontPath);
        }
        using (ctx.PushClip(new Rect(20, 210, 600, 220)))
        {
            ctx.DrawLayerTexture(handle, 0, -20);
        }
    }

    // A realistic application frame: title bar, sidebar with selection and icons, cards with
    // shadows, buttons, toggles, a progress bar, a list with separators and dense text.
    private static void DrawUi(DrawContext ctx, string fontPath)
    {
        var surface = new ColorValue("#F2F2F7");
        var accent = Blue;
        Background(ctx, surface);
        ctx.DrawRect(new Rect(0, 0, 640, 36), White);
        ctx.DrawRect(new Rect(0, 36, 640, 1), new ColorValue("#D1D1D6"));
        ctx.DrawCircle(new Point(18, 18), 6, Red);
        ctx.DrawCircle(new Point(36, 18), 6, Yellow);
        ctx.DrawCircle(new Point(54, 18), 6, Green);
        ctx.DrawText("Inbox — Cascade Mail", 250, 10, 14, Ink, fontPath);

        ctx.DrawRect(new Rect(0, 37, 160, 403), new ColorValue("#E5E5EA"));
        string[] folders = ["Inbox", "Drafts", "Sent", "Archive", "Junk", "Trash"];
        for (int i = 0; i < folders.Length; i++)
        {
            float y = 50 + i * 30;
            if (i == 0)
            {
                ctx.DrawRect(new Rect(8, y - 4, 144, 26), accent, radius: 6);
            }
            var ink = i == 0 ? White : Ink;
            var chevron = new PathBuilder()
                .MoveTo(new Point(18, y + 4))
                .LineTo(new Point(23, y + 9))
                .LineTo(new Point(18, y + 14))
                .Build();
            ctx.DrawPath(chevron, stroke: new Stroke(ink, 1.75f, StrokeCap.Round, StrokeJoin.Round));
            ctx.DrawText(folders[i], 32, y + 1, 13, ink, fontPath);
            ctx.DrawText($"{(i + 3) * 7}", 128, y + 1, 12, i == 0 ? White : Gray, fontPath);
        }

        for (int c = 0; c < 2; c++)
        {
            float x = 175 + c * 230;
            ctx.DrawBlurredRoundedRect(new Rect(x, 52, 215, 120), Black.Opacity(0.18f), 12, 6);
            ctx.DrawRect(new Rect(x, 48, 215, 120), White, radius: 12);
            ctx.DrawText(c == 0 ? "Storage" : "Sync", x + 14, 60, 15, Ink, fontPath);
            ctx.DrawText(c == 0 ? "42.7 GB of 64 GB used" : "Last synced 3 min ago", x + 14, 82, 12, Gray, fontPath);
            ctx.DrawRect(new Rect(x + 14, 108, 187, 8), new ColorValue("#E5E5EA"), radius: 4);
            ctx.DrawRect(new Rect(x + 14, 108, c == 0 ? 125 : 60, 8),
                Gradient.Linear(Angle.Degrees(0), new GradientStop(0f, Teal), new GradientStop(1f, accent)), radius: 4);
            ctx.DrawRect(new Rect(x + 14, 130, 90, 28), accent, radius: 8);
            ctx.DrawText("Manage", x + 32, 136, 13, White, fontPath);
            ctx.DrawRect(new Rect(x + 150, 132, 46, 24), c == 0 ? Green : new ColorValue("#E5E5EA"), radius: 12);
            ctx.DrawCircle(new Point(c == 0 ? x + 184 : x + 162, 144), 10, White);
        }

        ctx.DrawRect(new Rect(175, 185, 445, 245), White, radius: 12);
        string[] senders = ["Ada Lovelace", "Grace Hopper", "Alan Turing", "Katherine Johnson", "Edsger Dijkstra", "Barbara Liskov"];
        for (int i = 0; i < senders.Length; i++)
        {
            float y = 195 + i * 39;
            if (i == 2)
            {
                ctx.DrawRect(new Rect(180, y - 4, 435, 38), accent.Opacity(0.12f), radius: 6);
            }
            ctx.DrawCircle(new Point(200, y + 14), 12, new[] { Purple, Orange, Teal, Red, Green, Blue }[i]);
            ctx.DrawText(senders[i][..1], 195, y + 6, 13, White, fontPath);
            ctx.DrawText(senders[i], 222, y + 1, 13, Ink, fontPath);
            ctx.DrawText("Re: render parity between the GPU and CPU paths", 222, y + 17, 11, Gray, fontPath);
            ctx.DrawText($"{9 + i}:4{i}", 575, y + 1, 11, Gray, fontPath);
            if (i < senders.Length - 1)
            {
                ctx.DrawRect(new Rect(222, y + 34.5f, 390, 0.5f), new ColorValue("#C6C6C8"));
            }
        }
    }

    // Text: sizes from 9 to 48 px in several colours, on several backgrounds, including colour
    // emoji with text and translucent text.
    private static void DrawText(DrawContext ctx, string fontPath)
    {
        Background(ctx, White);
        float[] sizes = [9, 11, 13, 15, 18, 24, 32, 48];
        float y = 8;
        foreach (float size in sizes)
        {
            ctx.DrawText($"Sphinx of black quartz {size}px", 10, y, size, Ink, fontPath);
            y += size * 1.25f + 2;
        }
        ctx.DrawRect(new Rect(330, 0, 310, 440), Slate);
        y = 8;
        foreach (float size in sizes)
        {
            ctx.DrawText($"Judge my vow {size}", 340, y, size, White, fontPath);
            y += size * 1.25f + 2;
        }
        ctx.DrawText("Emoji \U0001F600\U0001F680 mix", 10, 330, 22, Ink, fontPath);
        ctx.DrawText("Faded \U0001F389 text", 10, 365, 22, Ink.Opacity(0.4f), fontPath);
        ctx.DrawText("Tinted", 10, 400, 22, Blue, fontPath);
        ctx.DrawText("Tinted", 340, 400, 22, Orange, fontPath);
    }

    private static ImageSource BuildChecker()
    {
        const int size = 8;
        byte[] rgba = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = (y * size + x) * 4;
                bool dark = ((x + y) & 1) == 0;
                rgba[i] = dark ? (byte)30 : (byte)240;
                rgba[i + 1] = dark ? (byte)60 : (byte)200;
                rgba[i + 2] = dark ? (byte)140 : (byte)40;
                rgba[i + 3] = 255;
            }
        }
        return ImageSource.FromBytes(rgba, size, size);
    }

    private static ImageSource BuildAlphaRamp()
    {
        const int width = 64;
        const int height = 16;
        byte[] rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 4;
                rgba[i] = 255;
                rgba[i + 1] = (byte)(y * 16);
                rgba[i + 2] = 0;
                rgba[i + 3] = (byte)(x * 255 / (width - 1));
            }
        }
        return ImageSource.FromBytes(rgba, width, height);
    }

    private static ImageSource BuildPhoto()
    {
        const int size = 64;
        byte[] rgba = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = (y * size + x) * 4;
                float dx = x - 40f;
                float dy = y - 20f;
                bool sun = dx * dx + dy * dy < 81f;
                bool hill = y > 40 + 6f * MathF.Sin(x * 0.2f);
                rgba[i] = sun ? (byte)255 : hill ? (byte)(40 + x) : (byte)(90 + y);
                rgba[i + 1] = sun ? (byte)210 : hill ? (byte)(140 + y / 2) : (byte)(150 + y);
                rgba[i + 2] = sun ? (byte)60 : hill ? (byte)50 : (byte)(230 - y);
                rgba[i + 3] = 255;
            }
        }
        return ImageSource.FromBytes(rgba, size, size);
    }
}

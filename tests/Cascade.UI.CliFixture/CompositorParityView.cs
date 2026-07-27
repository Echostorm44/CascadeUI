namespace Cascade.UI.CliFixture;

/// <summary>
/// Fixture view for the RENDER-001 compositor characterization net. Renders a
/// "parity card" — one row per primitive kind, each drawn by a <see cref="Canvas"/>
/// in a unique fill colour with anchor text on top — twice: once directly (draws
/// land in the main frame) and once inside a <see cref="ScrollView"/> (draws land in
/// a retained layer). Tests assert that every primitive renders the same in the layer
/// as direct, so the Phase 2–4 unification cannot silently drop or distort a primitive
/// (the way layer images were dropped before the 2026-06-26 fix).
///
/// <para>Each row covers its whole canvas with the primitive and stamps anchor text on
/// top, so <c>whodrew</c> at an anchor glyph (located via <c>glyph_instances</c>)
/// returns both the glyph and the primitive beneath it — no fragile device-coordinate
/// math. Rows are in a fixed order so the test maps them by ascending Y.</para>
///
/// Selected by <c>CASCADE_FIXTURE_VIEW=parity</c>; the default fixture is unchanged.
/// </summary>
internal sealed class CompositorParityView : Component
{
    // Unique, fully-opaque fill per primitive — the test verifies the layer draw
    // carries the same fill as the direct draw, so these must be distinct.
    internal const string RectHex = "#FF3B30FF";   // red
    internal const string CircleHex = "#34C759FF"; // green
    internal const string LineHex = "#007AFFFF";   // blue
    internal const string ImageHex = "#FF9500FF";  // orange (solid image)

    // Row order — tests map layer rows to primitives by ascending device Y.
    internal static readonly string[] RowOrder = ["rect", "circle", "line", "image"];

    private const float RowWidth = 340f;
    private const float RowHeight = 40f;

    private static readonly ImageSource SolidImage = MakeSolidImage(ImageHex);

    protected override Node Render()
    {
        // The ScrollView is the central element so `cascade mcp scroll` (which targets
        // the client-area center) drives it. Its viewport (200px) shows all four
        // primitive rows (~190px) at the top; filler rows below overflow it so scroll
        // tests actually move the content.
        return new Center(
            new Column(
                spacing: 12,
                crossAxisAlignment: CrossAxisAlignment.Center,
                children: new Node[]
                {
                    new Label("CompositorParityTitle").FontSize(16),
                    new ScrollView(new Column(spacing: 10, children: ScrollCardContent()))
                        .Width(RowWidth + 20f).Height(200f),
                }));
    }

    private static Node[] ParityRows()
    {
        return
        [
            PrimitiveRow("rect", DrawFilledRect),
            PrimitiveRow("circle", DrawFilledCircle),
            PrimitiveRow("line", DrawThickLine),
            PrimitiveRow("image", DrawSolidImage),
        ];
    }

    private static Node[] ScrollCardContent()
    {
        var primitives = ParityRows();
        var rows = new Node[primitives.Length + 6];
        Array.Copy(primitives, rows, primitives.Length);
        for (int i = 0; i < 6; i++)
        {
            rows[primitives.Length + i] = new Label($"FillerRow{i + 1:D2}");
        }
        return rows;
    }

    private static Node PrimitiveRow(string anchor, Action<DrawContext, Size> drawPrimitive)
    {
        return CanvasFactory.Canvas(
            new Size(RowWidth, RowHeight),
            (ctx, size) =>
            {
                drawPrimitive(ctx, size);
                // Anchor text on top, centred — gives tests a glyph to locate the row
                // and a pixel to probe (the primitive is drawn beneath it).
                ctx.DrawText(anchor, size.Width / 2f - 16f, size.Height / 2f - 8f, 14f,
                    new ColorValue("#000000FF"));
            });
    }

    private static void DrawFilledRect(DrawContext ctx, Size size)
    {
        ctx.DrawRect(new Rect(0, 0, size.Width, size.Height),
            fill: new ColorValue(RectHex), radius: 8f);
    }

    private static void DrawFilledCircle(DrawContext ctx, Size size)
    {
        // A full-row ellipse: a curved-fill primitive that covers the row's left half,
        // so a render-truthful colour probe off the centred anchor text lands on it.
        ctx.DrawEllipse(new Rect(0, 0, size.Width, size.Height), fill: new ColorValue(CircleHex));
    }

    private static void DrawThickLine(DrawContext ctx, Size size)
    {
        ctx.DrawLine(new Point(0, size.Height / 2f), new Point(size.Width, size.Height / 2f),
            new Stroke(new ColorValue(LineHex), size.Height - 4f));
    }

    private static void DrawSolidImage(DrawContext ctx, Size size)
    {
        ctx.DrawImage(SolidImage, new Rect(0, 0, size.Width, size.Height));
    }

    /// <summary>Builds a small solid-colour RGBA bitmap for the image-parity row.</summary>
    private static ImageSource MakeSolidImage(string hex)
    {
        var c = new ColorValue(hex);
        byte r = (byte)Math.Clamp((int)MathF.Round(c.R * 255f), 0, 255);
        byte g = (byte)Math.Clamp((int)MathF.Round(c.G * 255f), 0, 255);
        byte b = (byte)Math.Clamp((int)MathF.Round(c.B * 255f), 0, 255);
        const int dim = 16;
        var rgba = new byte[dim * dim * 4];
        for (int i = 0; i < dim * dim; i++)
        {
            rgba[i * 4] = r;
            rgba[i * 4 + 1] = g;
            rgba[i * 4 + 2] = b;
            rgba[i * 4 + 3] = 255;
        }
        return ImageSource.FromBytes(rgba, dim, dim);
    }
}

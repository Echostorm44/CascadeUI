using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Controls;

/// <summary>
/// An icon button drew its glyph at a flat half of its footprint, so the 24px buttons a table row
/// uses carried a 12px glyph lost in the circle; and without <c>.Size()</c> it asked for 40px and
/// took whatever height a short row gave it, painting a 40x28 pill. The glyph now follows
/// <see cref="IconButton.GlyphSizeFor"/>, the footprint shrinks to stay square in the room it is
/// given, and the circle is drawn in the largest square of the laid-out bounds.
/// </summary>
[NotInParallel(["FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class IconButtonSizingTests
{
    private static readonly Icon PinIcon = new("M12 17v5M9 3h6l-1 7 4 4H6l4-4z", new Size(24, 24), 24f, "Pin");

    [Test]
    [Arguments(40f, 20f)]
    [Arguments(24f, 16f)]
    [Arguments(32f, 18f)]
    [Arguments(56f, 24f)]
    [Arguments(12f, 9f)]
    public async Task GlyphSize_FollowsTheFootprint(float footprint, float glyph)
    {
        await Assert.That(IconButton.GlyphSizeFor(footprint)).IsEqualTo(glyph).Within(0.001f);
    }

    [Test]
    public async Task GlyphSize_NeverShrinksAsTheButtonGrows_AndStaysInsideIt()
    {
        float previous = 0f;
        for (float footprint = 8f; footprint <= 128f; footprint += 1f)
        {
            float glyph = IconButton.GlyphSizeFor(footprint);
            await Assert.That(glyph).IsGreaterThanOrEqualTo(previous);
            await Assert.That(glyph).IsLessThan(footprint);
            previous = glyph;
        }
    }

    [Test]
    public async Task Default_IsFortySquare()
    {
        var size = Measure(new IconButton(PinIcon, () => { }), new Size(500, 500));

        await Assert.That(size.Width).IsEqualTo(40f);
        await Assert.That(size.Height).IsEqualTo(40f);
    }

    [Test]
    public async Task Default_InAShortRow_ShrinksToASquare()
    {
        var size = Measure(new IconButton(PinIcon, () => { }), new Size(500, 28));

        await Assert.That(size.Width).IsEqualTo(28f);
        await Assert.That(size.Height).IsEqualTo(28f);
    }

    [Test]
    public async Task ExplicitSize_InANarrowSlot_ShrinksToASquare()
    {
        var size = Measure(new IconButton(PinIcon, () => { }).Size(32), new Size(20, 500));

        await Assert.That(size.Width).IsEqualTo(20f);
        await Assert.That(size.Height).IsEqualTo(20f);
    }

    [Test]
    public async Task ExplicitSize_WithRoom_IsKept()
    {
        var size = Measure(new IconButton(PinIcon, () => { }).Size(24), new Size(500, 500));

        await Assert.That(size.Width).IsEqualTo(24f);
        await Assert.That(size.Height).IsEqualTo(24f);
    }

    [Test]
    public async Task Size24_DrawsASixteenPixelGlyph()
    {
        var (circle, glyph) = Paint(new IconButton(PinIcon, () => { }).Size(24));

        await Assert.That(circle.W).IsEqualTo(24f).Within(0.01f);
        await Assert.That(glyph.W).IsEqualTo(16f).Within(0.01f);
    }

    [Test]
    public async Task StretchedBounds_PaintARoundButton_NotAPill()
    {
        var (circle, glyph) = Paint(new IconButton(PinIcon, () => { }).Width(120));

        // Laid out 120x40: the circle is the 40px square in the middle, with the default glyph.
        await Assert.That(circle.W).IsEqualTo(40f).Within(0.01f);
        await Assert.That(circle.H).IsEqualTo(40f).Within(0.01f);
        await Assert.That(circle.Radius).IsEqualTo(20f).Within(0.01f);
        await Assert.That(circle.X).IsEqualTo(40f).Within(0.01f);
        await Assert.That(glyph.W).IsEqualTo(20f).Within(0.01f);
        await Assert.That(glyph.X + (glyph.W / 2f)).IsEqualTo(60f).Within(0.01f);
    }

    private static Size Measure(IconButton button, Size room)
    {
        new LayoutEngine().Layout(button, LayoutConstraints.Loose(room));
        return button.LayoutData.MeasuredSize;
    }

    private static Func<Node> content = () => Node.Empty;

    private sealed class Host : Component
    {
        protected override Node Render()
        {
            return content();
        }
    }

    /// <summary>
    /// Paints <paramref name="button"/> alone in a frame and returns its background circle and its
    /// glyph image, in the button's own coordinates.
    /// </summary>
    private static (EtchBackend.SceneOp Circle, EtchBackend.SceneOp Glyph) Paint(IconButton button)
    {
        content = () => new Column(children: [button]);
        using var backend = new EtchBackend();
        using var orchestrator = new FrameOrchestrator(() => { }, () => { })
        {
            Theme = new AppleTheme(ThemeMode.Dark),
            RenderBackend = backend,
        };
        orchestrator.BeginFrameCallback = () =>
        {
            backend.Reset();
            return (1UL, 400u, 300u);
        };

        orchestrator.MountRoot<Host>(400f, 300f);
        orchestrator.Tick();

        var circle = backend.Commands.First(op => op.Kind == EtchBackend.OpKind.DrawRect && op.Radius > 0f && op.Fill is not null);
        var glyph = backend.Commands.First(op => op.Kind == EtchBackend.OpKind.DrawImage);
        return (circle, glyph);
    }
}

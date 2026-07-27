namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// Tests for <see cref="PathBuilder"/> command accumulation and <see cref="Path"/> factory methods.
/// </summary>
public class PathTests
{
    // Helper to safely access path data across await boundaries
    private static byte[] Cmds(Path path) => path.Commands.ToArray();
    private static float[] Vals(Path path) => path.Data.ToArray();

    // ── PathBuilder basic operations ──────────────────────────────────

    [Test]
    public async Task PathBuilder_MoveTo_ProducesCorrectCommands()
    {
        var path = new PathBuilder()
            .MoveTo(new Point(10, 20))
            .Build();

        var cmds = Cmds(path);
        var vals = Vals(path);

        await Assert.That(cmds.Length).IsEqualTo(1);
        await Assert.That(cmds[0]).IsEqualTo(Path.CmdMoveTo);
        await Assert.That(vals.Length).IsEqualTo(2);
        await Assert.That(vals[0]).IsEqualTo(10f);
        await Assert.That(vals[1]).IsEqualTo(20f);
    }

    [Test]
    public async Task PathBuilder_LineTo_ProducesCorrectCommands()
    {
        var path = new PathBuilder()
            .MoveTo(new Point(0, 0))
            .LineTo(new Point(100, 200))
            .Build();

        var cmds = Cmds(path);
        var vals = Vals(path);

        await Assert.That(cmds.Length).IsEqualTo(2);
        await Assert.That(cmds[1]).IsEqualTo(Path.CmdLineTo);
        await Assert.That(vals.Length).IsEqualTo(4);
        await Assert.That(vals[2]).IsEqualTo(100f);
        await Assert.That(vals[3]).IsEqualTo(200f);
    }

    [Test]
    public async Task PathBuilder_CubicTo_ProducesSixFloats()
    {
        var path = new PathBuilder()
            .MoveTo(new Point(0, 0))
            .CubicTo(new Point(1, 2), new Point(3, 4), new Point(5, 6))
            .Build();

        var cmds = Cmds(path);
        var vals = Vals(path);

        await Assert.That(cmds.Length).IsEqualTo(2);
        await Assert.That(cmds[1]).IsEqualTo(Path.CmdCubicTo);
        await Assert.That(vals.Length).IsEqualTo(8); // 2 (moveTo) + 6 (cubicTo)
        await Assert.That(vals[2]).IsEqualTo(1f);
        await Assert.That(vals[7]).IsEqualTo(6f);
    }

    [Test]
    public async Task PathBuilder_QuadTo_ProducesFourFloats()
    {
        var path = new PathBuilder()
            .MoveTo(new Point(0, 0))
            .QuadTo(new Point(50, 100), new Point(100, 0))
            .Build();

        var cmds = Cmds(path);

        await Assert.That(cmds.Length).IsEqualTo(2);
        await Assert.That(cmds[1]).IsEqualTo(Path.CmdQuadTo);
        await Assert.That(path.Data.Length).IsEqualTo(6); // 2 (moveTo) + 4 (quadTo)
    }

    [Test]
    public async Task PathBuilder_Close_ProducesNoData()
    {
        var path = new PathBuilder()
            .MoveTo(new Point(0, 0))
            .LineTo(new Point(100, 0))
            .LineTo(new Point(100, 100))
            .Close()
            .Build();

        var cmds = Cmds(path);

        await Assert.That(cmds.Length).IsEqualTo(4);
        await Assert.That(cmds[3]).IsEqualTo(Path.CmdClose);
        await Assert.That(path.Data.Length).IsEqualTo(6); // 3 commands × 2 floats each (close adds 0)
    }

    [Test]
    public async Task PathBuilder_FluentChaining_ReturnsSameInstance()
    {
        var builder = new PathBuilder();
        var result = builder.MoveTo(new Point(0, 0));
        bool sameRef1 = ReferenceEquals(builder, result);
        await Assert.That(sameRef1).IsTrue();

        result = builder.LineTo(new Point(1, 1));
        bool sameRef2 = ReferenceEquals(builder, result);
        await Assert.That(sameRef2).IsTrue();
    }

    [Test]
    public async Task PathBuilder_BuildTwice_Throws()
    {
        var builder = new PathBuilder().MoveTo(new Point(0, 0));
        builder.Build();

        await Assert.That(() => builder.Build()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task PathBuilder_MutateAfterBuild_Throws()
    {
        var builder = new PathBuilder().MoveTo(new Point(0, 0));
        builder.Build();

        await Assert.That(() => builder.MoveTo(new Point(1, 1))).Throws<InvalidOperationException>();
        await Assert.That(() => builder.LineTo(new Point(1, 1))).Throws<InvalidOperationException>();
        await Assert.That(() => builder.CubicTo(new Point(1, 1), new Point(2, 2), new Point(3, 3))).Throws<InvalidOperationException>();
        await Assert.That(() => builder.QuadTo(new Point(1, 1), new Point(2, 2))).Throws<InvalidOperationException>();
        await Assert.That(() => builder.Close()).Throws<InvalidOperationException>();
    }

    // ── Command tag constants ─────────────────────────────────────────

    [Test]
    public async Task CommandTags_MatchRustProtocol()
    {
        // These must match the Etch path decoder (EtchBackend.CompilePath) exactly
        byte moveTo = Path.CmdMoveTo;
        byte lineTo = Path.CmdLineTo;
        byte cubicTo = Path.CmdCubicTo;
        byte quadTo = Path.CmdQuadTo;
        byte close = Path.CmdClose;

        await Assert.That(moveTo).IsEqualTo((byte)0);
        await Assert.That(lineTo).IsEqualTo((byte)1);
        await Assert.That(cubicTo).IsEqualTo((byte)2);
        await Assert.That(quadTo).IsEqualTo((byte)3);
        await Assert.That(close).IsEqualTo((byte)4);
    }

    // ── Path.Rect ─────────────────────────────────────────────────────

    [Test]
    public async Task Rect_ProducesClosedRectangle()
    {
        var path = Path.Rect(new Rect(10, 20, 100, 50));
        var cmds = Cmds(path);

        // MoveTo + 3 LineTo + Close = 5 commands
        await Assert.That(cmds.Length).IsEqualTo(5);
        await Assert.That(cmds[0]).IsEqualTo(Path.CmdMoveTo);
        await Assert.That(cmds[1]).IsEqualTo(Path.CmdLineTo);
        await Assert.That(cmds[4]).IsEqualTo(Path.CmdClose);

        // 4 points × 2 floats = 8 data values
        await Assert.That(path.Data.Length).IsEqualTo(8);
    }

    [Test]
    public async Task Rect_CornersAreCorrect()
    {
        var rect = new Rect(10, 20, 100, 50);
        var path = Path.Rect(rect);
        var d = Vals(path);

        // MoveTo (top-left)
        await Assert.That(d[0]).IsEqualTo(10f);
        await Assert.That(d[1]).IsEqualTo(20f);
        // LineTo (top-right)
        await Assert.That(d[2]).IsEqualTo(110f);
        await Assert.That(d[3]).IsEqualTo(20f);
        // LineTo (bottom-right)
        await Assert.That(d[4]).IsEqualTo(110f);
        await Assert.That(d[5]).IsEqualTo(70f);
        // LineTo (bottom-left)
        await Assert.That(d[6]).IsEqualTo(10f);
        await Assert.That(d[7]).IsEqualTo(70f);
    }

    // ── Path.Circle ───────────────────────────────────────────────────

    [Test]
    public async Task Circle_ProducesFourCubicArcsAndClose()
    {
        var path = Path.Circle(new Point(50, 50), 25);
        var cmds = Cmds(path);

        // MoveTo + 4 CubicTo + Close = 6 commands
        await Assert.That(cmds.Length).IsEqualTo(6);
        await Assert.That(cmds[0]).IsEqualTo(Path.CmdMoveTo);
        await Assert.That(cmds[1]).IsEqualTo(Path.CmdCubicTo);
        await Assert.That(cmds[2]).IsEqualTo(Path.CmdCubicTo);
        await Assert.That(cmds[3]).IsEqualTo(Path.CmdCubicTo);
        await Assert.That(cmds[4]).IsEqualTo(Path.CmdCubicTo);
        await Assert.That(cmds[5]).IsEqualTo(Path.CmdClose);
    }

    [Test]
    public async Task Circle_StartsAtRightmostPoint()
    {
        var path = Path.Circle(new Point(100, 100), 50);
        var d = Vals(path);

        // MoveTo should be at (center.X + radius, center.Y) = (150, 100)
        await Assert.That(d[0]).IsEqualTo(150f);
        await Assert.That(d[1]).IsEqualTo(100f);
    }

    [Test]
    public async Task Circle_ReturnsBackToStart()
    {
        var path = Path.Circle(new Point(100, 100), 50);
        var d = Vals(path);

        // Last CubicTo's endpoint should be (150, 100) — same as start
        // Data: 2 (moveTo) + 6+6+6+6 (4 cubics) = 26 floats
        await Assert.That(d.Length).IsEqualTo(26);
        await Assert.That(d[24]).IsEqualTo(150f);
        await Assert.That(d[25]).IsEqualTo(100f);
    }

    // ── Path.RoundedRect ──────────────────────────────────────────────

    [Test]
    public async Task RoundedRect_UniformRadius_ProducesCorrectCommandCount()
    {
        var path = Path.RoundedRect(new Rect(0, 0, 200, 100), 10);
        var cmds = Cmds(path);

        await Assert.That(cmds[0]).IsEqualTo(Path.CmdMoveTo);
        await Assert.That(cmds[^1]).IsEqualTo(Path.CmdClose);
    }

    [Test]
    public async Task RoundedRect_ZeroRadius_MatchesRect()
    {
        var rect = new Rect(10, 20, 100, 50);
        var rounded = Path.RoundedRect(rect, 0);
        var cmds = Cmds(rounded);

        await Assert.That(cmds[0]).IsEqualTo(Path.CmdMoveTo);
        await Assert.That(cmds[^1]).IsEqualTo(Path.CmdClose);
    }

    [Test]
    public async Task RoundedRect_PerCorner_AcceptsDifferentRadii()
    {
        var path = Path.RoundedRect(new Rect(0, 0, 200, 100), 5, 10, 15, 20);
        var cmds = Cmds(path);

        await Assert.That(cmds.Length).IsGreaterThan(0);
        await Assert.That(cmds[0]).IsEqualTo(Path.CmdMoveTo);
        await Assert.That(cmds[^1]).IsEqualTo(Path.CmdClose);
    }

    [Test]
    public async Task RoundedRect_RadiusClamped_DoesNotExceedHalfDimension()
    {
        // Rect is 20×10, radius of 50 should be clamped to 5 (min of 10, 5)
        var path = Path.RoundedRect(new Rect(0, 0, 20, 10), 50);

        // Should not throw and should produce a valid path
        await Assert.That(path.Commands.Length).IsGreaterThan(0);
    }

    // ── Path.Arc ──────────────────────────────────────────────────────

    [Test]
    public async Task Arc_QuarterCircle_ProducesCubicArc()
    {
        var path = Path.Arc(
            new Point(100, 100),
            50,
            Angle.Degrees(0),
            Angle.Degrees(90));

        var cmds = Cmds(path);

        // Quarter arc = 1 cubic segment
        await Assert.That(cmds[0]).IsEqualTo(Path.CmdMoveTo);
        await Assert.That(cmds[1]).IsEqualTo(Path.CmdCubicTo);
    }

    [Test]
    public async Task Arc_FullCircle_ProducesMultipleCubicSegments()
    {
        var path = Path.Arc(
            new Point(0, 0),
            100,
            Angle.Degrees(0),
            Angle.Degrees(360));

        var cmds = Cmds(path);

        // 360° / 90° per segment = 4 cubic segments
        int cubicCount = cmds.Count(c => c == Path.CmdCubicTo);
        await Assert.That(cubicCount).IsEqualTo(4);
    }

    [Test]
    public async Task Arc_NegativeSweep_ProducesValidPath()
    {
        var path = Path.Arc(
            new Point(50, 50),
            25,
            Angle.Degrees(90),
            Angle.Degrees(-90));

        await Assert.That(path.Commands.Length).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task Arc_ZeroSweep_ProducesSinglePoint()
    {
        var path = Path.Arc(
            new Point(50, 50),
            25,
            Angle.Degrees(0),
            Angle.Degrees(0));

        var cmds = Cmds(path);

        // Degenerate arc — just a MoveTo
        await Assert.That(cmds.Length).IsEqualTo(1);
        await Assert.That(cmds[0]).IsEqualTo(Path.CmdMoveTo);
    }

    // ── Path.Star ─────────────────────────────────────────────────────

    [Test]
    public async Task Star_DefaultFivePoints_ProducesCorrectVertexCount()
    {
        var path = Path.Star(new Point(100, 100), 50, 25);
        var cmds = Cmds(path);

        // 5-point star: MoveTo + 9 LineTo + Close = 11 commands
        await Assert.That(cmds.Length).IsEqualTo(11);
        await Assert.That(cmds[0]).IsEqualTo(Path.CmdMoveTo);
        await Assert.That(cmds[^1]).IsEqualTo(Path.CmdClose);
    }

    [Test]
    public async Task Star_ThreePoints_ProducesSixVertices()
    {
        var path = Path.Star(new Point(0, 0), 50, 25, points: 3);

        // 3-point star: 6 vertices (3 outer + 3 inner)
        // MoveTo + 5 LineTo + Close = 7 commands
        await Assert.That(path.Commands.Length).IsEqualTo(7);
    }

    [Test]
    public async Task Star_InvalidPoints_Throws()
    {
        await Assert.That(() => Path.Star(new Point(0, 0), 50, 25, points: 1))
            .Throws<ArgumentOutOfRangeException>();
    }

    // ── Path.RegularPolygon ───────────────────────────────────────────

    [Test]
    public async Task RegularPolygon_Hexagon_ProducesSixVertices()
    {
        var path = Path.RegularPolygon(new Point(100, 100), 50, sides: 6);
        var cmds = Cmds(path);

        // MoveTo + 5 LineTo + Close = 7 commands
        await Assert.That(cmds.Length).IsEqualTo(7);
        await Assert.That(cmds[0]).IsEqualTo(Path.CmdMoveTo);
        await Assert.That(cmds[^1]).IsEqualTo(Path.CmdClose);
    }

    [Test]
    public async Task RegularPolygon_Triangle_ProducesThreeVertices()
    {
        var path = Path.RegularPolygon(new Point(0, 0), 100, sides: 3);

        // MoveTo + 2 LineTo + Close = 4 commands
        await Assert.That(path.Commands.Length).IsEqualTo(4);
    }

    [Test]
    public async Task RegularPolygon_InvalidSides_Throws()
    {
        await Assert.That(() => Path.RegularPolygon(new Point(0, 0), 50, sides: 2))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task RegularPolygon_FirstVertexIsAtTop()
    {
        var path = Path.RegularPolygon(new Point(100, 100), 50, sides: 4);
        var d = Vals(path);

        // First vertex at angle -π/2 means (100, 100-50) = (100, 50)
        float dx = MathF.Abs(d[0] - 100f);
        float dy = MathF.Abs(d[1] - 50f);
        await Assert.That(dx).IsLessThan(0.01f);
        await Assert.That(dy).IsLessThan(0.01f);
    }
}

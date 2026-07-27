using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using IOPath = System.IO.Path;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// RENDER-001 characterization net. Locks the retained-layer compositor's current
/// (correct) behaviour before the Phase 2–4 unification: every primitive kind drawn
/// inside a ScrollView must actually composite into the layer — the failure mode that
/// shipped invisibly when layer images were dropped (fixed 2026-06-26).
///
/// <para>Drives the <c>CASCADE_FIXTURE_VIEW=parity</c> fixture, whose ScrollView rows
/// each fill a canvas with a unique-coloured primitive (rect / ellipse / line / image)
/// and stamp anchor text on top. The probe is <em>render-truthful</em>: it reads the
/// actual framebuffer colour (a 1×1 device-space screenshot) just off the anchor text,
/// so it verifies what the layer composite genuinely put on screen — independent of the
/// devtools provenance path, whose shape records are mis-placed for layer canvas content
/// (a separate finding, see RENDER-001 spec). A dropped primitive reads as the dark
/// theme background, not its colour.</para>
/// </summary>
[NotInParallel("CliIntegration")]
public class CompositorParityTests
{
    private static readonly string AppId = CliTestHarness.NewFixtureAppId();
    private static Process? fixtureProcess;
    private static string? outputDir;

    // Must mirror CompositorParityView; row order is top-to-bottom in the card.
    private static readonly (string Name, byte R, byte G, byte B)[] Rows =
    [
        ("rect", 0xFF, 0x3B, 0x30),   // red
        ("circle", 0x34, 0xC7, 0x59), // green ellipse
        ("line", 0x00, 0x7A, 0xFF),   // blue
        ("image", 0xFF, 0x95, 0x00),  // orange
    ];

    // Horizontal offset (device px) from a row's anchor-text centre to a point that is
    // on the primitive but clear of the (black) text.
    private const int ProbeDx = 90;
    private const int ColourTolerance = 28;

    [Before(Class)]
    public static async Task LaunchFixture()
    {
        outputDir = IOPath.Combine(IOPath.GetTempPath(), $"cascade-parity-{Environment.ProcessId}");
        Directory.CreateDirectory(outputDir);
        fixtureProcess = CliTestHarness.StartFixture(
            AppId, new Dictionary<string, string> { ["CASCADE_FIXTURE_VIEW"] = "parity" });
        await CliTestHarness.WaitForFixtureRegistrationAsync(AppId, TimeSpan.FromSeconds(30), fixtureProcess.Id);
    }

    [After(Class)]
    public static void KillFixture()
    {
        if (fixtureProcess is not null)
        {
            if (!fixtureProcess.HasExited)
            {
                fixtureProcess.Kill(entireProcessTree: true);
                fixtureProcess.WaitForExit(5000);
            }
            fixtureProcess.Dispose();
            fixtureProcess = null;
        }

        if (outputDir is not null && Directory.Exists(outputDir))
        {
            try { Directory.Delete(outputDir, recursive: true); }
            catch (IOException) { /* best-effort */ }
        }
    }

    [Test]
    public async Task EveryPrimitiveKind_CompositesIntoTheLayer()
    {
        // The ScrollView defaults to the top, where all four primitive rows are visible.
        var rows = await ReadLayerRowCentresAsync();
        await Assert.That(rows.Count).IsGreaterThanOrEqualTo(Rows.Length)
            .Because("all four parity rows must render as retained-layer content");

        for (int i = 0; i < Rows.Length; i++)
        {
            (string name, byte r, byte g, byte b) = Rows[i];
            (int cx, int cy) = rows[i];
            (int sr, int sg, int sb) = await SampleDeviceColourAsync(cx - ProbeDx, cy);

            await Assert.That(IsClose(sr, sg, sb, r, g, b))
                .IsTrue()
                .Because($"the '{name}' row must composite its primitive into the layer; " +
                    $"expected ~({r},{g},{b}) but the framebuffer showed ({sr},{sg},{sb})");
        }
    }

    /// <summary>
    /// RENDER-003 regression: `whodrew` shape provenance must match the render for
    /// layer-canvas content. Before the fix, the devtools splice added the full composite
    /// offset without subtracting the clip origin, so a layer-canvas shape's record was
    /// double-offset by the ScrollView origin and reported ~origin pixels from where it
    /// painted (a top-row rect showed up at the image row). Here the rect's `whodrew`
    /// record must be a `layer`-tagged geometry rect whose bounds contain the probe point,
    /// and the framebuffer at that same point must be the rect's colour — provenance == render.
    /// </summary>
    [Test]
    public async Task LayerShapeProvenance_MatchesRender()
    {
        var rows = await ReadLayerRowCentresAsync();
        await Assert.That(rows.Count).IsGreaterThan(0);
        (int cx, int cy) = rows[0]; // rect row
        int px = cx - ProbeDx;

        // Framebuffer truth: the probe point is red (the rect).
        (int sr, int sg, int sb) = await SampleDeviceColourAsync(px, cy);
        (_, byte rr, byte rg, byte rb) = Rows[0];
        await Assert.That(IsClose(sr, sg, sb, rr, rg, rb))
            .IsTrue()
            .Because($"the rect must render red at the probe point, saw ({sr},{sg},{sb})");

        // Provenance: a layer-tagged geometry rect whose bounds contain that same point.
        JsonObject? rect = null;
        foreach (JsonNode? node in await WhodrewAsync(px, cy))
        {
            var draw = (JsonObject)node!;
            if (draw["kind"]?.GetValue<string>() == "rect" && draw.ContainsKey("layer"))
            {
                rect = draw;
                break;
            }
        }

        await Assert.That(rect)
            .IsNotNull()
            .Because($"whodrew at the rect's rendered pixel ({px},{cy}) must report a layer rect");
        await Assert.That(rect!["layer"]!.GetValue<long>()).IsGreaterThan(0);

        var bounds = (JsonObject)rect["bounds"]!;
        double bx = bounds["x"]!.GetValue<double>();
        double by = bounds["y"]!.GetValue<double>();
        double bw = bounds["width"]!.GetValue<double>();
        double bh = bounds["height"]!.GetValue<double>();
        await Assert.That(px >= bx && px <= bx + bw && cy >= by && cy <= by + bh)
            .IsTrue()
            .Because($"the rect's provenance bounds ({bx},{by},{bw},{bh}) must contain the " +
                $"point ({px},{cy}) where it actually painted — not be offset by the ScrollView origin");
    }

    /// <summary>
    /// Named regression for the 2026-06-26 fix: an image inside a ScrollView is
    /// composited by the per-layer image pass, not silently dropped (which would read
    /// as the dark theme background instead of the image's orange).
    /// </summary>
    [Test]
    public async Task LayerImage_IsComposited()
    {
        var rows = await ReadLayerRowCentresAsync();
        int idx = Array.FindIndex(Rows, r => r.Name == "image");
        await Assert.That(rows.Count).IsGreaterThan(idx);

        (int cx, int cy) = rows[idx];
        (int sr, int sg, int sb) = await SampleDeviceColourAsync(cx - ProbeDx, cy);

        (_, byte r, byte g, byte b) = Rows[idx];
        await Assert.That(IsClose(sr, sg, sb, r, g, b))
            .IsTrue()
            .Because($"a ScrollView icon must blit through the per-layer image pass; " +
                $"expected orange ~({r},{g},{b}) but saw ({sr},{sg},{sb})");
    }

    /// <summary>
    /// The composited layer applies the scroll delta: after a small scroll the top
    /// row's device Y decreases. Guards the offset handling the historical nav-misclick
    /// (double-counted offset) bug got wrong. Uses glyph instances only — reliable and
    /// render-matching — and restores the scroll so sibling tests see a clean state.
    /// </summary>
    [Test]
    public async Task LayerPrimitive_TracksScroll()
    {
        // Track the image row by its unique orange colour, so the comparison is robust
        // to other rows clipping in/out of the viewport (which changes row indices).
        int? y0 = await FindImageRowYAsync();
        await Assert.That(y0.HasValue).IsTrue().Because("the image row must be visible at the top");

        try
        {
            var scroll = await CliTestHarness.RunCliAsync("mcp", "scroll", "--delta-y", "30", "--app", AppId);
            await Assert.That(scroll.ExitCode).IsEqualTo(0);

            int? y1 = await FindImageRowYAsync();
            await Assert.That(y1.HasValue)
                .IsTrue()
                .Because("the image row stays visible after a small scroll");

            await Assert.That(y1!.Value).IsLessThan(y0!.Value)
                .Because("the retained layer must re-composite at the new scroll offset");
            await Assert.That(y0.Value - y1.Value).IsGreaterThanOrEqualTo(10);
        }
        finally
        {
            await CliTestHarness.RunCliAsync("mcp", "scroll", "--delta-y", "-1000", "--app", AppId);
        }
    }

    // ── Helpers ──────────────────────────────────────────────

    /// <summary>
    /// One probe point (device-space integer pixel) per retained-layer row, ordered top
    /// to bottom, at the row's anchor-text centre. Layer glyph positions are reliable and
    /// render-matching. The first glyph_instances call enables capture and blocks until a
    /// fully tagged snapshot is published.
    /// </summary>
    private static async Task<List<(int X, int Y)>> ReadLayerRowCentresAsync()
    {
        var result = await CliTestHarness.RunCliAsync("mcp", "instances", "--limit", "2000", "--app", AppId);
        await Assert.That(result.ExitCode).IsEqualTo(0);
        var response = (JsonObject)JsonNode.Parse(result.StdOut.Trim())!;

        var centres = new List<(double X, double Y)>();
        foreach (JsonNode? node in response["instances"]!.AsArray())
        {
            var glyph = (JsonObject)node!;
            if (glyph["category"]?.GetValue<string>() != "layer")
            {
                continue;
            }
            var bnd = (JsonObject)glyph["bounds"]!;
            centres.Add((
                bnd["x"]!.GetValue<double>() + bnd["width"]!.GetValue<double>() / 2.0,
                bnd["y"]!.GetValue<double>() + bnd["height"]!.GetValue<double>() / 2.0));
        }

        // Cluster glyphs into rows by Y (rows are ~50 logical px apart; an 18px device
        // band groups a row without merging neighbours).
        centres.Sort((a, b) => a.Y.CompareTo(b.Y));
        var rows = new List<(int X, int Y)>();
        double bandY = 0, xSum = 0;
        int count = 0;
        foreach ((double x, double y) in centres)
        {
            if (count > 0 && y - bandY > 18.0)
            {
                rows.Add(((int)Math.Round(xSum / count), (int)Math.Round(bandY)));
                xSum = 0;
                count = 0;
            }
            if (count == 0)
            {
                bandY = y;
            }
            xSum += x;
            count++;
        }
        if (count > 0)
        {
            rows.Add(((int)Math.Round(xSum / count), (int)Math.Round(bandY)));
        }
        return rows;
    }

    /// <summary>Returns the device Y of the image row — the layer row whose off-text
    /// pixel reads orange — or null if it is not currently visible. Identifies the row by
    /// colour so it survives other rows clipping in/out of the viewport.</summary>
    private static async Task<int?> FindImageRowYAsync()
    {
        (_, byte r, byte g, byte b) = Rows[Array.FindIndex(Rows, row => row.Name == "image")];
        foreach ((int cx, int cy) in await ReadLayerRowCentresAsync())
        {
            (int sr, int sg, int sb) = await SampleDeviceColourAsync(cx - ProbeDx, cy);
            if (IsClose(sr, sg, sb, r, g, b))
            {
                return cy;
            }
        }
        return null;
    }

    /// <summary>Returns every draw `whodrew` reports as touching a device-space pixel.</summary>
    private static async Task<JsonArray> WhodrewAsync(int x, int y)
    {
        var result = await CliTestHarness.RunCliAsync(
            "mcp", "whodrew",
            x.ToString(CultureInfo.InvariantCulture),
            y.ToString(CultureInfo.InvariantCulture),
            "--app", AppId);
        await Assert.That(result.ExitCode).IsEqualTo(0);
        var response = (JsonObject)JsonNode.Parse(result.StdOut.Trim())!;
        return response["draws"]!.AsArray();
    }

    /// <summary>Reads the rendered colour of a single device-space framebuffer pixel.</summary>
    private static async Task<(int R, int G, int B)> SampleDeviceColourAsync(int x, int y)
    {
        string path = IOPath.Combine(outputDir!, $"px-{x}-{y}.png");
        var result = await CliTestHarness.RunCliAsync(
            "mcp", "screenshot",
            "--region", $"{x.ToString(CultureInfo.InvariantCulture)},{y.ToString(CultureInfo.InvariantCulture)},1,1",
            "--region-space", "device",
            "-o", path, "--app", AppId);
        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(File.Exists(path)).IsTrue();
        return DecodeSinglePixelPng(await File.ReadAllBytesAsync(path));
    }

    /// <summary>
    /// Decodes the RGB of a 1×1 PNG. Walks chunks for the IDAT zlib stream, inflates it,
    /// and reads the first scanline's pixel (a 1×1 image has no left/up neighbour, so any
    /// PNG filter resolves to the stored bytes). Works for RGB and RGBA colour types.
    /// </summary>
    private static (int R, int G, int B) DecodeSinglePixelPng(byte[] png)
    {
        const int sig = 8;
        int pos = sig;
        using var idat = new MemoryStream();
        while (pos + 8 <= png.Length)
        {
            int len = (png[pos] << 24) | (png[pos + 1] << 16) | (png[pos + 2] << 8) | png[pos + 3];
            string type = Encoding.ASCII.GetString(png, pos + 4, 4);
            int dataStart = pos + 8;
            if (type == "IDAT")
            {
                idat.Write(png, dataStart, len);
            }
            pos = dataStart + len + 4; // skip data + CRC
            if (type == "IEND")
            {
                break;
            }
        }

        idat.Position = 0;
        using var inflate = new ZLibStream(idat, CompressionMode.Decompress);
        Span<byte> scan = stackalloc byte[8];
        int read = 0;
        while (read < scan.Length)
        {
            int n = inflate.Read(scan[read..]);
            if (n == 0)
            {
                break;
            }
            read += n;
        }

        // scan[0] is the filter byte; the pixel's R,G,B follow.
        return (scan[1], scan[2], scan[3]);
    }

    private static bool IsClose(int r, int g, int b, int er, int eg, int eb)
    {
        return Math.Abs(r - er) <= ColourTolerance
            && Math.Abs(g - eg) <= ColourTolerance
            && Math.Abs(b - eb) <= ColourTolerance;
    }
}

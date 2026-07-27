using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// WP-3517 regression: ScrollView retained-layer content must be clipped to its
/// viewport on the GPU path. Before the fix, a deep scroll painted early rows
/// over the title and button — the painter's viewport <c>PushClip</c> never
/// reached the GPU for composited layer instances or layer glyphs.
///
/// <para>The probe is GPU-truthful: <c>whodrew</c> reports a glyph quad as
/// touching a pixel only when the quad covers it <em>and</em> the glyph's clip
/// rect includes it — the same discard the glyph shader performs. So a layer
/// glyph that bleeds above the viewport shows up here exactly when it would
/// show up on screen. The negative assertion (no <c>category:"layer"</c> draw
/// above the viewport top) is paired with a positive control inside the
/// viewport, so it cannot pass merely because capture is broken.</para>
/// </summary>
[NotInParallel("CliIntegration")]
public class LayerViewportClipTests
{
    private static readonly string AppId = CliTestHarness.NewFixtureAppId();
    private static Process? fixtureProcess;

    [Before(Class)]
    public static async Task LaunchFixture()
    {
        fixtureProcess = CliTestHarness.StartFixture(AppId);
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
    }

    [Test]
    public async Task LayerContent_AfterDeepScroll_DoesNotDrawAboveViewport()
    {
        // Push the ScrollView content well up so the early rows would land over
        // the title/button if the viewport clip never reached the GPU. The
        // scroll target defaults to the client-area center, which the fixture
        // guarantees sits inside the 300x240 ScrollView.
        for (int i = 0; i < 8; i++)
        {
            var scroll = await CliTestHarness.RunCliAsync(
                "mcp", "scroll", "--delta-y", "40", "--app", AppId);
            await Assert.That(scroll.ExitCode).IsEqualTo(0);
            var scrollResponse = (JsonObject)JsonNode.Parse(scroll.StdOut.Trim())!;
            await Assert.That(scrollResponse["success"]!.GetValue<bool>()).IsTrue();
        }

        // Read the post-scroll glyph instances. The first instances call enables
        // draw capture and repaints, so retry briefly until the layer glyphs
        // (the ScrollView rows) appear in a settled snapshot.
        JsonArray layerGlyphs = await ReadLayerGlyphsAsync();

        // Every layer glyph must carry a clip rect — the viewport. Its top edge
        // is what content above must be clipped against.
        float viewportTop = float.MaxValue;
        float columnX = float.NaN;
        foreach (JsonNode? node in layerGlyphs)
        {
            var glyph = (JsonObject)node!;
            JsonObject? clip = glyph["clip"] as JsonObject;
            await Assert.That(clip).IsNotNull();
            float clipY = (float)clip!["y"]!.GetValue<double>();
            if (clipY < viewportTop)
            {
                viewportTop = clipY;
            }

            if (float.IsNaN(columnX))
            {
                var bounds = (JsonObject)glyph["bounds"]!;
                columnX = (float)(bounds["x"]!.GetValue<double>() + bounds["width"]!.GetValue<double>() / 2);
            }
        }

        await Assert.That(viewportTop).IsLessThan(float.MaxValue);
        await Assert.That(float.IsNaN(columnX)).IsFalse();

        // Positive control: a layer glyph sitting well inside the viewport must
        // still report as a layer draw. This proves the capture/clip pipeline
        // attributes layer glyphs correctly, so the negative probe below means
        // "clipped away", not "capture is dead".
        JsonObject? interior = null;
        foreach (JsonNode? node in layerGlyphs)
        {
            var glyph = (JsonObject)node!;
            var bounds = (JsonObject)glyph["bounds"]!;
            float top = (float)bounds["y"]!.GetValue<double>();
            float height = (float)bounds["height"]!.GetValue<double>();
            if (top >= viewportTop + 20 && top + height <= viewportTop + 180)
            {
                interior = glyph;
                break;
            }
        }

        await Assert.That(interior).IsNotNull();
        var interiorBounds = (JsonObject)interior!["bounds"]!;
        int interiorX = (int)(interiorBounds["x"]!.GetValue<double>() + interiorBounds["width"]!.GetValue<double>() / 2);
        int interiorY = (int)(interiorBounds["y"]!.GetValue<double>() + interiorBounds["height"]!.GetValue<double>() / 2);
        await Assert.That(await PixelHasLayerDrawAsync(interiorX, interiorY)).IsTrue();

        // Regression assertion: across a band of pixels just above the viewport
        // top — where scrolled-away rows would bleed — no layer glyph may draw.
        int col = (int)columnX;
        foreach (int dy in new[] { 4, 10, 20, 40, 80 })
        {
            int probeY = (int)viewportTop - dy;
            if (probeY < 0)
            {
                continue;
            }

            bool layerDraw = await PixelHasLayerDrawAsync(col, probeY);
            await Assert.That(layerDraw)
                .IsFalse()
                .Because($"layer content bled {dy}px above the viewport top at ({col},{probeY})");
        }
    }

    /// <summary>
    /// Queries the full-frame glyph instances and returns those drawn by the
    /// retained ScrollView layer (<c>category == "layer"</c>). The first query
    /// enables draw capture and, as of WP-3516, blocks inside the app until a
    /// fully tagged snapshot is published — every retained layer re-rendered with
    /// provenance — so a single call returns the settled layer glyphs without a
    /// wall-clock poll here. (The old poll re-read the same cached snapshot, so it
    /// could never recover from a pre-tag first frame; the wait now lives in
    /// EnsureDrawSnapshot, keyed off snapshot completeness rather than wall time.)
    /// </summary>
    private static async Task<JsonArray> ReadLayerGlyphsAsync()
    {
        var result = await CliTestHarness.RunCliAsync(
            "mcp", "instances", "--limit", "2000", "--app", AppId);
        await Assert.That(result.ExitCode).IsEqualTo(0);
        var response = (JsonObject)JsonNode.Parse(result.StdOut.Trim())!;

        var layerGlyphs = new JsonArray();
        foreach (JsonNode? node in response["instances"]!.AsArray())
        {
            var glyph = (JsonObject)node!;
            if (glyph["category"]?.GetValue<string>() == "layer")
            {
                layerGlyphs.Add(JsonNode.Parse(glyph.ToJsonString()));
            }
        }

        await Assert.That(layerGlyphs.Count)
            .IsGreaterThan(0)
            .Because("the fixture ScrollView rows must render as retained-layer glyphs");
        return layerGlyphs;
    }

    private static async Task<bool> PixelHasLayerDrawAsync(int x, int y)
    {
        var whodrew = await CliTestHarness.RunCliAsync(
            "mcp", "whodrew",
            x.ToString(CultureInfo.InvariantCulture),
            y.ToString(CultureInfo.InvariantCulture),
            "--app", AppId);
        await Assert.That(whodrew.ExitCode).IsEqualTo(0);

        var response = (JsonObject)JsonNode.Parse(whodrew.StdOut.Trim())!;
        foreach (JsonNode? draw in response["draws"]!.AsArray())
        {
            var obj = (JsonObject)draw!;
            if (obj["pass"]?.GetValue<string>() == "glyph" &&
                obj["category"]?.GetValue<string>() == "layer")
            {
                return true;
            }
        }

        return false;
    }
}

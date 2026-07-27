using System.Linq;

namespace Cascade.UI.Tests;

/// <summary>
/// WP-3521b: bidi layout integration. Mixed-direction text must now be reordered
/// into correct visual order (the RTL portions reversed), while pure-LTR and
/// pure-RTL text keep rendering as before. Asserts on per-cluster glyph X
/// positions from the real layout pipeline (HarfBuzz shaping + Inter primary +
/// system-font fallback for Hebrew).
/// </summary>
public class BidiLayoutTests
{
    private const string Hebrew = "שלום"; // שלום (logical: ש=0 ל=1 ו=2 ם=3)

    private static string InterPath()
    {
        string p = System.IO.Path.Combine(AppContext.BaseDirectory, "fonts", "Inter-Regular.ttf");
        if (!File.Exists(p))
        {
            throw new FileNotFoundException(p);
        }
        return p;
    }

    /// <summary>Maps each logical cluster index to the X of its first glyph.</summary>
    private static Dictionary<int, float> ClusterX(string text)
    {
        var opts = new TextLayoutOptions
        {
            FontPath = InterPath(),
            FontSize = 24f,
            MaxWidth = 4000f,
            MaxHeight = 1000f,
            MaxLines = 1,
            LineHeightMultiplier = 1f,
        };
        var line = TextLayoutEngine.Layout(text, opts).LinesArray[0];
        var map = new Dictionary<int, float>();
        foreach (var g in line.GlyphsArray)
        {
            if (!map.ContainsKey(g.ClusterIndex))
            {
                map[g.ClusterIndex] = g.X;
            }
        }
        return map;
    }

    [TUnit.Core.Test]
    public async Task PureLtr_RendersInLogicalOrder()
    {
        var x = ClusterX("abc");
        await TUnit.Assertions.Assert.That(x[0] < x[1]).IsTrue();
        await TUnit.Assertions.Assert.That(x[1] < x[2]).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task PureHebrew_RendersRightToLeft()
    {
        // Pure RTL goes through the existing (HarfBuzz-guess) path; first logical
        // char ש (cluster 0) must sit to the RIGHT of the last char ם (cluster 3).
        var x = ClusterX(Hebrew);
        await TUnit.Assertions.Assert.That(x[0] > x[1]).IsTrue();
        await TUnit.Assertions.Assert.That(x[1] > x[2]).IsTrue();
        await TUnit.Assertions.Assert.That(x[2] > x[3]).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task MixedDirection_HebrewWordIsReversed()
    {
        // "abc " + שלום + " xyz". Logical Hebrew clusters are 4(ש) 5(ל) 6(ו) 7(ם).
        // Correct bidi renders the Hebrew word RTL, so ש (4) is to the RIGHT of ם (7).
        var x = ClusterX("abc " + Hebrew + " xyz");
        await TUnit.Assertions.Assert.That(x[4] > x[5]).IsTrue();
        await TUnit.Assertions.Assert.That(x[5] > x[6]).IsTrue();
        await TUnit.Assertions.Assert.That(x[6] > x[7]).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task MixedDirection_LatinSegmentsKeepOrderAndStraddleHebrew()
    {
        var x = ClusterX("abc " + Hebrew + " xyz");

        // Latin runs stay left-to-right.
        await TUnit.Assertions.Assert.That(x[0] < x[1]).IsTrue();
        await TUnit.Assertions.Assert.That(x[1] < x[2]).IsTrue();
        await TUnit.Assertions.Assert.That(x[9] < x[10]).IsTrue();
        await TUnit.Assertions.Assert.That(x[10] < x[11]).IsTrue();

        // The Hebrew block sits between the two Latin runs visually.
        float hebrewMin = new[] { x[4], x[5], x[6], x[7] }.Min();
        float hebrewMax = new[] { x[4], x[5], x[6], x[7] }.Max();
        await TUnit.Assertions.Assert.That(x[2] < hebrewMin).IsTrue();   // abc left of Hebrew
        await TUnit.Assertions.Assert.That(hebrewMax < x[9]).IsTrue();   // Hebrew left of xyz
    }
}

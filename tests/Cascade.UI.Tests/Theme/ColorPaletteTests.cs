namespace Cascade.UI.Tests;

/// <summary>
/// Locks the chart palette generation. <see cref="ColorPalette.FromTheme"/> once cycled
/// raw theme tokens (fifth series = <c>Colors.Text</c>, a black bar in light mode); then
/// it rotated hue around the accent in OkLCH at the accent's own chroma, which left
/// non-accent hues undersaturated and the charts flat. It now leads with a curated,
/// vibrant categorical palette (theme-independent) and only falls back to OkLCH for
/// overflow series beyond the curated set. These tests guard the contract: vivid, opaque,
/// distinct, and never the text/surface colours.
/// </summary>
public class ColorPaletteTests
{
    [Test]
    public async Task FromTheme_ReturnsRequestedCount()
    {
        var palette = ColorPalette.FromTheme(new AppleTheme(ThemeMode.Light), count: 6);
        await Assert.That(palette.Count).IsEqualTo(6);
    }

    [Test]
    public async Task FromTheme_FirstSeriesIsCuratedAndVibrant()
    {
        // The palette is theme-independent now; the lead series is the curated blue,
        // which is also the Apple accent — so the common case is visually unchanged.
        var palette = ColorPalette.FromTheme(new AppleTheme(ThemeMode.Light));
        await Assert.That(palette.GetColor(0)).IsEqualTo(new ColorValue("#007AFF"));

        var (_, chroma, _, alpha) = palette.GetColor(0).ToOkLch();
        await Assert.That(alpha).IsEqualTo(1f).Within(0.001f);
        await Assert.That(chroma).IsGreaterThan(0.08f);
    }

    [Test]
    public async Task FromTheme_IsThemeIndependent()
    {
        // Curated colours are fixed, so charts stay vibrant and consistent regardless of
        // the active theme's accent.
        var apple = ColorPalette.FromTheme(new AppleTheme(ThemeMode.Dark), count: 10);
        var material = ColorPalette.FromTheme(new Material3Theme(ThemeMode.Light), count: 10);
        for (var i = 0; i < 10; i++)
        {
            await Assert.That(apple.GetColor(i)).IsEqualTo(material.GetColor(i));
        }
    }

    [Test]
    public async Task FromTheme_OverflowBeyondCuratedIsVividAndDistinct()
    {
        // Series past the curated set come from the OkLCH golden-angle fallback; they
        // must stay vivid, opaque, and distinct from one another.
        var palette = ColorPalette.FromTheme(new AppleTheme(ThemeMode.Light), count: 16);
        await Assert.That(palette.Count).IsEqualTo(16);

        for (var i = 10; i < 16; i++)
        {
            var (_, chroma, _, alpha) = palette.GetColor(i).ToOkLch();
            await Assert.That(alpha).IsEqualTo(1f).Within(0.001f);
            await Assert.That(chroma).IsGreaterThan(0.10f); // punchier than the old accent-chroma rotation
            for (var j = i + 1; j < 16; j++)
            {
                await Assert.That(palette.GetColor(i)).IsNotEqualTo(palette.GetColor(j));
            }
        }
    }

    [Test]
    public async Task FromTheme_NeverUsesTextOrSurfaceColors()
    {
        // The exact regression: a light-mode chart must not contain a black bar
        // (Colors.Text) or invisible white/near-white bars (Surface/SurfaceAlt).
        foreach (var mode in new[] { ThemeMode.Light, ThemeMode.Dark })
        {
            var theme = new AppleTheme(mode);
            var palette = ColorPalette.FromTheme(theme, count: 8);

            for (var i = 0; i < palette.Count; i++)
            {
                var c = palette.GetColor(i);
                await Assert.That(c).IsNotEqualTo(theme.Colors.Text);
                await Assert.That(c).IsNotEqualTo(theme.Colors.Surface);
                await Assert.That(c).IsNotEqualTo(theme.Colors.SurfaceAlt);
            }
        }
    }

    [Test]
    public async Task FromTheme_AllSeriesAreVividAndOpaque()
    {
        var palette = ColorPalette.FromTheme(new AppleTheme(ThemeMode.Light), count: 8);

        for (var i = 0; i < palette.Count; i++)
        {
            var (_, chroma, _, alpha) = palette.GetColor(i).ToOkLch();
            await Assert.That(alpha).IsEqualTo(1f).Within(0.001f);
            // A black/white/grey series would have ~zero chroma; require real colour.
            await Assert.That(chroma).IsGreaterThan(0.03f);
        }
    }

    [Test]
    public async Task FromTheme_SeriesAreDistinct()
    {
        var palette = ColorPalette.FromTheme(new AppleTheme(ThemeMode.Light), count: 8);

        for (var i = 0; i < palette.Count; i++)
        {
            for (var j = i + 1; j < palette.Count; j++)
            {
                await Assert.That(palette.GetColor(i)).IsNotEqualTo(palette.GetColor(j));
            }
        }
    }

    [Test]
    public async Task GetColor_WrapsAroundIndex()
    {
        var palette = ColorPalette.FromTheme(new AppleTheme(ThemeMode.Light), count: 4);
        await Assert.That(palette.GetColor(4)).IsEqualTo(palette.GetColor(0));
        await Assert.That(palette.GetColor(5)).IsEqualTo(palette.GetColor(1));
    }

    [Test]
    public async Task FromTheme_ThrowsOnNonPositiveCount()
    {
        await Assert.That(() => ColorPalette.FromTheme(new AppleTheme(), count: 0))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task FromTheme_WorksAcrossAllBuiltInThemes()
    {
        CascadeTheme[] themes =
        [
            new AppleTheme(ThemeMode.Light),
            new AppleTheme(ThemeMode.Dark),
            new FluentTheme(ThemeMode.Light),
            new FluentTheme(ThemeMode.Dark),
            new Material3Theme(ThemeMode.Light),
            new Material3Theme(ThemeMode.Dark),
        ];

        foreach (var theme in themes)
        {
            var palette = ColorPalette.FromTheme(theme, count: 8);
            await Assert.That(palette.Count).IsEqualTo(8);
            // Every series vivid and opaque on every theme (the curated set is fixed, but
            // this guards the contract across the FromTheme entry points).
            for (var i = 0; i < palette.Count; i++)
            {
                var (_, chroma, _, alpha) = palette.GetColor(i).ToOkLch();
                await Assert.That(alpha).IsEqualTo(1f).Within(0.001f);
                await Assert.That(chroma).IsGreaterThan(0.03f);
            }
        }
    }

    // ── ColorValue.ToOkLch (the enabler) ─────────────────────────

    [Test]
    public async Task ToOkLch_RoundTripsWithOkLchFactory()
    {
        var color = ColorValue.OkLch(0.62f, 0.15f, 250f);
        var (l, c, h, a) = color.ToOkLch();

        await Assert.That(l).IsEqualTo(0.62f).Within(0.002f);
        await Assert.That(c).IsEqualTo(0.15f).Within(0.002f);
        await Assert.That(h).IsEqualTo(250f).Within(0.5f);
        await Assert.That(a).IsEqualTo(1f).Within(0.001f);
    }

    [Test]
    public async Task ToOkLch_TransparentReturnsZeros()
    {
        var (l, c, h, a) = ColorValue.Transparent.ToOkLch();
        await Assert.That(l).IsEqualTo(0f).Within(0.001f);
        await Assert.That(c).IsEqualTo(0f).Within(0.001f);
        await Assert.That(h).IsEqualTo(0f).Within(0.001f);
        await Assert.That(a).IsEqualTo(0f).Within(0.001f);
    }
}

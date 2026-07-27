namespace Cascade.UI.Tests;

/// <summary>
/// Locks the distinction between <see cref="ColorValue.Opacity(float)"/> (sets alpha)
/// and <see cref="ColorValue.ScaleAlpha(float)"/> (multiplies alpha). Conflating the
/// two turned the themes' 25%-alpha focus rings into fully-opaque blue outlines when
/// faded in by progress — <c>ring.Opacity(progress)</c> overwrote the 0.25 with 1.0.
/// </summary>
public class ColorAlphaTests
{
    [Test]
    public async Task Opacity_SetsAlphaToArgument()
    {
        var c = new ColorValue("#0A84FF").Opacity(0.25f);

        await Assert.That(c.A).IsEqualTo(0.25f).Within(0.001f);

        // Opacity SETS — a second call overrides the first.
        await Assert.That(c.Opacity(1.0f).A).IsEqualTo(1.0f).Within(0.001f);
    }

    [Test]
    public async Task ScaleAlpha_MultipliesExistingAlpha()
    {
        var ring = new ColorValue("#0A84FF").Opacity(0.25f);

        // Full progress preserves the resting transparency (does NOT become opaque).
        await Assert.That(ring.ScaleAlpha(1.0f).A).IsEqualTo(0.25f).Within(0.001f);
        // Half progress halves the alpha.
        await Assert.That(ring.ScaleAlpha(0.5f).A).IsEqualTo(0.125f).Within(0.001f);
        // Zero progress is fully transparent.
        await Assert.That(ring.ScaleAlpha(0f).A).IsEqualTo(0f).Within(0.001f);
    }

    [Test]
    public async Task ScaleAlpha_PreservesHue()
    {
        var blue = new ColorValue("#0A84FF").Opacity(0.25f);
        var scaled = blue.ScaleAlpha(0.5f);

        // Un-premultiplied channel ratios are unchanged — only alpha scales.
        await Assert.That(scaled.R / scaled.A).IsEqualTo(blue.R / blue.A).Within(0.01f);
        await Assert.That(scaled.G / scaled.A).IsEqualTo(blue.G / blue.A).Within(0.01f);
        await Assert.That(scaled.B / scaled.A).IsEqualTo(blue.B / blue.A).Within(0.01f);
    }
}

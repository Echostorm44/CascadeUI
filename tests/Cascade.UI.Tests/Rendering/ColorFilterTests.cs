namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// Tests for <see cref="ColorFilter"/> factory methods and their color matrices.
/// </summary>
public class ColorFilterTests
{
    [Test]
    public async Task Grayscale_ProducesValidMatrix()
    {
        var filter = ColorFilter.Grayscale();
        await Assert.That(filter.Matrix).IsNotNull();
        await Assert.That(filter.Matrix.Length).IsEqualTo(20);
    }

    [Test]
    public async Task Grayscale_RowsUseLuminanceWeights()
    {
        var m = ColorFilter.Grayscale().Matrix;

        // R, G, B rows should all have the same luminance weights
        // Rec. 709: R=0.2126, G=0.7152, B=0.0722
        float tolerance = 0.001f;

        // Row 0 (R output)
        await Assert.That(MathF.Abs(m[0] - 0.2126f) < tolerance).IsTrue();
        await Assert.That(MathF.Abs(m[1] - 0.7152f) < tolerance).IsTrue();
        await Assert.That(MathF.Abs(m[2] - 0.0722f) < tolerance).IsTrue();

        // Row 1 (G output) should match Row 0
        await Assert.That(MathF.Abs(m[5] - m[0]) < tolerance).IsTrue();
        await Assert.That(MathF.Abs(m[6] - m[1]) < tolerance).IsTrue();
        await Assert.That(MathF.Abs(m[7] - m[2]) < tolerance).IsTrue();

        // Row 2 (B output) should match Row 0
        await Assert.That(MathF.Abs(m[10] - m[0]) < tolerance).IsTrue();
        await Assert.That(MathF.Abs(m[11] - m[1]) < tolerance).IsTrue();
        await Assert.That(MathF.Abs(m[12] - m[2]) < tolerance).IsTrue();
    }

    [Test]
    public async Task Grayscale_AlphaRowIsIdentity()
    {
        var m = ColorFilter.Grayscale().Matrix;

        // Row 3 (A output): should be [0, 0, 0, 1, 0]
        await Assert.That(m[15]).IsEqualTo(0f);
        await Assert.That(m[16]).IsEqualTo(0f);
        await Assert.That(m[17]).IsEqualTo(0f);
        await Assert.That(m[18]).IsEqualTo(1f);
        await Assert.That(m[19]).IsEqualTo(0f);
    }

    [Test]
    public async Task Sepia_ProducesValidMatrix()
    {
        var filter = ColorFilter.Sepia();
        await Assert.That(filter.Matrix.Length).IsEqualTo(20);

        // Sepia should have R row summing to roughly 1.351
        float rowSum = filter.Matrix[0] + filter.Matrix[1] + filter.Matrix[2];
        await Assert.That(rowSum > 1.0f).IsTrue(); // warm tint adds brightness
    }

    [Test]
    public async Task Invert_ProducesNegativeDiagonalWithOffset()
    {
        var m = ColorFilter.Invert().Matrix;

        // R row: [-1, 0, 0, 0, 1]
        await Assert.That(m[0]).IsEqualTo(-1f);
        await Assert.That(m[4]).IsEqualTo(1f);

        // G row: [0, -1, 0, 0, 1]
        await Assert.That(m[6]).IsEqualTo(-1f);
        await Assert.That(m[9]).IsEqualTo(1f);

        // B row: [0, 0, -1, 0, 1]
        await Assert.That(m[12]).IsEqualTo(-1f);
        await Assert.That(m[14]).IsEqualTo(1f);
    }

    [Test]
    public async Task Tint_ZeroIntensity_IsIdentity()
    {
        var m = ColorFilter.Tint(new ColorValue("#FF0000"), intensity: 0f).Matrix;

        // With zero intensity, R diagonal should be 1.0 (identity)
        await Assert.That(m[0]).IsEqualTo(1f);
        await Assert.That(m[6]).IsEqualTo(1f);
        await Assert.That(m[12]).IsEqualTo(1f);

        // Offsets should be zero
        await Assert.That(m[4]).IsEqualTo(0f);
        await Assert.That(m[9]).IsEqualTo(0f);
        await Assert.That(m[14]).IsEqualTo(0f);
    }

    [Test]
    public async Task Tint_FullIntensity_HasZeroDiagonal()
    {
        var m = ColorFilter.Tint(new ColorValue("#FF0000"), intensity: 1f).Matrix;

        // At full intensity, diagonal should be 0 (all color from tint)
        await Assert.That(m[0]).IsEqualTo(0f);
        await Assert.That(m[6]).IsEqualTo(0f);
        await Assert.That(m[12]).IsEqualTo(0f);
    }

    [Test]
    public async Task Saturate_FactorOne_IsNearIdentity()
    {
        var m = ColorFilter.Saturate(factor: 1.0f).Matrix;
        float tolerance = 0.001f;

        // At factor 1.0, should be close to identity for RGB diagonal
        await Assert.That(MathF.Abs(m[0] - 1.0f) < tolerance).IsTrue();
        await Assert.That(MathF.Abs(m[6] - 1.0f) < tolerance).IsTrue();
        await Assert.That(MathF.Abs(m[12] - 1.0f) < tolerance).IsTrue();
    }

    [Test]
    public async Task Saturate_FactorZero_EqualsGrayscale()
    {
        var sat = ColorFilter.Saturate(factor: 0f).Matrix;
        var gray = ColorFilter.Grayscale().Matrix;
        float tolerance = 0.001f;

        // Saturate(0) should produce the same matrix as Grayscale
        for (int i = 0; i < 15; i++)
        {
            await Assert.That(MathF.Abs(sat[i] - gray[i]) < tolerance).IsTrue();
        }
    }

    [Test]
    public async Task Brightness_FactorOne_IsIdentity()
    {
        var m = ColorFilter.Brightness(factor: 1.0f).Matrix;

        await Assert.That(m[0]).IsEqualTo(1f);
        await Assert.That(m[6]).IsEqualTo(1f);
        await Assert.That(m[12]).IsEqualTo(1f);
    }

    [Test]
    public async Task Brightness_FactorTwo_DoublesDiagonal()
    {
        var m = ColorFilter.Brightness(factor: 2.0f).Matrix;

        await Assert.That(m[0]).IsEqualTo(2f);
        await Assert.That(m[6]).IsEqualTo(2f);
        await Assert.That(m[12]).IsEqualTo(2f);
    }

    [Test]
    public async Task Contrast_FactorOne_IsIdentity()
    {
        var m = ColorFilter.Contrast(factor: 1.0f).Matrix;
        float tolerance = 0.001f;

        await Assert.That(MathF.Abs(m[0] - 1.0f) < tolerance).IsTrue();
        await Assert.That(MathF.Abs(m[4]) < tolerance).IsTrue(); // offset should be 0
    }

    [Test]
    public async Task Contrast_FactorZero_ProducesGrayOffset()
    {
        var m = ColorFilter.Contrast(factor: 0f).Matrix;

        // At factor 0, diagonal is 0, offset is 0.5 (everything becomes gray)
        await Assert.That(m[0]).IsEqualTo(0f);
        await Assert.That(m[4]).IsEqualTo(0.5f);
        await Assert.That(m[6]).IsEqualTo(0f);
        await Assert.That(m[9]).IsEqualTo(0.5f);
    }

    [Test]
    public async Task AllFilters_PreserveAlpha()
    {
        var filters = new[]
        {
            ColorFilter.Grayscale(),
            ColorFilter.Sepia(),
            ColorFilter.Invert(),
            ColorFilter.Tint(new ColorValue("#00FF00")),
            ColorFilter.Saturate(),
            ColorFilter.Brightness(),
            ColorFilter.Contrast()
        };

        foreach (var filter in filters)
        {
            // Alpha row should be [0, 0, 0, 1, 0] — alpha passes through unchanged
            await Assert.That(filter.Matrix[15]).IsEqualTo(0f);
            await Assert.That(filter.Matrix[16]).IsEqualTo(0f);
            await Assert.That(filter.Matrix[17]).IsEqualTo(0f);
            await Assert.That(filter.Matrix[18]).IsEqualTo(1f);
            await Assert.That(filter.Matrix[19]).IsEqualTo(0f);
        }
    }
}

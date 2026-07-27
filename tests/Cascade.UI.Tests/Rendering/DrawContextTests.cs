using System.IO.Compression;

namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// Tests for <see cref="DrawContext"/> in no-backend mode.
/// The parameterless internal constructor creates a DrawContext with a null backend.
/// All draw methods must be safe no-ops; scoped operations must return valid IDisposable guards.
/// </summary>
public class DrawContextTests
{
    // ── No-backend draw primitives ────────────────────────────────────

    [Test]
    public async Task DrawRect_SolidFill_NoBackend_DoesNotThrow()
    {
        var ctx = new DrawContext();
        Exception? caught = null;
        try
        {
            ctx.DrawRect(new Rect(0, 0, 100, 50), fill: new ColorValue("#FF0000"));
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        await Assert.That(caught).IsNull();
    }

    [Test]
    public async Task DrawRect_GradientFill_NoBackend_DoesNotThrow()
    {
        var ctx = new DrawContext();
        var gradient = Gradient.Linear(
            Angle.Degrees(90),
            new GradientStop(0f, new ColorValue("#FF0000")),
            new GradientStop(1f, new ColorValue("#0000FF")));
        Exception? caught = null;
        try
        {
            ctx.DrawRect(new Rect(0, 0, 100, 50), gradient);
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        await Assert.That(caught).IsNull();
    }

    [Test]
    public async Task DrawRect_WithStroke_NoBackend_DoesNotThrow()
    {
        var ctx = new DrawContext();
        var stroke = new Stroke(new ColorValue("#000000"), 2f);
        Exception? caught = null;
        try
        {
            ctx.DrawRect(new Rect(0, 0, 100, 50), stroke: stroke);
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        await Assert.That(caught).IsNull();
    }

    [Test]
    public async Task DrawCircle_NoBackend_DoesNotThrow()
    {
        var ctx = new DrawContext();
        Exception? caught = null;
        try
        {
            ctx.DrawCircle(new Point(50, 50), 25, fill: new ColorValue("#00FF00"));
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        await Assert.That(caught).IsNull();
    }

    [Test]
    public async Task DrawEllipse_NoBackend_DoesNotThrow()
    {
        var ctx = new DrawContext();
        Exception? caught = null;
        try
        {
            ctx.DrawEllipse(new Rect(10, 10, 80, 40), fill: new ColorValue("#0000FF"));
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        await Assert.That(caught).IsNull();
    }

    [Test]
    public async Task DrawLine_NoBackend_DoesNotThrow()
    {
        var ctx = new DrawContext();
        var stroke = new Stroke(new ColorValue("#FF0000"), 1f);
        Exception? caught = null;
        try
        {
            ctx.DrawLine(new Point(0, 0), new Point(100, 100), stroke);
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        await Assert.That(caught).IsNull();
    }

    [Test]
    public async Task DrawArc_NoBackend_DoesNotThrow()
    {
        var ctx = new DrawContext();
        var stroke = new Stroke(new ColorValue("#FF0000"), 2f);
        Exception? caught = null;
        try
        {
            ctx.DrawArc(new Point(50, 50), 30, Angle.Degrees(0), Angle.Degrees(180), stroke);
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        await Assert.That(caught).IsNull();
    }

    [Test]
    public async Task DrawPath_SolidFill_NoBackend_DoesNotThrow()
    {
        var ctx = new DrawContext();
        var path = Path.Circle(new Point(50, 50), 25);
        Exception? caught = null;
        try
        {
            ctx.DrawPath(path, fill: new ColorValue("#FF0000"));
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        await Assert.That(caught).IsNull();
    }

    [Test]
    public async Task DrawPath_GradientFill_NoBackend_DoesNotThrow()
    {
        var ctx = new DrawContext();
        var path = Path.Rect(new Rect(0, 0, 100, 100));
        var gradient = Gradient.Radial(
            new Point(50, 50),
            50,
            new GradientStop(0f, new ColorValue("#FFFFFF")),
            new GradientStop(1f, new ColorValue("#000000")));
        Exception? caught = null;
        try
        {
            ctx.DrawPath(path, gradient);
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        await Assert.That(caught).IsNull();
    }

    [Test]
    public async Task DrawImage_NoBackend_DoesNotThrow()
    {
        var ctx = new DrawContext();
        using var image = ImageSource.FromBytes(
            new byte[] { 255, 0, 0, 255 },
            1, 1);
        Exception? caught = null;
        try
        {
            ctx.DrawImage(image, new Rect(0, 0, 100, 100));
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        await Assert.That(caught).IsNull();
    }

    // ── Scoped transforms ─────────────────────────────────────────────

    [Test]
    public async Task PushTranslate_NoBackend_ReturnsNonNullDisposable()
    {
        var ctx = new DrawContext();
        var scope = ctx.PushTranslate(10, 20);
        await Assert.That(scope).IsNotNull();
        scope.Dispose();
    }

    [Test]
    public async Task PushScale_NoBackend_ReturnsNonNullDisposable()
    {
        var ctx = new DrawContext();
        var scope = ctx.PushScale(2f, 2f);
        await Assert.That(scope).IsNotNull();
        scope.Dispose();
    }

    [Test]
    public async Task PushRotate_NoBackend_ReturnsNonNullDisposable()
    {
        var ctx = new DrawContext();
        var scope = ctx.PushRotate(Angle.Degrees(45));
        await Assert.That(scope).IsNotNull();
        scope.Dispose();
    }

    [Test]
    public async Task PushSkew_NoBackend_ReturnsNonNullDisposable()
    {
        var ctx = new DrawContext();
        var scope = ctx.PushSkew(0.1f, 0.1f);
        await Assert.That(scope).IsNotNull();
        scope.Dispose();
    }

    // ── Scoped clipping ───────────────────────────────────────────────

    [Test]
    public async Task PushClipRect_NoBackend_ReturnsNonNullDisposable()
    {
        var ctx = new DrawContext();
        var scope = ctx.PushClip(new Rect(0, 0, 100, 100));
        await Assert.That(scope).IsNotNull();
        scope.Dispose();
    }

    [Test]
    public async Task PushClipPath_NoBackend_ReturnsNonNullDisposable()
    {
        var ctx = new DrawContext();
        var path = Path.Circle(new Point(50, 50), 50);
        var scope = ctx.PushClip(path);
        await Assert.That(scope).IsNotNull();
        scope.Dispose();
    }

    [Test]
    public async Task PushRoundedClip_NoBackend_ReturnsNonNullDisposable()
    {
        var ctx = new DrawContext();
        var scope = ctx.PushRoundedClip(new Rect(0, 0, 100, 100), 8f);
        await Assert.That(scope).IsNotNull();
        scope.Dispose();
    }

    // ── Scoped opacity, blend, filters, layers ────────────────────────

    [Test]
    public async Task PushOpacity_NoBackend_ReturnsNonNullDisposable()
    {
        var ctx = new DrawContext();
        var scope = ctx.PushOpacity(0.5f);
        await Assert.That(scope).IsNotNull();
        scope.Dispose();
    }

    [Test]
    public async Task PushBlendMode_NoBackend_ReturnsNonNullDisposable()
    {
        var ctx = new DrawContext();
        var scope = ctx.PushBlendMode(BlendMode.Multiply);
        await Assert.That(scope).IsNotNull();
        scope.Dispose();
    }

    [Test]
    public async Task PushBlur_NoBackend_ReturnsNonNullDisposable()
    {
        var ctx = new DrawContext();
        var scope = ctx.PushBlur(4f);
        await Assert.That(scope).IsNotNull();
        scope.Dispose();
    }

    [Test]
    public async Task PushDropShadow_NoBackend_ReturnsNonNullDisposable()
    {
        var ctx = new DrawContext();
        var shadow = new ShadowValue(2f, 4f, 8f, 0f, new ColorValue("#00000080"));
        var scope = ctx.PushDropShadow(shadow);
        await Assert.That(scope).IsNotNull();
        scope.Dispose();
    }

    [Test]
    public async Task PushColorFilter_NoBackend_ReturnsNonNullDisposable()
    {
        var ctx = new DrawContext();
        var scope = ctx.PushColorFilter(ColorFilter.Grayscale());
        await Assert.That(scope).IsNotNull();
        scope.Dispose();
    }

    [Test]
    public async Task PushLayer_NoBackend_ReturnsNonNullDisposable()
    {
        var ctx = new DrawContext();
        var scope = ctx.PushLayer(0.8f, BlendMode.Screen);
        await Assert.That(scope).IsNotNull();
        scope.Dispose();
    }

    // ── ScopeGuard disposal safety ────────────────────────────────────

    [Test]
    public async Task ScopeGuard_Dispose_DoesNotThrow()
    {
        var ctx = new DrawContext();
        var scope = ctx.PushTranslate(5, 5);
        Exception? caught = null;
        try
        {
            scope.Dispose();
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        await Assert.That(caught).IsNull();
    }

    [Test]
    public async Task ScopeGuard_DoubleDispose_DoesNotThrow()
    {
        var ctx = new DrawContext();
        var scope = ctx.PushTranslate(5, 5);
        scope.Dispose();
        Exception? caught = null;
        try
        {
            scope.Dispose();
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        await Assert.That(caught).IsNull();
    }

    [Test]
    public async Task ScopeGuard_UsedInUsingBlock_DoesNotThrow()
    {
        var ctx = new DrawContext();
        Exception? caught = null;
        try
        {
            using (ctx.PushTranslate(10, 10))
            {
                using (ctx.PushScale(2, 2))
                {
                    ctx.DrawRect(new Rect(0, 0, 50, 50), fill: new ColorValue("#FF0000"));
                }
            }
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        await Assert.That(caught).IsNull();
    }

    // ── MeasureText ───────────────────────────────────────────────────

    [Test]
    public async Task MeasureText_EmptyString_ReturnsZero()
    {
        var ctx = new DrawContext();
        var size = ctx.MeasureText(string.Empty);
        await Assert.That(size.Width).IsEqualTo(0f);
        await Assert.That(size.Height).IsEqualTo(0f);
    }

    [Test]
    public async Task MeasureText_NullString_ReturnsZero()
    {
        var ctx = new DrawContext();
        var size = ctx.MeasureText(null!);
        await Assert.That(size.Width).IsEqualTo(0f);
        await Assert.That(size.Height).IsEqualTo(0f);
    }

    [Test]
    public async Task MeasureText_FiveChars_ReturnsExpectedDimensions()
    {
        var ctx = new DrawContext();
        var size = ctx.MeasureText("Hello");

        // width = 5 chars × 14px font × 0.6 avg ratio = 42
        float widthDiff = MathF.Abs(size.Width - 42f);
        await Assert.That(widthDiff).IsLessThan(0.01f);

        // height = 14px × 1.2 line height = 16.8
        float heightDiff = MathF.Abs(size.Height - 16.8f);
        await Assert.That(heightDiff).IsLessThan(0.01f);
    }

    [Test]
    public async Task MeasureText_SingleChar_ReturnsExpectedWidth()
    {
        var ctx = new DrawContext();
        var size = ctx.MeasureText("A");

        // width = 1 × 14 × 0.6 = 8.4
        float widthDiff = MathF.Abs(size.Width - 8.4f);
        await Assert.That(widthDiff).IsLessThan(0.01f);

        float heightDiff = MathF.Abs(size.Height - 16.8f);
        await Assert.That(heightDiff).IsLessThan(0.01f);
    }

    [Test]
    public async Task MeasureText_WidthScalesWithLength()
    {
        var ctx = new DrawContext();
        var size3 = ctx.MeasureText("abc");
        var size6 = ctx.MeasureText("abcdef");

        float ratio = size6.Width / size3.Width;
        float diff = MathF.Abs(ratio - 2f);
        await Assert.That(diff).IsLessThan(0.001f);
    }

    // ── Properties ────────────────────────────────────────────────────

    [Test]
    public async Task PixelRatio_DefaultIsOne()
    {
        var ctx = new DrawContext();
        await Assert.That(ctx.PixelRatio).IsEqualTo(1f);
    }

    [Test]
    public async Task Size_DefaultIsZero()
    {
        var ctx = new DrawContext();
        await Assert.That(ctx.Size.Width).IsEqualTo(0f);
        await Assert.That(ctx.Size.Height).IsEqualTo(0f);
    }

    // ── Text rendering (new DrawText API) ─────────────────────────────

    [Test]
    public async Task MeasureText_WithFontSize_ScalesProportionally()
    {
        var ctx = new DrawContext();
        var small = ctx.MeasureText("Test", 10f);
        var large = ctx.MeasureText("Test", 20f);
        await Assert.That(large.Width).IsGreaterThan(small.Width);
        await Assert.That(large.Height).IsGreaterThan(small.Height);
    }

    [Test]
    public async Task MeasureText_WithFontSize_EmptyString_ReturnsZero()
    {
        var ctx = new DrawContext();
        var size = ctx.MeasureText("", 14f);
        await Assert.That(size.Width).IsEqualTo(0f);
        await Assert.That(size.Height).IsEqualTo(0f);
    }

    [Test]
    public async Task DrawText_NoBackend_DoesNotThrow()
    {
        var ctx = new DrawContext();
        await Assert.That(() => ctx.DrawText("Hello", 10, 20, 14f, new ColorValue("#000000"))).ThrowsNothing();
    }

    [Test]
    public async Task DrawText_EmptyString_NoBackend_DoesNotThrow()
    {
        var ctx = new DrawContext();
        await Assert.That(() => ctx.DrawText("", 0, 0, 14f, new ColorValue("#000000"))).ThrowsNothing();
    }

    [Test]
    public async Task DrawText_NullString_NoBackend_DoesNotThrow()
    {
        var ctx = new DrawContext();
        await Assert.That(() => ctx.DrawText(null!, 0, 0, 14f, new ColorValue("#000000"))).ThrowsNothing();
    }

    [Test]
    public async Task DefaultFontPath_InitiallyNull()
    {
        var ctx = new DrawContext();
        await Assert.That(ctx.DefaultFontPath).IsNull();
    }
}

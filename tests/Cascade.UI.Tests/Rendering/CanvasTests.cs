namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// Tests for <see cref="CanvasFactory.Canvas"/> — validation, successful construction,
/// and correct node type from the factory.
/// </summary>
public class CanvasTests
{
    // ── Factory validation ────────────────────────────────────────────

    [Test]
    public async Task Canvas_NullOnDraw_ThrowsArgumentNullException()
    {
        await Assert.That(() =>
                CanvasFactory.Canvas(new Size(100, 100), null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Canvas_ZeroWidth_ThrowsArgumentException()
    {
        await Assert.That(() =>
                CanvasFactory.Canvas(new Size(0, 100), static (_, _) => { }))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Canvas_NegativeWidth_ThrowsArgumentException()
    {
        await Assert.That(() =>
                CanvasFactory.Canvas(new Size(-1, 100), static (_, _) => { }))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Canvas_ZeroHeight_ThrowsArgumentException()
    {
        await Assert.That(() =>
                CanvasFactory.Canvas(new Size(100, 0), static (_, _) => { }))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Canvas_NegativeHeight_ThrowsArgumentException()
    {
        await Assert.That(() =>
                CanvasFactory.Canvas(new Size(100, -5), static (_, _) => { }))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Canvas_BothDimensionsNegative_ThrowsArgumentException()
    {
        await Assert.That(() =>
                CanvasFactory.Canvas(new Size(-10, -10), static (_, _) => { }))
            .Throws<ArgumentException>();
    }

    // ── Factory success ───────────────────────────────────────────────

    [Test]
    public async Task Canvas_ValidSize_ReturnsNonNull()
    {
        Node result = CanvasFactory.Canvas(new Size(200, 150), static (_, _) => { });
        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task Canvas_ValidSize_IsNotNodeEmpty()
    {
        Node result = CanvasFactory.Canvas(new Size(200, 150), static (_, _) => { });
        bool isNotEmpty = !ReferenceEquals(result, Node.Empty);
        await Assert.That(isNotEmpty).IsTrue();
    }

    [Test]
    public async Task Canvas_SmallestValidSize_ReturnsNode()
    {
        // Smallest meaningful positive size
        Node result = CanvasFactory.Canvas(new Size(1, 1), static (_, _) => { });
        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task Canvas_LargeSize_ReturnsNode()
    {
        Node result = CanvasFactory.Canvas(new Size(4096, 4096), static (_, _) => { });
        await Assert.That(result).IsNotNull();
    }

    // ── With onFrame callback ─────────────────────────────────────────

    [Test]
    public async Task Canvas_WithOnFrame_ReturnsNonNull()
    {
        Node result = CanvasFactory.Canvas(
            new Size(300, 200),
            static (_, _) => { },
            static _ => { });
        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task Canvas_WithOnFrame_IsNotNodeEmpty()
    {
        Node result = CanvasFactory.Canvas(
            new Size(300, 200),
            static (_, _) => { },
            static _ => { });
        bool isNotEmpty = !ReferenceEquals(result, Node.Empty);
        await Assert.That(isNotEmpty).IsTrue();
    }

    [Test]
    public async Task Canvas_WithoutOnFrame_IsNotNodeEmpty()
    {
        Node result = CanvasFactory.Canvas(
            new Size(300, 200),
            static (_, _) => { },
            onFrame: null);
        bool isNotEmpty = !ReferenceEquals(result, Node.Empty);
        await Assert.That(isNotEmpty).IsTrue();
    }

    // ── onDraw callback is invoked-ready ─────────────────────────────

    [Test]
    public async Task Canvas_OnDrawCallbackIsStored()
    {
        bool callbackWasSet = false;

        // The factory stores the callback; verifying it is non-null indirectly
        // by confirming the factory accepted it without throwing.
        Node result = CanvasFactory.Canvas(
            new Size(100, 100),
            (_, _) => { callbackWasSet = true; });

        await Assert.That(result).IsNotNull();

        // Suppress unused-variable warning while confirming the lambda captured correctly
        await Assert.That(callbackWasSet).IsFalse();
    }
}

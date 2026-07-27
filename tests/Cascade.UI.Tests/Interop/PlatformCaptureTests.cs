using Cascade.UI;

namespace Cascade.UI.Tests.Interop;

// ─── PlatformCapture Tests ───────────────────────────────────────────────────

/// <summary>
/// Minimal concrete adapter for testing NativeViewAdapter.
/// </summary>
file sealed class TestAdapter : NativeViewAdapter
{
    protected override void OnCreate(NativeViewHost host) { }
    protected override void OnDestroy() { }
    protected override void OnResize(int widthPx, int heightPx, float scale) { }

    internal bool CallPlatformCapture(Span<byte> buffer, int widthPx, int heightPx, int strideBytes)
        => PlatformCapture(buffer, widthPx, heightPx, strideBytes);
}

public class PlatformCaptureTests
{
    // ── Null / zero host guard ────────────────────────────────────────

    [Test]
    public async Task PlatformCapture_NullHost_ReturnsFalse()
    {
        using var adapter = new TestAdapter();
        // Host is null! (OnCreate not called — Host stays null)
        byte[] buf = new byte[4 * 10 * 10];
        bool result = adapter.CallPlatformCapture(buf, 10, 10, 40);

        await Assert.That(result).IsFalse();
    }

    // ── Dimension validation ──────────────────────────────────────────

    [Test]
    public async Task PlatformCapture_ZeroWidth_ReturnsFalse()
    {
        using var adapter = new TestAdapter();
        byte[] buf = new byte[100];
        bool result = adapter.CallPlatformCapture(buf, 0, 10, 40);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task PlatformCapture_ZeroHeight_ReturnsFalse()
    {
        using var adapter = new TestAdapter();
        byte[] buf = new byte[100];
        bool result = adapter.CallPlatformCapture(buf, 10, 0, 40);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task PlatformCapture_ZeroStride_ReturnsFalse()
    {
        using var adapter = new TestAdapter();
        byte[] buf = new byte[100];
        bool result = adapter.CallPlatformCapture(buf, 10, 10, 0);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task PlatformCapture_BufferTooSmall_ReturnsFalse()
    {
        using var adapter = new TestAdapter();
        // stride * height = 40 * 10 = 400, but buffer is only 100 bytes
        byte[] buf = new byte[100];
        bool result = adapter.CallPlatformCapture(buf, 10, 10, 40);

        await Assert.That(result).IsFalse();
    }

    // ── BGRA→RGBA conversion logic ────────────────────────────────────

    [Test]
    public async Task ConvertBgraToRgba_SwapsRedAndBlue()
    {
        // One pixel: BGRA = [10, 20, 30, 255]
        // After swap:  RGBA = [30, 20, 10, 255]
        byte[] buffer = [10, 20, 30, 255];
        NativeViewAdapter.ConvertBgraToRgba(buffer, 1, 1, 4);

        await Assert.That(buffer[0]).IsEqualTo((byte)30); // R (was B)
        await Assert.That(buffer[1]).IsEqualTo((byte)20); // G unchanged
        await Assert.That(buffer[2]).IsEqualTo((byte)10); // B (was R)
        await Assert.That(buffer[3]).IsEqualTo((byte)255); // A unchanged
    }

    [Test]
    public async Task ConvertBgraToRgba_PreservesGreenAndAlpha()
    {
        // Two pixels: BGRA = [1,2,3,4, 5,6,7,8]
        // Expected:   RGBA = [3,2,1,4, 7,6,5,8]
        byte[] buffer = [1, 2, 3, 4, 5, 6, 7, 8];
        NativeViewAdapter.ConvertBgraToRgba(buffer, 2, 1, 8);

        await Assert.That(buffer[0]).IsEqualTo((byte)3);
        await Assert.That(buffer[1]).IsEqualTo((byte)2);
        await Assert.That(buffer[2]).IsEqualTo((byte)1);
        await Assert.That(buffer[3]).IsEqualTo((byte)4);
        await Assert.That(buffer[4]).IsEqualTo((byte)7);
        await Assert.That(buffer[5]).IsEqualTo((byte)6);
        await Assert.That(buffer[6]).IsEqualTo((byte)5);
        await Assert.That(buffer[7]).IsEqualTo((byte)8);
    }

    [Test]
    public async Task ConvertBgraToRgba_IsIdempotentWhenAppliedTwice()
    {
        // Applying BGRA→RGBA twice should return to original values.
        byte[] original = [10, 20, 30, 255];
        byte[] buffer   = [10, 20, 30, 255];

        NativeViewAdapter.ConvertBgraToRgba(buffer, 1, 1, 4);
        NativeViewAdapter.ConvertBgraToRgba(buffer, 1, 1, 4);

        await Assert.That(buffer[0]).IsEqualTo(original[0]);
        await Assert.That(buffer[1]).IsEqualTo(original[1]);
        await Assert.That(buffer[2]).IsEqualTo(original[2]);
        await Assert.That(buffer[3]).IsEqualTo(original[3]);
    }

    [Test]
    public async Task ConvertBgraToRgba_MultiRow_RespectsStride()
    {
        // 2x2 image, stride = 8 (4 bytes per pixel, no padding)
        // Pixel layout: row0=[B0,G0,R0,A0, B1,G1,R1,A1] row1=[B2,G2,R2,A2, B3,G3,R3,A3]
        byte[] buffer =
        [
            10, 11, 12, 13,   // pixel (0,0): BGRA
            20, 21, 22, 23,   // pixel (1,0): BGRA
            30, 31, 32, 33,   // pixel (0,1): BGRA
            40, 41, 42, 43,   // pixel (1,1): BGRA
        ];

        NativeViewAdapter.ConvertBgraToRgba(buffer, 2, 2, 8);

        // pixel (0,0): R←12, G←11, B←10
        await Assert.That(buffer[0]).IsEqualTo((byte)12);
        await Assert.That(buffer[2]).IsEqualTo((byte)10);

        // pixel (1,0): R←22, G←21, B←20
        await Assert.That(buffer[4]).IsEqualTo((byte)22);
        await Assert.That(buffer[6]).IsEqualTo((byte)20);

        // pixel (0,1): R←32
        await Assert.That(buffer[8]).IsEqualTo((byte)32);

        // pixel (1,1): R←42
        await Assert.That(buffer[12]).IsEqualTo((byte)42);
    }
}

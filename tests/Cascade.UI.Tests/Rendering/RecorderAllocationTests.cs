using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// Recording a frame that repeats the last one allocates nothing: gradient stops are converted
/// once per distinct gradient (cached by content), not once per draw per frame.
/// </summary>
[NotInParallel(nameof(RecorderAllocationTests))]
public class RecorderAllocationTests
{
    private static readonly GradientStop[] Stops =
    [
        new GradientStop(0, ColorValue.FromRgba(1, 0, 0)),
        new GradientStop(0.5f, ColorValue.FromRgba(0, 1, 0, 0.5f)),
        new GradientStop(1, ColorValue.FromRgba(0, 0, 1)),
    ];

    private static readonly byte[] Verbs = [0x00, 0x01, 0x01, 0x04, 0xFF];
    private static readonly float[] Coords = [0, 0, 20, 0, 10, 20];

    [Test]
    public async Task RepeatedGradientFrame_RecordsWithoutAllocating()
    {
        using var provider = new EtchBackendProvider();
        var backend = provider.Backend;
        provider.BeginFrame(200, 200);
        ulong path = backend.CompilePath(Verbs, Coords);
        backend.DrawRectGradient(0, 0, 0, 100, 40, 0, 0, Stops, 0, 0, 100, 0);
        backend.DrawRectGradient(0, 0, 50, 100, 40, 8, 1, Stops, 50, 70, 50, 0);
        backend.DrawPathGradient(0, path, 2, Stops, 10, 10, 0, 0, null, 0, default, default);

        // Warm: the first recording converts the stops and sizes the recording's buffers.
        provider.RecordFrame(ColorValue.FromRgba(1, 1, 1));
        provider.RecordFrame(ColorValue.FromRgba(1, 1, 1));

        long before = GC.GetAllocatedBytesForCurrentThread();
        provider.RecordFrame(ColorValue.FromRgba(1, 1, 1));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0L);
        await Assert.That(provider.Recorder.CachedGradientCount).IsEqualTo(1);
        await Assert.That(provider.Recorder.UnhandledOps).IsEqualTo(0);
    }

    [Test]
    public async Task DifferentStops_GetTheirOwnConversion()
    {
        GradientStop[] other = [new GradientStop(0, ColorValue.FromRgba(1, 1, 0)), new GradientStop(1, ColorValue.FromRgba(0, 1, 1))];
        byte[] rgba = CpuFrame.Render(100, 100, ColorValue.FromRgba(1, 1, 1), backend =>
        {
            backend.DrawRectGradient(0, 0, 0, 100, 40, 0, 0, Stops, 0, 0, 100, 0);
            backend.DrawRectGradient(0, 0, 50, 100, 40, 0, 0, other, 0, 0, 100, 0);
            backend.DrawRectGradient(0, 0, 0, 100, 40, 0, 0, Stops, 0, 0, 100, 0);
        });

        // Left edge: red for the first gradient, yellow for the second.
        int top = (20 * 100 + 0) * 4;
        int bottom = (70 * 100 + 0) * 4;
        await Assert.That(rgba[top]).IsGreaterThan((byte)240);
        await Assert.That(rgba[top + 1]).IsLessThan((byte)20);
        await Assert.That(rgba[bottom]).IsGreaterThan((byte)240);
        await Assert.That(rgba[bottom + 1]).IsGreaterThan((byte)240);
    }
}
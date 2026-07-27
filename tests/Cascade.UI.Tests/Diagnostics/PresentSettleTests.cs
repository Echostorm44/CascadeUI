using System.Diagnostics;
using Cascade.UI.Diagnostics;

namespace Cascade.UI.Tests;

/// <summary>
/// WP-3528: <see cref="PresentMonitor.WaitForSettle"/> is the primitive that fixed
/// the golden-capture flake — a screenshot without an explicit target frame waits
/// until the render has settled (≥1 frame presented + a quiet window) instead of
/// grabbing the blank initial window or a mid-burst intermediate frame. The flake
/// itself is a launch-timing race (validated by 10× golden runs); these tests lock
/// the settle primitive's contract deterministically.
/// </summary>
public class PresentSettleTests
{
    [TUnit.Core.Test]
    public async Task WaitForSettle_ReturnsTrue_WhenAFramePresentedAndQuiescent()
    {
        PresentMonitor.NotifyPresented(); // ensure ≥1 frame exists

        var sw = Stopwatch.StartNew();
        bool settled = PresentMonitor.WaitForSettle(TimeSpan.FromMilliseconds(30), TimeSpan.FromSeconds(2));
        sw.Stop();

        await TUnit.Assertions.Assert.That(settled).IsTrue();
        // Settles via the quiescence window, well before the timeout.
        await TUnit.Assertions.Assert.That(sw.Elapsed).IsLessThan(TimeSpan.FromSeconds(1));
    }

    [TUnit.Core.Test]
    public async Task WaitForSettle_WaitsUntilPresentBurstStops()
    {
        PresentMonitor.NotifyPresented(); // baseline frame

        // A burst of presents every ~15ms for ~120ms, then silence. The quiescence
        // window (60ms) exceeds the inter-present gap, so WaitForSettle must not
        // return until the burst ends.
        using var done = new System.Threading.CancellationTokenSource();
        var burst = Task.Run(async () =>
        {
            for (int i = 0; i < 8; i++)
            {
                await Task.Delay(15);
                PresentMonitor.NotifyPresented();
            }
        });

        var sw = Stopwatch.StartNew();
        bool settled = PresentMonitor.WaitForSettle(TimeSpan.FromMilliseconds(60), TimeSpan.FromSeconds(5));
        sw.Stop();
        await burst;

        await TUnit.Assertions.Assert.That(settled).IsTrue();
        // Must have waited through the ~120ms burst (generous lower bound for load).
        await TUnit.Assertions.Assert.That(sw.ElapsedMilliseconds).IsGreaterThan(90L);
        await TUnit.Assertions.Assert.That(sw.Elapsed).IsLessThan(TimeSpan.FromSeconds(5));
    }
}

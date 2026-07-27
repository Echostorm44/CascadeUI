using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Cascade.UI;

/// <summary>
/// Tracks entrance animation state for chart controls across re-renders.
/// <para>
/// Charts are recreated on every <c>Render()</c>, so state is stored externally
/// in a <b>slot</b> keyed by node identity and carried across a reconcile by
/// <see cref="TransferState"/> (mirroring <see cref="ControlStateAnimator"/>).
/// A slot remembers the content it last showed, which lets the tracker honour
/// <see cref="AnimateTrigger"/> precisely: <see cref="AnimateTrigger.Load"/>
/// animates only when the slot is new (the chart entered the tree),
/// <see cref="AnimateTrigger.DataChange"/> only when an existing slot's data
/// changed, and <see cref="AnimateTrigger.Both"/> in either case.
/// </para>
/// <para>
/// A fresh slot, or one whose data just changed, reports as animating from
/// <see cref="IsAnimating"/> even before its clock starts, so a cached ScrollView
/// layer direct-paints that frame and lets <see cref="GetProgress"/> bootstrap the
/// animation (a recapture paints with animations skipped, so it can never start one).
/// </para>
/// </summary>
internal static class ChartAnimationTracker
{
    // EaseOutCubic: (0.33, 1, 0.68, 1) — smooth deceleration, feels polished
    private const float EaseX1 = 0.33f;
    private const float EaseY1 = 1.0f;
    private const float EaseX2 = 0.68f;
    private const float EaseY2 = 1.0f;

    private const float BarChartDurationMs = 800f;
    private const float PieChartDurationMs = 800f;
    private const float LineChartDurationMs = 800f;
    private const float AreaChartDurationMs = 800f;
    private const float DonutGaugeDurationMs = 600f;
    private const float HeatMapChartDurationMs = 800f;
    private const float TreeMapChartDurationMs = 700f;
    private const float WaterfallChartDurationMs = 800f;
    private const float ScatterPlotDurationMs = 600f;
    private const float BarStaggerMs = 50f;
    private const float CellStaggerMs = 30f;

    /// <summary>Per-chart-slot entrance-animation state.</summary>
    private sealed class Slot
    {
        /// <summary>The content hash this slot last saw (detects data changes).</summary>
        internal int LastContentHash;

        /// <summary>Whether the current (entrance or data-change) event should animate.</summary>
        internal bool ShouldAnimate;

        /// <summary>True once the clock has started (i.e. the chart became visible).</summary>
        internal bool Started;

        /// <summary>Animation start timestamp; valid only when <see cref="Started"/>.</summary>
        internal long StartTs;

        /// <summary>Frame index this slot was last touched, for stale-slot cleanup.</summary>
        internal long LastTouchedFrame;
    }

    // Keyed by node identity (RuntimeHelpers.GetHashCode). Survives re-renders via
    // TransferState; orphaned slots (charts removed and never transferred) are swept
    // periodically in BeginFrame.
    private static readonly Dictionary<int, Slot> slots = new();

    private static long frameCounter;
    private const int CleanupInterval = 300;      // ~5s at 60fps
    private const long StaleFrameThreshold = 240; // untouched for ~4s → orphaned

    /// <summary>
    /// Advances the per-frame bookkeeping and periodically sweeps orphaned slots
    /// (charts that were removed without their state being transferred). Call once
    /// per paint pass.
    /// </summary>
    internal static void BeginFrame()
    {
        frameCounter++;
        if (frameCounter % CleanupInterval == 0)
        {
            CleanupStaleSlots();
        }
    }

    private static void CleanupStaleSlots()
    {
        List<int>? toRemove = null;
        foreach (var (key, slot) in slots)
        {
            if (frameCounter - slot.LastTouchedFrame > StaleFrameThreshold)
            {
                (toRemove ??= new List<int>()).Add(key);
            }
        }

        if (toRemove is null)
        {
            return;
        }

        foreach (int key in toRemove)
        {
            slots.Remove(key);
        }
    }

    /// <summary>
    /// Carries a chart's animation slot from an old node instance to its replacement
    /// during reconciliation, so an in-place re-render is seen as the same slot (and a
    /// data change within it, rather than a fresh entrance).
    /// </summary>
    internal static void TransferState(Node from, Node to)
    {
        int fromKey = RuntimeHelpers.GetHashCode(from);
        if (slots.Remove(fromKey, out var slot))
        {
            slots[RuntimeHelpers.GetHashCode(to)] = slot;
        }
    }

    /// <summary>
    /// Looks up (creating if needed) the slot for a chart node and reconciles it with
    /// the current content hash and trigger. Sets <see cref="Slot.ShouldAnimate"/> on a
    /// qualifying entrance or data change and resets the clock; does not start it (that
    /// needs visibility, handled by <see cref="GetProgress"/>).
    /// </summary>
    private static Slot ReconcileSlot(Node node, int contentHash, AnimateTrigger trigger)
    {
        int key = RuntimeHelpers.GetHashCode(node);
        if (!slots.TryGetValue(key, out var slot))
        {
            // New slot = the chart just entered the tree → an initial render.
            slot = new Slot
            {
                LastContentHash = contentHash,
                ShouldAnimate = trigger is AnimateTrigger.Load or AnimateTrigger.Both,
                Started = false,
            };
            slots[key] = slot;
        }
        else if (slot.LastContentHash != contentHash)
        {
            // Existing slot, new content → a data change.
            slot.LastContentHash = contentHash;
            slot.ShouldAnimate = trigger is AnimateTrigger.DataChange or AnimateTrigger.Both;
            slot.Started = false;
        }

        slot.LastTouchedFrame = frameCounter;
        return slot;
    }

    /// <summary>
    /// Computes entrance progress (0→1, eased) for a chart. Records the start time on
    /// the first visible frame after a qualifying entrance/data change; returns 1 when
    /// the trigger says this event should not animate, or when the animation completes.
    /// </summary>
    internal static float GetProgress(Node node, int contentHash, AnimateTrigger trigger, float durationMs, bool isVisible)
    {
        var slot = ReconcileSlot(node, contentHash, trigger);
        if (StartClockIfReady(slot, isVisible))
        {
            return 0f; // exactly zero on the frame the animation starts
        }
        return ComputeProgress(slot, durationMs);
    }

    /// <summary>
    /// Computes staggered entrance progress for one bar. Each bar starts
    /// <see cref="BarStaggerMs"/> after the previous one.
    /// </summary>
    internal static float GetBarProgress(Node node, int contentHash, AnimateTrigger trigger, int barIndex, int totalBars, bool isVisible)
    {
        var slot = ReconcileSlot(node, contentHash, trigger);
        if (StartClockIfReady(slot, isVisible))
        {
            return 0f;
        }

        if (!slot.ShouldAnimate)
        {
            return 1f;
        }

        if (!slot.Started)
        {
            return 0f;
        }

        float elapsedMs = ElapsedMs(slot.StartTs);
        float staggerDelay = barIndex * BarStaggerMs;
        float barElapsed = elapsedMs - staggerDelay;
        if (barElapsed <= 0f)
        {
            return 0f;
        }

        // Each bar animates over the base duration minus the total stagger time,
        // so all bars finish roughly together.
        float totalStagger = (totalBars - 1) * BarStaggerMs;
        float barDuration = BarChartDurationMs - totalStagger;
        if (barDuration < 200f)
        {
            barDuration = 200f;
        }

        if (barElapsed >= barDuration)
        {
            return 1f;
        }

        return CurveSolver.Evaluate(barElapsed / barDuration, EaseX1, EaseY1, EaseX2, EaseY2);
    }

    /// <summary>
    /// Computes staggered entrance progress for one cell (heat map, tree map). Each cell
    /// starts <see cref="CellStaggerMs"/> after the previous one.
    /// </summary>
    internal static float GetCellProgress(Node node, int contentHash, AnimateTrigger trigger, int cellIndex, int totalCells, float durationMs, bool isVisible)
    {
        var slot = ReconcileSlot(node, contentHash, trigger);
        if (StartClockIfReady(slot, isVisible))
        {
            return 0f;
        }

        if (!slot.ShouldAnimate)
        {
            return 1f;
        }

        if (!slot.Started)
        {
            return 0f;
        }

        float elapsedMs = ElapsedMs(slot.StartTs);
        float staggerDelay = cellIndex * CellStaggerMs;
        float cellElapsed = elapsedMs - staggerDelay;
        if (cellElapsed <= 0f)
        {
            return 0f;
        }

        float totalStagger = (totalCells - 1) * CellStaggerMs;
        float cellDuration = durationMs - Math.Min(totalStagger, durationMs * 0.6f);
        if (cellDuration < 200f)
        {
            cellDuration = 200f;
        }

        if (cellElapsed >= cellDuration)
        {
            return 1f;
        }

        return CurveSolver.Evaluate(cellElapsed / cellDuration, EaseX1, EaseY1, EaseX2, EaseY2);
    }

    /// <summary>
    /// Returns true if the chart's entrance animation is running or about to start
    /// this frame. A fresh slot or a just-changed one reports true (before its clock
    /// starts) so a cached ScrollView layer direct-paints and lets the animation
    /// bootstrap. Reconciles the slot so a data change is recorded even when the
    /// trigger suppresses the animation (keeping the baseline for the next change).
    /// </summary>
    internal static bool IsAnimating(Node node, int contentHash, AnimateTrigger trigger, float durationMs)
    {
        var slot = ReconcileSlot(node, contentHash, trigger);
        if (!slot.ShouldAnimate)
        {
            return false;
        }

        if (!slot.Started)
        {
            return true; // qualifying event, waiting to start (visibility) — keep the loop alive
        }

        return ElapsedMs(slot.StartTs) < durationMs;
    }

    /// <summary>
    /// Returns true if a bar chart's staggered entrance is still running (accounting
    /// for the per-bar stagger).
    /// </summary>
    internal static bool IsBarChartAnimating(Node node, int contentHash, AnimateTrigger trigger, int totalBars)
    {
        float totalDuration = BarChartDurationMs + (totalBars - 1) * BarStaggerMs;
        return IsAnimating(node, contentHash, trigger, totalDuration);
    }

    /// <summary>Starts the clock if the slot is a qualifying, visible, not-yet-started
    /// animation. Returns true only on the frame the clock actually starts.</summary>
    private static bool StartClockIfReady(Slot slot, bool isVisible)
    {
        if (slot.ShouldAnimate && !slot.Started && isVisible)
        {
            slot.StartTs = Stopwatch.GetTimestamp();
            slot.Started = true;
            return true;
        }
        return false;
    }

    private static float ComputeProgress(Slot slot, float durationMs)
    {
        if (!slot.ShouldAnimate)
        {
            return 1f;
        }

        if (!slot.Started)
        {
            return 0f; // qualifying but not yet visible
        }

        float elapsedMs = ElapsedMs(slot.StartTs);
        if (elapsedMs >= durationMs)
        {
            return 1f;
        }

        return CurveSolver.Evaluate(elapsedMs / durationMs, EaseX1, EaseY1, EaseX2, EaseY2);
    }

    private static float ElapsedMs(long startTs) =>
        (float)Stopwatch.GetElapsedTime(startTs, Stopwatch.GetTimestamp()).TotalMilliseconds;

    /// <summary>
    /// Computes a stable identity hash for a BarChart based on its data.
    /// </summary>
    internal static int ComputeBarChartHash(BarChart chart)
    {
        var hash = new HashCode();
        hash.Add(typeof(BarChart));
        foreach (var series in chart.Series)
        {
            foreach (var pt in series.DataPoints)
            {
                hash.Add(pt.X?.GetHashCode() ?? 0);
                hash.Add(pt.Y);
            }
        }
        return hash.ToHashCode();
    }

    /// <summary>
    /// Computes a stable identity hash for a PieChart based on its slices.
    /// </summary>
    internal static int ComputePieChartHash(PieChart chart)
    {
        var hash = new HashCode();
        hash.Add(typeof(PieChart));
        foreach (var slice in chart.Slices)
        {
            hash.Add(slice.Label);
            hash.Add(slice.Value);
        }
        return hash.ToHashCode();
    }

    /// <summary>
    /// Computes a stable identity hash for a LineChart based on its series data.
    /// </summary>
    internal static int ComputeLineChartHash(LineChart chart)
    {
        var hash = new HashCode();
        hash.Add(typeof(LineChart));
        foreach (var series in chart.Series)
        {
            hash.Add(series.SeriesName);
            foreach (var pt in series.DataPoints)
            {
                hash.Add(pt.X?.GetHashCode() ?? 0);
                hash.Add(pt.Y);
            }
        }
        return hash.ToHashCode();
    }

    /// <summary>
    /// Computes a stable identity hash for an AreaChart based on its series data.
    /// </summary>
    internal static int ComputeAreaChartHash(AreaChart chart)
    {
        var hash = new HashCode();
        hash.Add(typeof(AreaChart));
        foreach (var series in chart.Series)
        {
            hash.Add(series.SeriesName);
            foreach (var pt in series.DataPoints)
            {
                hash.Add(pt.X?.GetHashCode() ?? 0);
                hash.Add(pt.Y);
            }
        }
        return hash.ToHashCode();
    }

    /// <summary>
    /// Computes a stable identity hash for a DonutGauge based on its value.
    /// </summary>
    internal static int ComputeDonutGaugeHash(DonutGauge gauge)
    {
        var hash = new HashCode();
        hash.Add(typeof(DonutGauge));
        hash.Add(gauge.gaugeValue);
        hash.Add(gauge.labelText);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Computes a stable identity hash for a HeatMapChart based on its cell data.
    /// </summary>
    internal static int ComputeHeatMapChartHash(HeatMapChart chart)
    {
        var hash = new HashCode();
        hash.Add(typeof(HeatMapChart));
        foreach (var cell in chart.cellsList)
        {
            hash.Add(cell.Row?.GetHashCode() ?? 0);
            hash.Add(cell.Column?.GetHashCode() ?? 0);
            hash.Add(cell.Value);
        }
        return hash.ToHashCode();
    }

    /// <summary>
    /// Computes a stable identity hash for a TreeMapChart based on its node data.
    /// </summary>
    internal static int ComputeTreeMapChartHash(TreeMapChart chart)
    {
        var hash = new HashCode();
        hash.Add(typeof(TreeMapChart));
        foreach (var node in chart.nodesList)
        {
            hash.Add(node.Label);
            hash.Add(node.Value);
        }
        return hash.ToHashCode();
    }

    /// <summary>
    /// Computes a stable identity hash for a WaterfallChart based on its item data.
    /// </summary>
    internal static int ComputeWaterfallChartHash(WaterfallChart chart)
    {
        var hash = new HashCode();
        hash.Add(typeof(WaterfallChart));
        foreach (var item in chart.itemsList)
        {
            hash.Add(item.Label);
            hash.Add(item.Value);
            hash.Add((int)item.Type);
        }
        return hash.ToHashCode();
    }

    /// <summary>
    /// Computes a stable identity hash for a ScatterPlot based on its series data.
    /// </summary>
    internal static int ComputeScatterPlotHash(ScatterPlot chart)
    {
        var hash = new HashCode();
        hash.Add(typeof(ScatterPlot));
        foreach (var series in chart.seriesList)
        {
            hash.Add(series.SeriesName);
            foreach (var pt in series.dataPointsList)
            {
                hash.Add(pt.X);
                hash.Add(pt.Y);
            }
        }
        return hash.ToHashCode();
    }

    /// <summary>Duration constants for external callers.</summary>
    internal const float BarDuration = BarChartDurationMs;
    internal const float PieDuration = PieChartDurationMs;
    internal const float LineDuration = LineChartDurationMs;
    internal const float AreaDuration = AreaChartDurationMs;
    internal const float GaugeDuration = DonutGaugeDurationMs;
    internal const float HeatMapDuration = HeatMapChartDurationMs;
    internal const float TreeMapDuration = TreeMapChartDurationMs;
    internal const float WaterfallDuration = WaterfallChartDurationMs;
    internal const float ScatterDuration = ScatterPlotDurationMs;

    /// <summary>
    /// Resets all tracked animation state. Used in tests.
    /// </summary>
    internal static void Reset()
    {
        slots.Clear();
        frameCounter = 0;
    }
}

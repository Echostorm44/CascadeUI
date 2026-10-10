using System.Diagnostics;
using Cascade.UI.Backend.Etch;

namespace Cascade.UI;

/// <summary>
/// Carets: one blink for every control that edits text, and the bookkeeping that lets a blink be
/// presented without painting the tree again.
/// </summary>
/// <remarks>
/// <para>Every caret is drawn through <see cref="DrawCaret"/> or <see cref="DrawCaretLine"/>. The
/// painter keeps the op each one emitted and the colour it shows when on. A blink changes nothing but
/// those colours, so the frame orchestrator presents it by calling <see cref="RefreshCarets"/> and
/// presenting the previous frame's command stream again: no re-render, layout or paint walk.</para>
/// <para>A caret that is off is drawn transparent rather than left out, so it keeps its op and a later
/// toggle has something to change. Etch gives a transparent shape its place in paint order without
/// drawing it, so the draws after the caret batch the same either way and a blink changes only the
/// caret's own pixels (on the CPU path, one tile).</para>
/// </remarks>
internal sealed partial class NodePainter
{
    private struct PaintedCaret
    {
        public EtchBackend.SceneOp Op;
        public ColorValue OnColor;
        public bool IsStroke;
        public double IntervalMs;
    }

    // The carets the last full paint drew, in paint order.
    private readonly List<PaintedCaret> paintedCarets = new();

    /// <summary>
    /// When the earliest caret painted this frame next changes (Stopwatch timestamp; 0 = none).
    /// The frame loop sleeps until then instead of repainting every vblank for a blinking caret.
    /// </summary>
    internal static long NextCaretToggle { get; set; }

    /// <summary>True when the last full paint drew a blinking caret that <see cref="RefreshCarets"/> can toggle.</summary>
    internal bool HasPaintedCarets => paintedCarets.Count > 0;

    /// <summary>
    /// Caret opacity, 0 or 1: solid for one blink interval after input, then a hard on/off blink of
    /// half an interval each, like the native Windows caret. The platform's rate overrides
    /// <paramref name="themeIntervalMs"/>; with blinking turned off, or once the platform's blink
    /// timeout has passed since the last input, the caret is solid and needs no further frame.
    /// Records the next change in <see cref="NextCaretToggle"/>.
    /// </summary>
    internal static float CaretBlink(double themeIntervalMs, long resetTimestamp)
    {
        if (CaretSettings.BlinkDisabled)
        {
            return 1f;
        }

        double intervalMs = CaretSettings.PlatformHalfPeriodMs > 0 ? CaretSettings.PlatformHalfPeriodMs * 2.0 : themeIntervalMs;
        if (intervalMs <= 0)
        {
            return 1f;
        }

        double elapsed = Stopwatch.GetElapsedTime(resetTimestamp, CaretSettings.Now()).TotalMilliseconds;
        int timeoutMs = CaretSettings.TimeoutMs;
        if (timeoutMs > 0 && elapsed >= timeoutMs)
        {
            return 1f;
        }

        double half = intervalMs / 2.0;
        float opacity;
        double nextChangeMs;
        if (elapsed < intervalMs)
        {
            opacity = 1f;
            nextChangeMs = intervalMs;
        }
        else
        {
            long step = (long)((elapsed - intervalMs) / half);
            opacity = (step & 1) == 0 ? 0f : 1f;
            nextChangeMs = intervalMs + ((step + 1) * half);
        }

        if (timeoutMs > 0 && nextChangeMs >= timeoutMs)
        {
            // Blinking stops at the timeout with the caret on: an on caret stays on, an off one
            // comes back on at the timeout and stays.
            if (opacity == 1f)
            {
                return 1f;
            }
            nextChangeMs = timeoutMs;
        }

        long toggle = resetTimestamp + (long)(nextChangeMs * Stopwatch.Frequency / 1000.0);
        if (NextCaretToggle == 0 || toggle < NextCaretToggle)
        {
            NextCaretToggle = toggle;
        }
        return opacity;
    }

    /// <summary>
    /// Draws a filled caret at its current blink phase (solid under reduced motion) and remembers it
    /// for <see cref="RefreshCarets"/>.
    /// </summary>
    private void DrawCaret(Rect rect, ColorValue color)
    {
        var before = ctx.Backend?.LastCommand;
        ctx.DrawRect(rect, color);
        RememberCaret(before, isStroke: false);
    }

    /// <summary>A caret drawn as a stroked line; see <see cref="DrawCaret"/>.</summary>
    private void DrawCaretLine(Point from, Point to, ColorValue color, float width)
    {
        var before = ctx.Backend?.LastCommand;
        ctx.DrawLine(from, to, new Stroke(color, width));
        RememberCaret(before, isStroke: true);
    }

    private void RememberCaret(EtchBackend.SceneOp? before, bool isStroke)
    {
        if (ControlStateAnimator.ReducedMotion)
        {
            return;
        }

        double intervalMs = theme.Caret.BlinkInterval.TotalMilliseconds;
        float opacity = CaretBlink(intervalMs, InputDispatcher.CaretResetTimestamp);
        var op = ctx.Backend?.LastCommand;
        if (op is null || ReferenceEquals(op, before))
        {
            // Nothing was emitted (no backend, or a zero-width stroke).
            return;
        }

        ColorValue? emitted = isStroke ? op.StrokeColor : op.Fill;
        if (emitted is not ColorValue onColor)
        {
            return;
        }

        var caret = new PaintedCaret { Op = op, OnColor = onColor, IsStroke = isStroke, IntervalMs = intervalMs };
        paintedCarets.Add(caret);
        ApplyCaretPhase(caret, opacity);
    }

    /// <summary>
    /// Sets every caret the last full paint drew to its current blink phase, in place, and records
    /// the next change in <see cref="NextCaretToggle"/>. The previous frame's command stream then
    /// presents the blink as it is.
    /// </summary>
    internal void RefreshCarets()
    {
        long reset = InputDispatcher.CaretResetTimestamp;
        for (int i = 0; i < paintedCarets.Count; i++)
        {
            var caret = paintedCarets[i];
            ApplyCaretPhase(caret, CaretBlink(caret.IntervalMs, reset));
        }
    }

    private static void ApplyCaretPhase(PaintedCaret caret, float opacity)
    {
        ColorValue color = opacity > 0.5f ? caret.OnColor : caret.OnColor.Opacity(0f);
        if (caret.IsStroke)
        {
            caret.Op.StrokeColor = color;
        }
        else
        {
            caret.Op.Fill = color;
        }
    }
}

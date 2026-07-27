using TUnit.Core;
using TUnit.Core.Interfaces;

// WP-3516: Cascade.UI.Tests exercises a single-threaded UI framework that is built
// on process-global mutable statics — FocusManager (focused element + focusable
// registry), NodePainter.HasActive* paint flags, SharedScheduler, ChartAnimationTracker,
// ControlStateAnimator, drag/dialog/toast state, and more. Running tests in parallel
// races those statics: a test's assertion reads state a sibling concurrently mutated,
// producing ~1 nondeterministic failure per full-suite run. Per-test [NotInParallel]
// keys only patch individual resources and never fully close the gap here — the
// contention surface is broad and partly indirect (focus is touched via GC-collected
// WeakReferences and click routing, not just explicit FocusManager calls).
//
// Capping parallelism at 1 removes the contention entirely and deterministically. It
// is also FASTER for this suite (~9 s vs 35-75 s) because it eliminates the retry and
// thread-thrash overhead the contention was causing. The framework itself is
// single-threaded, so serial unit-test execution costs no coverage. If a future suite
// genuinely benefits from parallelism, raise this limit and pay down the static state
// with proper per-resource isolation first.
[assembly: ParallelLimiter<Cascade.UI.Tests.SerialLimit>]

namespace Cascade.UI.Tests;

/// <summary>Caps the whole test suite at one test at a time — see the file header.</summary>
public sealed class SerialLimit : IParallelLimit
{
    public int Limit => 1;
}

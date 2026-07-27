#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class SpringTests
{
    // ── Critically damped ────────────────────────────────────────────

    [Test]
    public async Task CriticallyDampedConvergesToTarget()
    {
        var solver = new SpringSolver(400f, 40f, -1f, 0f);

        for (int i = 0; i < 300; i++)
        {
            solver.Advance(1f / 60f);
        }

        var displacement = MathF.Abs(solver.Displacement);
        await Assert.That(displacement).IsLessThan(0.001f);
    }

    [Test]
    public async Task CriticallyDampedDoesNotOscillate()
    {
        var solver = new SpringSolver(400f, 40f, -1f, 0f);
        bool crossedZero = false;
        float prevDisplacement = solver.Displacement;

        for (int i = 0; i < 300; i++)
        {
            solver.Advance(1f / 60f);
            if (prevDisplacement < 0f && solver.Displacement > 0.01f)
            {
                crossedZero = true;
            }
            prevDisplacement = solver.Displacement;
        }

        await Assert.That(crossedZero).IsEqualTo(false);
    }

    // ── Underdamped ──────────────────────────────────────────────────

    [Test]
    public async Task UnderdampedOscillatesThenConverges()
    {
        var solver = new SpringSolver(400f, 10f, -1f, 0f);
        bool hadPositiveDisplacement = false;

        for (int i = 0; i < 600; i++)
        {
            solver.Advance(1f / 60f);
            if (solver.Displacement > 0.05f)
            {
                hadPositiveDisplacement = true;
            }
        }

        await Assert.That(hadPositiveDisplacement).IsEqualTo(true);

        var finalDisplacement = MathF.Abs(solver.Displacement);
        await Assert.That(finalDisplacement).IsLessThan(0.001f);
    }

    [Test]
    public async Task UnderdampedEventuallySettles()
    {
        var solver = new SpringSolver(400f, 15f, -1f, 0f);

        for (int i = 0; i < 600; i++)
        {
            solver.Advance(1f / 60f);
        }

        var settled = solver.IsSettled;
        await Assert.That(settled).IsEqualTo(true);
    }

    // ── Overdamped ───────────────────────────────────────────────────

    [Test]
    public async Task OverdampedConvergesSlowly()
    {
        var solver = new SpringSolver(100f, 50f, -1f, 0f);

        solver.Advance(0.1f);
        var earlyDisplacement = MathF.Abs(solver.Displacement);
        await Assert.That(earlyDisplacement).IsGreaterThan(0.1f);

        for (int i = 0; i < 1000; i++)
        {
            solver.Advance(1f / 60f);
        }

        var finalDisplacement = MathF.Abs(solver.Displacement);
        await Assert.That(finalDisplacement).IsLessThan(0.01f);
    }

    [Test]
    public async Task OverdampedDoesNotOscillate()
    {
        var solver = new SpringSolver(100f, 50f, -1f, 0f);
        float minDisplacement = 0f;

        for (int i = 0; i < 600; i++)
        {
            solver.Advance(1f / 60f);
            if (solver.Displacement > minDisplacement + 0.01f)
            {
                minDisplacement = float.MaxValue;
            }
        }

        var noOvershoots = minDisplacement < float.MaxValue;
        await Assert.That(noOvershoots).IsEqualTo(true);
    }

    // ── Presets settle ───────────────────────────────────────────────

    [Test]
    public async Task SnappyPresetSettles()
    {
        var solver = CreateFromModel(AnimationModel.Spring.Snappy);

        SimulateToSettled(solver, maxFrames: 600);

        var settled = solver.IsSettled;
        await Assert.That(settled).IsEqualTo(true);
    }

    [Test]
    public async Task StandardPresetSettles()
    {
        var solver = CreateFromModel(AnimationModel.Spring.Standard);

        SimulateToSettled(solver, maxFrames: 600);

        var settled = solver.IsSettled;
        await Assert.That(settled).IsEqualTo(true);
    }

    [Test]
    public async Task GentlePresetSettles()
    {
        var solver = CreateFromModel(AnimationModel.Spring.Gentle);

        SimulateToSettled(solver, maxFrames: 1200);

        var settled = solver.IsSettled;
        await Assert.That(settled).IsEqualTo(true);
    }

    [Test]
    public async Task BouncyPresetSettles()
    {
        var solver = CreateFromModel(AnimationModel.Spring.Bouncy);

        SimulateToSettled(solver, maxFrames: 1200);

        var settled = solver.IsSettled;
        await Assert.That(settled).IsEqualTo(true);
    }

    // ── Performance ──────────────────────────────────────────────────

    [Test]
    public async Task ThousandSpringsUnder2ms()
    {
        Skip.When(TestEnv.IsCi, TestEnv.PerfSkipReason);
        var solvers = new SpringSolver[1000];
        for (int i = 0; i < 1000; i++)
        {
            solvers[i] = new SpringSolver(400f, 28f, -1f, 0f);
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int frame = 0; frame < 10; frame++)
        {
            for (int i = 0; i < 1000; i++)
            {
                solvers[i].Advance(1f / 60f);
            }
        }
        sw.Stop();

        float avgMs = sw.ElapsedTicks / (float)System.Diagnostics.Stopwatch.Frequency * 1000f / 10f;
        await Assert.That(avgMs).IsLessThan(2f);
    }

    // ── Redirect ─────────────────────────────────────────────────────

    [Test]
    public async Task RedirectPreservesPhysicalContinuity()
    {
        var solver = new SpringSolver(400f, 28f, -1f, 0f);

        for (int i = 0; i < 30; i++)
        {
            solver.Advance(1f / 60f);
        }

        float midDisplacement = solver.Displacement;
        float midVelocity = solver.Velocity;
        solver.Redirect(midDisplacement, midVelocity);

        solver.Advance(1f / 60f);

        var displacementAfter = solver.Displacement;
        await Assert.That(MathF.Abs(displacementAfter)).IsLessThan(1f);
    }

    // ── Settling time estimate ───────────────────────────────────────

    [Test]
    public async Task EstimateSettlingTimeReturnsPositive()
    {
        var estimate = SpringSolver.EstimateSettlingTime(400f, 28f);
        await Assert.That(estimate).IsGreaterThan(0f);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static SpringSolver CreateFromModel(AnimationModel model)
    {
        model.TryGetSpringConfig(out float stiffness, out float damping, out float velocity);
        return new SpringSolver(stiffness, damping, -1f, velocity);
    }

    private static void SimulateToSettled(SpringSolver solver, int maxFrames)
    {
        for (int i = 0; i < maxFrames; i++)
        {
            solver.Advance(1f / 60f);
            if (solver.IsSettled)
            {
                break;
            }
        }
    }
}

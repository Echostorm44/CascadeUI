namespace Cascade.UI;

/// <summary>
/// Analytical spring physics solver supporting critically damped, underdamped,
/// and overdamped spring models. Zero allocations in the hot path.
/// </summary>
/// <remarks>
/// Solves the second-order ODE: x'' + (damping)*x' + (stiffness)*x = 0
/// where x is the displacement from the target position (mass = 1).
/// Uses closed-form solutions for each damping regime rather than
/// numerical integration, giving exact results regardless of timestep.
/// </remarks>
internal sealed class SpringSolver
{
    private const float SettlePositionThreshold = 0.0005f;
    private const float SettleVelocityThreshold = 0.005f;

    private readonly float omega;
    private readonly float zetaOmega;
    private readonly SpringRegime regime;

    // Underdamped parameters
    private readonly float omegaDamped;

    // Overdamped parameters
    private readonly float r1;
    private readonly float r2;

    // Current state
    private float x0;
    private float v0;
    private float elapsed;

    internal SpringSolver(float stiffness, float damping, float initialDisplacement, float initialVelocity)
    {
        if (stiffness <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(stiffness), "Stiffness must be positive.");
        }

        if (damping < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(damping), "Damping must be non-negative.");
        }

        omega = MathF.Sqrt(stiffness);
        float zeta = omega > 0f ? damping / (2f * omega) : 0f;
        zetaOmega = zeta * omega;

        x0 = initialDisplacement;
        v0 = initialVelocity;
        elapsed = 0f;

        if (zeta < 0.999f)
        {
            regime = SpringRegime.Underdamped;
            omegaDamped = omega * MathF.Sqrt(1f - zeta * zeta);
        }
        else if (zeta > 1.001f)
        {
            regime = SpringRegime.Overdamped;
            float disc = omega * MathF.Sqrt(zeta * zeta - 1f);
            r1 = -zetaOmega + disc;
            r2 = -zetaOmega - disc;
        }
        else
        {
            regime = SpringRegime.CriticallyDamped;
        }
    }

    /// <summary>The current displacement from the target (0 = at target).</summary>
    internal float Displacement { get; private set; }

    /// <summary>The current velocity.</summary>
    internal float Velocity { get; private set; }

    /// <summary>The current position as a normalized 0–1 value (1 = at target).</summary>
    internal float Position => 1f + Displacement;

    /// <summary>True when the spring has effectively settled at the target.</summary>
    internal bool IsSettled => MathF.Abs(Displacement) < SettlePositionThreshold
                            && MathF.Abs(Velocity) < SettleVelocityThreshold;

    /// <summary>
    /// Advances the spring simulation by the given time delta.
    /// </summary>
    internal void Advance(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        elapsed += deltaTime;

        switch (regime)
        {
            case SpringRegime.Underdamped:
                AdvanceUnderdamped(elapsed);
                break;
            case SpringRegime.CriticallyDamped:
                AdvanceCriticallyDamped(elapsed);
                break;
            case SpringRegime.Overdamped:
                AdvanceOverdamped(elapsed);
                break;
        }
    }

    /// <summary>
    /// Redirects the spring to a new equilibrium, preserving physical continuity.
    /// The current displacement and velocity become the new initial conditions.
    /// </summary>
    internal void Redirect(float newDisplacement, float newVelocity)
    {
        x0 = newDisplacement;
        v0 = newVelocity;
        elapsed = 0f;
        Displacement = newDisplacement;
        Velocity = newVelocity;
    }

    private void AdvanceUnderdamped(float t)
    {
        float expTerm = MathF.Exp(-zetaOmega * t);
        float cosWd = MathF.Cos(omegaDamped * t);
        float sinWd = MathF.Sin(omegaDamped * t);

        float a = x0;
        float b = (v0 + zetaOmega * x0) / omegaDamped;

        Displacement = expTerm * (a * cosWd + b * sinWd);
        Velocity = expTerm * ((-zetaOmega * a + omegaDamped * b) * cosWd
                            + (-zetaOmega * b - omegaDamped * a) * sinWd);
    }

    private void AdvanceCriticallyDamped(float t)
    {
        float expTerm = MathF.Exp(-omega * t);
        float bCoeff = v0 + omega * x0;

        Displacement = (x0 + bCoeff * t) * expTerm;
        Velocity = (v0 * (1f - omega * t) - omega * omega * x0 * t) * expTerm;
    }

    private void AdvanceOverdamped(float t)
    {
        float denom = r1 - r2;
        if (MathF.Abs(denom) < 1e-10f)
        {
            AdvanceCriticallyDamped(t);
            return;
        }

        float a = (v0 - r2 * x0) / denom;
        float b = (r1 * x0 - v0) / denom;

        float e1 = MathF.Exp(r1 * t);
        float e2 = MathF.Exp(r2 * t);

        Displacement = a * e1 + b * e2;
        Velocity = r1 * a * e1 + r2 * b * e2;
    }

    /// <summary>
    /// Estimates the settling time for a spring (within 1% of target).
    /// </summary>
    internal static float EstimateSettlingTime(float stiffness, float damping)
    {
        if (stiffness <= 0f)
        {
            return float.PositiveInfinity;
        }

        float omega0 = MathF.Sqrt(stiffness);
        float zeta = omega0 > 0f ? damping / (2f * omega0) : 0f;

        if (zeta <= 0f)
        {
            return float.PositiveInfinity;
        }

        // For 1% settling: e^(-ζω₀t) ≈ 0.01 → t ≈ ln(100)/(ζω₀)
        // Adjusted for underdamped overshoot
        float settleConstant = zeta < 1f ? 5.3f : 4.6f;
        return settleConstant / (zeta * omega0);
    }

    private enum SpringRegime
    {
        CriticallyDamped,
        Underdamped,
        Overdamped,
    }
}

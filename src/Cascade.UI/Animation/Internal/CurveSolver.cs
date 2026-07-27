namespace Cascade.UI;

/// <summary>
/// Cubic Bézier curve evaluator for duration-based animations.
/// Uses Newton-Raphson iteration with bisection fallback for the
/// x→t parameter lookup. Accurate to less than 0.001 error.
/// </summary>
/// <remarks>
/// The curve is defined by control points (x1,y1) and (x2,y2) with
/// implicit endpoints at (0,0) and (1,1). Given an input x (time progress),
/// it solves for the parameter t such that Bx(t) = x, then returns By(t)
/// as the eased output value.
/// </remarks>
internal static class CurveSolver
{
    private const int MaxNewtonIterations = 8;
    private const int MaxBisectionIterations = 20;
    private const float NewtonEpsilon = 1e-7f;
    private const float BisectionEpsilon = 1e-7f;

    /// <summary>
    /// Evaluates the cubic Bézier easing curve at the given time fraction.
    /// </summary>
    /// <param name="x">Input time fraction (0.0–1.0).</param>
    /// <param name="x1">X coordinate of the first control point.</param>
    /// <param name="y1">Y coordinate of the first control point.</param>
    /// <param name="x2">X coordinate of the second control point.</param>
    /// <param name="y2">Y coordinate of the second control point.</param>
    /// <returns>The eased output value (0.0–1.0).</returns>
    internal static float Evaluate(float x, float x1, float y1, float x2, float y2)
    {
        if (x <= 0f)
        {
            return 0f;
        }

        if (x >= 1f)
        {
            return 1f;
        }

        // Linear shortcut
        if (x1 == 0f && y1 == 0f && x2 == 1f && y2 == 1f)
        {
            return x;
        }

        float t = SolveForT(x, x1, x2);
        return SampleY(t, y1, y2);
    }

    private static float SolveForT(float x, float x1, float x2)
    {
        // Start with a linear estimate
        float t = x;

        // Newton-Raphson iteration
        for (int i = 0; i < MaxNewtonIterations; i++)
        {
            float residual = SampleX(t, x1, x2) - x;
            if (MathF.Abs(residual) < NewtonEpsilon)
            {
                return t;
            }

            float derivative = SampleXDerivative(t, x1, x2);
            if (MathF.Abs(derivative) < 1e-10f)
            {
                break;
            }

            t -= residual / derivative;
            t = Math.Clamp(t, 0f, 1f);
        }

        // Bisection fallback for robustness
        return Bisect(x, x1, x2);
    }

    private static float Bisect(float x, float x1, float x2)
    {
        float lo = 0f;
        float hi = 1f;

        for (int i = 0; i < MaxBisectionIterations; i++)
        {
            float mid = (lo + hi) * 0.5f;
            float sample = SampleX(mid, x1, x2);

            if (MathF.Abs(sample - x) < BisectionEpsilon)
            {
                return mid;
            }

            if (sample < x)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        return (lo + hi) * 0.5f;
    }

    /// <summary>
    /// Samples the X component of the cubic Bézier at parameter t.
    /// B(t) = 3(1-t)²t·x1 + 3(1-t)t²·x2 + t³
    /// </summary>
    private static float SampleX(float t, float x1, float x2)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        float mt = 1f - t;
        float mt2 = mt * mt;
        return 3f * mt2 * t * x1 + 3f * mt * t2 * x2 + t3;
    }

    /// <summary>
    /// Derivative of the X component: dBx/dt.
    /// </summary>
    private static float SampleXDerivative(float t, float x1, float x2)
    {
        float mt = 1f - t;
        return 3f * mt * mt * x1 + 6f * mt * t * (x2 - x1) + 3f * t * t * (1f - x2);
    }

    /// <summary>
    /// Samples the Y component of the cubic Bézier at parameter t.
    /// </summary>
    private static float SampleY(float t, float y1, float y2)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        float mt = 1f - t;
        float mt2 = mt * mt;
        return 3f * mt2 * t * y1 + 3f * mt * t2 * y2 + t3;
    }
}

namespace Cascade.UI;

/// <summary>
/// Defines a dash pattern for stroked paths. Specifies alternating on/off
/// lengths and an initial offset into the pattern.
/// </summary>
/// <param name="On">Length of the visible dash segment.</param>
/// <param name="Off">Length of the gap between dashes.</param>
/// <param name="Offset">Initial offset into the pattern (default 0).</param>
public readonly record struct DashPattern(float On, float Off, float Offset = 0f);

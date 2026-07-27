using System.Collections.Immutable;

namespace Cascade.UI;

/// <summary>
/// A composable shadow specification supporting multiple drop shadows and inner
/// shadows simultaneously. Any node or control state can carry a ShadowSpec.
/// </summary>
public record ShadowSpec
{
    /// <summary>Drop shadows (rendered below the node).</summary>
    public ImmutableArray<DropShadow> Drop { get; init; } = [];

    /// <summary>Inner shadows (rendered inside the node).</summary>
    public ImmutableArray<InnerShadow> Inner { get; init; } = [];

    /// <summary>No shadow.</summary>
    public static readonly ShadowSpec None = new();

    /// <summary>Creates a spec from a single drop shadow.</summary>
    public static ShadowSpec FromDrop(DropShadow s)
    {
        return new ShadowSpec { Drop = [s] };
    }

    /// <summary>Creates a spec from a single inner shadow.</summary>
    public static ShadowSpec FromInner(InnerShadow s)
    {
        return new ShadowSpec { Inner = [s] };
    }

    /// <summary>Adds a drop shadow layer to this spec.</summary>
    public ShadowSpec AndDrop(DropShadow s)
    {
        return this with { Drop = Drop.Add(s) };
    }

    /// <summary>Adds an inner shadow layer to this spec.</summary>
    public ShadowSpec AndInner(InnerShadow s)
    {
        return this with { Inner = Inner.Add(s) };
    }

    /// <summary>Implicit conversion from a single DropShadow for convenience.</summary>
    public static implicit operator ShadowSpec(DropShadow s)
    {
        return FromDrop(s);
    }

    /// <summary>
    /// Linearly interpolates between two shadow specs. Interpolates the first drop
    /// shadow's properties. If the specs have different layer counts, the result
    /// uses the "to" spec's structure with blended values.
    /// </summary>
    public static ShadowSpec Lerp(ShadowSpec from, ShadowSpec to, float t)
    {
        if (t <= 0f)
        {
            return from;
        }

        if (t >= 1f)
        {
            return to;
        }

        // Interpolate drop shadows (match by index, up to the shorter list)
        var fromDrop = from.Drop;
        var toDrop = to.Drop;
        int dropCount = Math.Max(fromDrop.Length, toDrop.Length);
        var dropBuilder = ImmutableArray.CreateBuilder<DropShadow>(dropCount);

        for (int i = 0; i < dropCount; i++)
        {
            var a = i < fromDrop.Length ? fromDrop[i] : DropShadow.None;
            var b = i < toDrop.Length ? toDrop[i] : DropShadow.None;
            dropBuilder.Add(new DropShadow
            {
                Blur = a.Blur + (b.Blur - a.Blur) * t,
                Spread = a.Spread + (b.Spread - a.Spread) * t,
                OffsetX = a.OffsetX + (b.OffsetX - a.OffsetX) * t,
                OffsetY = a.OffsetY + (b.OffsetY - a.OffsetY) * t,
                Color = ColorValue.Lerp(a.Color, b.Color, t),
            });
        }

        return new ShadowSpec { Drop = dropBuilder.MoveToImmutable() };
    }
}

/// <summary>
/// A single drop shadow layer.
/// </summary>
public readonly record struct DropShadow
{
    /// <summary>Blur radius in logical pixels.</summary>
    public required float Blur { get; init; }

    /// <summary>Spread radius in logical pixels.</summary>
    public float Spread { get; init; }

    /// <summary>Horizontal offset in logical pixels.</summary>
    public float OffsetX { get; init; }

    /// <summary>Vertical offset in logical pixels.</summary>
    public float OffsetY { get; init; }

    /// <summary>Shadow color.</summary>
    public required ColorValue Color { get; init; }

    /// <summary>No shadow.</summary>
    public static readonly DropShadow None =
        new() { Blur = 0, Color = ColorValue.Transparent };
}

/// <summary>
/// A single inner shadow layer.
/// </summary>
public readonly record struct InnerShadow
{
    /// <summary>Blur radius in logical pixels.</summary>
    public required float Blur { get; init; }

    /// <summary>Spread radius in logical pixels.</summary>
    public float Spread { get; init; }

    /// <summary>Horizontal offset in logical pixels.</summary>
    public float OffsetX { get; init; }

    /// <summary>Vertical offset in logical pixels.</summary>
    public float OffsetY { get; init; }

    /// <summary>Shadow color.</summary>
    public required ColorValue Color { get; init; }

    /// <summary>No inner shadow.</summary>
    public static readonly InnerShadow None =
        new() { Blur = 0, Color = ColorValue.Transparent };
}

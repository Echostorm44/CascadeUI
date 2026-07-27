namespace Cascade.UI;

/// <summary>
/// Defines the visual shape of particles in a particle effect.
/// </summary>
public sealed class ParticleShape
{
    private ParticleShape()
    {
    }

    internal ShapeKind Kind { get; private set; }
    internal float RadiusValue { get; private set; }
    internal Size SizeValue { get; private set; }
    internal float TriangleSizeValue { get; private set; }
    internal Path? CustomPathValue { get; private set; }
    internal string? EmojiText { get; private set; }
    internal float FontSizeValue { get; private set; }
    internal ParticleShape[]? MixedShapes { get; private set; }

    /// <summary>Circular particle.</summary>
    public static ParticleShape Circle(float radius)
    {
        return new ParticleShape
        {
            Kind = ShapeKind.Circle,
            RadiusValue = radius,
        };
    }

    /// <summary>Rectangular particle.</summary>
    public static ParticleShape Rect(Size size)
    {
        return new ParticleShape
        {
            Kind = ShapeKind.Rect,
            SizeValue = size,
        };
    }

    /// <summary>Triangular particle.</summary>
    public static ParticleShape Triangle(float size)
    {
        return new ParticleShape
        {
            Kind = ShapeKind.Triangle,
            TriangleSizeValue = size,
        };
    }

    /// <summary>Custom path-based particle shape.</summary>
    public static ParticleShape Custom(Path path)
    {
        return new ParticleShape
        {
            Kind = ShapeKind.Custom,
            CustomPathValue = path,
        };
    }

    /// <summary>Emoji or symbol character as a particle.</summary>
    public static ParticleShape Text(string emoji, float size)
    {
        return new ParticleShape
        {
            Kind = ShapeKind.Text,
            EmojiText = emoji,
            FontSizeValue = size,
        };
    }

    /// <summary>
    /// Mixes multiple shapes — each particle is randomly assigned one
    /// of the supplied shapes at spawn time. Distribution is uniform.
    /// Accepts 2–8 shapes.
    /// </summary>
    public static ParticleShape Mix(params ParticleShape[] shapes)
    {
        if (shapes.Length < 2 || shapes.Length > 8)
        {
            throw new ArgumentException("Mix requires between 2 and 8 shapes.", nameof(shapes));
        }

        var copy = new ParticleShape[shapes.Length];
        Array.Copy(shapes, copy, shapes.Length);

        return new ParticleShape
        {
            Kind = ShapeKind.Mix,
            MixedShapes = copy,
        };
    }

    internal enum ShapeKind
    {
        Circle,
        Rect,
        Triangle,
        Custom,
        Text,
        Mix,
    }
}

using System;

namespace Cascade.UI;

/// <summary>
/// Complete typography specification for a theme: font families and a type scale.
/// Resolved text styles are derived from the scale and font family.
/// </summary>
public record TypographySet
{
    /// <summary>Primary font family used throughout the theme.</summary>
    public required FontFamily FontFamily { get; init; }

    /// <summary>Type scale defining sizes, weights, and line heights for each text role.</summary>
    public required TypeScale Scale { get; init; }

    /// <summary>Monospace font family for code blocks. Falls back to system monospace if null.</summary>
    public FontFamily? MonoFamily { get; init; }

    /// <summary>Display text style — large hero text.</summary>
    public TextStyle Display => Scale.Display;

    /// <summary>Heading 1 text style.</summary>
    public TextStyle Heading1 => Scale.H1;

    /// <summary>Heading 2 text style.</summary>
    public TextStyle Heading2 => Scale.H2;

    /// <summary>Heading 3 text style.</summary>
    public TextStyle Heading3 => Scale.H3;

    /// <summary>Body text style — primary content text.</summary>
    public TextStyle Body => Scale.Body;

    /// <summary>Small body text style.</summary>
    public TextStyle BodySmall => Scale.BodySmall;

    /// <summary>Caption text style — labels, hints.</summary>
    public TextStyle Caption => Scale.Caption;

    /// <summary>Code text style — monospace content.</summary>
    public TextStyle Code => Scale.Code;

    /// <summary>Shorthand alias for <see cref="Heading1"/>.</summary>
    public TextStyle H1 => Heading1;

    /// <summary>Shorthand alias for <see cref="Heading2"/>.</summary>
    public TextStyle H2 => Heading2;

    /// <summary>Shorthand alias for <see cref="Heading3"/>.</summary>
    public TextStyle H3 => Heading3;
}

/// <summary>
/// Defines the complete type scale: size, weight, and line height for each text role.
/// </summary>
public record TypeScale
{
    /// <summary>Display text — large hero text.</summary>
    public required TextStyle Display { get; init; }

    /// <summary>Heading 1.</summary>
    public required TextStyle H1 { get; init; }

    /// <summary>Heading 2.</summary>
    public required TextStyle H2 { get; init; }

    /// <summary>Heading 3.</summary>
    public required TextStyle H3 { get; init; }

    /// <summary>Body text — primary content.</summary>
    public required TextStyle Body { get; init; }

    /// <summary>Small body text.</summary>
    public required TextStyle BodySmall { get; init; }

    /// <summary>Caption text — labels, hints, timestamps.</summary>
    public required TextStyle Caption { get; init; }

    /// <summary>Code text — monospace content.</summary>
    public required TextStyle Code { get; init; }

    /// <summary>A default type scale suitable for general use.</summary>
    public static TypeScale Default { get; } = new()
    {
        Display   = new TextStyle(34, FontWeight.Bold,     1.2f),
        H1        = new TextStyle(28, FontWeight.Bold,     1.2f),
        H2        = new TextStyle(22, FontWeight.SemiBold, 1.3f),
        H3        = new TextStyle(17, FontWeight.SemiBold, 1.4f),
        Body      = new TextStyle(17, FontWeight.Regular,  1.5f),
        BodySmall = new TextStyle(15, FontWeight.Regular,  1.5f),
        Caption   = new TextStyle(12, FontWeight.Regular,  1.4f),
        Code      = new TextStyle(14, FontWeight.Regular,  1.6f),
    };
}

/// <summary>
/// A resolved text style with font size, weight, and line height.
/// </summary>
public readonly record struct TextStyle(float Size, FontWeight Weight, float LineHeight);

/// <summary>
/// Font weight values following the CSS/OpenType standard (100–900).
/// </summary>
public enum FontWeight
{
    /// <summary>No weight specified.</summary>
    None = 0,

    /// <summary>Thin (100).</summary>
    Thin = 100,

    /// <summary>Extra-light (200).</summary>
    ExtraLight = 200,

    /// <summary>Light (300).</summary>
    Light = 300,

    /// <summary>Regular / Normal (400).</summary>
    Regular = 400,

    /// <summary>Medium (500).</summary>
    Medium = 500,

    /// <summary>Semi-bold (600).</summary>
    SemiBold = 600,

    /// <summary>Bold (700).</summary>
    Bold = 700,

    /// <summary>Extra-bold (800).</summary>
    ExtraBold = 800,

    /// <summary>Black (900).</summary>
    Black = 900,
}

/// <summary>
/// Represents a font family. Constructed via factory methods.
/// </summary>
public sealed class FontFamily
{
    private FontFamily()
        : this(FontFamilyKind.SystemDefault)
    {
    }

    private FontFamily(FontFamilyKind kind, string? bundledName = null, SystemFont? category = null)
    {
        Kind = kind;
        BundledName = bundledName;
        Category = category;
    }

    /// <summary>The platform's default system font.</summary>
    public static FontFamily System { get; } = new();

    /// <summary>Returns a system font for the specified category.</summary>
    public static FontFamily ForCategory(SystemFont font)
    {
        return new FontFamily(FontFamilyKind.SystemCategory, category: font);
    }

    /// <summary>
    /// References a font bundled in the application. The name must match a
    /// CascadeFont declaration in the csproj — compile-time verified by source generator.
    /// </summary>
    public static FontFamily Bundled(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Font family name cannot be null or whitespace.", nameof(name));
        }

        return new FontFamily(FontFamilyKind.Bundled, bundledName: name.Trim());
    }

    /// <summary>Identifies how this font family should be resolved.</summary>
    internal FontFamilyKind Kind { get; }

    /// <summary>Name of the bundled font when <see cref="Kind"/> is <see cref="FontFamilyKind.Bundled"/>.</summary>
    internal string? BundledName { get; }

    /// <summary>Requested system font category when applicable.</summary>
    internal SystemFont? Category { get; }
}

/// <summary>
/// Categories of system fonts.
/// </summary>
public enum SystemFont
{
    /// <summary>The default system UI font.</summary>
    Default,

    /// <summary>The system monospace font.</summary>
    Monospace,

    /// <summary>The system serif font.</summary>
    Serif,

    /// <summary>The system rounded font (where available).</summary>
    Rounded,
}

internal enum FontFamilyKind
{
    SystemDefault,
    SystemCategory,
    Bundled,
}

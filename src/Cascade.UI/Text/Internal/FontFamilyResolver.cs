using System.Collections.Concurrent;

namespace Cascade.UI;

/// <summary>
/// Turns a <see cref="FontFamily"/>, weight and style into the font locator text is shaped,
/// measured and drawn with (see <see cref="FontLocator"/>). Layout and paint both resolve through
/// here, so a label is measured in exactly the font it is drawn in. Results are cached: resolving
/// probes the file system.
/// </summary>
internal static class FontFamilyResolver
{
    // Per family (by value), weight and style; the empty family key is the theme default.
    private static readonly ConcurrentDictionary<(string Family, string? DefaultPath, FontWeight Weight, bool Italic), string?> resolved = new();

    private static readonly string[] monospaceFonts =
    [
        "CascadiaMono.ttf", "consola.ttf", "cour.ttf",                     // Windows
        "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf",             // Debian/Ubuntu
        "/usr/share/fonts/dejavu/DejaVuSansMono.ttf",                      // Fedora
        "/System/Library/Fonts/Menlo.ttc", "/System/Library/Fonts/Supplemental/Courier New.ttf", // macOS
    ];

    private static readonly string[] serifFonts =
    [
        "georgia.ttf", "times.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSerif.ttf",
        "/usr/share/fonts/dejavu/DejaVuSerif.ttf",
        "/System/Library/Fonts/Supplemental/Georgia.ttf", "/System/Library/Fonts/Supplemental/Times New Roman.ttf",
    ];

    /// <summary>
    /// The locator for <paramref name="family"/> (null: the theme's family, whose regular font is
    /// <paramref name="defaultPath"/>) at <paramref name="weight"/> and <paramref name="italic"/>.
    /// Null when no font is available at all (headless tests).
    /// </summary>
    public static string? Resolve(FontFamily? family, FontWeight weight, bool italic, string? defaultPath)
    {
        string familyKey = family is null ? "" : $"{family.Kind}:{family.BundledName}:{family.Category}";
        return resolved.GetOrAdd((familyKey, family is null ? defaultPath : null, weight, italic), _ =>
        {
            string? regular = family is null ? defaultPath : RegularPath(family);
            regular ??= defaultPath;
            return regular is null ? null : FontFallback.ResolveFont(regular, weight, italic);
        });
    }

    /// <summary>The regular font file of a family: a bundled font from the app's fonts directory, or a system font.</summary>
    public static string? RegularPath(FontFamily family)
    {
        switch (family.Kind)
        {
            case FontFamilyKind.Bundled when family.BundledName is not null:
                // The build copies bundled fonts to Fonts/; older layouts used fonts/ (the same
                // folder on Windows, not on Linux or macOS).
                return ResolveBundledFontFile(System.IO.Path.Combine(AppContext.BaseDirectory, "Fonts"), family.BundledName)
                    ?? ResolveBundledFontFile(System.IO.Path.Combine(AppContext.BaseDirectory, "fonts"), family.BundledName)
                    ?? FontFallback.FindFallbackFont('A');
            case FontFamilyKind.SystemCategory when family.Category == SystemFont.Monospace:
                return FirstExisting(monospaceFonts) ?? FontFallback.FindFallbackFont('A');
            case FontFamilyKind.SystemCategory when family.Category == SystemFont.Serif:
                return FirstExisting(serifFonts) ?? FontFallback.FindFallbackFont('A');
            default:
                return FontFallback.FindFallbackFont('A');
        }
    }

    private static string? FirstExisting(string[] candidates)
    {
        string systemFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        foreach (string candidate in candidates)
        {
            string path = System.IO.Path.IsPathRooted(candidate) || string.IsNullOrEmpty(systemFonts)
                ? candidate
                : System.IO.Path.Combine(systemFonts, candidate);
            if (System.IO.Path.IsPathRooted(path) && File.Exists(path))
            {
                return path;
            }
        }
        return null;
    }

    /// <summary>
    /// A bundled font file by family name: the exact file name, then <c>Name.ttf</c>,
    /// <c>NameVariable.ttf</c> (a variable font serves every weight) and <c>Name-Regular.ttf</c>,
    /// for .ttf, .otf and .ttc.
    /// </summary>
    internal static string? ResolveBundledFontFile(string fontsDir, string familyName)
    {
        if (!Directory.Exists(fontsDir))
        {
            return null;
        }

        string exact = System.IO.Path.Combine(fontsDir, familyName);
        if (File.Exists(exact))
        {
            return exact;
        }

        foreach (string ext in (string[])[".ttf", ".otf", ".ttc"])
        {
            foreach (string suffix in (string[])["", "Variable", "-Regular"])
            {
                string candidate = System.IO.Path.Combine(fontsDir, familyName + suffix + ext);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        return null;
    }
}

namespace Cascade.UI;

/// <summary>
/// Discovers fallback fonts when the primary font is missing a glyph.
/// Checks common system fonts by Unicode block and caches decisions
/// per codepoint range for fast subsequent lookups.
/// </summary>
internal static class FontFallback
{
    static readonly Lock lockObj = new();
    static readonly Dictionary<UnicodeBlock, string?> blockCache = new();
    static readonly Dictionary<string, HarfBuzzShaper> probeCache = new();

    static readonly string fontsDir = GetSystemFontsDir();

    // Candidates ordered by coverage and quality for each block
    static readonly string[] latinFonts =
        ["arial.ttf", "segoeui.ttf", "tahoma.ttf", "verdana.ttf", "calibri.ttf", "times.ttf"];
    static readonly string[] cjkFonts =
        ["msyh.ttc", "simsun.ttc", "msgothic.ttc", "malgun.ttf", "msjh.ttc", "YuGothR.ttc"];
    static readonly string[] arabicFonts =
        ["arial.ttf", "segoeui.ttf", "tahoma.ttf"];
    static readonly string[] hebrewFonts =
        ["arial.ttf", "segoeui.ttf", "tahoma.ttf"];
    static readonly string[] devanagariFonts =
        // Nirmala UI ships as a TrueType *collection* (.ttc) on Win10/11 — the
        // primary Devanagari face. Mangal (.ttf) is the older fallback.
        ["nirmala.ttc", "nirmala.ttf", "mangal.ttf", "aparaj.ttf", "kokila.ttf"];
    static readonly string[] emojiFonts =
        ["seguiemj.ttf", "seguisym.ttf"];
    static readonly string[] genericFallback =
        ["arial.ttf", "segoeui.ttf", "tahoma.ttf", "DejaVuSans.ttf"];

    /// <summary>
    /// Resolves a weight-specific variant of a font file (the upright style). See
    /// <see cref="ResolveFont"/>.
    /// </summary>
    internal static string ResolveFontForWeight(string regularFontPath, FontWeight weight)
        => ResolveFont(regularFontPath, weight, italic: false);

    /// <summary>
    /// The font locator (see <see cref="FontLocator"/>) for <paramref name="weight"/> and
    /// <paramref name="italic"/> in the family of <paramref name="regularFontPath"/>. The italic
    /// style is a sibling file (InterVariable-Italic.ttf, segoeuii.ttf). A variable font serves
    /// every weight from its weight axis; a static family by its weight files
    /// (Inter-Light.ttf, seguisb.ttf), falling back to the nearest weight it has. Returns the
    /// regular font when the family has neither.
    /// </summary>
    internal static string ResolveFont(string regularFontPath, FontWeight weight, bool italic)
    {
        string path = FontLocator.FilePath(regularFontPath);
        if (italic && ResolveItalicFile(path) is { } italicPath)
        {
            path = italicPath;
        }
        if (FontLocator.HasWeightAxis(path))
        {
            return FontLocator.WithWeight(path, weight);
        }
        if (weight is FontWeight.Regular or FontWeight.None)
        {
            return path;
        }

        string dir = System.IO.Path.GetDirectoryName(path) ?? "";
        string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
        string ext = System.IO.Path.GetExtension(path);
        foreach (string candidate in StaticWeightCandidates(fileName, weight))
        {
            string fullPath = System.IO.Path.Combine(dir, candidate + ext);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }
        return path;
    }

    /// <summary>The italic sibling of a font file, or null when the family has none.</summary>
    internal static string? ResolveItalicFile(string fontPath)
    {
        string dir = System.IO.Path.GetDirectoryName(fontPath) ?? "";
        string fileName = System.IO.Path.GetFileNameWithoutExtension(fontPath);
        string ext = System.IO.Path.GetExtension(fontPath);
        if (fileName.EndsWith("Italic", StringComparison.OrdinalIgnoreCase))
        {
            return fontPath;
        }

        string[] candidates = fileName.EndsWith("-Regular", StringComparison.OrdinalIgnoreCase)
            ? [fileName[..^"-Regular".Length] + "-Italic"]
            : [fileName + "-Italic", fileName + "Italic", fileName + "i"];
        foreach (string candidate in candidates)
        {
            string fullPath = System.IO.Path.Combine(dir, candidate + ext);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }
        return null;
    }

    // Static weight files, nearest weight first: "Family-Regular" / "Family-Italic" follow the
    // standard naming (Family-Light, Family-BoldItalic); the Windows core fonts their own short
    // names.
    private static IEnumerable<string> StaticWeightCandidates(string fileName, FontWeight weight)
    {
        foreach (var nearest in NearestWeights(weight))
        {
            if (fileName.EndsWith("-Regular", StringComparison.OrdinalIgnoreCase))
            {
                yield return fileName[..^"-Regular".Length] + "-" + WeightName(nearest);
            }
            else if (fileName.EndsWith("-Italic", StringComparison.OrdinalIgnoreCase))
            {
                yield return fileName[..^"-Italic".Length] + "-" + WeightName(nearest) + "Italic";
            }
            else if (WindowsCoreFontWeight(fileName, nearest) is { } coreFont)
            {
                yield return coreFont;
            }
            else
            {
                yield return fileName + "-" + WeightName(nearest);
            }
        }
    }

    /// <summary>The weights to try for <paramref name="weight"/>: itself, then the others by distance (bolder first on a tie), never regular.</summary>
    private static IEnumerable<FontWeight> NearestWeights(FontWeight weight)
    {
        int requested = (int)weight;
        return Enum.GetValues<FontWeight>()
            .Where(w => w is not (FontWeight.None or FontWeight.Regular))
            .OrderBy(w => Math.Abs((int)w - requested))
            .ThenByDescending(w => (int)w)
            .Where(w => Math.Abs((int)w - requested) <= 200);
    }

    private static string WeightName(FontWeight weight) => weight switch
    {
        FontWeight.Thin => "Thin",
        FontWeight.ExtraLight => "ExtraLight",
        FontWeight.Light => "Light",
        FontWeight.Medium => "Medium",
        FontWeight.SemiBold => "SemiBold",
        FontWeight.Bold => "Bold",
        FontWeight.ExtraBold => "ExtraBold",
        FontWeight.Black => "Black",
        _ => "Regular",
    };

    // Windows' core fonts: regular and italic file names to their weight files.
    private static string? WindowsCoreFontWeight(string fileName, FontWeight weight)
    {
        string name = fileName.ToUpperInvariant();
        return (name, weight) switch
        {
            ("SEGOEUI", FontWeight.Light or FontWeight.ExtraLight or FontWeight.Thin) => "segoeuil",
            ("SEGOEUI", FontWeight.SemiBold or FontWeight.Medium) => "seguisb",
            ("SEGOEUI", FontWeight.Bold) => "segoeuib",
            ("SEGOEUI", FontWeight.ExtraBold or FontWeight.Black) => "seguibl",
            ("SEGOEUII", FontWeight.Light or FontWeight.ExtraLight or FontWeight.Thin) => "seguili",
            ("SEGOEUII", FontWeight.SemiBold or FontWeight.Medium) => "seguisbi",
            ("SEGOEUII", FontWeight.Bold) => "segoeuiz",
            ("SEGOEUII", FontWeight.ExtraBold or FontWeight.Black) => "seguibli",
            ("ARIAL", FontWeight.SemiBold or FontWeight.Bold or FontWeight.ExtraBold or FontWeight.Black) => "arialbd",
            ("ARIALI", FontWeight.SemiBold or FontWeight.Bold or FontWeight.ExtraBold or FontWeight.Black) => "arialbi",
            ("CALIBRI", FontWeight.Light or FontWeight.ExtraLight or FontWeight.Thin) => "calibril",
            ("CALIBRI", FontWeight.SemiBold or FontWeight.Bold or FontWeight.ExtraBold or FontWeight.Black) => "calibrib",
            ("CALIBRII", FontWeight.Light or FontWeight.ExtraLight or FontWeight.Thin) => "calibrili",
            ("CALIBRII", FontWeight.SemiBold or FontWeight.Bold or FontWeight.ExtraBold or FontWeight.Black) => "calibriz",
            ("CONSOLA", FontWeight.SemiBold or FontWeight.Bold or FontWeight.ExtraBold or FontWeight.Black) => "consolab",
            ("CONSOLAI", FontWeight.SemiBold or FontWeight.Bold or FontWeight.ExtraBold or FontWeight.Black) => "consolaz",
            ("GEORGIA", FontWeight.SemiBold or FontWeight.Bold or FontWeight.ExtraBold or FontWeight.Black) => "georgiab",
            ("GEORGIAI", FontWeight.SemiBold or FontWeight.Bold or FontWeight.ExtraBold or FontWeight.Black) => "georgiaz",
            ("TIMES", FontWeight.SemiBold or FontWeight.Bold or FontWeight.ExtraBold or FontWeight.Black) => "timesbd",
            ("TIMESI", FontWeight.SemiBold or FontWeight.Bold or FontWeight.ExtraBold or FontWeight.Black) => "timesbi",
            ("TAHOMA", FontWeight.SemiBold or FontWeight.Bold or FontWeight.ExtraBold or FontWeight.Black) => "tahomabd",
            ("VERDANA", FontWeight.SemiBold or FontWeight.Bold or FontWeight.ExtraBold or FontWeight.Black) => "verdanab",
            ("VERDANAI", FontWeight.SemiBold or FontWeight.Bold or FontWeight.ExtraBold or FontWeight.Black) => "verdanaz",
            _ => null,
        };
    }

    /// <summary>
    /// Finds a font that contains a glyph for the given codepoint.
    /// Returns the full path to the font file, or null if no fallback is available.
    /// Results are cached per Unicode block.
    /// </summary>
    internal static string? FindFallbackFont(int codepoint)
    {
        var block = GetUnicodeBlock(codepoint);

        lock (lockObj)
        {
            if (blockCache.TryGetValue(block, out var cached))
            {
                return cached;
            }
        }

        // Try block-specific candidates first
        string? result = TryCandidates(GetCandidatesForBlock(block), codepoint);

        // If block-specific search failed, try generic fallback
        if (result == null && block != UnicodeBlock.Latin)
        {
            result = TryCandidates(genericFallback, codepoint);
        }

        // Last resort: scan all .ttf files in the fonts directory
        if (result == null)
        {
            result = ScanFontsDirectory(codepoint);
        }

        lock (lockObj)
        {
            blockCache[block] = result;
        }
        return result;
    }

    static string? cachedEmojiFontPath;

    /// <summary>
    /// Returns the system emoji font path (e.g., Segoe UI Emoji on Windows).
    /// Caches the result after first lookup.
    /// </summary>
    internal static string? GetEmojiFontPath()
    {
        if (cachedEmojiFontPath != null)
        {
            return cachedEmojiFontPath;
        }

        // Try a well-known emoji codepoint (😀 U+1F600) to probe the emoji font
        cachedEmojiFontPath = FindFallbackFont(0x1F600);
        return cachedEmojiFontPath;
    }

    /// <summary>
    /// Returns true if the given font file contains a glyph for the codepoint.
    /// </summary>
    internal static bool FontHasGlyph(string fontPath, int codepoint)
    {
        var shaper = GetOrCreateProbe(fontPath);
        return shaper != null && shaper.HasGlyph(codepoint);
    }

    /// <summary>
    /// Clears all cached fallback decisions and disposes probed font handles.
    /// </summary>
    internal static void ClearCache()
    {
        lock (lockObj)
        {
            blockCache.Clear();
            foreach (var shaper in probeCache.Values)
            {
                shaper.Dispose();
            }
            probeCache.Clear();
        }
    }

    static string? TryCandidates(string[] candidates, int codepoint)
    {
        foreach (var fontFile in candidates)
        {
            string fullPath = System.IO.Path.Combine(fontsDir, fontFile);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var shaper = GetOrCreateProbe(fullPath);
            if (shaper != null && shaper.HasGlyph(codepoint))
            {
                return fullPath;
            }
        }
        return null;
    }

    static string? ScanFontsDirectory(int codepoint)
    {
        if (!Directory.Exists(fontsDir))
        {
            return null;
        }

        foreach (var file in Directory.EnumerateFiles(fontsDir, "*.ttf"))
        {
            var shaper = GetOrCreateProbe(file);
            if (shaper != null && shaper.HasGlyph(codepoint))
            {
                return file;
            }
        }
        return null;
    }

    static HarfBuzzShaper? GetOrCreateProbe(string fontPath)
    {
        lock (lockObj)
        {
            if (probeCache.TryGetValue(fontPath, out var existing))
            {
                return existing;
            }
        }

        try
        {
            var shaper = new HarfBuzzShaper(fontPath);
            lock (lockObj)
            {
                // Check again under lock (double-checked locking)
                if (probeCache.TryGetValue(fontPath, out var existing))
                {
                    shaper.Dispose();
                    return existing;
                }
                probeCache[fontPath] = shaper;
            }
            return shaper;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (System.IO.IOException)
        {
            return null;
        }
    }

    static string[] GetCandidatesForBlock(UnicodeBlock block)
    {
        return block switch
        {
            UnicodeBlock.Latin      => latinFonts,
            UnicodeBlock.CJK        => cjkFonts,
            UnicodeBlock.Arabic     => arabicFonts,
            UnicodeBlock.Hebrew     => hebrewFonts,
            UnicodeBlock.Devanagari => devanagariFonts,
            UnicodeBlock.Emoji      => emojiFonts,
            _                       => genericFallback,
        };
    }

    static UnicodeBlock GetUnicodeBlock(int codepoint)
    {
        return codepoint switch
        {
            >= 0x0000 and <= 0x024F => UnicodeBlock.Latin,
            >= 0x0250 and <= 0x02AF => UnicodeBlock.Latin,   // IPA Extensions
            >= 0x0370 and <= 0x03FF => UnicodeBlock.Latin,   // Greek (treat as Latin for fallback)
            >= 0x0400 and <= 0x052F => UnicodeBlock.Latin,   // Cyrillic
            >= 0x0590 and <= 0x05FF => UnicodeBlock.Hebrew,
            >= 0x0600 and <= 0x06FF => UnicodeBlock.Arabic,
            >= 0x0750 and <= 0x077F => UnicodeBlock.Arabic,
            >= 0x08A0 and <= 0x08FF => UnicodeBlock.Arabic,
            >= 0x0900 and <= 0x097F => UnicodeBlock.Devanagari,
            >= 0x2600 and <= 0x27BF => UnicodeBlock.Emoji,   // Miscellaneous Symbols / Dingbats
            >= 0x2E80 and <= 0x9FFF => UnicodeBlock.CJK,
            >= 0xAC00 and <= 0xD7AF => UnicodeBlock.CJK,    // Hangul
            >= 0xF900 and <= 0xFAFF => UnicodeBlock.CJK,
            >= 0x1F000 and <= 0x1FFFF => UnicodeBlock.Emoji, // Supplementary Symbols (emoji)
            _                       => UnicodeBlock.Other,
        };
    }

    static string GetSystemFontsDir()
    {
        // Windows
        string windowsFonts = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
        if (Directory.Exists(windowsFonts))
        {
            return windowsFonts;
        }

        // Linux
        if (Directory.Exists("/usr/share/fonts"))
        {
            return "/usr/share/fonts";
        }

        // macOS
        if (Directory.Exists("/System/Library/Fonts"))
        {
            return "/System/Library/Fonts";
        }

        return "";
    }
}

/// <summary>
/// Coarse Unicode block classification for font fallback lookup.
/// </summary>
internal enum UnicodeBlock
{
    Latin,
    Hebrew,
    Arabic,
    Devanagari,
    CJK,
    Emoji,
    Other,
}

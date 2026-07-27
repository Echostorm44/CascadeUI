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
    /// Resolves a weight-specific variant of a font file.
    /// For example, segoeui.ttf → seguisb.ttf for SemiBold.
    /// Returns the original path if no variant is found.
    /// </summary>
    internal static string ResolveFontForWeight(string regularFontPath, FontWeight weight)
    {
        if (weight is FontWeight.Regular or FontWeight.None)
        {
            return regularFontPath;
        }

        string dir = System.IO.Path.GetDirectoryName(regularFontPath) ?? "";
        string fileName = System.IO.Path.GetFileNameWithoutExtension(regularFontPath);
        string ext = System.IO.Path.GetExtension(regularFontPath);

        // Try known weight mappings for common system fonts (case-insensitive)
        string[]? candidates = GetWeightCandidates(fileName, weight, ext);
        if (candidates != null)
        {
            foreach (string candidate in candidates)
            {
                string fullPath = System.IO.Path.Combine(dir, candidate);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        // Generic suffix approach: try appending weight suffix
        string[] genericSuffixes = weight switch
        {
            FontWeight.Bold or FontWeight.ExtraBold or FontWeight.Black =>
                ["-Bold", "b", "-bold", "bd"],
            FontWeight.SemiBold or FontWeight.Medium =>
                ["-Semibold", "sb", "-semibold", "-Medium", "-Bold", "b", "-bold", "bd"],
            FontWeight.Light or FontWeight.ExtraLight or FontWeight.Thin =>
                ["-Light", "l", "-light"],
            _ => [],
        };

        foreach (string suffix in genericSuffixes)
        {
            string candidate = System.IO.Path.Combine(dir, fileName + suffix + ext);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return regularFontPath;
    }

    private static string[]? GetWeightCandidates(string fileName, FontWeight weight, string ext)
    {
        bool isBoldish = weight is FontWeight.Bold or FontWeight.ExtraBold or FontWeight.Black;
        bool isSemiBoldish = weight is FontWeight.SemiBold or FontWeight.Medium;
        bool isLightish = weight is FontWeight.Light or FontWeight.ExtraLight or FontWeight.Thin;

        // Case-insensitive comparison for font file names
        return string.Equals(fileName, "segoeui", StringComparison.OrdinalIgnoreCase)
            ? (isSemiBoldish ? ["seguisb" + ext] :
               isBoldish ? ["segoeuib" + ext] :
               isLightish ? ["segoeuil" + ext] : null)
            : string.Equals(fileName, "arial", StringComparison.OrdinalIgnoreCase)
            ? (isBoldish || isSemiBoldish ? ["arialbd" + ext] : null)
            : string.Equals(fileName, "tahoma", StringComparison.OrdinalIgnoreCase)
            ? (isBoldish || isSemiBoldish ? ["tahomabd" + ext] : null)
            : string.Equals(fileName, "verdana", StringComparison.OrdinalIgnoreCase)
            ? (isBoldish || isSemiBoldish ? ["verdanab" + ext] : null)
            : string.Equals(fileName, "calibri", StringComparison.OrdinalIgnoreCase)
            ? (isBoldish || isSemiBoldish ? ["calibrib" + ext] :
               isLightish ? ["calibril" + ext] : null)
            : string.Equals(fileName, "times", StringComparison.OrdinalIgnoreCase)
            ? (isBoldish || isSemiBoldish ? ["timesbd" + ext] : null)
            : string.Equals(fileName, "Inter-Regular", StringComparison.OrdinalIgnoreCase)
            ? (isBoldish ? ["Inter-SemiBold" + ext, "Inter-Medium" + ext] :
               isSemiBoldish ? ["Inter-SemiBold" + ext, "Inter-Medium" + ext] :
               isLightish ? ["Inter-Regular" + ext] : null)
            : null;
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

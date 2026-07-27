using System;
using System.Collections.Generic;
using System.IO;

namespace Cascade.UI.Backend.Etch;

/// <summary>
/// Categories for env-gated debug logging. Each category writes to its own
/// file under <c>debug-logs/&lt;category&gt;.log</c> next to the executable.
/// </summary>
internal enum DebugLogCategory
{
    Present,
    Transform,
    Capture,
    Layer,
    Clip,
    Frame,
    Instance,
    Glyph,
    Validation,
}

/// <summary>
/// Env-gated debug logger for the Etch backend. Enabled categories are parsed
/// once from <c>CASCADE_DEBUG_LOG</c> (comma-separated category names,
/// case-insensitive, or <c>all</c>). <c>CASCADE_GLYPH_DUMP=1</c> remains an
/// alias for enabling the <see cref="DebugLogCategory.Glyph"/> category.
///
/// Callers must guard with <see cref="IsEnabled"/> so message interpolation
/// costs nothing when the category is off:
/// <code>
/// if (DebugLog.IsEnabled(DebugLogCategory.Capture))
/// {
///     DebugLog.Write(DebugLogCategory.Capture, $"...");
/// }
/// </code>
///
/// Each category file is hard-capped at 64 MB: when the cap is reached a
/// truncation notice is written and further output for that category is
/// dropped. Streams are opened lazily and kept open for the process lifetime.
/// </summary>
internal static class DebugLog
{
    private const long MaxBytesPerCategory = 64L * 1024 * 1024;
    private const int CategoryCount = 9;

    private static readonly bool[] enabledByCategory = ParseEnabledCategories();
    private static readonly StreamWriter?[] writers = new StreamWriter?[CategoryCount];
    private static readonly long[] bytesWritten = new long[CategoryCount];
    private static readonly bool[] capped = new bool[CategoryCount];
    private static readonly object writeLock = new();

    public static bool IsEnabled(DebugLogCategory category)
        => enabledByCategory[(int)category];

    public static void Write(DebugLogCategory category, string message)
    {
        int index = (int)category;
        if (!enabledByCategory[index] || capped[index])
        {
            return;
        }

        lock (writeLock)
        {
            if (capped[index])
            {
                return;
            }

            try
            {
                var writer = writers[index] ??= OpenWriter(category);
                if (writer is null)
                {
                    // Could not create the log directory/file — disable the
                    // category rather than retrying every call.
                    capped[index] = true;
                    return;
                }

                writer.WriteLine(message);
                // Count encoded bytes, not chars — the cap is on file size and
                // log text routinely contains multi-byte UTF-8 characters.
                bytesWritten[index] += System.Text.Encoding.UTF8.GetByteCount(message) + 2;

                if (bytesWritten[index] >= MaxBytesPerCategory)
                {
                    writer.WriteLine($"=== log capped at {MaxBytesPerCategory / (1024 * 1024)} MB — further output suppressed ===");
                    writer.Flush();
                    capped[index] = true;
                }
            }
            catch (IOException)
            {
                // A failing debug log must never take down rendering or mask
                // the error it was meant to expose. Disable the category.
                capped[index] = true;
            }
        }
    }

    private static StreamWriter? OpenWriter(DebugLogCategory category)
    {
        try
        {
            string dir = System.IO.Path.Combine(AppContext.BaseDirectory, "debug-logs");
            Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, CategoryFileName(category));
            var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            return new StreamWriter(stream) { AutoFlush = true };
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string CategoryFileName(DebugLogCategory category) => category switch
    {
        DebugLogCategory.Present => "present.log",
        DebugLogCategory.Transform => "transform.log",
        DebugLogCategory.Capture => "capture.log",
        DebugLogCategory.Layer => "layer.log",
        DebugLogCategory.Clip => "clip.log",
        DebugLogCategory.Frame => "frame.log",
        DebugLogCategory.Instance => "instance.log",
        DebugLogCategory.Glyph => "glyph.log",
        DebugLogCategory.Validation => "validation.log",
        _ => "other.log",
    };

    private static bool[] ParseEnabledCategories()
    {
        var enabled = new bool[CategoryCount];

        string? spec = Environment.GetEnvironmentVariable("CASCADE_DEBUG_LOG");
        if (!string.IsNullOrEmpty(spec))
        {
            foreach (var rawToken in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (rawToken.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    for (int i = 0; i < enabled.Length; i++)
                    {
                        enabled[i] = true;
                    }
                    break;
                }

                if (TryParseCategory(rawToken, out var category))
                {
                    enabled[(int)category] = true;
                }
            }
        }

        // Back-compat alias from the 2026-06-10 glyph investigation tooling.
        if (Environment.GetEnvironmentVariable("CASCADE_GLYPH_DUMP") == "1")
        {
            enabled[(int)DebugLogCategory.Glyph] = true;
        }

        return enabled;
    }

    private static bool TryParseCategory(string token, out DebugLogCategory category)
    {
        switch (token.ToUpperInvariant())
        {
            case "PRESENT": category = DebugLogCategory.Present; return true;
            case "TRANSFORM": category = DebugLogCategory.Transform; return true;
            case "CAPTURE": category = DebugLogCategory.Capture; return true;
            case "LAYER": category = DebugLogCategory.Layer; return true;
            case "CLIP": category = DebugLogCategory.Clip; return true;
            case "FRAME": category = DebugLogCategory.Frame; return true;
            case "INSTANCE": category = DebugLogCategory.Instance; return true;
            case "GLYPH": category = DebugLogCategory.Glyph; return true;
            case "VALIDATION": category = DebugLogCategory.Validation; return true;
            default: category = default; return false;
        }
    }
}

namespace Cascade.UI;

/// <summary>
/// Pure C# barcode encoder. Produces a bool[] of bar/space modules
/// for common 1D barcode symbologies.
/// </summary>
internal static class BarcodeEncoder
{
    /// <summary>
    /// Encodes content into a boolean array where true = bar (dark), false = space (light).
    /// </summary>
    internal static bool[] Encode(string content, BarcodeFormat? format)
    {
        var effectiveFormat = format ?? AutoDetectFormat(content);
        return effectiveFormat switch
        {
            BarcodeFormat.Code128 => EncodeCode128(content),
            BarcodeFormat.Code39 => EncodeCode39(content),
            BarcodeFormat.EAN13 => EncodeEAN13(content),
            BarcodeFormat.EAN8 => EncodeEAN8(content),
            BarcodeFormat.UPCA => EncodeUPCA(content),
            BarcodeFormat.ITF => EncodeITF(content),
            BarcodeFormat.Codabar => EncodeCodabar(content),
            _ => EncodeCode128(content),
        };
    }

    private static BarcodeFormat AutoDetectFormat(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return BarcodeFormat.Code128;
        }

        if (content.Length is 12 or 13 && IsAllDigits(content))
        {
            return BarcodeFormat.EAN13;
        }

        if (content.Length is 7 or 8 && IsAllDigits(content))
        {
            return BarcodeFormat.EAN8;
        }

        return BarcodeFormat.Code128;
    }

    private static bool IsAllDigits(string s)
    {
        foreach (char c in s)
        {
            if (c < '0' || c > '9')
            {
                return false;
            }
        }
        return true;
    }

    private static void AppendWidthPattern(List<bool> modules, byte[] widths)
    {
        bool isBar = true;
        foreach (byte w in widths)
        {
            for (int i = 0; i < w; i++)
            {
                modules.Add(isBar);
            }
            isBar = !isBar;
        }
    }

    // ── Code 128 ────────────────────────────────────────────────────────

    private static readonly byte[] Code128Stop = [2, 3, 3, 1, 1, 1, 2];

    private static bool[] EncodeCode128(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return [];
        }

        var values = new List<int>();
        values.Add(104); // Start Code B

        foreach (char c in content)
        {
            int val = c - 32;
            if (val < 0 || val > 94)
            {
                val = 0;
            }
            values.Add(val);
        }

        int checksum = values[0];
        for (int i = 1; i < values.Count; i++)
        {
            checksum += values[i] * i;
        }
        checksum %= 103;
        values.Add(checksum);

        var modules = new List<bool>();
        foreach (int val in values)
        {
            AppendWidthPattern(modules, GetCode128Widths(val));
        }
        AppendWidthPattern(modules, Code128Stop);

        return modules.ToArray();
    }

    private static byte[] GetCode128Widths(int value) => value switch
    {
        0 => [2,1,2,2,2,2],   1 => [2,2,2,1,2,2],   2 => [2,2,2,2,2,1],
        3 => [1,2,1,2,2,3],   4 => [1,2,1,3,2,2],   5 => [1,3,1,2,2,2],
        6 => [1,2,2,2,1,3],   7 => [1,2,2,3,1,2],   8 => [1,3,2,2,1,2],
        9 => [2,2,1,2,1,3],  10 => [2,2,1,3,1,2],  11 => [2,3,1,2,1,2],
       12 => [1,1,2,2,3,2],  13 => [1,2,2,1,3,2],  14 => [1,2,2,2,3,1],
       15 => [1,1,3,2,2,2],  16 => [1,2,3,1,2,2],  17 => [1,2,3,2,2,1],
       18 => [2,2,3,2,1,1],  19 => [2,2,1,1,3,2],  20 => [2,2,1,2,3,1],
       21 => [2,1,3,2,1,2],  22 => [2,2,3,1,1,2],  23 => [3,1,2,1,3,1],
       24 => [3,1,1,2,2,2],  25 => [3,2,1,1,2,2],  26 => [3,2,1,2,2,1],
       27 => [3,1,2,2,1,2],  28 => [3,2,2,1,1,2],  29 => [3,2,2,2,1,1],
       30 => [2,1,2,1,2,3],  31 => [2,1,2,3,2,1],  32 => [2,3,2,1,2,1],
       33 => [1,1,1,3,2,3],  34 => [1,3,1,1,2,3],  35 => [1,3,1,3,2,1],
       36 => [1,1,2,3,1,3],  37 => [1,3,2,1,1,3],  38 => [1,3,2,3,1,1],
       39 => [2,1,1,3,1,3],  40 => [2,3,1,1,1,3],  41 => [2,3,1,3,1,1],
       42 => [1,1,2,1,3,3],  43 => [1,1,2,3,3,1],  44 => [1,3,2,1,3,1],
       45 => [1,1,3,1,2,3],  46 => [1,1,3,3,2,1],  47 => [1,3,3,1,2,1],
       48 => [3,1,3,1,2,1],  49 => [2,1,1,3,3,1],  50 => [2,3,1,1,3,1],
       51 => [2,1,3,1,1,3],  52 => [2,1,3,3,1,1],  53 => [2,1,3,1,3,1],
       54 => [3,1,1,1,2,3],  55 => [3,1,1,3,2,1],  56 => [3,3,1,1,2,1],
       57 => [3,1,2,1,1,3],  58 => [3,1,2,3,1,1],  59 => [3,3,2,1,1,1],
       60 => [2,1,1,1,3,3],  61 => [2,1,1,3,1,3],  62 => [2,3,1,1,1,3],
       63 => [1,1,4,1,1,3],  64 => [1,1,4,3,1,1],  65 => [1,3,4,1,1,1],
       66 => [4,1,1,1,1,3],  67 => [4,1,1,3,1,1],  68 => [1,1,3,1,4,1],
       69 => [1,1,4,1,3,1],  70 => [3,1,1,1,4,1],  71 => [4,1,1,1,3,1],
       72 => [2,1,1,4,1,2],  73 => [2,1,1,2,1,4],  74 => [2,1,1,2,3,2],
       75 => [2,3,3,1,1,1],  76 => [4,3,1,1,1,2],  77 => [4,1,1,2,1,2],
       78 => [1,1,1,2,4,2],  79 => [1,2,1,1,4,2],  80 => [1,2,1,2,4,1],
       81 => [1,1,4,2,1,2],  82 => [1,2,4,1,1,2],  83 => [1,2,4,2,1,1],
       84 => [4,1,1,2,1,2],  85 => [4,2,1,1,1,2],  86 => [4,2,1,2,1,1],
       87 => [2,1,2,1,4,1],  88 => [2,1,4,1,2,1],  89 => [4,1,2,1,2,1],
       90 => [1,1,1,1,4,3],  91 => [1,1,1,3,4,1],  92 => [1,3,1,1,4,1],
       93 => [1,1,4,1,1,3],  94 => [1,1,4,3,1,1],  95 => [4,1,1,1,1,3],
       96 => [4,1,1,3,1,1],  97 => [1,1,3,1,4,1],  98 => [1,1,4,1,3,1],
       99 => [3,1,1,1,4,1], 100 => [4,1,1,1,3,1], 101 => [2,1,1,4,1,2],
      102 => [2,1,4,1,2,1], 103 => [2,1,4,3,1,1], 104 => [2,1,4,1,3,1],
        _ => [2,1,2,2,2,2],
    };

    // ── Code 39 ────────────────────────────────────────────────────────

    // Patterns: N = narrow (1), W = wide (3), alternating bar/space
    private static readonly Dictionary<char, string> Code39Patterns = new()
    {
        ['0'] = "NnNwWnWnN", ['1'] = "WnNwNnNnW", ['2'] = "NnWwNnNnW",
        ['3'] = "WnWwNnNnN", ['4'] = "NnNwWnNnW", ['5'] = "WnNwWnNnN",
        ['6'] = "NnWwWnNnN", ['7'] = "NnNwNnWnW", ['8'] = "WnNwNnWnN",
        ['9'] = "NnWwNnWnN", ['A'] = "WnNnNwNnW", ['B'] = "NnWnNwNnW",
        ['C'] = "WnWnNwNnN", ['D'] = "NnNnWwNnW", ['E'] = "WnNnWwNnN",
        ['F'] = "NnWnWwNnN", ['G'] = "NnNnNwWnW", ['H'] = "WnNnNwWnN",
        ['I'] = "NnWnNwWnN", ['J'] = "NnNnWwWnN", ['K'] = "WnNnNnNwW",
        ['L'] = "NnWnNnNwW", ['M'] = "WnWnNnNwN", ['N'] = "NnNnWnNwW",
        ['O'] = "WnNnWnNwN", ['P'] = "NnWnWnNwN", ['Q'] = "NnNnNnWwW",
        ['R'] = "WnNnNnWwN", ['S'] = "NnWnNnWwN", ['T'] = "NnNnWnWwN",
        ['U'] = "WwNnNnNnW", ['V'] = "NwWnNnNnW", ['W'] = "WwWnNnNnN",
        ['X'] = "NwNnWnNnW", ['Y'] = "WwNnWnNnN", ['Z'] = "NwWnWnNnN",
        ['-'] = "NwNnNnWnW", ['.'] = "WwNnNnWnN", [' '] = "NwWnNnWnN",
        ['$'] = "NwNwNwNnN", ['/'] = "NwNwNnNwN", ['+'] = "NwNnNwNwN",
        ['%'] = "NnNwNwNwN", ['*'] = "NwNnWnWnN",
    };

    private static bool[] EncodeCode39(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return [];
        }

        var upper = content.ToUpperInvariant();
        var modules = new List<bool>();

        AppendCode39Char(modules, '*');

        foreach (char c in upper)
        {
            modules.Add(false); // Inter-character gap
            AppendCode39Char(modules, c);
        }

        modules.Add(false); // Inter-character gap
        AppendCode39Char(modules, '*');

        return modules.ToArray();
    }

    private static void AppendCode39Char(List<bool> modules, char c)
    {
        if (!Code39Patterns.TryGetValue(c, out string? pattern))
        {
            pattern = Code39Patterns[' '];
        }

        for (int i = 0; i < pattern.Length; i++)
        {
            bool isBar = (i % 2 == 0);
            int width = char.ToUpperInvariant(pattern[i]) == 'W' ? 3 : 1;
            for (int w = 0; w < width; w++)
            {
                modules.Add(isBar);
            }
        }
    }

    // ── EAN-13 ──────────────────────────────────────────────────────────

    private static readonly byte[][] EanLPatterns =
    {
        [0,0,0,1,1,0,1], [0,0,1,1,0,0,1], [0,0,1,0,0,1,1],
        [0,1,1,1,1,0,1], [0,1,0,0,0,1,1], [0,1,1,0,0,0,1],
        [0,1,0,1,1,1,1], [0,1,1,1,0,1,1], [0,1,1,0,1,1,1],
        [0,0,0,1,0,1,1],
    };

    private static readonly byte[][] EanGPatterns =
    {
        [0,1,0,0,1,1,1], [0,1,1,0,0,1,1], [0,0,1,1,0,1,1],
        [0,1,0,0,0,0,1], [0,0,1,1,1,0,1], [0,1,1,1,0,0,1],
        [0,0,0,0,1,0,1], [0,0,1,0,0,0,1], [0,0,0,1,0,0,1],
        [0,0,1,0,1,1,1],
    };

    private static readonly byte[][] EanRPatterns =
    {
        [1,1,1,0,0,1,0], [1,1,0,0,1,1,0], [1,1,0,1,1,0,0],
        [1,0,0,0,0,1,0], [1,0,1,1,1,0,0], [1,0,0,1,1,1,0],
        [1,0,1,0,0,0,0], [1,0,0,0,1,0,0], [1,0,0,1,0,0,0],
        [1,1,1,0,1,0,0],
    };

    // Parity pattern for left half based on first digit (L=0, G=1)
    private static readonly byte[][] EanFirstDigitPatterns =
    {
        [0,0,0,0,0,0], [0,0,1,0,1,1], [0,0,1,1,0,1],
        [0,0,1,1,1,0], [0,1,0,0,1,1], [0,1,1,0,0,1],
        [0,1,1,1,0,0], [0,1,0,1,0,1], [0,1,0,1,1,0],
        [0,1,1,0,1,0],
    };

    private static bool[] EncodeEAN13(string content)
    {
        if (content.Length < 12)
        {
            return [];
        }

        var digits = new int[13];
        for (int i = 0; i < Math.Min(content.Length, 13); i++)
        {
            digits[i] = content[i] - '0';
        }

        if (content.Length == 12)
        {
            digits[12] = ComputeEanCheckDigit(digits, 12);
        }

        var modules = new List<bool>();

        // Start guard: 101
        modules.Add(true); modules.Add(false); modules.Add(true);

        // Left half (digits[1] through digits[6])
        var parityPattern = EanFirstDigitPatterns[digits[0]];
        for (int i = 1; i <= 6; i++)
        {
            byte[][] patterns = parityPattern[i - 1] == 0 ? EanLPatterns : EanGPatterns;
            foreach (byte b in patterns[digits[i]])
            {
                modules.Add(b == 1);
            }
        }

        // Center guard: 01010
        modules.Add(false); modules.Add(true); modules.Add(false);
        modules.Add(true); modules.Add(false);

        // Right half (digits[7] through digits[12])
        for (int i = 7; i <= 12; i++)
        {
            foreach (byte b in EanRPatterns[digits[i]])
            {
                modules.Add(b == 1);
            }
        }

        // End guard: 101
        modules.Add(true); modules.Add(false); modules.Add(true);

        return modules.ToArray();
    }

    private static int ComputeEanCheckDigit(int[] digits, int count)
    {
        int sum = 0;
        for (int i = 0; i < count; i++)
        {
            sum += digits[i] * (i % 2 == 0 ? 1 : 3);
        }
        return (10 - (sum % 10)) % 10;
    }

    // ── EAN-8 ───────────────────────────────────────────────────────────

    private static bool[] EncodeEAN8(string content)
    {
        if (content.Length < 7)
        {
            return [];
        }

        var digits = new int[8];
        for (int i = 0; i < Math.Min(content.Length, 8); i++)
        {
            digits[i] = content[i] - '0';
        }

        if (content.Length == 7)
        {
            digits[7] = ComputeEanCheckDigit(digits, 7);
        }

        var modules = new List<bool>();

        // Start guard
        modules.Add(true); modules.Add(false); modules.Add(true);

        // Left half — all L-code
        for (int i = 0; i < 4; i++)
        {
            foreach (byte b in EanLPatterns[digits[i]])
            {
                modules.Add(b == 1);
            }
        }

        // Center guard
        modules.Add(false); modules.Add(true); modules.Add(false);
        modules.Add(true); modules.Add(false);

        // Right half — all R-code
        for (int i = 4; i < 8; i++)
        {
            foreach (byte b in EanRPatterns[digits[i]])
            {
                modules.Add(b == 1);
            }
        }

        // End guard
        modules.Add(true); modules.Add(false); modules.Add(true);

        return modules.ToArray();
    }

    // ── UPC-A ───────────────────────────────────────────────────────────

    private static bool[] EncodeUPCA(string content)
    {
        string ean13Content = "0" + content;
        if (ean13Content.Length < 12)
        {
            return [];
        }
        return EncodeEAN13(ean13Content);
    }

    // ── ITF (Interleaved 2-of-5) ────────────────────────────────────────

    // Each digit encoded as 5 elements: 0 = narrow, 1 = wide
    private static readonly byte[][] ItfPatterns =
    {
        [0,0,1,1,0], [1,0,0,0,1], [0,1,0,0,1],
        [1,1,0,0,0], [0,0,1,0,1], [1,0,1,0,0],
        [0,1,1,0,0], [0,0,0,1,1], [1,0,0,1,0],
        [0,1,0,1,0],
    };

    private static bool[] EncodeITF(string content)
    {
        string data = content;
        if (data.Length % 2 != 0)
        {
            data = "0" + data;
        }

        if (data.Length < 2)
        {
            return [];
        }

        var modules = new List<bool>();

        // Start pattern: narrow bar, narrow space, narrow bar, narrow space
        modules.Add(true); modules.Add(false); modules.Add(true); modules.Add(false);

        for (int i = 0; i < data.Length; i += 2)
        {
            int d1 = data[i] - '0';
            int d2 = data[i + 1] - '0';
            var barsPattern = ItfPatterns[d1];
            var spacesPattern = ItfPatterns[d2];

            for (int j = 0; j < 5; j++)
            {
                int barWidth = barsPattern[j] == 1 ? 3 : 1;
                int spaceWidth = spacesPattern[j] == 1 ? 3 : 1;
                for (int w = 0; w < barWidth; w++) { modules.Add(true); }
                for (int w = 0; w < spaceWidth; w++) { modules.Add(false); }
            }
        }

        // Stop pattern: wide bar, narrow space, narrow bar
        for (int w = 0; w < 3; w++) { modules.Add(true); }
        modules.Add(false);
        modules.Add(true);

        return modules.ToArray();
    }

    // ── Codabar ─────────────────────────────────────────────────────────

    private static readonly Dictionary<char, string> CodabarPatterns = new()
    {
        ['0'] = "NnNnNwW", ['1'] = "NnNnWwN", ['2'] = "NnNwNnW",
        ['3'] = "WwNnNnN", ['4'] = "NnWnNwN", ['5'] = "WnNnNwN",
        ['6'] = "NwNnNnW", ['7'] = "NwNnWnN", ['8'] = "NwWnNnN",
        ['9'] = "WnNnWnN", ['-'] = "NnNwWnN", ['$'] = "NnWwNnN",
        [':'] = "WnNwNnW", ['/'] = "WnWnNnW", ['.'] = "WnWnWnN",
        ['+'] = "NnWnWnW",
        ['A'] = "NnWwNwN", ['B'] = "NwNnNwW",
        ['C'] = "NnNnWwW", ['D'] = "NnNwWwN",
    };

    private static bool[] EncodeCodabar(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return [];
        }

        var upper = content.ToUpperInvariant();
        var modules = new List<bool>();

        char first = upper[0];
        char last = upper[^1];
        bool hasStartStop = (first is 'A' or 'B' or 'C' or 'D') &&
                           (last is 'A' or 'B' or 'C' or 'D');

        string data = hasStartStop ? upper : "A" + upper + "B";

        for (int i = 0; i < data.Length; i++)
        {
            if (i > 0)
            {
                modules.Add(false); // Inter-character gap
            }
            AppendCodabarChar(modules, data[i]);
        }

        return modules.ToArray();
    }

    private static void AppendCodabarChar(List<bool> modules, char c)
    {
        if (!CodabarPatterns.TryGetValue(c, out string? pattern))
        {
            pattern = CodabarPatterns['0'];
        }

        for (int i = 0; i < pattern.Length; i++)
        {
            bool isBar = (i % 2 == 0);
            int width = char.ToUpperInvariant(pattern[i]) == 'W' ? 3 : 1;
            for (int w = 0; w < width; w++)
            {
                modules.Add(isBar);
            }
        }
    }
}

using System.Globalization;

namespace Cascade.UI;

/// <summary>
/// Minimal Unicode character classification tables for word boundary detection
/// and text segmentation. Uses <see cref="CharUnicodeInfo"/> as the backing
/// data source, with targeted refinements for boundary detection.
/// </summary>
internal static class UnicodeData
{
    /// <summary>
    /// Word boundary categories loosely based on Unicode UAX #29, simplified
    /// for practical text editing use.
    /// </summary>
    internal enum WordCategory
    {
        Other,
        ALetter,
        Numeric,
        MidLetter,
        MidNum,
        MidNumLet,
        Whitespace,
        Newline,
        ExtendFormat,
        Katakana,
        CJK,
        Emoji,
    }

    // ── Code point reading helpers ──────────────────────────────────────

    /// <summary>
    /// Returns the Unicode code point at the given index in a string,
    /// handling surrogate pairs.
    /// </summary>
    internal static int GetCodePoint(string text, int index)
    {
        if ((uint)index >= (uint)text.Length)
        {
            return -1;
        }

        char ch = text[index];
        if (char.IsHighSurrogate(ch) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            return char.ConvertToUtf32(ch, text[index + 1]);
        }

        return ch;
    }

    /// <summary>
    /// Returns the number of UTF-16 code units for the code point starting at index.
    /// </summary>
    internal static int CodePointCharCount(string text, int index)
    {
        if ((uint)index >= (uint)text.Length)
        {
            return 0;
        }

        if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            return 2;
        }

        return 1;
    }

    /// <summary>
    /// Returns the number of UTF-16 code units for the code point ending just
    /// before <paramref name="index"/> (used for backward iteration).
    /// </summary>
    internal static int CodePointCharCountBefore(string text, int index)
    {
        if (index <= 0 || index > text.Length)
        {
            return 0;
        }

        if (char.IsLowSurrogate(text[index - 1]) && index >= 2 && char.IsHighSurrogate(text[index - 2]))
        {
            return 2;
        }

        return 1;
    }

    /// <summary>
    /// Reads a code point forward from a <see cref="TextDocument"/> without
    /// materializing the entire text.
    /// </summary>
    internal static (int CodePoint, int CharCount) ReadForward(TextDocument doc, int index)
    {
        if (index >= doc.Length)
        {
            return (-1, 0);
        }

        char ch = doc[index];
        if (char.IsHighSurrogate(ch) && index + 1 < doc.Length && char.IsLowSurrogate(doc[index + 1]))
        {
            return (char.ConvertToUtf32(ch, doc[index + 1]), 2);
        }

        return (ch, 1);
    }

    /// <summary>
    /// Reads a code point backward from a <see cref="TextDocument"/> without
    /// materializing the entire text.
    /// </summary>
    internal static (int CodePoint, int CharCount) ReadBackward(TextDocument doc, int index)
    {
        if (index <= 0)
        {
            return (-1, 0);
        }

        char ch = doc[index - 1];
        if (char.IsLowSurrogate(ch) && index >= 2 && char.IsHighSurrogate(doc[index - 2]))
        {
            return (char.ConvertToUtf32(doc[index - 2], ch), 2);
        }

        return (ch, 1);
    }

    // ── Classification ──────────────────────────────────────────────────

    /// <summary>Classifies a code point into a word boundary category.</summary>
    internal static WordCategory Classify(int codePoint)
    {
        if (codePoint < 0)
        {
            return WordCategory.Other;
        }

        if (codePoint < 128)
        {
            return ClassifyAscii(codePoint);
        }

        // Newline separators
        if (codePoint is 0x0085 or 0x2028 or 0x2029)
        {
            return WordCategory.Newline;
        }

        // Katakana block
        if (codePoint >= 0x30A0 && codePoint <= 0x30FF)
        {
            return WordCategory.Katakana;
        }

        // Half-width Katakana
        if (codePoint is 0xFF65 || (codePoint >= 0xFF66 && codePoint <= 0xFF9F))
        {
            return WordCategory.Katakana;
        }

        // CJK Ideographs
        if (IsCjk(codePoint))
        {
            return WordCategory.CJK;
        }

        // Emoji
        if (IsEmoji(codePoint))
        {
            return WordCategory.Emoji;
        }

        // BMP character — use CharUnicodeInfo
        if (codePoint <= 0xFFFF)
        {
            return ClassifyBmp((char)codePoint);
        }

        // Supplementary characters
        var category = CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(codePoint), 0);
        return MapUnicodeCategory(category);
    }

    // ── Private helpers ─────────────────────────────────────────────────

    static WordCategory ClassifyAscii(int ch)
    {
        if (ch >= 'A' && ch <= 'Z')
        {
            return WordCategory.ALetter;
        }

        if (ch >= 'a' && ch <= 'z')
        {
            return WordCategory.ALetter;
        }

        if (ch >= '0' && ch <= '9')
        {
            return WordCategory.Numeric;
        }

        if (ch is '\r' or '\n' or '\x0B' or '\x0C')
        {
            return WordCategory.Newline;
        }

        if (ch is ' ' or '\t')
        {
            return WordCategory.Whitespace;
        }

        if (ch == '_')
        {
            return WordCategory.ALetter;
        }

        if (ch == '\'')
        {
            return WordCategory.MidNumLet;
        }

        if (ch == '.')
        {
            return WordCategory.MidNumLet;
        }

        if (ch == ':')
        {
            return WordCategory.MidLetter;
        }

        if (ch == ',')
        {
            return WordCategory.MidNum;
        }

        return WordCategory.Other;
    }

    static WordCategory ClassifyBmp(char ch)
    {
        if (char.IsWhiteSpace(ch))
        {
            return WordCategory.Whitespace;
        }

        // Mid-letter punctuation (UAX #29 MidLetter)
        if (ch is '\u00B7' or '\u0387' or '\u05F4' or '\u2027')
        {
            return WordCategory.MidLetter;
        }

        // Mid-number punctuation
        if (ch is '\u066C' or '\uFE50' or '\uFE54' or '\uFF0C' or '\uFF1B')
        {
            return WordCategory.MidNum;
        }

        // MidNumLet: quotation marks and similar
        if (ch is '\u2018' or '\u2019' or '\u2024' or '\uFE52' or '\uFF07' or '\uFF0E')
        {
            return WordCategory.MidNumLet;
        }

        var category = CharUnicodeInfo.GetUnicodeCategory(ch);
        return MapUnicodeCategory(category);
    }

    static WordCategory MapUnicodeCategory(UnicodeCategory category)
    {
        return category switch
        {
            UnicodeCategory.UppercaseLetter     => WordCategory.ALetter,
            UnicodeCategory.LowercaseLetter     => WordCategory.ALetter,
            UnicodeCategory.TitlecaseLetter     => WordCategory.ALetter,
            UnicodeCategory.ModifierLetter      => WordCategory.ALetter,
            UnicodeCategory.OtherLetter         => WordCategory.ALetter,
            UnicodeCategory.LetterNumber        => WordCategory.ALetter,
            UnicodeCategory.DecimalDigitNumber  => WordCategory.Numeric,
            UnicodeCategory.OtherNumber         => WordCategory.Numeric,
            UnicodeCategory.NonSpacingMark      => WordCategory.ExtendFormat,
            UnicodeCategory.SpacingCombiningMark => WordCategory.ExtendFormat,
            UnicodeCategory.EnclosingMark       => WordCategory.ExtendFormat,
            UnicodeCategory.Format              => WordCategory.ExtendFormat,
            UnicodeCategory.SpaceSeparator      => WordCategory.Whitespace,
            UnicodeCategory.LineSeparator       => WordCategory.Newline,
            UnicodeCategory.ParagraphSeparator  => WordCategory.Newline,
            _                                   => WordCategory.Other,
        };
    }

    static bool IsCjk(int cp)
    {
        if (cp >= 0x4E00 && cp <= 0x9FFF) { return true; }
        if (cp >= 0x3400 && cp <= 0x4DBF) { return true; }
        if (cp >= 0x20000 && cp <= 0x2A6DF) { return true; }
        if (cp >= 0xF900 && cp <= 0xFAFF) { return true; }
        if (cp >= 0x2E80 && cp <= 0x2EFF) { return true; }
        if (cp >= 0x2F00 && cp <= 0x2FDF) { return true; }
        if (cp >= 0x3040 && cp <= 0x309F) { return true; }
        if (cp >= 0x3100 && cp <= 0x312F) { return true; }
        if (cp >= 0xAC00 && cp <= 0xD7AF) { return true; }
        return false;
    }

    static bool IsEmoji(int cp)
    {
        if (cp >= 0x1F600 && cp <= 0x1F64F) { return true; }
        if (cp >= 0x1F300 && cp <= 0x1F5FF) { return true; }
        if (cp >= 0x1F680 && cp <= 0x1F6FF) { return true; }
        if (cp >= 0x1F900 && cp <= 0x1F9FF) { return true; }
        if (cp >= 0x1FA00 && cp <= 0x1FA6F) { return true; }
        if (cp >= 0x1FA70 && cp <= 0x1FAFF) { return true; }
        if (cp >= 0x2600 && cp <= 0x26FF) { return true; }
        if (cp >= 0x2700 && cp <= 0x27BF) { return true; }
        if (cp >= 0xFE00 && cp <= 0xFE0F) { return true; }
        if (cp == 0x200D) { return true; }
        if (cp >= 0x1F1E0 && cp <= 0x1F1FF) { return true; }
        return false;
    }
}

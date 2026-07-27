namespace Cascade.UI;

/// <summary>
/// Finds valid line break opportunities in text following the core rules of
/// Unicode Line Break Algorithm (UAX #14). Handles spaces, hyphens,
/// CJK ideographs, non-breaking spaces, zero-width spaces, and soft hyphens.
/// </summary>
internal static class LineBreaker
{
    /// <summary>
    /// Returns an ordered list of positions where the text may (or must) break
    /// across lines. Each position is the character index where a new line starts.
    /// </summary>
    internal static List<LineBreakOpportunity> FindBreakOpportunities(ReadOnlySpan<char> text)
    {
        var breaks = new List<LineBreakOpportunity>();
        if (text.Length == 0)
        {
            return breaks;
        }

        int i = 0;
        while (i < text.Length)
        {
            char ch = text[i];

            // Mandatory break: LF
            if (ch == '\n')
            {
                breaks.Add(new LineBreakOpportunity(i + 1, true));
                i++;
                continue;
            }

            // Mandatory break: CR or CR+LF
            if (ch == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    breaks.Add(new LineBreakOpportunity(i + 2, true));
                    i += 2;
                }
                else
                {
                    breaks.Add(new LineBreakOpportunity(i + 1, true));
                    i++;
                }
                continue;
            }

            // Check for optional break AFTER this character
            if (i + 1 < text.Length)
            {
                var before = Classify(ch);
                var after = Classify(text[i + 1]);

                if (CanBreakBetween(before, after))
                {
                    breaks.Add(new LineBreakOpportunity(i + 1, false));
                }
            }

            i++;
        }

        return breaks;
    }

    static bool CanBreakBetween(BreakClass before, BreakClass after)
    {
        // LB6: × BK — never break before hard break (handled in main loop)
        // LB7: × SP — never break before a space
        if (after == BreakClass.SP)
        {
            return false;
        }

        // LB8: ZW ÷ — always break after zero-width space
        if (before == BreakClass.ZW)
        {
            return true;
        }

        // LB11: × WJ, WJ × — never break around word joiner
        if (before == BreakClass.WJ || after == BreakClass.WJ)
        {
            return false;
        }

        // LB12: GL × — never break after non-breaking (glue)
        if (before == BreakClass.GL)
        {
            return false;
        }

        // LB12a: × GL — never break before non-breaking
        if (after == BreakClass.GL)
        {
            return false;
        }

        // LB13: × CL, × EX, × IS, × SY — never break before close/excl/infix/symbol
        if (after == BreakClass.CL || after == BreakClass.EX ||
            after == BreakClass.IS || after == BreakClass.SY)
        {
            return false;
        }

        // LB14: OP × — never break after opening punctuation
        if (before == BreakClass.OP)
        {
            return false;
        }

        // LB18: SP ÷ — break after space
        if (before == BreakClass.SP)
        {
            return true;
        }

        // LB20: ÷ CB, CB ÷ — break before/after contingent break
        if (before == BreakClass.CB || after == BreakClass.CB)
        {
            return true;
        }

        // LB21: × BA, × HY — do not break before break-after or hyphen
        if (after == BreakClass.BA || after == BreakClass.HY)
        {
            return false;
        }

        // LB21a: After HY or BA, allow break
        if (before == BreakClass.HY || before == BreakClass.BA)
        {
            return true;
        }

        // LB23: AL × NU, NU × AL — don't break between letters and numbers
        if ((before == BreakClass.AL && after == BreakClass.NU) ||
            (before == BreakClass.NU && after == BreakClass.AL))
        {
            return false;
        }

        // LB25: Don't break in number sequences
        if (before == BreakClass.NU && after == BreakClass.NU)
        {
            return false;
        }

        // LB28: AL × AL — don't break between alphabetics
        if (before == BreakClass.AL && after == BreakClass.AL)
        {
            return false;
        }

        // LB29: IS × AL — don't break between infix separator and alphabetic
        if (before == BreakClass.IS && after == BreakClass.AL)
        {
            return false;
        }

        // LB30: (AL | NU) × OP — don't break before opening after alphanum
        if ((before == BreakClass.AL || before == BreakClass.NU) && after == BreakClass.OP)
        {
            return false;
        }

        // LB30: CL × (AL | NU) — don't break after closing before alphanum
        if (before == BreakClass.CL && (after == BreakClass.AL || after == BreakClass.NU))
        {
            return false;
        }

        // CJK ideographs: break before and after
        if (before == BreakClass.ID || after == BreakClass.ID)
        {
            return true;
        }

        // LB31: ALL ÷ ALL — default: allow break
        return true;
    }

    static BreakClass Classify(char ch)
    {
        switch (ch)
        {
            case '\r' or '\n':
                return BreakClass.BK;
            case ' ':
                return BreakClass.SP;
            case '\t':
                return BreakClass.BA;
            case '\u00A0': // non-breaking space
                return BreakClass.GL;
            case '\u200B': // zero-width space
                return BreakClass.ZW;
            case '\u2060': // word joiner
            case '\uFEFF': // zero-width no-break space (BOM)
                return BreakClass.WJ;
            case '\u00AD': // soft hyphen
                return BreakClass.BA;
            case '-':
                return BreakClass.HY;
            case '(' or '[' or '{' or '\u00AB' or '\u2018' or '\u201C':
                return BreakClass.OP;
            case ')' or ']' or '}' or '\u00BB' or '\u2019' or '\u201D':
                return BreakClass.CL;
            case '!' or '?' or '\uFF01' or '\uFF1F':
                return BreakClass.EX;
            case '.' or ',' or ':' or ';':
                return BreakClass.IS;
            case '/':
                return BreakClass.SY;
        }

        // Unicode space characters
        if (ch >= '\u2000' && ch <= '\u200A')
        {
            return BreakClass.SP;
        }
        if (ch == '\u205F' || ch == '\u3000')
        {
            return BreakClass.SP;
        }

        // CJK Ideographic characters
        if (IsCjkIdeograph(ch))
        {
            return BreakClass.ID;
        }

        // Digits
        if (ch >= '0' && ch <= '9')
        {
            return BreakClass.NU;
        }

        // Default: alphabetic for letters, alphabetic for everything else
        return BreakClass.AL;
    }

    static bool IsCjkIdeograph(char ch)
    {
        // CJK Unified Ideographs and related blocks
        if (ch >= '\u2E80' && ch <= '\u2EFF') { return true; } // CJK Radicals Supplement
        if (ch >= '\u2F00' && ch <= '\u2FDF') { return true; } // Kangxi Radicals
        if (ch >= '\u3040' && ch <= '\u309F') { return true; } // Hiragana
        if (ch >= '\u30A0' && ch <= '\u30FF') { return true; } // Katakana
        if (ch >= '\u3100' && ch <= '\u312F') { return true; } // Bopomofo
        if (ch >= '\u3400' && ch <= '\u4DBF') { return true; } // CJK Extension A
        if (ch >= '\u4E00' && ch <= '\u9FFF') { return true; } // CJK Unified Ideographs
        if (ch >= '\uF900' && ch <= '\uFAFF') { return true; } // CJK Compatibility
        return false;
    }
}

/// <summary>
/// A position in text where a line break may occur.
/// </summary>
internal readonly record struct LineBreakOpportunity(
    /// <summary>
    /// Character index where the new line would start.
    /// Text before this index stays on the current line.
    /// </summary>
    int Position,
    /// <summary>True for hard line breaks (newline characters).</summary>
    bool IsMandatory
);

/// <summary>
/// Simplified Unicode line break classes (UAX #14).
/// </summary>
internal enum BreakClass
{
    BK,  // Mandatory break
    SP,  // Space
    ZW,  // Zero width space
    WJ,  // Word joiner
    GL,  // Non-breaking (glue)
    BA,  // Break after
    HY,  // Hyphen
    OP,  // Opening punctuation
    CL,  // Closing punctuation
    EX,  // Exclamation/question
    IS,  // Infix separator
    SY,  // Symbol (slash)
    CB,  // Contingent break
    NU,  // Numeric
    AL,  // Alphabetic (default)
    ID,  // Ideographic (CJK)
}

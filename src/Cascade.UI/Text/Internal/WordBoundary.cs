namespace Cascade.UI;

/// <summary>
/// Unicode UAX #29 word boundary detection. Identifies word boundaries for
/// Ctrl+Left/Right navigation, double-click word selection, and
/// Ctrl+Backspace/Delete word deletion.
/// </summary>
internal static class WordBoundary
{
    /// <summary>
    /// Finds the start of the word before or at <paramref name="offset"/>.
    /// Used for Ctrl+Left / Option+Left navigation.
    /// </summary>
    internal static int FindWordStart(TextDocument doc, int offset)
    {
        if (offset <= 0)
        {
            return 0;
        }

        int length = doc.Length;
        if (offset > length)
        {
            offset = length;
        }

        int pos = offset;

        // Step 1: skip backward past whitespace
        while (pos > 0)
        {
            var (cp, cpLen) = UnicodeData.ReadBackward(doc, pos);
            var cat = UnicodeData.Classify(cp);
            if (cat != UnicodeData.WordCategory.Whitespace && cat != UnicodeData.WordCategory.Newline)
            {
                break;
            }
            pos -= cpLen;
        }

        if (pos == 0)
        {
            return 0;
        }

        // Step 2: determine category of the character we landed on
        var (landCp, landCpLen) = UnicodeData.ReadBackward(doc, pos);
        var landCat = UnicodeData.Classify(landCp);

        // CJK and emoji: each character is its own word
        if (landCat == UnicodeData.WordCategory.CJK || landCat == UnicodeData.WordCategory.Emoji)
        {
            return pos - landCpLen;
        }

        // Step 3: move backward through same-group characters
        while (pos > 0)
        {
            var (cp, cpLen) = UnicodeData.ReadBackward(doc, pos);
            var cat = UnicodeData.Classify(cp);

            if (!IsSameWordGroup(cat, landCat))
            {
                break;
            }
            pos -= cpLen;
        }

        return pos;
    }

    /// <summary>
    /// Finds the end of the word at or after <paramref name="offset"/>.
    /// Used for Ctrl+Right / Option+Right navigation.
    /// </summary>
    internal static int FindWordEnd(TextDocument doc, int offset)
    {
        int length = doc.Length;
        if (offset >= length)
        {
            return length;
        }

        if (offset < 0)
        {
            offset = 0;
        }

        int pos = offset;

        // Step 1: skip forward past whitespace
        while (pos < length)
        {
            var (cp, cpLen) = UnicodeData.ReadForward(doc, pos);
            var cat = UnicodeData.Classify(cp);
            if (cat != UnicodeData.WordCategory.Whitespace && cat != UnicodeData.WordCategory.Newline)
            {
                break;
            }
            pos += cpLen;
        }

        if (pos >= length)
        {
            return length;
        }

        // Step 2: determine category of the character we landed on
        var (startCp, _) = UnicodeData.ReadForward(doc, pos);
        var startCat = UnicodeData.Classify(startCp);

        // CJK and emoji: each character is its own word
        if (startCat == UnicodeData.WordCategory.CJK || startCat == UnicodeData.WordCategory.Emoji)
        {
            return pos + UnicodeData.ReadForward(doc, pos).CharCount;
        }

        // Step 3: move forward through same-group characters
        while (pos < length)
        {
            var (cp, cpLen) = UnicodeData.ReadForward(doc, pos);
            var cat = UnicodeData.Classify(cp);

            if (!IsSameWordGroup(cat, startCat))
            {
                break;
            }
            pos += cpLen;
        }

        return pos;
    }

    /// <summary>
    /// Returns the word range (start, end) at the given position.
    /// Used for double-click word selection.
    /// </summary>
    internal static (int Start, int End) WordAt(TextDocument doc, int offset)
    {
        int length = doc.Length;
        if (length == 0)
        {
            return (0, 0);
        }

        if (offset < 0)
        {
            offset = 0;
        }

        if (offset >= length)
        {
            offset = length - 1;
        }

        // Determine the category at the offset
        var (cp, _) = UnicodeData.ReadForward(doc, offset);
        var cat = UnicodeData.Classify(cp);

        // For whitespace or newline, select the whitespace run
        if (cat == UnicodeData.WordCategory.Whitespace || cat == UnicodeData.WordCategory.Newline)
        {
            int start = offset;
            while (start > 0)
            {
                var (prevCp, prevLen) = UnicodeData.ReadBackward(doc, start);
                var prevCat = UnicodeData.Classify(prevCp);
                if (prevCat != cat)
                {
                    break;
                }
                start -= prevLen;
            }

            int end = offset;
            while (end < length)
            {
                var (nextCp, nextLen) = UnicodeData.ReadForward(doc, end);
                var nextCat = UnicodeData.Classify(nextCp);
                if (nextCat != cat)
                {
                    break;
                }
                end += nextLen;
            }

            return (start, end);
        }

        // CJK or emoji: single character is the word
        if (cat == UnicodeData.WordCategory.CJK || cat == UnicodeData.WordCategory.Emoji)
        {
            var (_, charCount) = UnicodeData.ReadForward(doc, offset);
            return (offset, offset + charCount);
        }

        // General case: expand in both directions through same-group characters
        int wordStart = offset;
        while (wordStart > 0)
        {
            var (prevCp, prevLen) = UnicodeData.ReadBackward(doc, wordStart);
            var prevCat = UnicodeData.Classify(prevCp);
            if (!IsSameWordGroup(prevCat, cat))
            {
                break;
            }
            wordStart -= prevLen;
        }

        int wordEnd = offset;
        while (wordEnd < length)
        {
            var (nextCp, nextLen) = UnicodeData.ReadForward(doc, wordEnd);
            var nextCat = UnicodeData.Classify(nextCp);
            if (!IsSameWordGroup(nextCat, cat))
            {
                break;
            }
            wordEnd += nextLen;
        }

        return (wordStart, wordEnd);
    }

    /// <summary>
    /// Finds the start of the paragraph containing <paramref name="offset"/>.
    /// </summary>
    internal static int FindParagraphStart(TextDocument doc, int offset)
    {
        if (offset <= 0)
        {
            return 0;
        }

        int length = doc.Length;
        if (offset > length)
        {
            offset = length;
        }

        int pos = offset;

        // If we're immediately after a newline, step back past it first
        if (pos > 0 && doc[pos - 1] == '\n')
        {
            pos--;
            if (pos > 0 && doc[pos - 1] == '\r')
            {
                pos--;
            }
        }
        else if (pos > 0 && doc[pos - 1] == '\r')
        {
            pos--;
        }

        // Scan backward for a newline
        while (pos > 0)
        {
            char ch = doc[pos - 1];
            if (ch == '\n' || ch == '\r')
            {
                return pos;
            }
            pos--;
        }

        return 0;
    }

    /// <summary>
    /// Finds the end of the paragraph containing <paramref name="offset"/>.
    /// The returned offset is after the line terminator if one exists.
    /// </summary>
    internal static int FindParagraphEnd(TextDocument doc, int offset)
    {
        int length = doc.Length;
        if (offset >= length)
        {
            return length;
        }

        if (offset < 0)
        {
            offset = 0;
        }

        int pos = offset;
        while (pos < length)
        {
            char ch = doc[pos];
            if (ch == '\n')
            {
                return pos + 1;
            }

            if (ch == '\r')
            {
                if (pos + 1 < length && doc[pos + 1] == '\n')
                {
                    return pos + 2;
                }
                return pos + 1;
            }
            pos++;
        }

        return length;
    }

    // ── Private helpers ─────────────────────────────────────────────────

    static bool IsSameWordGroup(UnicodeData.WordCategory cat, UnicodeData.WordCategory baseCat)
    {
        // Extending/format characters always continue the current group
        if (cat == UnicodeData.WordCategory.ExtendFormat)
        {
            return true;
        }

        // Exact match
        if (cat == baseCat)
        {
            return true;
        }

        // Letters and numbers group together (for identifiers like "var1")
        if (IsAlphanumeric(cat) && IsAlphanumeric(baseCat))
        {
            return true;
        }

        // MidLetter within a letter group
        if (cat == UnicodeData.WordCategory.MidLetter && baseCat == UnicodeData.WordCategory.ALetter)
        {
            return true;
        }

        // MidNumLet within letter or numeric groups
        if (cat == UnicodeData.WordCategory.MidNumLet && IsAlphanumeric(baseCat))
        {
            return true;
        }

        // MidNum within numeric groups
        if (cat == UnicodeData.WordCategory.MidNum && baseCat == UnicodeData.WordCategory.Numeric)
        {
            return true;
        }

        return false;
    }

    static bool IsAlphanumeric(UnicodeData.WordCategory cat)
    {
        return cat == UnicodeData.WordCategory.ALetter || cat == UnicodeData.WordCategory.Numeric;
    }
}

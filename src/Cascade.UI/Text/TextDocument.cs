namespace Cascade.UI;

/// <summary>
/// The backing store for editable text. Uses a piece table internally for
/// O(log n) edits that scale gracefully to large documents. For short text
/// (below a configurable threshold), a plain string is used instead.
/// </summary>
public sealed class TextDocument
{
    const int PieceTableThreshold = 4096;

    // Exactly one of these is non-null at any time.
    string? stringBacking;
    PieceTable? pieceTable;

    // Lazily rebuilt line-start offsets (null = dirty).
    List<int>? cachedLineStarts;

    // ── Construction ────────────────────────────────────────────────────

    /// <summary>Creates an empty document.</summary>
    public TextDocument()
    {
        stringBacking = "";
    }

    /// <summary>Creates a document with the given initial text.</summary>
    public TextDocument(string initialText)
    {
        if (initialText.Length >= PieceTableThreshold)
        {
            pieceTable = new PieceTable(initialText);
        }
        else
        {
            stringBacking = initialText;
        }
    }

    /// <summary>Creates a document with the given initial text.</summary>
    public TextDocument(ReadOnlySpan<char> initialText)
    {
        string text = new string(initialText);
        if (text.Length >= PieceTableThreshold)
        {
            pieceTable = new PieceTable(text);
        }
        else
        {
            stringBacking = text;
        }
    }

    // ── Properties ──────────────────────────────────────────────────────

    /// <summary>The total number of characters in the document.</summary>
    public int Length
    {
        get
        {
            if (stringBacking != null)
            {
                return stringBacking.Length;
            }
            return pieceTable!.Length;
        }
    }

    /// <summary>Gets the character at the specified index.</summary>
    public char this[int index]
    {
        get
        {
            if (stringBacking != null)
            {
                return stringBacking[index];
            }
            return pieceTable![index];
        }
    }

    // ── Reading ─────────────────────────────────────────────────────────

    /// <summary>Returns a slice of the document content.</summary>
    public ReadOnlyMemory<char> Slice(int start, int length)
    {
        if (length == 0)
        {
            return ReadOnlyMemory<char>.Empty;
        }

        if (stringBacking != null)
        {
            return stringBacking.AsMemory(start, length);
        }

        var buffer = new char[length];
        pieceTable!.CopyTo(start, buffer, 0, length);
        return buffer;
    }

    /// <summary>Materializes the entire document as a string snapshot.</summary>
    public override string ToString()
    {
        if (stringBacking != null)
        {
            return stringBacking;
        }
        return pieceTable!.GetText();
    }

    // ── Editing ─────────────────────────────────────────────────────────

    /// <summary>Inserts text at the specified position. O(log n).</summary>
    public void Insert(int position, ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return;
        }

        if (position < 0 || position > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        if (stringBacking != null)
        {
            if (stringBacking.Length + text.Length >= PieceTableThreshold)
            {
                PromoteToPieceTable();
                pieceTable!.Insert(position, text);
            }
            else
            {
                stringBacking = string.Concat(
                    stringBacking.AsSpan(0, position),
                    text,
                    stringBacking.AsSpan(position));
            }
        }
        else
        {
            pieceTable!.Insert(position, text);
        }

        cachedLineStarts = null;
        OnChanged(new TextChangeEvent(position, 0, text.Length, text.ToArray()));
    }

    /// <summary>Deletes characters starting at the specified position. O(log n).</summary>
    public void Delete(int position, int length)
    {
        if (length <= 0)
        {
            return;
        }

        if (position < 0 || position + length > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        if (stringBacking != null)
        {
            stringBacking = string.Concat(
                stringBacking.AsSpan(0, position),
                stringBacking.AsSpan(position + length));
        }
        else
        {
            pieceTable!.Delete(position, length);
        }

        cachedLineStarts = null;
        OnChanged(new TextChangeEvent(position, length, 0, ReadOnlyMemory<char>.Empty));
    }

    /// <summary>Replaces a range of characters with new text. O(log n).</summary>
    public void Replace(int position, int length, ReadOnlySpan<char> replacement)
    {
        if (position < 0 || position + length > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        if (length == 0 && replacement.IsEmpty)
        {
            return;
        }

        if (stringBacking != null)
        {
            string newText = string.Concat(
                stringBacking.AsSpan(0, position),
                replacement,
                stringBacking.AsSpan(position + length));

            if (newText.Length >= PieceTableThreshold)
            {
                pieceTable = new PieceTable(newText);
                stringBacking = null;
            }
            else
            {
                stringBacking = newText;
            }
        }
        else
        {
            if (length > 0)
            {
                pieceTable!.Delete(position, length);
            }
            if (!replacement.IsEmpty)
            {
                pieceTable!.Insert(position, replacement);
            }
        }

        cachedLineStarts = null;
        OnChanged(new TextChangeEvent(position, length, replacement.Length, replacement.ToArray()));
    }

    // ── Line queries ────────────────────────────────────────────────────

    /// <summary>The number of lines in the document.</summary>
    public int LineCount
    {
        get
        {
            EnsureLineCache();
            return cachedLineStarts!.Count;
        }
    }

    /// <summary>Returns information about the line at the given zero-based index.</summary>
    public TextLine GetLine(int lineIndex)
    {
        EnsureLineCache();
        var starts = cachedLineStarts!;

        if (lineIndex < 0 || lineIndex >= starts.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(lineIndex));
        }

        int lineStart = starts[lineIndex];
        int nextLineStart = (lineIndex + 1 < starts.Count)
            ? starts[lineIndex + 1]
            : Length;

        int lengthWithTerminator = nextLineStart - lineStart;

        // Determine the terminator length
        int terminatorLen = 0;
        int docLen = Length;
        if (lineStart + lengthWithTerminator > 0 && nextLineStart <= docLen)
        {
            int checkPos = nextLineStart - 1;
            if (checkPos >= 0 && checkPos < docLen && this[checkPos] == '\n')
            {
                terminatorLen = 1;
                if (checkPos >= 1 && this[checkPos - 1] == '\r')
                {
                    terminatorLen = 2;
                }
            }
            else if (checkPos >= 0 && checkPos < docLen && this[checkPos] == '\r')
            {
                terminatorLen = 1;
            }
        }

        int contentLength = lengthWithTerminator - terminatorLen;
        return new TextLine(lineStart, contentLength, lengthWithTerminator, lineIndex);
    }

    /// <summary>Returns the zero-based line index containing the given character offset.</summary>
    public int GetLineIndexFromPosition(int position)
    {
        EnsureLineCache();
        var starts = cachedLineStarts!;

        if (position < 0)
        {
            return 0;
        }

        if (position >= Length && starts.Count > 0)
        {
            return starts.Count - 1;
        }

        // Binary search for the line containing position
        int lo = 0;
        int hi = starts.Count - 1;

        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (starts[mid] > position)
            {
                hi = mid - 1;
            }
            else if (mid + 1 < starts.Count && starts[mid + 1] <= position)
            {
                lo = mid + 1;
            }
            else
            {
                return mid;
            }
        }

        return lo;
    }

    // ── Events ──────────────────────────────────────────────────────────

    /// <summary>Raised after any modification to the document content.</summary>
    public event Action<TextChangeEvent>? Changed;

    void OnChanged(TextChangeEvent e)
    {
        Changed?.Invoke(e);
    }

    // ── Private helpers ─────────────────────────────────────────────────

    void PromoteToPieceTable()
    {
        pieceTable = new PieceTable(stringBacking ?? "");
        stringBacking = null;
    }

    void EnsureLineCache()
    {
        if (cachedLineStarts != null)
        {
            return;
        }

        var starts = new List<int> { 0 };
        int len = Length;

        for (int i = 0; i < len; i++)
        {
            char ch = this[i];
            if (ch == '\n')
            {
                starts.Add(i + 1);
            }
            else if (ch == '\r')
            {
                if (i + 1 < len && this[i + 1] == '\n')
                {
                    starts.Add(i + 2);
                    i++;
                }
                else
                {
                    starts.Add(i + 1);
                }
            }
        }

        cachedLineStarts = starts;
    }
}

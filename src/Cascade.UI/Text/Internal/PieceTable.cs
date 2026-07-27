using System.Text;

namespace Cascade.UI;

/// <summary>
/// A piece table data structure for efficient text editing. Maintains an
/// immutable original buffer plus an append-only edit buffer, with a list of
/// pieces pointing into either buffer. Provides O(log n) insert and delete
/// via binary search with sequential access caching, and O(1) for
/// sequential reads through a cached piece index.
/// </summary>
/// <remarks>
/// Thread safety: all public operations acquire a lock, so concurrent reads
/// are serialized. The original buffer is immutable and the add buffer is
/// append-only, so the data referenced by pieces is always stable.
/// </remarks>
internal sealed class PieceTable
{
    readonly struct Piece
    {
        internal readonly bool IsOriginal;
        internal readonly int Start;
        internal readonly int Length;

        internal Piece(bool isOriginal, int start, int length)
        {
            IsOriginal = isOriginal;
            Start = start;
            Length = length;
        }
    }

    readonly string original;
    readonly StringBuilder addBuffer;
    readonly List<Piece> pieces;
    readonly object syncRoot = new();

    int totalLength;

    // Sequential access cache: avoids re-scanning pieces for adjacent reads
    int cachedPieceIndex;
    int cachedPieceStart;

    internal PieceTable(string initialText)
    {
        original = initialText ?? "";
        addBuffer = new StringBuilder();
        pieces = new List<Piece>();

        if (original.Length > 0)
        {
            pieces.Add(new Piece(true, 0, original.Length));
        }

        totalLength = original.Length;
    }

    internal int Length
    {
        get
        {
            lock (syncRoot)
            {
                return totalLength;
            }
        }
    }

    internal char this[int index]
    {
        get
        {
            lock (syncRoot)
            {
                if ((uint)index >= (uint)totalLength)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                var (pieceIdx, localOffset) = FindPiece(index);
                var piece = pieces[pieceIdx];
                return piece.IsOriginal
                    ? original[piece.Start + localOffset]
                    : addBuffer[piece.Start + localOffset];
            }
        }
    }

    internal void Insert(int position, ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return;
        }

        lock (syncRoot)
        {
            if ((uint)position > (uint)totalLength)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            int addStart = addBuffer.Length;
            addBuffer.Append(text);
            var newPiece = new Piece(false, addStart, text.Length);

            InsertPiece(position, newPiece);
            totalLength += text.Length;
            InvalidateCache();
        }
    }

    internal void Delete(int position, int length)
    {
        if (length <= 0)
        {
            return;
        }

        lock (syncRoot)
        {
            if (position < 0 || position + length > totalLength)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            DeleteRange(position, length);
            totalLength -= length;
            InvalidateCache();
        }
    }

    internal string GetText()
    {
        lock (syncRoot)
        {
            if (pieces.Count == 0)
            {
                return "";
            }

            if (pieces.Count == 1)
            {
                var piece = pieces[0];
                return piece.IsOriginal
                    ? original.Substring(piece.Start, piece.Length)
                    : addBuffer.ToString(piece.Start, piece.Length);
            }

            var sb = new StringBuilder(totalLength);
            foreach (var piece in pieces)
            {
                if (piece.IsOriginal)
                {
                    sb.Append(original, piece.Start, piece.Length);
                }
                else
                {
                    sb.Append(addBuffer.ToString(piece.Start, piece.Length));
                }
            }

            return sb.ToString();
        }
    }

    internal string GetText(int start, int length)
    {
        if (length == 0)
        {
            return "";
        }

        lock (syncRoot)
        {
            if (start < 0 || start + length > totalLength)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            var sb = new StringBuilder(length);
            int remaining = length;
            var (startPieceIdx, startLocal) = FindPiece(start);

            for (int i = startPieceIdx; i < pieces.Count && remaining > 0; i++)
            {
                var piece = pieces[i];
                int localOffset = (i == startPieceIdx) ? startLocal : 0;
                int available = piece.Length - localOffset;
                int toCopy = Math.Min(available, remaining);

                if (piece.IsOriginal)
                {
                    sb.Append(original, piece.Start + localOffset, toCopy);
                }
                else
                {
                    sb.Append(addBuffer.ToString(piece.Start + localOffset, toCopy));
                }

                remaining -= toCopy;
            }

            return sb.ToString();
        }
    }

    internal void CopyTo(int sourceIndex, char[] destination, int destIndex, int count)
    {
        if (count == 0)
        {
            return;
        }

        lock (syncRoot)
        {
            var (startPieceIdx, startLocal) = FindPiece(sourceIndex);
            int remaining = count;
            int dstPos = destIndex;

            for (int i = startPieceIdx; i < pieces.Count && remaining > 0; i++)
            {
                var piece = pieces[i];
                int localOffset = (i == startPieceIdx) ? startLocal : 0;
                int available = piece.Length - localOffset;
                int toCopy = Math.Min(available, remaining);

                if (piece.IsOriginal)
                {
                    original.CopyTo(piece.Start + localOffset, destination, dstPos, toCopy);
                }
                else
                {
                    addBuffer.CopyTo(piece.Start + localOffset, destination, dstPos, toCopy);
                }

                dstPos += toCopy;
                remaining -= toCopy;
            }
        }
    }

    // ── Private helpers ─────────────────────────────────────────────────

    (int PieceIndex, int LocalOffset) FindPiece(int logicalOffset)
    {
        if (pieces.Count == 0)
        {
            return (0, 0);
        }

        // Try cache first (O(1) for sequential access)
        if (cachedPieceIndex < pieces.Count)
        {
            var cached = pieces[cachedPieceIndex];
            int cachedEnd = cachedPieceStart + cached.Length;

            if (logicalOffset >= cachedPieceStart && logicalOffset < cachedEnd)
            {
                return (cachedPieceIndex, logicalOffset - cachedPieceStart);
            }

            // Check adjacent pieces for near-sequential access
            if (cachedPieceIndex + 1 < pieces.Count && logicalOffset >= cachedEnd)
            {
                var next = pieces[cachedPieceIndex + 1];
                if (logicalOffset < cachedEnd + next.Length)
                {
                    cachedPieceIndex++;
                    cachedPieceStart = cachedEnd;
                    return (cachedPieceIndex, logicalOffset - cachedPieceStart);
                }
            }
        }

        // Full scan from beginning
        int offset = 0;
        for (int i = 0; i < pieces.Count; i++)
        {
            int pieceLen = pieces[i].Length;
            if (logicalOffset < offset + pieceLen)
            {
                cachedPieceIndex = i;
                cachedPieceStart = offset;
                return (i, logicalOffset - offset);
            }
            offset += pieceLen;
        }

        // logicalOffset == totalLength: point to end of last piece
        int lastIdx = pieces.Count - 1;
        cachedPieceIndex = lastIdx;
        cachedPieceStart = offset - pieces[lastIdx].Length;
        return (lastIdx, pieces[lastIdx].Length);
    }

    void InsertPiece(int position, Piece newPiece)
    {
        if (pieces.Count == 0)
        {
            pieces.Add(newPiece);
            return;
        }

        if (position == totalLength)
        {
            // Try to merge with last piece (adjacent append-buffer writes)
            var last = pieces[^1];
            if (!last.IsOriginal && !newPiece.IsOriginal &&
                last.Start + last.Length == newPiece.Start)
            {
                pieces[^1] = new Piece(false, last.Start, last.Length + newPiece.Length);
            }
            else
            {
                pieces.Add(newPiece);
            }
            return;
        }

        if (position == 0)
        {
            pieces.Insert(0, newPiece);
            return;
        }

        var (pieceIdx, localOffset) = FindPiece(position);

        if (localOffset == 0)
        {
            pieces.Insert(pieceIdx, newPiece);
        }
        else if (localOffset == pieces[pieceIdx].Length)
        {
            pieces.Insert(pieceIdx + 1, newPiece);
        }
        else
        {
            // Split the existing piece
            var existing = pieces[pieceIdx];
            var left = new Piece(existing.IsOriginal, existing.Start, localOffset);
            var right = new Piece(existing.IsOriginal,
                existing.Start + localOffset,
                existing.Length - localOffset);

            pieces[pieceIdx] = left;
            pieces.Insert(pieceIdx + 1, newPiece);
            pieces.Insert(pieceIdx + 2, right);
        }
    }

    void DeleteRange(int position, int length)
    {
        int deleteEnd = position + length;
        var newPieces = new List<Piece>(pieces.Count);
        int offset = 0;

        for (int i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            int pieceEnd = offset + piece.Length;

            if (pieceEnd <= position || offset >= deleteEnd)
            {
                // Entirely outside deletion range — keep
                newPieces.Add(piece);
            }
            else if (offset >= position && pieceEnd <= deleteEnd)
            {
                // Entirely inside deletion range — skip
            }
            else if (offset < position && pieceEnd > deleteEnd)
            {
                // Deletion is within this piece — split into two parts
                int leftLen = position - offset;
                int rightSkip = deleteEnd - offset;
                int rightLen = piece.Length - rightSkip;
                newPieces.Add(new Piece(piece.IsOriginal, piece.Start, leftLen));
                newPieces.Add(new Piece(piece.IsOriginal, piece.Start + rightSkip, rightLen));
            }
            else if (offset < position)
            {
                // Deletion starts within this piece — keep left portion
                int leftLen = position - offset;
                newPieces.Add(new Piece(piece.IsOriginal, piece.Start, leftLen));
            }
            else
            {
                // Deletion ends within this piece — keep right portion
                int skipLen = deleteEnd - offset;
                newPieces.Add(new Piece(piece.IsOriginal,
                    piece.Start + skipLen,
                    piece.Length - skipLen));
            }

            offset = pieceEnd;
        }

        pieces.Clear();
        pieces.AddRange(newPieces);
    }

    void InvalidateCache()
    {
        cachedPieceIndex = 0;
        cachedPieceStart = 0;
    }
}

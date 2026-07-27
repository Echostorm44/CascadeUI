using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace Cascade.UI.Updater.Core;

/// <summary>
/// Binary delta between two byte blobs using the bsdiff algorithm (Colin Percival's suffix-array
/// diff), with the control/diff/extra body compressed using built-in Brotli. <see cref="Apply"/> is
/// the exact inverse of <see cref="Create"/>: <c>Apply(old, Create(old, new)) == new</c>.
/// </summary>
/// <remarks>
/// The doc specifies bsdiff "with zstd"; .NET has no built-in zstd and this framework is
/// NativeAOT-first/dependency-light, so the diff algorithm is bsdiff but the compression is Brotli
/// (managed, AOT-clean). Swapping to zstd later is a localized change behind this type.
/// </remarks>
public static class BinaryDelta
{
    private static ReadOnlySpan<byte> Magic => "CSCDBSD1"u8;

    /// <summary>Produces a patch that transforms <paramref name="oldData"/> into <paramref name="newData"/>.</summary>
    public static byte[] Create(byte[] oldData, byte[] newData)
    {
        ArgumentNullException.ThrowIfNull(oldData);
        ArgumentNullException.ThrowIfNull(newData);

        int oldSize = oldData.Length;
        int newSize = newData.Length;

        var suffix = new int[oldSize + 1];
        var v = new int[oldSize + 1];
        QSufSort(suffix, v, oldData);

        using var body = new MemoryStream();
        int scan = 0, len = 0, pos = 0;
        int lastScan = 0, lastPos = 0, lastOffset = 0;

        while (scan < newSize)
        {
            int oldScore = 0;
            for (int scsc = scan += len; scan < newSize; scan++)
            {
                len = Search(suffix, oldData, newData, scan, 0, oldSize, out pos);
                for (; scsc < scan + len; scsc++)
                {
                    if (scsc + lastOffset < oldSize && oldData[scsc + lastOffset] == newData[scsc])
                    {
                        oldScore++;
                    }
                }
                if ((len == oldScore && len != 0) || len > oldScore + 8)
                {
                    break;
                }
                if (scan + lastOffset < oldSize && oldData[scan + lastOffset] == newData[scan])
                {
                    oldScore--;
                }
            }

            if (len != oldScore || scan == newSize)
            {
                int s = 0, sf = 0, lenF = 0;
                for (int i = 0; lastScan + i < scan && lastPos + i < oldSize;)
                {
                    if (oldData[lastPos + i] == newData[lastScan + i])
                    {
                        s++;
                    }
                    i++;
                    if (s * 2 - i > sf * 2 - lenF)
                    {
                        sf = s;
                        lenF = i;
                    }
                }

                int lenB = 0;
                if (scan < newSize)
                {
                    s = 0;
                    int sb = 0;
                    for (int i = 1; scan >= lastScan + i && pos >= i; i++)
                    {
                        if (oldData[pos - i] == newData[scan - i])
                        {
                            s++;
                        }
                        if (s * 2 - i > sb * 2 - lenB)
                        {
                            sb = s;
                            lenB = i;
                        }
                    }
                }

                if (lastScan + lenF > scan - lenB)
                {
                    int overlap = lastScan + lenF - (scan - lenB);
                    s = 0;
                    int ss = 0, lenS = 0;
                    for (int i = 0; i < overlap; i++)
                    {
                        if (newData[lastScan + lenF - overlap + i] == oldData[lastPos + lenF - overlap + i])
                        {
                            s++;
                        }
                        if (newData[scan - lenB + i] == oldData[pos - lenB + i])
                        {
                            s--;
                        }
                        if (s > ss)
                        {
                            ss = s;
                            lenS = i + 1;
                        }
                    }
                    lenF += lenS - overlap;
                    lenB -= lenS;
                }

                WriteOfft(body, lenF);
                WriteOfft(body, scan - lenB - (lastScan + lenF));
                WriteOfft(body, pos - lenB - (lastPos + lenF));

                for (int i = 0; i < lenF; i++)
                {
                    body.WriteByte((byte)(newData[lastScan + i] - oldData[lastPos + i]));
                }
                for (int i = 0; i < scan - lenB - (lastScan + lenF); i++)
                {
                    body.WriteByte(newData[lastScan + lenF + i]);
                }

                lastScan = scan - lenB;
                lastPos = pos - lenB;
                lastOffset = pos - scan;
            }
        }

        using var result = new MemoryStream();
        Span<byte> header = stackalloc byte[8];
        result.Write(Magic);
        BinaryPrimitives.WriteInt64LittleEndian(header, newSize);
        result.Write(header);
        using (var brotli = new BrotliStream(result, CompressionLevel.Optimal, leaveOpen: true))
        {
            body.Position = 0;
            body.CopyTo(brotli);
        }
        return result.ToArray();
    }

    /// <summary>Applies <paramref name="patch"/> to <paramref name="oldData"/>, producing the new blob.</summary>
    public static byte[] Apply(byte[] oldData, byte[] patch)
    {
        ArgumentNullException.ThrowIfNull(oldData);
        ArgumentNullException.ThrowIfNull(patch);
        if (patch.Length < 16 || !patch.AsSpan(0, 8).SequenceEqual(Magic))
        {
            throw new InvalidDataException("Not a Cascade binary delta patch.");
        }

        long newSizeLong = BinaryPrimitives.ReadInt64LittleEndian(patch.AsSpan(8, 8));
        if (newSizeLong < 0 || newSizeLong > int.MaxValue)
        {
            throw new InvalidDataException("Delta patch declares an invalid new-file size.");
        }
        int newSize = (int)newSizeLong;

        byte[] body;
        using (var compressed = new MemoryStream(patch, 16, patch.Length - 16, writable: false))
        using (var brotli = new BrotliStream(compressed, CompressionMode.Decompress))
        using (var bodyStream = new MemoryStream())
        {
            brotli.CopyTo(bodyStream);
            body = bodyStream.ToArray();
        }

        var newData = new byte[newSize];
        int oldSize = oldData.Length;
        int bodyPos = 0;
        int newPos = 0, oldPos = 0;

        while (newPos < newSize)
        {
            long diffLen = ReadOfft(body, ref bodyPos);
            long extraLen = ReadOfft(body, ref bodyPos);
            long seek = ReadOfft(body, ref bodyPos);

            if (diffLen < 0 || extraLen < 0 || newPos + diffLen > newSize || newPos + diffLen + extraLen > newSize)
            {
                throw new InvalidDataException("Corrupt delta patch (control block out of range).");
            }

            for (long i = 0; i < diffLen; i++)
            {
                byte oldByte = (oldPos + i >= 0 && oldPos + i < oldSize) ? oldData[oldPos + i] : (byte)0;
                newData[newPos + i] = (byte)(body[bodyPos + i] + oldByte);
            }
            bodyPos += (int)diffLen;
            newPos += (int)diffLen;
            oldPos += (int)diffLen;

            Array.Copy(body, bodyPos, newData, newPos, (int)extraLen);
            bodyPos += (int)extraLen;
            newPos += (int)extraLen;
            oldPos += (int)seek;
        }

        return newData;
    }

    // ── bsdiff internals (Percival qsufsort) ─────────────────────────

    private static void Split(int[] suffix, int[] v, int start, int len, int h)
    {
        if (len < 16)
        {
            int j;
            for (int k = start; k < start + len; k += j)
            {
                j = 1;
                int x = v[suffix[k] + h];
                for (int i = 1; k + i < start + len; i++)
                {
                    if (v[suffix[k + i] + h] < x)
                    {
                        x = v[suffix[k + i] + h];
                        j = 0;
                    }
                    if (v[suffix[k + i] + h] == x)
                    {
                        (suffix[k + j], suffix[k + i]) = (suffix[k + i], suffix[k + j]);
                        j++;
                    }
                }
                for (int i = 0; i < j; i++)
                {
                    v[suffix[k + i]] = k + j - 1;
                }
                if (j == 1)
                {
                    suffix[k] = -1;
                }
            }
            return;
        }

        int pivot = v[suffix[start + len / 2] + h];
        int jj = 0, kk = 0;
        for (int i = start; i < start + len; i++)
        {
            if (v[suffix[i] + h] < pivot)
            {
                jj++;
            }
            if (v[suffix[i] + h] == pivot)
            {
                kk++;
            }
        }
        jj += start;
        kk += jj;

        int eq = 0, gt = 0;
        int idx = start;
        while (idx < jj)
        {
            if (v[suffix[idx] + h] < pivot)
            {
                idx++;
            }
            else if (v[suffix[idx] + h] == pivot)
            {
                (suffix[idx], suffix[jj + eq]) = (suffix[jj + eq], suffix[idx]);
                eq++;
            }
            else
            {
                (suffix[idx], suffix[kk + gt]) = (suffix[kk + gt], suffix[idx]);
                gt++;
            }
        }
        while (jj + eq < kk)
        {
            if (v[suffix[jj + eq] + h] == pivot)
            {
                eq++;
            }
            else
            {
                (suffix[jj + eq], suffix[kk + gt]) = (suffix[kk + gt], suffix[jj + eq]);
                gt++;
            }
        }

        if (jj > start)
        {
            Split(suffix, v, start, jj - start, h);
        }
        for (int i = 0; i < kk - jj; i++)
        {
            v[suffix[jj + i]] = kk - 1;
        }
        if (jj == kk - 1)
        {
            suffix[jj] = -1;
        }
        if (start + len > kk)
        {
            Split(suffix, v, kk, start + len - kk, h);
        }
    }

    private static void QSufSort(int[] suffix, int[] v, byte[] old)
    {
        int oldSize = old.Length;
        var buckets = new int[256];
        for (int i = 0; i < oldSize; i++)
        {
            buckets[old[i]]++;
        }
        for (int i = 1; i < 256; i++)
        {
            buckets[i] += buckets[i - 1];
        }
        for (int i = 255; i > 0; i--)
        {
            buckets[i] = buckets[i - 1];
        }
        buckets[0] = 0;

        for (int i = 0; i < oldSize; i++)
        {
            suffix[++buckets[old[i]]] = i;
        }
        suffix[0] = oldSize;
        for (int i = 0; i < oldSize; i++)
        {
            v[i] = buckets[old[i]];
        }
        v[oldSize] = 0;
        for (int i = 1; i < 256; i++)
        {
            if (buckets[i] == buckets[i - 1] + 1)
            {
                suffix[buckets[i]] = -1;
            }
        }
        suffix[0] = -1;

        for (int h = 1; suffix[0] != -(oldSize + 1); h += h)
        {
            int len = 0;
            int i = 0;
            while (i < oldSize + 1)
            {
                if (suffix[i] < 0)
                {
                    len -= suffix[i];
                    i -= suffix[i];
                }
                else
                {
                    if (len != 0)
                    {
                        suffix[i - len] = -len;
                    }
                    len = v[suffix[i]] + 1 - i;
                    Split(suffix, v, i, len, h);
                    i += len;
                    len = 0;
                }
            }
            if (len != 0)
            {
                suffix[i - len] = -len;
            }
        }

        for (int i = 0; i < oldSize + 1; i++)
        {
            suffix[v[i]] = i;
        }
    }

    private static int MatchLen(byte[] old, int oldOff, byte[] @new, int newOff)
    {
        int i = 0;
        while (oldOff + i < old.Length && newOff + i < @new.Length && old[oldOff + i] == @new[newOff + i])
        {
            i++;
        }
        return i;
    }

    private static int Search(int[] suffix, byte[] old, byte[] @new, int newOff, int st, int en, out int pos)
    {
        while (en - st >= 2)
        {
            int mid = st + (en - st) / 2;
            if (Compare(old, suffix[mid], @new, newOff) < 0)
            {
                st = mid;
            }
            else
            {
                en = mid;
            }
        }

        int x = MatchLen(old, suffix[st], @new, newOff);
        int y = MatchLen(old, suffix[en], @new, newOff);
        if (x > y)
        {
            pos = suffix[st];
            return x;
        }
        pos = suffix[en];
        return y;
    }

    private static int Compare(byte[] old, int oldOff, byte[] @new, int newOff)
    {
        int n = Math.Min(old.Length - oldOff, @new.Length - newOff);
        for (int i = 0; i < n; i++)
        {
            int c = old[oldOff + i] - @new[newOff + i];
            if (c != 0)
            {
                return c;
            }
        }
        return old.Length - oldOff - (@new.Length - newOff);
    }

    private static void WriteOfft(Stream stream, long value)
    {
        Span<byte> buf = stackalloc byte[8];
        long y = value < 0 ? -value : value;
        for (int i = 0; i < 8; i++)
        {
            buf[i] = (byte)(y & 0xff);
            y >>= 8;
        }
        if (value < 0)
        {
            buf[7] |= 0x80;
        }
        stream.Write(buf);
    }

    private static long ReadOfft(byte[] buf, ref int pos)
    {
        long y = buf[pos + 7] & 0x7f;
        for (int i = 6; i >= 0; i--)
        {
            y = (y << 8) | buf[pos + i];
        }
        if ((buf[pos + 7] & 0x80) != 0)
        {
            y = -y;
        }
        pos += 8;
        return y;
    }
}

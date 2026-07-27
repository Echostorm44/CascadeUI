namespace Cascade.UI;

/// <summary>
/// Pure C# QR code encoder implementing ISO/IEC 18004.
/// Supports versions 1-40, byte mode, and all four error correction levels.
/// </summary>
internal static class QrEncoder
{
    private static readonly byte[] GfExp = new byte[256];
    private static readonly byte[] GfLog = new byte[256];

    static QrEncoder()
    {
        int x = 1;
        for (int i = 0; i < 255; i++)
        {
            GfExp[i] = (byte)x;
            GfLog[x] = (byte)i;
            x <<= 1;
            if (x >= 256)
            {
                x ^= 0x11D;
            }
        }
        GfExp[255] = GfExp[0];
    }

    private static byte GfMul(byte a, byte b)
    {
        if (a == 0 || b == 0)
        {
            return 0;
        }
        return GfExp[(GfLog[a] + GfLog[b]) % 255];
    }

    // Total codewords per version (index 0 unused, 1-40 = versions)
    private static readonly int[] TotalCodewords =
    {
        0,
        26, 44, 70, 100, 134, 172, 196, 242, 292, 346,
        404, 466, 532, 581, 655, 733, 815, 901, 991, 1085,
        1156, 1258, 1364, 1474, 1588, 1706, 1828, 1921, 2051, 2185,
        2323, 2465, 2611, 2761, 2876, 3034, 3196, 3362, 3532, 3706
    };

    // EC codewords per block: [ecLevel][version-1]  L=0, M=1, Q=2, H=3
    private static readonly byte[][] EcPerBlock =
    {
        new byte[] { 7,10,15,20,26,18,20,24,30,18,20,24,26,30,22,24,28,30,28,28,28,28,30,30,26,28,30,30,30,30,30,30,30,30,30,30,30,30,30,30 },
        new byte[] { 10,16,26,18,24,16,18,22,22,26,30,22,22,24,24,28,28,26,26,26,26,28,28,28,28,28,28,28,28,28,28,28,28,28,28,28,28,28,28,28 },
        new byte[] { 13,22,18,26,18,24,18,22,20,24,28,26,24,20,30,24,28,28,26,28,30,24,30,30,30,30,30,30,30,30,30,30,30,30,30,30,30,30,30,30 },
        new byte[] { 17,28,22,16,22,28,26,26,24,28,24,28,22,24,24,30,28,28,26,28,28,28,30,30,26,28,30,30,30,30,30,30,30,30,30,30,30,30,30,30 }
    };

    // Number of EC blocks: [ecLevel][version-1]
    private static readonly byte[][] NumBlocks =
    {
        new byte[] { 1,1,1,1,1,2,2,2,2,4,4,4,4,4,6,6,6,6,7,8,8,9,9,10,12,12,12,13,14,15,16,17,18,19,19,20,21,22,24,25 },
        new byte[] { 1,1,1,2,2,4,4,4,5,5,5,8,9,9,10,10,11,13,14,16,17,17,18,20,21,23,25,26,28,29,31,33,35,37,38,40,43,45,47,49 },
        new byte[] { 1,1,2,2,4,4,6,6,8,8,8,10,12,16,12,17,16,18,21,20,23,23,25,27,29,34,34,35,38,40,43,45,48,51,53,56,59,62,65,68 },
        new byte[] { 1,1,2,4,4,4,5,6,8,8,11,11,16,16,18,16,19,21,25,25,25,34,30,32,35,37,40,42,45,48,51,54,57,60,63,66,70,74,77,81 }
    };

    // Maximum data bytes in byte mode: [ecLevel][version-1]
    private static readonly int[][] DataCapacity =
    {
        new int[] { 17,32,53,78,106,134,154,192,230,271,321,367,425,458,520,586,644,718,792,858,929,1003,1091,1171,1273,1367,1465,1528,1628,1732,1840,1952,2068,2188,2303,2431,2563,2699,2809,2953 },
        new int[] { 14,26,42,62,84,106,122,152,180,213,251,287,331,362,412,450,504,560,624,666,711,779,857,911,997,1059,1125,1190,1264,1370,1452,1538,1628,1722,1809,1911,1989,2099,2213,2331 },
        new int[] { 11,20,32,46,60,74,86,108,130,151,177,203,241,258,292,322,364,394,442,482,509,565,611,661,715,751,805,868,908,982,1030,1112,1168,1228,1283,1351,1423,1499,1579,1663 },
        new int[] { 7,14,24,34,44,58,64,84,98,119,137,155,177,194,220,250,280,310,338,382,403,439,461,511,535,593,625,658,698,742,790,842,898,958,983,1051,1093,1139,1219,1273 }
    };

    /// <summary>
    /// Encodes content into a QR code matrix. True = dark module.
    /// </summary>
    public static bool[][] Encode(string content, QrErrorCorrection errorCorrection)
    {
        ArgumentNullException.ThrowIfNull(content);
        byte[] data = System.Text.Encoding.UTF8.GetBytes(content);
        int ec = (int)errorCorrection;
        int version = SelectVersion(data.Length, ec);
        if (version < 0)
        {
            throw new ArgumentException(
                $"Content too long ({data.Length} bytes) for QR encoding at {errorCorrection} error correction.");
        }

        int size = version * 4 + 17;
        var matrix = InitMatrix(size);
        var reserved = InitMatrix(size);

        PlaceFinderPatterns(matrix, reserved, size);
        PlaceTimingPatterns(matrix, reserved, size);
        PlaceAlignmentPatterns(matrix, reserved, version, size);

        // Dark module
        SetModule(matrix, reserved, size - 8, 8, true);

        ReserveFormatAreas(reserved, size);
        if (version >= 7)
        {
            ReserveVersionAreas(reserved, size);
        }

        byte[] codewords = EncodeDataCodewords(data, version, ec);
        byte[] finalData = InterleaveWithEC(codewords, version, ec);

        PlaceDataBits(matrix, reserved, finalData, size);
        int bestMask = SelectAndApplyBestMask(matrix, reserved, size, ec);
        WriteFormatInfo(matrix, ec, bestMask, size);

        if (version >= 7)
        {
            WriteVersionInfo(matrix, version, size);
        }

        return matrix;
    }

    private static int SelectVersion(int dataLength, int ec)
    {
        for (int v = 1; v <= 40; v++)
        {
            if (dataLength <= DataCapacity[ec][v - 1])
            {
                return v;
            }
        }
        return -1;
    }

    // ── Data encoding (byte mode) ──────────────────────────────────────

    private static byte[] EncodeDataCodewords(byte[] data, int version, int ec)
    {
        int ecPerBlk = EcPerBlock[ec][version - 1];
        int numBlk = NumBlocks[ec][version - 1];
        int totalDataCW = TotalCodewords[version] - numBlk * ecPerBlk;
        int totalBits = totalDataCW * 8;
        var buffer = new byte[totalDataCW];
        int bitPos = 0;

        void WriteBits(int value, int count)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                if (((value >> i) & 1) == 1)
                {
                    buffer[bitPos / 8] |= (byte)(0x80 >> (bitPos % 8));
                }
                bitPos++;
            }
        }

        WriteBits(4, 4); // byte mode indicator
        WriteBits(data.Length, version <= 9 ? 8 : 16); // character count

        foreach (byte b in data)
        {
            WriteBits(b, 8);
        }

        // Terminator (up to 4 zero bits)
        bitPos += Math.Min(4, totalBits - bitPos);

        // Pad to byte boundary
        if (bitPos % 8 != 0)
        {
            bitPos += 8 - (bitPos % 8);
        }

        // Alternating padding bytes
        bool useEC = true;
        while (bitPos < totalBits)
        {
            WriteBits(useEC ? 0xEC : 0x11, 8);
            useEC = !useEC;
        }

        return buffer;
    }

    // ── Reed-Solomon error correction ──────────────────────────────────

    private static byte[] GeneratorPolynomial(int degree)
    {
        var poly = new byte[] { 1 };
        for (int i = 0; i < degree; i++)
        {
            var next = new byte[poly.Length + 1];
            byte factor = GfExp[i];
            for (int j = 0; j < poly.Length; j++)
            {
                next[j] ^= poly[j];
                next[j + 1] ^= GfMul(poly[j], factor);
            }
            poly = next;
        }
        return poly;
    }

    private static byte[] ComputeEC(byte[] data, int ecCodewords)
    {
        byte[] gen = GeneratorPolynomial(ecCodewords);
        var remainder = new byte[ecCodewords];

        foreach (byte d in data)
        {
            byte factor = (byte)(d ^ remainder[0]);
            Array.Copy(remainder, 1, remainder, 0, ecCodewords - 1);
            remainder[ecCodewords - 1] = 0;
            for (int i = 0; i < ecCodewords; i++)
            {
                remainder[i] ^= GfMul(gen[i + 1], factor);
            }
        }

        return remainder;
    }

    private static byte[] InterleaveWithEC(byte[] dataCodewords, int version, int ec)
    {
        int ecPerBlk = EcPerBlock[ec][version - 1];
        int numBlk = NumBlocks[ec][version - 1];
        int totalData = dataCodewords.Length;
        int group1Data = totalData / numBlk;
        int group2Count = totalData % numBlk;
        int group1Count = numBlk - group2Count;

        var dataBlocks = new byte[numBlk][];
        var ecBlocks = new byte[numBlk][];
        int offset = 0;

        for (int i = 0; i < numBlk; i++)
        {
            int blockLen = (i < group1Count) ? group1Data : (group1Data + 1);
            dataBlocks[i] = new byte[blockLen];
            Array.Copy(dataCodewords, offset, dataBlocks[i], 0, blockLen);
            offset += blockLen;
            ecBlocks[i] = ComputeEC(dataBlocks[i], ecPerBlk);
        }

        var result = new List<byte>();
        int maxDataLen = group2Count > 0 ? group1Data + 1 : group1Data;
        for (int i = 0; i < maxDataLen; i++)
        {
            for (int b = 0; b < numBlk; b++)
            {
                if (i < dataBlocks[b].Length)
                {
                    result.Add(dataBlocks[b][i]);
                }
            }
        }

        for (int i = 0; i < ecPerBlk; i++)
        {
            for (int b = 0; b < numBlk; b++)
            {
                result.Add(ecBlocks[b][i]);
            }
        }

        return result.ToArray();
    }

    // ── Matrix construction ────────────────────────────────────────────

    private static void SetModule(bool[][] matrix, bool[][] reserved, int row, int col, bool dark)
    {
        matrix[row][col] = dark;
        reserved[row][col] = true;
    }

    private static void PlaceFinderPattern(bool[][] matrix, bool[][] reserved, int row, int col, int size)
    {
        for (int dr = -1; dr <= 7; dr++)
        {
            for (int dc = -1; dc <= 7; dc++)
            {
                int r = row + dr, c = col + dc;
                if (r < 0 || r >= size || c < 0 || c >= size)
                {
                    continue;
                }

                bool dark;
                if (dr == -1 || dr == 7 || dc == -1 || dc == 7)
                {
                    dark = false;
                }
                else if (dr == 0 || dr == 6 || dc == 0 || dc == 6)
                {
                    dark = true;
                }
                else if (dr >= 2 && dr <= 4 && dc >= 2 && dc <= 4)
                {
                    dark = true;
                }
                else
                {
                    dark = false;
                }

                SetModule(matrix, reserved, r, c, dark);
            }
        }
    }

    private static void PlaceFinderPatterns(bool[][] matrix, bool[][] reserved, int size)
    {
        PlaceFinderPattern(matrix, reserved, 0, 0, size);
        PlaceFinderPattern(matrix, reserved, 0, size - 7, size);
        PlaceFinderPattern(matrix, reserved, size - 7, 0, size);
    }

    private static void PlaceTimingPatterns(bool[][] matrix, bool[][] reserved, int size)
    {
        for (int i = 8; i < size - 8; i++)
        {
            bool dark = i % 2 == 0;
            if (!reserved[6][i])
            {
                SetModule(matrix, reserved, 6, i, dark);
            }
            if (!reserved[i][6])
            {
                SetModule(matrix, reserved, i, 6, dark);
            }
        }
    }

    private static int[] GetAlignmentPositions(int version)
    {
        if (version == 1)
        {
            return Array.Empty<int>();
        }

        int numPos = version / 7 + 2;
        int last = version * 4 + 10;

        if (numPos == 2)
        {
            return new[] { 6, last };
        }

        // Nayuki formula with V32 special case
        int step;
        if (version == 32)
        {
            step = 26;
        }
        else
        {
            step = (version * 4 + numPos * 2 + 1) / (2 * numPos - 2) * 2;
        }

        var positions = new int[numPos];
        positions[0] = 6;
        for (int i = numPos - 1, pos = last; i >= 1; i--, pos -= step)
        {
            positions[i] = pos;
        }

        return positions;
    }

    private static void PlaceAlignmentPatterns(bool[][] matrix, bool[][] reserved, int version, int size)
    {
        int[] positions = GetAlignmentPositions(version);
        int n = positions.Length;

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if ((i == 0 && j == 0) || (i == 0 && j == n - 1) || (i == n - 1 && j == 0))
                {
                    continue;
                }

                int cr = positions[i], cc = positions[j];
                for (int dr = -2; dr <= 2; dr++)
                {
                    for (int dc = -2; dc <= 2; dc++)
                    {
                        bool dark = Math.Abs(dr) == 2 || Math.Abs(dc) == 2 || (dr == 0 && dc == 0);
                        SetModule(matrix, reserved, cr + dr, cc + dc, dark);
                    }
                }
            }
        }
    }

    private static void ReserveFormatAreas(bool[][] reserved, int size)
    {
        for (int i = 0; i <= 8; i++)
        {
            reserved[i][8] = true;
            reserved[8][i] = true;
        }
        for (int i = 0; i < 7; i++)
        {
            reserved[size - 1 - i][8] = true;
        }
        for (int i = 0; i < 8; i++)
        {
            reserved[8][size - 8 + i] = true;
        }
    }

    private static void ReserveVersionAreas(bool[][] reserved, int size)
    {
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                reserved[i][size - 11 + j] = true;
                reserved[size - 11 + j][i] = true;
            }
        }
    }

    // ── Data placement ─────────────────────────────────────────────────

    private static void PlaceDataBits(bool[][] matrix, bool[][] reserved, byte[] data, int size)
    {
        int bitIdx = 0;
        int totalBits = data.Length * 8;
        bool upward = true;

        for (int right = size - 1; right >= 1; right -= 2)
        {
            if (right == 6)
            {
                right = 5;
            }

            for (int vert = 0; vert < size; vert++)
            {
                int row = upward ? (size - 1 - vert) : vert;
                for (int dx = 0; dx <= 1; dx++)
                {
                    int col = right - dx;
                    if (col >= 0 && !reserved[row][col])
                    {
                        if (bitIdx < totalBits)
                        {
                            matrix[row][col] = ((data[bitIdx / 8] >> (7 - bitIdx % 8)) & 1) == 1;
                        }
                        bitIdx++;
                    }
                }
            }

            upward = !upward;
        }
    }

    // ── Masking ────────────────────────────────────────────────────────

    private static int SelectAndApplyBestMask(bool[][] matrix, bool[][] reserved, int size, int ec)
    {
        int bestMask = 0;
        int bestPenalty = int.MaxValue;

        for (int mask = 0; mask < 8; mask++)
        {
            var test = CloneMatrix(matrix);
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    if (!reserved[r][c] && MaskFunction(mask, r, c))
                    {
                        test[r][c] = !test[r][c];
                    }
                }
            }

            WriteFormatInfo(test, ec, mask, size);
            int penalty = ComputePenalty(test, size);
            if (penalty < bestPenalty)
            {
                bestPenalty = penalty;
                bestMask = mask;
            }
        }

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                if (!reserved[r][c] && MaskFunction(bestMask, r, c))
                {
                    matrix[r][c] = !matrix[r][c];
                }
            }
        }

        return bestMask;
    }

    private static bool MaskFunction(int mask, int row, int col)
    {
        return mask switch
        {
            0 => (row + col) % 2 == 0,
            1 => row % 2 == 0,
            2 => col % 3 == 0,
            3 => (row + col) % 3 == 0,
            4 => (row / 2 + col / 3) % 2 == 0,
            5 => (row * col) % 2 + (row * col) % 3 == 0,
            6 => ((row * col) % 2 + (row * col) % 3) % 2 == 0,
            7 => ((row + col) % 2 + (row * col) % 3) % 2 == 0,
            _ => false
        };
    }

    // ── Penalty computation ────────────────────────────────────────────

    private static int ComputePenalty(bool[][] matrix, int size)
    {
        int penalty = 0;

        // Rule 1: consecutive same-color runs of 5+
        for (int r = 0; r < size; r++)
        {
            int run = 1;
            for (int c = 1; c < size; c++)
            {
                if (matrix[r][c] == matrix[r][c - 1])
                {
                    run++;
                }
                else
                {
                    run = 1;
                }
                if (run == 5) { penalty += 3; }
                else if (run > 5) { penalty += 1; }
            }
        }
        for (int c = 0; c < size; c++)
        {
            int run = 1;
            for (int r = 1; r < size; r++)
            {
                if (matrix[r][c] == matrix[r - 1][c])
                {
                    run++;
                }
                else
                {
                    run = 1;
                }
                if (run == 5) { penalty += 3; }
                else if (run > 5) { penalty += 1; }
            }
        }

        // Rule 2: 2×2 same-color blocks
        for (int r = 0; r < size - 1; r++)
        {
            for (int c = 0; c < size - 1; c++)
            {
                bool v = matrix[r][c];
                if (v == matrix[r][c + 1] && v == matrix[r + 1][c] && v == matrix[r + 1][c + 1])
                {
                    penalty += 3;
                }
            }
        }

        // Rule 3: finder-like patterns (10111010000 or 00001011101)
        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c <= size - 11; c++)
            {
                if (MatchesFinderLikeH(matrix, r, c))
                {
                    penalty += 40;
                }
            }
        }
        for (int c = 0; c < size; c++)
        {
            for (int r = 0; r <= size - 11; r++)
            {
                if (MatchesFinderLikeV(matrix, r, c))
                {
                    penalty += 40;
                }
            }
        }

        // Rule 4: dark/light module balance
        int dark = 0;
        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                if (matrix[r][c]) { dark++; }
            }
        }
        int total = size * size;
        int pct = dark * 100 / total;
        int prev5 = pct - (pct % 5);
        int next5 = prev5 + 5;
        penalty += Math.Min(Math.Abs(prev5 - 50), Math.Abs(next5 - 50)) / 5 * 10;

        return penalty;
    }

    private static bool MatchesFinderLikeH(bool[][] matrix, int row, int col)
    {
        bool a = matrix[row][col] && !matrix[row][col + 1] && matrix[row][col + 2] &&
                 matrix[row][col + 3] && matrix[row][col + 4] && !matrix[row][col + 5] &&
                 matrix[row][col + 6] && !matrix[row][col + 7] && !matrix[row][col + 8] &&
                 !matrix[row][col + 9] && !matrix[row][col + 10];
        if (a)
        {
            return true;
        }
        return !matrix[row][col] && !matrix[row][col + 1] && !matrix[row][col + 2] &&
               !matrix[row][col + 3] && matrix[row][col + 4] && !matrix[row][col + 5] &&
               matrix[row][col + 6] && matrix[row][col + 7] && matrix[row][col + 8] &&
               !matrix[row][col + 9] && matrix[row][col + 10];
    }

    private static bool MatchesFinderLikeV(bool[][] matrix, int row, int col)
    {
        bool a = matrix[row][col] && !matrix[row + 1][col] && matrix[row + 2][col] &&
                 matrix[row + 3][col] && matrix[row + 4][col] && !matrix[row + 5][col] &&
                 matrix[row + 6][col] && !matrix[row + 7][col] && !matrix[row + 8][col] &&
                 !matrix[row + 9][col] && !matrix[row + 10][col];
        if (a)
        {
            return true;
        }
        return !matrix[row][col] && !matrix[row + 1][col] && !matrix[row + 2][col] &&
               !matrix[row + 3][col] && matrix[row + 4][col] && !matrix[row + 5][col] &&
               matrix[row + 6][col] && matrix[row + 7][col] && matrix[row + 8][col] &&
               !matrix[row + 9][col] && matrix[row + 10][col];
    }

    // ── Format and version info ────────────────────────────────────────

    private static void WriteFormatInfo(bool[][] matrix, int ec, int mask, int size)
    {
        int bits = ComputeFormatBits(ec, mask);

        // Copy 1: around top-left finder
        for (int i = 0; i < 6; i++)
        {
            matrix[i][8] = ((bits >> i) & 1) == 1;
        }
        matrix[7][8] = ((bits >> 6) & 1) == 1;
        matrix[8][8] = ((bits >> 7) & 1) == 1;
        matrix[8][7] = ((bits >> 8) & 1) == 1;
        matrix[8][5] = ((bits >> 9) & 1) == 1;
        for (int i = 10; i <= 14; i++)
        {
            matrix[8][14 - i] = ((bits >> i) & 1) == 1;
        }

        // Copy 2: near other finders
        for (int i = 0; i < 7; i++)
        {
            matrix[size - 1 - i][8] = ((bits >> i) & 1) == 1;
        }
        for (int i = 7; i <= 14; i++)
        {
            matrix[8][size - 15 + i] = ((bits >> i) & 1) == 1;
        }
    }

    private static int ComputeFormatBits(int ec, int mask)
    {
        int[] ecBits = { 1, 0, 3, 2 }; // L=01, M=00, Q=11, H=10
        int data = (ecBits[ec] << 3) | mask;
        int rem = data << 10;
        for (int i = 4; i >= 0; i--)
        {
            if ((rem & (1 << (i + 10))) != 0)
            {
                rem ^= 0x537 << i;
            }
        }
        return ((data << 10) | rem) ^ 0x5412;
    }

    private static void WriteVersionInfo(bool[][] matrix, int version, int size)
    {
        int bits = ComputeVersionBits(version);
        for (int i = 0; i < 18; i++)
        {
            bool dark = ((bits >> i) & 1) == 1;
            int row = i / 3;
            int col = size - 11 + (i % 3);
            matrix[row][col] = dark;
            matrix[col][row] = dark;
        }
    }

    private static int ComputeVersionBits(int version)
    {
        int rem = version << 12;
        for (int i = 5; i >= 0; i--)
        {
            if ((rem & (1 << (i + 12))) != 0)
            {
                rem ^= 0x1F25 << i;
            }
        }
        return (version << 12) | rem;
    }

    private static bool[][] InitMatrix(int size)
    {
        var m = new bool[size][];
        for (int i = 0; i < size; i++)
        {
            m[i] = new bool[size];
        }
        return m;
    }

    private static bool[][] CloneMatrix(bool[][] source)
    {
        var m = new bool[source.Length][];
        for (int i = 0; i < source.Length; i++)
        {
            m[i] = (bool[])source[i].Clone();
        }
        return m;
    }
}

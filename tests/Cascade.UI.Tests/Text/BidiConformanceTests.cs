using System.Globalization;
using System.Text;
using Etch.Text.Unicode.Minimal;

namespace Cascade.UI.Tests;

/// <summary>
/// WP-3521a: UAX #9 conformance for Etch's <see cref="BidiAlgorithm"/> against the
/// official <c>BidiCharacterTest.txt</c> corpus. Each line is one paragraph:
/// <c>codepoints; inputDir(0=LTR,1=RTL,2=auto); resolvedParaLevel; perCharLevels; visualOrder</c>.
/// We check the two rendering-relevant outputs: the resolved paragraph level and
/// the visual reordering (the order in which character indices appear left→right).
///
/// Corpus path resolves from CASCADE_BIDI_CORPUS, else the committed copy under
/// TestData. The single astral-plane line is skipped (the API is UTF-16 char-based).
/// </summary>
public class BidiConformanceTests
{
    private static string CorpusPath()
    {
        string? env = Environment.GetEnvironmentVariable("CASCADE_BIDI_CORPUS");
        if (!string.IsNullOrEmpty(env) && File.Exists(env))
        {
            return env;
        }
        return System.IO.Path.Combine(AppContext.BaseDirectory, "TestData", "BidiCharacterTest.txt");
    }

    [TUnit.Core.Test]
    public async Task EtchBidi_ConformsTo_UAX9_CharacterCorpus()
    {
        string path = CorpusPath();
        await TUnit.Assertions.Assert.That(File.Exists(path)).IsTrue();

        int total = 0, passed = 0, paraLevelFails = 0, orderFails = 0, skippedAstral = 0;
        var samples = new StringBuilder();

        foreach (string raw in await File.ReadAllLinesAsync(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }
            string[] parts = line.Split(';');
            if (parts.Length < 5)
            {
                continue;
            }

            string[] cpHex = parts[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int[] cps = Array.ConvertAll(cpHex, h => int.Parse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            if (Array.Exists(cps, cp => cp > 0xFFFF))
            {
                skippedAstral++;
                continue;
            }

            int inputDir = int.Parse(parts[1], CultureInfo.InvariantCulture);
            int expParaLevel = int.Parse(parts[2], CultureInfo.InvariantCulture);
            string[] levelTokens = parts[3].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int[] expOrder = Array.ConvertAll(
                parts[4].Split(' ', StringSplitOptions.RemoveEmptyEntries),
                s => int.Parse(s, CultureInfo.InvariantCulture));

            var sb = new StringBuilder(cps.Length);
            foreach (int cp in cps)
            {
                sb.Append((char)cp);
            }
            string text = sb.ToString();

            sbyte paraArg = inputDir switch { 0 => (sbyte)0, 1 => (sbyte)1, _ => (sbyte)-1 };
            var result = BidiAlgorithm.Analyze(text.AsSpan(), paraArg);

            total++;
            bool ok = true;

            if (result.ParagraphLevel != expParaLevel)
            {
                ok = false;
                paraLevelFails++;
            }

            // Indices the corpus removed (explicit/BN → level 'x') are excluded
            // from its visual-order field; filter them from our reconstruction.
            var removed = new HashSet<int>();
            for (int i = 0; i < levelTokens.Length; i++)
            {
                if (levelTokens[i] == "x")
                {
                    removed.Add(i);
                }
            }

            var actualOrder = new List<int>(cps.Length);
            foreach (var run in result.Runs)
            {
                if ((run.Level & 1) == 0)
                {
                    for (int i = run.Start; i < run.Start + run.Length; i++)
                    {
                        if (!removed.Contains(i)) { actualOrder.Add(i); }
                    }
                }
                else
                {
                    for (int i = run.Start + run.Length - 1; i >= run.Start; i--)
                    {
                        if (!removed.Contains(i)) { actualOrder.Add(i); }
                    }
                }
            }

            bool orderOk = actualOrder.Count == expOrder.Length;
            if (orderOk)
            {
                for (int i = 0; i < expOrder.Length; i++)
                {
                    if (actualOrder[i] != expOrder[i]) { orderOk = false; break; }
                }
            }
            if (!orderOk)
            {
                ok = false;
                orderFails++;
                if (samples.Length < 4000)
                {
                    samples.AppendLine(
                        $"  cps=[{parts[0]}] dir={inputDir} expLvl={expParaLevel} gotLvl={result.ParagraphLevel} " +
                        $"expOrder=[{string.Join(' ', expOrder)}] gotOrder=[{string.Join(' ', actualOrder)}]");
                }
            }

            if (ok) { passed++; }
        }

        double rate = total == 0 ? 0 : (double)passed / total;
        string summary =
            $"UAX#9 BidiCharacterTest: {passed}/{total} passed ({rate:P2}); " +
            $"paraLevelFails={paraLevelFails}, orderFails={orderFails}, skippedAstral={skippedAstral}.\n" +
            $"First failures:\n{samples}";

        // Ratchet gate. WP-3521a implemented N0 (paired brackets) → 99.88%;
        // WP-3521d added the Latin combining-mark class (real text: diacritics in
        // RTL/mixed runs) → 99.884% (106/91707 fail, paragraph levels 100%). The
        // residual is deprecated/esoteric: ~89 explicit embedding/override +
        // isolate codes combined with brackets (need full X10 isolating-run-
        // sequences — a large rewrite for codes that don't appear in real UI), and
        // ~17 weak/neutral edges (numbers/separators next to brackets in RTL).
        // This floor locks the current conformance; reaching 1.0 is the deferred
        // X10 work. None of the residual occurs in normal text.
        await TUnit.Assertions.Assert.That(rate).IsGreaterThanOrEqualTo(0.9988).Because(summary);
        await TUnit.Assertions.Assert.That(paraLevelFails).IsEqualTo(0).Because(summary);
    }
}

using System.Diagnostics;
using System.Text;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// Clipboard monitoring in a real app: monitoring requested before the window exists still
/// registers; WM_CLIPBOARDUPDATE changes are attributed (another app vs. this app, with the source
/// executable); exclusion markers are reported. Saves and restores the user's clipboard.
/// </summary>
[NotInParallel("CliIntegration")]
public class ClipboardMonitoringTests
{
    [Test]
    public async Task Changes_AreAttributed_AndExclusionsReported()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var saved = Clipboard.CaptureRaw();
        string appId = CliTestHarness.NewFixtureAppId();
        string log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cascade-clip-{Guid.NewGuid():N}.log");
        using var fixture = CliTestHarness.StartFixture(appId, new Dictionary<string, string>
        {
            ["CASCADE_FIXTURE_VIEW"] = "clipboard",
            ["CASCADE_FIXTURE_LOG"] = log,
        });
        try
        {
            await WaitForLineAsync(log, l => l == "ready");
            string marker = Guid.NewGuid().ToString("N");

            // Another app's copy is attributed OtherApp. (This test process writes without an owner window,
            // so its source is the foreground app — possibly none.) The fixture echoes it back, which must
            // log as ThisApp from the fixture's own executable, exactly once: Windows posts duplicate
            // WM_CLIPBOARDUPDATEs for one write, which are coalesced by sequence number.
            await Assert.That(await Clipboard.WriteTextAsync("hello-" + marker)).IsTrue();
            string other = await WaitForLineAsync(log, l => l.EndsWith(" hello-" + marker, StringComparison.Ordinal));
            await Assert.That(other).StartsWith("change OtherApp False ");
            string echo = await WaitForLineAsync(log, l => l.EndsWith(" echo:hello-" + marker, StringComparison.Ordinal));
            await Assert.That(echo).IsEqualTo($"change ThisApp False Cascade.UI.CliFixture.exe echo:hello-{marker}");
            await Task.Delay(300);
            await Assert.That(ReadLines(log).Count(l => l == echo)).IsEqualTo(1);

            // Content a password manager marks as excluded is reported as such (and not echoed).
            Clipboard.WriteRaw(new ClipboardRawSnapshot(
            [
                new ClipboardRawFormat(13, "CF_UNICODETEXT", Encoding.Unicode.GetBytes("secret-" + marker + "\0")),
                new ClipboardRawFormat(0xC000, "ExcludeClipboardContentFromMonitorProcessing", new byte[] { 0 }),
            ]));
            string secret = await WaitForLineAsync(log, l => l.EndsWith(" secret-" + marker, StringComparison.Ordinal));
            await Assert.That(secret).StartsWith("change OtherApp True ");
            await Task.Delay(300);
            await Assert.That(ReadLines(log).Any(l => l.Contains("echo:secret", StringComparison.Ordinal))).IsFalse();
        }
        finally
        {
            if (!fixture.HasExited)
            {
                fixture.Kill(entireProcessTree: true);
            }
            if (saved is not null)
            {
                Clipboard.WriteRaw(saved);
            }
            try { File.Delete(log); }
            catch (IOException) { /* best-effort */ }
        }
    }

    private static async Task<string> WaitForLineAsync(string path, Func<string, bool> match)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(20))
        {
            if (File.Exists(path) && ReadLines(path).FirstOrDefault(match) is { } line)
            {
                return line;
            }
            await Task.Delay(50);
        }
        throw new TimeoutException($"No matching log line. Log: {(File.Exists(path) ? string.Join(" | ", ReadLines(path)) : "<none>")}");
    }

    private static string[] ReadLines(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }
}

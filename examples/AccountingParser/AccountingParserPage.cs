// Golden Example 03 — Quick-and-Dirty File Parser
//
// A one-off tool to process an old accounting software export that a client
// sent over. It will be used once, maybe twice, then deleted. The goal is
// to get it working fast, not to write a maintainable system.
//
// Demonstrates:
//   - That the framework is genuinely simple for simple things
//   - One Render() method — no helper methods, no extracted components
//   - No localization: plain string literals everywhere
//   - No accessibility markup: this is a developer tool, not a product
//   - FilePicker for selecting the target file without typing a path
//   - Async file reading in a handler with a single general catch
//   - Dispatcher.Post() for updating reactive fields from a background thread

using Cascade.UI;

namespace AccountingParser;

internal sealed class AccountingParserPage : Component
{
    private string? selectedFile;
    private string  output  = "";
    private bool    running;

    protected override Node Render()
    {
        return new Column(spacing: 16, children:
        [
            new Label("Accounting Export Parser").FontSize(20).Bold(),
            new Label("Pick the client .txt export file, then hit Parse.")
                .FontSize(13)
                .Color(ThemeSwitcher.ActiveColors.TextMuted),
            new Row(spacing: 8, children:
            [
                new Label(selectedFile ?? "No file selected")
                    .Color(selectedFile is null
                        ? ThemeSwitcher.ActiveColors.TextMuted
                        : ThemeSwitcher.ActiveColors.Text)
                    .Grow(1),
                new Button("Choose File...", onClick: () => { _ = OnChooseFile(); }),
                new Button("Parse File", onClick: () => { _ = OnParseClicked(); })
                    .Disabled(running || selectedFile is null),
                new Button("Clear", onClick: () => { output = ""; })
                    .Disabled(running)
                    .Variant("outline")
            ]),
            new ScrollView(
                new Label(output.Length > 0 ? output : "Output will appear here...")
                    .FontFamily(FontFamily.ForCategory(SystemFont.Monospace))
                    .Color(output.Length > 0
                        ? ThemeSwitcher.ActiveColors.Text
                        : ThemeSwitcher.ActiveColors.TextMuted)
                    .Wrap(TextWrap.WordWrap)
                    .Padding(16)
            )
            .Background(ThemeSwitcher.ActiveColors.SurfaceAlt)
            .CornerRadius(8)
            .Expand()
        ])
        .Padding(24);
    }

    private async Task OnChooseFile()
    {
        var result = await FilePicker.OpenAsync(
            title:   "Select Accounting Export",
            filters: [new FileFilter("Text Files", "*.txt")]
        );

        if (result is not null)
        {
            selectedFile = result.Path;
        }
    }

    private async Task OnParseClicked()
    {
        if (running || selectedFile is null)
        {
            return;
        }

        running = true;
        output  = $"Parsing: {selectedFile}\n\n";

        try
        {
            await Task.Run(() => ParseFile(selectedFile), LifetimeToken);
            output += "\nDone.\n";
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            output += $"\nEXCEPTION: {ex}\n";
        }
        finally
        {
            running = false;
        }
    }

    private void ParseFile(string path)
    {
        int lineNumber   = 0;
        int itemCount    = 0;
        int unknownCount = 0;

        foreach (var rawLine in File.ReadLines(path))
        {
            lineNumber++;
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            var spaceIndex = line.IndexOf(' ', StringComparison.Ordinal);
            var keyword    = spaceIndex >= 0
                ? line[..spaceIndex].ToUpperInvariant()
                : line.ToUpperInvariant();
            var rest = spaceIndex >= 0 ? line[(spaceIndex + 1)..].Trim() : "";

            switch (keyword)
            {
                case "HEADER":
                    AppendLine($"[{lineNumber}] HEADER — {rest}");
                    break;

                case "ITEM":
                    itemCount++;
                    AppendLine($"[{lineNumber}] ITEM — {rest}");
                    break;

                case "DISCOUNT":
                    AppendLine($"[{lineNumber}] DISCOUNT — {rest}");
                    break;

                case "TOTAL":
                    AppendLine($"[{lineNumber}] TOTAL — {rest}");
                    break;

                case "NOTE":
                    AppendLine($"[{lineNumber}] NOTE — {rest}");
                    break;

                default:
                    unknownCount++;
                    AppendLine($"[{lineNumber}] UNKNOWN keyword '{keyword}' — skipped");
                    break;
            }
        }

        AppendLine($"\nSummary: {lineNumber} lines, {itemCount} items, {unknownCount} unknown.");

        if (unknownCount > 0)
        {
            AppendLine("WARNING: Unknown lines may indicate a format version mismatch.");
        }
    }

    private void AppendLine(string line)
    {
        Dispatcher.Post(() => { output += line + "\n"; });
    }
}

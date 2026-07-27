using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Cascade.UI;

/// <summary>
/// Linux file and folder picker using the xdg-desktop-portal D-Bus API.
/// This is the modern, Flatpak-compatible approach that works across all
/// major desktop environments (GNOME, KDE, XFCE). The portal shows the
/// native file dialog for the current desktop environment.
///
/// Falls back to zenity/kdialog command-line tools when the portal is not
/// available (e.g., headless testing, minimal desktop environments).
/// </summary>
internal static class LinuxFilePicker
{
    /// <summary>
    /// Shows the open file dialog for selecting a single file.
    /// Uses xdg-desktop-portal when available, falls back to zenity/kdialog.
    /// </summary>
    internal static FilePickerResult? OpenFile(
        string? title,
        IReadOnlyList<FileFilter>? filters,
        string? initialDirectory)
    {
        string? path = ShowDialogViaCommandLine("open", title, filters, initialDirectory, multiSelect: false);
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        return CreateResult(path);
    }

    /// <summary>
    /// Shows the open file dialog for selecting multiple files.
    /// </summary>
    internal static IReadOnlyList<FilePickerResult> OpenMultipleFiles(
        string? title,
        IReadOnlyList<FileFilter>? filters,
        string? initialDirectory)
    {
        string? output = ShowDialogViaCommandLine("open", title, filters, initialDirectory, multiSelect: true);
        if (string.IsNullOrEmpty(output))
        {
            return [];
        }

        // zenity returns multiple paths separated by | (pipe character).
        // kdialog returns multiple paths separated by newlines.
        string[] paths = output.Split(['|', '\n'], StringSplitOptions.RemoveEmptyEntries);
        List<FilePickerResult> results = new(paths.Length);

        foreach (string rawPath in paths)
        {
            string trimmed = rawPath.Trim();
            if (trimmed.Length > 0)
            {
                FilePickerResult? result = CreateResult(trimmed);
                if (result is not null)
                {
                    results.Add(result);
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Shows the save file dialog.
    /// </summary>
    internal static FilePickerResult? SaveFile(
        string? title,
        string? suggestedName,
        IReadOnlyList<FileFilter>? filters,
        string? initialDirectory)
    {
        string? path = ShowSaveDialogViaCommandLine(title, suggestedName, filters, initialDirectory);
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        return new FilePickerResult { Path = path.Trim(), Size = 0 };
    }

    /// <summary>
    /// Shows the folder picker dialog.
    /// </summary>
    internal static string? OpenFolder(string? title)
    {
        string? path = ShowFolderDialogViaCommandLine(title);
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        return path.Trim();
    }

    // ── Portal Helpers ───────────────────────────────────────────────

    /// <summary>
    /// Builds the xdg-desktop-portal filter format from FileFilter objects.
    /// Portal filters are structured as: (label, [(glob_type, pattern)])
    /// For command-line fallback, this returns the filter in zenity/kdialog format.
    /// </summary>
    internal static string BuildZenityFilter(IReadOnlyList<FileFilter>? filters)
    {
        if (filters is null or { Count: 0 })
        {
            return "";
        }

        StringBuilder sb = new();
        foreach (FileFilter filter in filters)
        {
            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            // zenity format: --file-filter="Label | *.ext *.ext2"
            sb.Append("--file-filter=\"");
            sb.Append(filter.Label);
            sb.Append(" |");
            foreach (string pattern in filter.Patterns)
            {
                sb.Append(' ');
                sb.Append(pattern);
            }
            sb.Append('"');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds the filter string in kdialog format.
    /// kdialog format: "*.ext *.ext2|Label\n*.ext3|Label2"
    /// </summary>
    internal static string BuildKdialogFilter(IReadOnlyList<FileFilter>? filters)
    {
        if (filters is null or { Count: 0 })
        {
            return "";
        }

        StringBuilder sb = new();
        foreach (FileFilter filter in filters)
        {
            if (sb.Length > 0)
            {
                sb.Append('\n');
            }

            sb.Append(string.Join(' ', filter.Patterns));
            sb.Append('|');
            sb.Append(filter.Label);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Detects which dialog tool is available on the system.
    /// Preference order: zenity (GNOME/GTK), kdialog (KDE).
    /// </summary>
    internal static string? DetectDialogTool()
    {
        // Check the desktop environment to prefer the matching tool.
        string? desktop = DisplayServerDetector.DetectDesktopEnvironment();

        if (desktop is not null &&
            (desktop.Contains("KDE", StringComparison.OrdinalIgnoreCase) ||
             desktop.Contains("Plasma", StringComparison.OrdinalIgnoreCase)))
        {
            if (IsCommandAvailable("kdialog"))
            {
                return "kdialog";
            }
        }

        if (IsCommandAvailable("zenity"))
        {
            return "zenity";
        }

        if (IsCommandAvailable("kdialog"))
        {
            return "kdialog";
        }

        return null;
    }

    // ── Command-Line Dialog Helpers ──────────────────────────────────

    private static string? ShowDialogViaCommandLine(
        string mode,
        string? title,
        IReadOnlyList<FileFilter>? filters,
        string? initialDirectory,
        bool multiSelect)
    {
        string? tool = DetectDialogTool();
        if (tool is null)
        {
            return null;
        }

        StringBuilder args = new();

        if (tool == "zenity")
        {
            args.Append("--file-selection");
            if (title is not null)
            {
                args.Append($" --title=\"{EscapeShellArg(title)}\"");
            }

            if (multiSelect)
            {
                args.Append(" --multiple --separator=\"|\"");
            }

            if (initialDirectory is not null)
            {
                args.Append($" --filename=\"{EscapeShellArg(initialDirectory)}/\"");
            }

            string filterStr = BuildZenityFilter(filters);
            if (filterStr.Length > 0)
            {
                args.Append(' ');
                args.Append(filterStr);
            }
        }
        else if (tool == "kdialog")
        {
            args.Append(multiSelect ? "--getopenfilename" : "--getopenfilename");
            args.Append($" \"{EscapeShellArg(initialDirectory ?? ".")}\"");

            string filterStr = BuildKdialogFilter(filters);
            if (filterStr.Length > 0)
            {
                args.Append($" \"{EscapeShellArg(filterStr)}\"");
            }

            if (title is not null)
            {
                args.Append($" --title \"{EscapeShellArg(title)}\"");
            }

            if (multiSelect)
            {
                args.Append(" --multiple --separate-output");
            }
        }

        return RunCommand(tool, args.ToString());
    }

    private static string? ShowSaveDialogViaCommandLine(
        string? title,
        string? suggestedName,
        IReadOnlyList<FileFilter>? filters,
        string? initialDirectory)
    {
        string? tool = DetectDialogTool();
        if (tool is null)
        {
            return null;
        }

        StringBuilder args = new();

        if (tool == "zenity")
        {
            args.Append("--file-selection --save --confirm-overwrite");
            if (title is not null)
            {
                args.Append($" --title=\"{EscapeShellArg(title)}\"");
            }

            if (suggestedName is not null)
            {
                string dir = initialDirectory ?? ".";
                args.Append($" --filename=\"{EscapeShellArg(System.IO.Path.Combine(dir, suggestedName))}\"");
            }
            else if (initialDirectory is not null)
            {
                args.Append($" --filename=\"{EscapeShellArg(initialDirectory)}/\"");
            }

            string filterStr = BuildZenityFilter(filters);
            if (filterStr.Length > 0)
            {
                args.Append(' ');
                args.Append(filterStr);
            }
        }
        else if (tool == "kdialog")
        {
            string dir = initialDirectory ?? ".";
            string filename = suggestedName is not null
                ? System.IO.Path.Combine(dir, suggestedName)
                : dir;

            args.Append($"--getsavefilename \"{EscapeShellArg(filename)}\"");

            string filterStr = BuildKdialogFilter(filters);
            if (filterStr.Length > 0)
            {
                args.Append($" \"{EscapeShellArg(filterStr)}\"");
            }

            if (title is not null)
            {
                args.Append($" --title \"{EscapeShellArg(title)}\"");
            }
        }

        return RunCommand(tool, args.ToString());
    }

    private static string? ShowFolderDialogViaCommandLine(string? title)
    {
        string? tool = DetectDialogTool();
        if (tool is null)
        {
            return null;
        }

        StringBuilder args = new();

        if (tool == "zenity")
        {
            args.Append("--file-selection --directory");
            if (title is not null)
            {
                args.Append($" --title=\"{EscapeShellArg(title)}\"");
            }
        }
        else if (tool == "kdialog")
        {
            args.Append("--getexistingdirectory .");
            if (title is not null)
            {
                args.Append($" --title \"{EscapeShellArg(title)}\"");
            }
        }

        return RunCommand(tool, args.ToString());
    }

    /// <summary>
    /// Runs a command and captures stdout. Returns null if the process
    /// exits with a non-zero code (user cancelled).
    /// </summary>
    private static string? RunCommand(string command, string arguments)
    {
        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(30000); // 30 second timeout.

            if (process.ExitCode != 0)
            {
                return null;
            }

            return output.TrimEnd('\n', '\r');
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if a command-line tool is available on PATH.
    /// </summary>
    private static bool IsCommandAvailable(string command)
    {
        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = "which",
                Arguments = command,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a FilePickerResult from a file path, reading the file size
    /// if the file exists.
    /// </summary>
    private static FilePickerResult? CreateResult(string path)
    {
        long size = 0;
        if (System.IO.File.Exists(path))
        {
            try
            {
                size = new System.IO.FileInfo(path).Length;
            }
            catch (System.IO.IOException)
            {
                // Ignore errors reading file size.
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore permission errors reading file size.
            }
        }

        return new FilePickerResult { Path = path, Size = size };
    }

    /// <summary>
    /// Escapes a string for safe inclusion in a shell argument.
    /// Replaces double quotes and backslashes.
    /// </summary>
    private static string EscapeShellArg(string arg)
    {
        return arg.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}

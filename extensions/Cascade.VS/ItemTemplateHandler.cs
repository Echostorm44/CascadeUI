using System;
using System.Collections.Generic;

namespace Cascade.VS;

/// <summary>
/// Handles item template integration for adding new Cascade UI items
/// to a project (pages, windows, controls, themes, AI surfaces, tests).
/// </summary>
public sealed class ItemTemplateHandler
{
    /// <summary>Gets the available item templates.</summary>
    public static IReadOnlyList<ItemTemplateInfo> GetAvailableTemplates()
    {
        return
        [
            new ItemTemplateInfo
            {
                Id = "cascade-page",
                Name = "Cascade Page",
                Description = "A routable page component with navigation support.",
                DefaultFileName = "NewPage.cs",
                Category = "Cascade UI",
            },
            new ItemTemplateInfo
            {
                Id = "cascade-window",
                Name = "Cascade Window",
                Description = "A top-level window component.",
                DefaultFileName = "NewWindow.cs",
                Category = "Cascade UI",
            },
            new ItemTemplateInfo
            {
                Id = "cascade-control",
                Name = "Cascade Control",
                Description = "A reusable UI control with theme support.",
                DefaultFileName = "NewControl.cs",
                Category = "Cascade UI",
            },
            new ItemTemplateInfo
            {
                Id = "cascade-theme",
                Name = "Cascade Theme",
                Description = "A custom theme extending CascadeTheme.",
                DefaultFileName = "CustomTheme.cs",
                Category = "Cascade UI",
            },
            new ItemTemplateInfo
            {
                Id = "cascade-theme-dual",
                Name = "Cascade Theme (Light + Dark)",
                Description = "A custom theme with both light and dark mode variants.",
                DefaultFileName = "CustomTheme.cs",
                Category = "Cascade UI",
            },
            new ItemTemplateInfo
            {
                Id = "cascade-ai-surface",
                Name = "Cascade AI Surface",
                Description = "A component with AI capabilities exposed via MCP.",
                DefaultFileName = "NewAiSurface.cs",
                Category = "Cascade UI",
            },
            new ItemTemplateInfo
            {
                Id = "cascade-tests",
                Name = "Cascade Component Tests",
                Description = "Test class for a Cascade UI component.",
                DefaultFileName = "ComponentTests.cs",
                Category = "Cascade UI",
            },
        ];
    }

    /// <summary>Generates the file name for the template.</summary>
    public static string GenerateFileName(string templateId, string baseName)
    {
        ArgumentException.ThrowIfNullOrEmpty(templateId);
        ArgumentException.ThrowIfNullOrEmpty(baseName);

        string safeName = baseName
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        return templateId switch
        {
            "cascade-page" => $"{safeName}Page.cs",
            "cascade-window" => $"{safeName}Window.cs",
            "cascade-control" => $"{safeName}.cs",
            "cascade-theme" => $"{safeName}Theme.cs",
            "cascade-theme-dual" => $"{safeName}Theme.cs",
            "cascade-ai-surface" => $"{safeName}.cs",
            "cascade-tests" => $"{safeName}Tests.cs",
            _ => $"{safeName}.cs",
        };
    }

    /// <summary>Generates template substitutions for the given item.</summary>
    public static IReadOnlyDictionary<string, string> GenerateSubstitutions(
        string templateId, string itemName, string namespaceName)
    {
        ArgumentException.ThrowIfNullOrEmpty(templateId);
        ArgumentException.ThrowIfNullOrEmpty(itemName);
        ArgumentException.ThrowIfNullOrEmpty(namespaceName);

        var safeItemName = itemName
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

        return new Dictionary<string, string>
        {
            ["$itemname$"] = itemName,
            ["$namespace$"] = namespaceName,
            ["$safeitemname$"] = safeItemName,
            ["$templateid$"] = templateId,
        };
    }
}

public sealed class ItemTemplateInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string DefaultFileName { get; init; }
    public required string Category { get; init; }
}

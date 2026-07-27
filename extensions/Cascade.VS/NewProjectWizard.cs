using System;
using System.Collections.Generic;

namespace Cascade.VS;

/// <summary>
/// Handles the new project wizard for creating Cascade UI projects.
/// Presents theme, template, and configuration options.
/// </summary>
public sealed class NewProjectWizard
{
    private WizardConfig config;

    public NewProjectWizard()
    {
        config = WizardConfig.Default;
    }

    /// <summary>The current wizard configuration.</summary>
    public WizardConfig Config => config;

    /// <summary>Updates the wizard configuration.</summary>
    public void Configure(WizardConfig newConfig)
    {
        ArgumentNullException.ThrowIfNull(newConfig);
        config = newConfig;
    }

    /// <summary>Gets the available project templates.</summary>
    public static IReadOnlyList<ProjectTemplateInfo> GetAvailableTemplates()
    {
        return
        [
            new ProjectTemplateInfo
            {
                Id = "cascade-app-blank",
                Name = "Cascade UI App (Blank)",
                Description = "An empty Cascade UI application with a single window.",
                Category = "Cascade UI",
            },
            new ProjectTemplateInfo
            {
                Id = "cascade-app-nav",
                Name = "Cascade UI App (Navigation)",
                Description = "A Cascade UI application with sidebar navigation and multiple pages.",
                Category = "Cascade UI",
            },
            new ProjectTemplateInfo
            {
                Id = "cascade-lib",
                Name = "Cascade UI Library",
                Description = "A class library for reusable Cascade UI components.",
                Category = "Cascade UI",
            },
            new ProjectTemplateInfo
            {
                Id = "cascade-controls",
                Name = "Cascade UI Control Library",
                Description = "A library for custom Cascade UI controls with theme support.",
                Category = "Cascade UI",
            },
        ];
    }

    /// <summary>Gets the available themes for project creation.</summary>
    public static IReadOnlyList<string> GetAvailableThemes()
    {
        return ["AppleTheme", "FluentTheme", "Material3Theme"];
    }

    /// <summary>Gets the available theme modes.</summary>
    public static IReadOnlyList<string> GetAvailableModes()
    {
        return ["Light", "Dark", "Auto"];
    }

    /// <summary>Generates the substitution dictionary for template expansion.</summary>
    public IReadOnlyDictionary<string, string> GenerateSubstitutions(string projectName)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectName);
        var safeProjectName = projectName
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        var localizationFlag = config.EnableLocalization ? "true" : "false";
        var aiFlag = config.EnableAi ? "true" : "false";
        return new Dictionary<string, string>
        {
            ["$projectname$"] = projectName,
            ["$safeprojectname$"] = safeProjectName,
            ["$theme$"] = config.Theme,
            ["$thememode$"] = config.ThemeMode,
            ["$template$"] = config.TemplateName,
            ["$enablelocalization$"] = localizationFlag,
            ["$enableai$"] = aiFlag,
        };
    }
}

public sealed record WizardConfig
{
    public string Theme { get; init; } = "AppleTheme";
    public string ThemeMode { get; init; } = "Light";
    public string TemplateName { get; init; } = "cascade-app-blank";
    public bool EnableLocalization { get; init; }
    public bool EnableAi { get; init; }

    public static WizardConfig Default => new();
}

public sealed class ProjectTemplateInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
}

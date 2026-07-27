using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Cascade.Generators.Tests;

/// <summary>
/// Tests for all diagnostic descriptors across the Cascade.Generators project.
/// Verifies correct IDs, severities, categories, enabled-by-default flags,
/// and uniqueness of diagnostic IDs.
/// </summary>
public class DiagnosticDescriptorTests
{
    // ── Navigation diagnostics ────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task NavigationDuplicateRoute_HasCorrectProperties()
    {
        var descriptor = NavigationDiagnostics.DuplicateRoute;

        var id = descriptor.Id;
        var severity = descriptor.DefaultSeverity;
        var category = descriptor.Category;
        var enabled = descriptor.IsEnabledByDefault;

        await TUnit.Assertions.Assert.That(id).IsEqualTo("CASCADENAV001");
        await TUnit.Assertions.Assert.That(severity).IsEqualTo(DiagnosticSeverity.Error);
        await TUnit.Assertions.Assert.That(category).IsEqualTo("Cascade.Navigation");
        await TUnit.Assertions.Assert.That(enabled).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task NavigationRouteParameterTypeMismatch_HasCorrectProperties()
    {
        var descriptor = NavigationDiagnostics.RouteParameterTypeMismatch;

        var id = descriptor.Id;
        var severity = descriptor.DefaultSeverity;
        var category = descriptor.Category;
        var enabled = descriptor.IsEnabledByDefault;

        await TUnit.Assertions.Assert.That(id).IsEqualTo("CASCADENAV002");
        await TUnit.Assertions.Assert.That(severity).IsEqualTo(DiagnosticSeverity.Error);
        await TUnit.Assertions.Assert.That(category).IsEqualTo("Cascade.Navigation");
        await TUnit.Assertions.Assert.That(enabled).IsTrue();
    }

    // ── Icon diagnostics ──────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task IconNotFound_HasCorrectProperties()
    {
        var descriptor = IconDiagnostics.IconNotFound;

        var id = descriptor.Id;
        var severity = descriptor.DefaultSeverity;
        var category = descriptor.Category;
        var enabled = descriptor.IsEnabledByDefault;

        await TUnit.Assertions.Assert.That(id).IsEqualTo("CASCADEICON001");
        await TUnit.Assertions.Assert.That(severity).IsEqualTo(DiagnosticSeverity.Warning);
        await TUnit.Assertions.Assert.That(category).IsEqualTo("Cascade.Icons");
        await TUnit.Assertions.Assert.That(enabled).IsTrue();
    }

    // ── Theme diagnostics ─────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task ThemeMissingRequiredTokenOverride_HasCorrectProperties()
    {
        var descriptor = ThemeDiagnostics.MissingRequiredTokenOverride;

        var id = descriptor.Id;
        var severity = descriptor.DefaultSeverity;
        var category = descriptor.Category;
        var enabled = descriptor.IsEnabledByDefault;

        await TUnit.Assertions.Assert.That(id).IsEqualTo("CASCADETHEME001");
        await TUnit.Assertions.Assert.That(severity).IsEqualTo(DiagnosticSeverity.Warning);
        await TUnit.Assertions.Assert.That(category).IsEqualTo("Cascade.Themes");
        await TUnit.Assertions.Assert.That(enabled).IsTrue();
    }

    // ── AOT diagnostics ───────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task AotIncompatiblePattern_HasCorrectProperties()
    {
        var descriptor = AotDiagnostics.AotIncompatiblePattern;

        var id = descriptor.Id;
        var severity = descriptor.DefaultSeverity;
        var category = descriptor.Category;
        var enabled = descriptor.IsEnabledByDefault;

        await TUnit.Assertions.Assert.That(id).IsEqualTo("CASCADEAOT001");
        await TUnit.Assertions.Assert.That(severity).IsEqualTo(DiagnosticSeverity.Error);
        await TUnit.Assertions.Assert.That(category).IsEqualTo("Cascade.AOT");
        await TUnit.Assertions.Assert.That(enabled).IsTrue();
    }

    // ── Accessibility diagnostics ─────────────────────────────────────

    [TUnit.Core.Test]
    public async Task AccessibilityMissingLabel_HasCorrectProperties()
    {
        var descriptor = AccessibilityDiagnostics.MissingAccessibleLabel;

        var id = descriptor.Id;
        var severity = descriptor.DefaultSeverity;
        var category = descriptor.Category;
        var enabled = descriptor.IsEnabledByDefault;

        await TUnit.Assertions.Assert.That(id).IsEqualTo("CASCADEA11Y001");
        await TUnit.Assertions.Assert.That(severity).IsEqualTo(DiagnosticSeverity.Warning);
        await TUnit.Assertions.Assert.That(category).IsEqualTo("Cascade.Accessibility");
        await TUnit.Assertions.Assert.That(enabled).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task AccessibilityMissingAltText_HasCorrectProperties()
    {
        var descriptor = AccessibilityDiagnostics.MissingImageAltText;

        var id = descriptor.Id;
        var severity = descriptor.DefaultSeverity;
        var category = descriptor.Category;
        var enabled = descriptor.IsEnabledByDefault;

        await TUnit.Assertions.Assert.That(id).IsEqualTo("CASCADEA11Y002");
        await TUnit.Assertions.Assert.That(severity).IsEqualTo(DiagnosticSeverity.Warning);
        await TUnit.Assertions.Assert.That(category).IsEqualTo("Cascade.Accessibility");
        await TUnit.Assertions.Assert.That(enabled).IsTrue();
    }

    // ── Localization diagnostics (new additions) ──────────────────────

    [TUnit.Core.Test]
    public async Task LocalizationHardcodedString_HasCorrectProperties()
    {
        var descriptor = LocalizationDiagnostics.HardcodedString;

        var id = descriptor.Id;
        var severity = descriptor.DefaultSeverity;
        var category = descriptor.Category;
        var enabled = descriptor.IsEnabledByDefault;

        await TUnit.Assertions.Assert.That(id).IsEqualTo("CASCADELOC001");
        await TUnit.Assertions.Assert.That(severity).IsEqualTo(DiagnosticSeverity.Warning);
        await TUnit.Assertions.Assert.That(category).IsEqualTo("Cascade.Localization");
        await TUnit.Assertions.Assert.That(enabled).IsTrue();
    }

    [TUnit.Core.Test]
    public async Task LocalizationMissingKey_HasCorrectProperties()
    {
        var descriptor = LocalizationDiagnostics.MissingLocalizationKey;

        var id = descriptor.Id;
        var severity = descriptor.DefaultSeverity;
        var category = descriptor.Category;
        var enabled = descriptor.IsEnabledByDefault;

        await TUnit.Assertions.Assert.That(id).IsEqualTo("CASCADELOC002");
        await TUnit.Assertions.Assert.That(severity).IsEqualTo(DiagnosticSeverity.Error);
        await TUnit.Assertions.Assert.That(category).IsEqualTo("Cascade.Localization");
        await TUnit.Assertions.Assert.That(enabled).IsTrue();
    }

    // ── ID uniqueness across all diagnostic files ─────────────────────

    [TUnit.Core.Test]
    public async Task AllDiagnosticIds_AreUnique()
    {
        var allDescriptors = GetAllDiagnosticDescriptors();
        var ids = allDescriptors.Select(d => d.Id).ToList();
        var duplicates = ids
            .GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        var duplicateCount = duplicates.Count;
        await TUnit.Assertions.Assert.That(duplicateCount).IsEqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task AllDiagnosticDescriptors_AreEnabledByDefault()
    {
        var allDescriptors = GetAllDiagnosticDescriptors();

        foreach (var descriptor in allDescriptors)
        {
            var enabled = descriptor.IsEnabledByDefault;
            await TUnit.Assertions.Assert.That(enabled).IsTrue();
        }
    }

    // ── Helper ────────────────────────────────────────────────────────

    private static List<DiagnosticDescriptor> GetAllDiagnosticDescriptors()
    {
        var diagnosticTypes = new[]
        {
            typeof(ReactivityDiagnostics),
            typeof(AiDiagnostics),
            typeof(FontDiagnostics),
            typeof(LocalizationDiagnostics),
            typeof(PersistDiagnostics),
            typeof(StorageDiagnostics),
            typeof(NavigationDiagnostics),
            typeof(IconDiagnostics),
            typeof(ThemeDiagnostics),
            typeof(AotDiagnostics),
            typeof(AccessibilityDiagnostics),
        };

        var descriptors = new List<DiagnosticDescriptor>();

        foreach (var type in diagnosticTypes)
        {
            var fields = type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(DiagnosticDescriptor))
                {
                    var value = field.GetValue(null) as DiagnosticDescriptor;
                    if (value is not null)
                    {
                        descriptors.Add(value);
                    }
                }
            }
        }

        return descriptors;
    }
}

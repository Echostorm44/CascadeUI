using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Cascade.Generators;

/// <summary>
/// Incremental source generator that watches for SVG files in <c>icons/</c>
/// directories as AdditionalFiles and generates static <see cref="Icon"/>
/// fields containing the SVG path data extracted from each file.
/// </summary>
internal static class IconPackGenerator
{
    /// <summary>
    /// Registers the icon pack pipeline with the incremental generator context.
    /// Called from <see cref="CascadeGenerator.Initialize"/>.
    /// </summary>
    public static void Register(IncrementalGeneratorInitializationContext context)
    {
        var svgFiles = context.AdditionalTextsProvider
            .Where(IsIconFile)
            .Select((file, ct) =>
            {
                var text = file.GetText(ct);
                if (text is null)
                {
                    return default;
                }
                return new IconFileModel(file.Path, text.ToString());
            })
            .Where(model => model.Path is not null);

        var collected = svgFiles.Collect();

        context.RegisterSourceOutput(collected, static (spc, files) =>
        {
            if (files.Length == 0)
            {
                return;
            }

            // Group SVG files by their parent directory name (the icon pack name).
            var packs = new Dictionary<string, List<IconFileModel>>();
            foreach (var file in files)
            {
                var packName = GetPackName(file.Path!);
                if (!packs.TryGetValue(packName, out var list))
                {
                    list = new List<IconFileModel>();
                    packs[packName] = list;
                }
                list.Add(file);
            }

            foreach (var pack in packs)
            {
                var source = GenerateIconPack(pack.Key, pack.Value);
                spc.AddSource($"{pack.Key}Icons.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        });

        // CASCADEICON001: validate literal icon names passed to a generated pack's
        // Get/Has/TryGet against the icons that actually exist in that pack.
        var usages = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsIconLookupCandidate(node),
                transform: static (ctx, ct) => ExtractIconUsage(ctx))
            .Where(static u => u is not null)
            .Collect();

        context.RegisterSourceOutput(usages.Combine(collected), static (spc, pair) =>
        {
            var (calls, files) = pair;
            if (calls.Length == 0 || files.Length == 0)
            {
                return;
            }

            var namesByPack = BuildNameMap(files);
            foreach (var call in calls)
            {
                if (call is null || !namesByPack.TryGetValue(call.Value.PackClass, out var names))
                {
                    continue; // not a known generated pack — leave it alone
                }

                if (!names.Contains(call.Value.IconName))
                {
                    var available = string.Join(", ", names.OrderBy(n => n, System.StringComparer.Ordinal));
                    spc.ReportDiagnostic(Diagnostic.Create(
                        IconDiagnostics.IconNotFound,
                        call.Value.Location,
                        call.Value.IconName,
                        call.Value.PackClass,
                        available));
                }
            }
        });
    }

    private readonly struct IconUsage
    {
        public IconUsage(string packClass, string iconName, Location location)
        {
            PackClass = packClass;
            IconName = iconName;
            Location = location;
        }

        public string PackClass { get; }
        public string IconName { get; }
        public Location Location { get; }
    }

    private static bool IsIconLookupCandidate(SyntaxNode node)
    {
        // `SomethingIcons.Get("x")` / `.Has("x")` / `.TryGet("x", out _)` with a string literal.
        return node is InvocationExpressionSyntax inv
            && inv.Expression is MemberAccessExpressionSyntax ma
            && ma.Name.Identifier.Text is "Get" or "Has" or "TryGet"
            && ma.Expression is IdentifierNameSyntax recv
            && recv.Identifier.Text.EndsWith("Icons", System.StringComparison.Ordinal)
            && inv.ArgumentList.Arguments.Count >= 1
            && inv.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax lit
            && lit.Token.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralToken);
    }

    private static IconUsage? ExtractIconUsage(GeneratorSyntaxContext ctx)
    {
        var inv = (InvocationExpressionSyntax)ctx.Node;
        var ma = (MemberAccessExpressionSyntax)inv.Expression;
        var recv = (IdentifierNameSyntax)ma.Expression;
        var lit = (LiteralExpressionSyntax)inv.ArgumentList.Arguments[0].Expression;
        return new IconUsage(recv.Identifier.Text, lit.Token.ValueText, lit.GetLocation());
    }

    private static Dictionary<string, HashSet<string>> BuildNameMap(
        System.Collections.Immutable.ImmutableArray<IconFileModel> files)
    {
        var map = new Dictionary<string, HashSet<string>>(System.StringComparer.Ordinal);
        foreach (var file in files)
        {
            string packClass = GetPackName(file.Path!) + "Icons";
            if (!map.TryGetValue(packClass, out var names))
            {
                names = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                map[packClass] = names;
            }
            names.Add(GetIconFieldName(file.Path!));
        }
        return map;
    }

    private static bool IsIconFile(AdditionalText file)
    {
        var path = file.Path.Replace('\\', '/');
        return path.EndsWith(".svg", System.StringComparison.OrdinalIgnoreCase) && path.Contains("icons/");
    }

    private static string GetPackName(string path)
    {
        var normalized = path.Replace('\\', '/');
        var segments = normalized.Split('/');

        // Find the segment after "icons/" — that is the pack directory name.
        // If the SVG is directly in icons/, use "Icons" as the pack name.
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i] == "icons" && i + 2 < segments.Length)
            {
                return ToPascalCase(segments[i + 1]);
            }
        }
        return "Icons";
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "Icons";
        }

        var sb = new StringBuilder();
        bool capitalizeNext = true;
        foreach (char c in name)
        {
            if (c == '-' || c == '_' || c == ' ')
            {
                capitalizeNext = true;
                continue;
            }
            if (capitalizeNext)
            {
                sb.Append(char.ToUpperInvariant(c));
                capitalizeNext = false;
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.Length > 0 ? sb.ToString() : "Icons";
    }

    // ── SVG path extraction ───────────────────────────────────────────

    /// <summary>
    /// Extracts SVG path <c>d</c> attribute values from an SVG file.
    /// Uses simple string scanning — no XML parser needed for netstandard2.0.
    /// </summary>
    internal static List<string> ExtractSvgPaths(string svgContent)
    {
        var paths = new List<string>();
        int searchStart = 0;

        while (searchStart < svgContent.Length)
        {
            int pathTagStart = svgContent.IndexOf("<path", searchStart, System.StringComparison.OrdinalIgnoreCase);
            if (pathTagStart < 0)
            {
                break;
            }

            int tagEnd = svgContent.IndexOf('>', pathTagStart);
            if (tagEnd < 0)
            {
                break;
            }

            string tagContent = svgContent.Substring(pathTagStart, tagEnd - pathTagStart + 1);
            var dValue = ExtractAttribute(tagContent, "d");
            if (dValue is not null)
            {
                paths.Add(dValue);
            }

            searchStart = tagEnd + 1;
        }

        return paths;
    }

    /// <summary>
    private static readonly char[] ViewBoxSeparators = { ' ', ',' };

    /// <summary>
    /// Extracts the viewBox dimensions from an SVG root element.
    /// Returns (width, height) or (24, 24) as default.
    /// </summary>
    internal static (float Width, float Height) ExtractViewBox(string svgContent)
    {
        int svgTagStart = svgContent.IndexOf("<svg", System.StringComparison.OrdinalIgnoreCase);
        if (svgTagStart < 0)
        {
            return (24f, 24f);
        }

        int tagEnd = svgContent.IndexOf('>', svgTagStart);
        if (tagEnd < 0)
        {
            return (24f, 24f);
        }

        string tagContent = svgContent.Substring(svgTagStart, tagEnd - svgTagStart + 1);
        var viewBox = ExtractAttribute(tagContent, "viewBox");
        if (viewBox is not null)
        {
            var parts = viewBox.Split(ViewBoxSeparators, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4 &&
                float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float w) &&
                float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float h))
            {
                return (w, h);
            }
        }

        return (24f, 24f);
    }

    private static string? ExtractAttribute(string tag, string attributeName)
    {
        // Search for attributeName="value" or attributeName='value'
        string search = attributeName + "=\"";
        int attrStart = tag.IndexOf(search, System.StringComparison.OrdinalIgnoreCase);
        if (attrStart >= 0)
        {
            int valueStart = attrStart + search.Length;
            int valueEnd = tag.IndexOf('"', valueStart);
            if (valueEnd > valueStart)
            {
                return tag.Substring(valueStart, valueEnd - valueStart);
            }
        }

        search = attributeName + "='";
        attrStart = tag.IndexOf(search, System.StringComparison.OrdinalIgnoreCase);
        if (attrStart >= 0)
        {
            int valueStart = attrStart + search.Length;
            int valueEnd = tag.IndexOf('\'', valueStart);
            if (valueEnd > valueStart)
            {
                return tag.Substring(valueStart, valueEnd - valueStart);
            }
        }

        return null;
    }

    private static string GetIconFieldName(string filePath)
    {
        var normalized = filePath.Replace('\\', '/');
        var fileName = normalized.Substring(normalized.LastIndexOf('/') + 1);
        // Strip .svg extension
        if (fileName.EndsWith(".svg", System.StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName.Substring(0, fileName.Length - 4);
        }
        return ToPascalCase(fileName);
    }

    // ── Code generation ───────────────────────────────────────────────

    private static string GenerateIconPack(string packName, List<IconFileModel> icons)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Generated by Cascade.Generators — Icon Pack Pipeline");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Icon pack generated from SVG files in the icons/{packName}/ directory.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public static partial class {packName}Icons");
        sb.AppendLine("{");

        var fieldNames = new List<string>();
        for (int i = 0; i < icons.Count; i++)
        {
            var icon = icons[i];
            var fieldName = GetIconFieldName(icon.Path!);
            var paths = ExtractSvgPaths(icon.Content);
            var viewBox = ExtractViewBox(icon.Content);

            if (paths.Count == 0)
            {
                continue;
            }

            fieldNames.Add(fieldName);

            if (i > 0)
            {
                sb.AppendLine();
            }

            if (paths.Count == 1)
            {
                sb.AppendLine($"    public static readonly global::Cascade.UI.Icon {fieldName} = new global::Cascade.UI.Icon(");
                sb.AppendLine($"        \"{EscapeString(paths[0])}\",");
                sb.AppendLine($"        new global::Cascade.UI.Size({FormatFloat(viewBox.Width)}f, {FormatFloat(viewBox.Height)}f),");
                sb.AppendLine($"        {FormatFloat(viewBox.Width)}f,");
                sb.AppendLine($"        \"{fieldName}\");");
            }
            else
            {
                sb.AppendLine($"    public static readonly global::Cascade.UI.Icon {fieldName} = new global::Cascade.UI.Icon(");
                sb.Append("        new[] { ");
                for (int p = 0; p < paths.Count; p++)
                {
                    if (p > 0)
                    {
                        sb.Append(", ");
                    }
                    sb.Append($"\"{EscapeString(paths[p])}\"");
                }
                sb.AppendLine(" },");
                sb.AppendLine($"        new global::Cascade.UI.Size({FormatFloat(viewBox.Width)}f, {FormatFloat(viewBox.Height)}f),");
                sb.AppendLine($"        {FormatFloat(viewBox.Width)}f,");
                sb.AppendLine($"        \"{fieldName}\");");
            }
        }

        // Dynamic, case-insensitive lookup by name — for data-driven icon references that
        // can't use the typed fields. CASCADEICON001 validates literal names against this set.
        sb.AppendLine();
        sb.AppendLine("    private static readonly global::System.Collections.Generic.Dictionary<string, global::Cascade.UI.Icon> _byName =");
        sb.AppendLine("        new global::System.Collections.Generic.Dictionary<string, global::Cascade.UI.Icon>(global::System.StringComparer.OrdinalIgnoreCase)");
        sb.AppendLine("        {");
        foreach (var name in fieldNames)
        {
            sb.AppendLine($"            {{ \"{name}\", {name} }},");
        }
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>All icon names available in this pack (case-insensitive).</summary>");
        sb.AppendLine("    public static global::System.Collections.Generic.IReadOnlyCollection<string> Names => _byName.Keys;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>True if an icon with the given name exists in this pack (case-insensitive).</summary>");
        sb.AppendLine("    public static bool Has(string name) => _byName.ContainsKey(name);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Looks up an icon by name (case-insensitive); returns true and the icon if found.</summary>");
        sb.AppendLine("    public static bool TryGet(string name, out global::Cascade.UI.Icon icon) => _byName.TryGetValue(name, out icon);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Looks up an icon by name (case-insensitive); throws if it does not exist.</summary>");
        sb.AppendLine("    public static global::Cascade.UI.Icon Get(string name) =>");
        sb.AppendLine("        _byName.TryGetValue(name, out var icon) ? icon");
        sb.AppendLine($"            : throw new global::System.Collections.Generic.KeyNotFoundException(\"Icon '\" + name + \"' not found in {packName}Icons.\");");

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string EscapeString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string FormatFloat(float value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    // ── Pipeline data types ───────────────────────────────────────────

    internal readonly struct IconFileModel : System.IEquatable<IconFileModel>
    {
        public string? Path { get; }
        public string Content { get; }

        public IconFileModel(string path, string content)
        {
            Path = path;
            Content = content;
        }

        public bool Equals(IconFileModel other)
        {
            return string.Equals(Path, other.Path, System.StringComparison.Ordinal)
                && string.Equals(Content, other.Content, System.StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is IconFileModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Path?.GetHashCode() ?? 0) * 397) ^ (Content?.GetHashCode() ?? 0);
            }
        }
    }
}

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
/// fields containing the path data of every shape in each file
/// (see <see cref="SvgIconParser"/>).
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
                var source = GenerateIconPack(spc, pack.Key, pack.Value);
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
            names.Add(IconNames.ToPascalCase(GetFileStem(file.Path!)));
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
                var packName = IconNames.ToPascalCase(segments[i + 1]);
                return packName.Length > 0 ? packName : "Icons";
            }
        }
        return "Icons";
    }

    private static string GetFileStem(string filePath)
    {
        var normalized = filePath.Replace('\\', '/');
        var fileName = normalized.Substring(normalized.LastIndexOf('/') + 1);
        if (fileName.EndsWith(".svg", System.StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName.Substring(0, fileName.Length - 4);
        }
        return fileName;
    }

    private static Location FileLocation(string filePath)
    {
        return Location.Create(filePath, default, default);
    }

    // ── Code generation ───────────────────────────────────────────────

    private static string GenerateIconPack(SourceProductionContext spc, string packName, List<IconFileModel> icons)
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
        foreach (var icon in icons)
        {
            var stem = GetFileStem(icon.Path!);
            var fieldName = IconNames.ToPascalCase(stem);
            var svg = SvgIconParser.Parse(icon.Content);

            foreach (var feature in svg.Unsupported)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    IconDiagnostics.UnsupportedSvgFeature,
                    FileLocation(icon.Path!),
                    stem,
                    feature));
            }

            if (svg.Paths.Count == 0)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    IconDiagnostics.NoDrawableShapes,
                    FileLocation(icon.Path!),
                    stem));
                continue;
            }

            if (fieldNames.Count > 0)
            {
                sb.AppendLine();
            }
            fieldNames.Add(fieldName);

            string accessibleName = svg.Title ?? IconNames.ToAccessibleName(stem);
            string width = FormatFloat(svg.ViewBoxWidth);
            string height = FormatFloat(svg.ViewBoxHeight);

            sb.AppendLine($"    public static readonly global::Cascade.UI.Icon {fieldName} = new global::Cascade.UI.Icon(");
            if (svg.Paths.Count == 1)
            {
                sb.AppendLine($"        \"{EscapeString(svg.Paths[0])}\",");
            }
            else
            {
                sb.Append("        new[] { ");
                for (int p = 0; p < svg.Paths.Count; p++)
                {
                    if (p > 0)
                    {
                        sb.Append(", ");
                    }
                    sb.Append($"\"{EscapeString(svg.Paths[p])}\"");
                }
                sb.AppendLine(" },");
            }
            sb.AppendLine($"        new global::Cascade.UI.Size({width}f, {height}f),");
            sb.AppendLine($"        {width}f,");
            sb.AppendLine($"        \"{EscapeString(accessibleName)}\");");
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
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");
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

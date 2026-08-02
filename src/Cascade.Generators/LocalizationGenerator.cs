using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Cascade.Generators;

/// <summary>
/// Incremental source generator that watches for <c>strings/*.json</c>
/// AdditionalFiles, parses the JSON to discover localization keys, and
/// generates a strongly typed static <c>S</c> class with nested classes
/// for each namespace and typed <see cref="LocKey"/> properties for each key.
/// </summary>
internal static class LocalizationGenerator
{
    /// <summary>
    /// Registers the localization pipeline with the incremental generator context.
    /// Called from <see cref="CascadeGenerator.Initialize"/>.
    /// </summary>
    public static void Register(IncrementalGeneratorInitializationContext context)
    {
        // Stage 1: Filter AdditionalFiles to strings/*.json locale files.
        var jsonFiles = context.AdditionalTextsProvider
            .Where(IsLocaleFile)
            .Select((file, ct) =>
            {
                var text = file.GetText(ct);
                if (text is null)
                {
                    return default;
                }
                return new LocaleFileModel(file.Path, text.ToString());
            })
            .Where(model => model.Path is not null);

        var collected = jsonFiles.Collect();

        // Stage 2: Generate the S class from all collected locale files.
        context.RegisterSourceOutput(collected, static (spc, files) =>
        {
            if (files.Length == 0)
            {
                return;
            }

            // Check if any file is flagged as a reference locale
            bool hasReferenceLocale = false;
            LocaleFileModel referenceFile = default;
            foreach (var file in files)
            {
                if (file.Path is not null)
                {
                    hasReferenceLocale = true;
                    referenceFile = file;
                    break;
                }
            }

            if (!hasReferenceLocale)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    LocalizationDiagnostics.ReferenceLocaleMissing,
                    Location.None));
                return;
            }

            // Use the first file as the reference locale to discover keys.
            var namespaces = ParseLocaleJson(referenceFile.Content);

            if (namespaces.Count == 0)
            {
                return;
            }

            var source = GenerateSClass(namespaces);
            spc.AddSource("S.g.cs", SourceText.From(source, Encoding.UTF8));
        });

        // CASCADELOC002: a string-literal LocKey (new LocKey("ns.key")) must reference a
        // key declared in the strings/*.json resources. Only validated when resources exist
        // (the typed S.Ns.Key accessors are already compile-safe; this covers raw strings).
        var usages = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsLocKeyCreation(node),
                transform: static (ctx, ct) => ExtractLocKeyUsage(ctx, ct))
            .Where(static u => u is not null)
            .Collect();

        context.RegisterSourceOutput(usages.Combine(collected), static (spc, pair) =>
        {
            var (calls, files) = pair;
            if (calls.Length == 0 || files.Length == 0)
            {
                return;
            }

            var keys = BuildKeySet(files);
            if (keys.Count == 0)
            {
                return; // no declared keys — nothing to validate against
            }

            foreach (var call in calls)
            {
                if (call is not null && !keys.Contains(call.Value.Key))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        LocalizationDiagnostics.MissingLocalizationKey, call.Value.Location, call.Value.Key));
                }
            }
        });
    }

    private readonly struct LocKeyUsage
    {
        public LocKeyUsage(string key, Location location)
        {
            Key = key;
            Location = location;
        }

        public string Key { get; }
        public Location Location { get; }
    }

    private static bool IsLocKeyCreation(SyntaxNode node)
    {
        return node is ObjectCreationExpressionSyntax oc
            && LastName(oc.Type) == "LocKey"
            && oc.ArgumentList is { Arguments.Count: >= 1 }
            && oc.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax lit
            && lit.Token.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralToken);
    }

    private static LocKeyUsage? ExtractLocKeyUsage(GeneratorSyntaxContext ctx, System.Threading.CancellationToken ct)
    {
        var oc = (ObjectCreationExpressionSyntax)ctx.Node;
        var type = ctx.SemanticModel.GetTypeInfo(oc, ct).Type;
        if (type?.ToDisplayString() != "Cascade.UI.LocKey")
        {
            return null;
        }

        var lit = (LiteralExpressionSyntax)oc.ArgumentList!.Arguments[0].Expression;
        return new LocKeyUsage(lit.Token.ValueText, lit.GetLocation());
    }

    private static HashSet<string> BuildKeySet(
        System.Collections.Immutable.ImmutableArray<LocaleFileModel> files)
    {
        var keys = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var file in files)
        {
            foreach (var ns in ParseLocaleJson(file.Content))
            {
                foreach (var entry in ns.Value)
                {
                    keys.Add(ns.Key + "." + entry.Key);
                }
            }
        }
        return keys;
    }

    private static string? LastName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        QualifiedNameSyntax q => q.Right.Identifier.Text,
        _ => null,
    };

    private static bool IsLocaleFile(AdditionalText file)
    {
        var path = file.Path.Replace('\\', '/');
        return path.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase) && path.Contains("strings/");
    }

    // ── JSON parsing ──────────────────────────────────────────────────

    /// <summary>
    /// Parses a simple JSON object with one level of nesting:
    /// <c>{ "Namespace": { "Key": "Value", ... }, ... }</c>
    /// Returns a map of namespace → list of (key, value) pairs.
    /// </summary>
    internal static Dictionary<string, List<LocaleEntry>> ParseLocaleJson(string json)
    {
        var result = new Dictionary<string, List<LocaleEntry>>();
        int index = 0;

        SkipWhitespace(json, ref index);
        if (!TryConsume(json, ref index, '{'))
        {
            return result;
        }

        while (index < json.Length)
        {
            SkipWhitespace(json, ref index);
            if (TryConsume(json, ref index, '}'))
            {
                break;
            }

            TryConsume(json, ref index, ',');
            SkipWhitespace(json, ref index);

            var namespaceName = ReadString(json, ref index);
            if (namespaceName is null)
            {
                break;
            }

            SkipWhitespace(json, ref index);
            if (!TryConsume(json, ref index, ':'))
            {
                break;
            }

            SkipWhitespace(json, ref index);
            if (!TryConsume(json, ref index, '{'))
            {
                break;
            }

            var entries = new List<LocaleEntry>();

            while (index < json.Length)
            {
                SkipWhitespace(json, ref index);
                if (TryConsume(json, ref index, '}'))
                {
                    break;
                }

                TryConsume(json, ref index, ',');
                SkipWhitespace(json, ref index);

                var key = ReadString(json, ref index);
                if (key is null)
                {
                    break;
                }

                SkipWhitespace(json, ref index);
                if (!TryConsume(json, ref index, ':'))
                {
                    break;
                }

                SkipWhitespace(json, ref index);
                var value = ReadString(json, ref index);
                if (value is null)
                {
                    break;
                }

                // Detect interpolation placeholders like {0}, {1}, etc.
                int placeholderCount = CountPlaceholders(value);
                entries.Add(new LocaleEntry(key, value, placeholderCount));
            }

            result[namespaceName] = entries;
        }

        return result;
    }

    private static int CountPlaceholders(string value)
    {
        int max = -1;
        int i = 0;
        while (i < value.Length)
        {
            if (value[i] == '{')
            {
                int end = value.IndexOf('}', i + 1);
                if (end > i + 1)
                {
                    var inner = value.Substring(i + 1, end - i - 1);
                    if (int.TryParse(inner, out int num) && num > max)
                    {
                        max = num;
                    }
                    i = end + 1;
                    continue;
                }
            }
            i++;
        }
        return max >= 0 ? max + 1 : 0;
    }

    private static void SkipWhitespace(string json, ref int index)
    {
        while (index < json.Length && char.IsWhiteSpace(json[index]))
        {
            index++;
        }
    }

    private static bool TryConsume(string json, ref int index, char expected)
    {
        if (index < json.Length && json[index] == expected)
        {
            index++;
            return true;
        }
        return false;
    }

    private static string? ReadString(string json, ref int index)
    {
        if (index >= json.Length || json[index] != '"')
        {
            return null;
        }

        index++; // skip opening quote
        var sb = new StringBuilder();
        while (index < json.Length)
        {
            char c = json[index];
            if (c == '\\' && index + 1 < json.Length)
            {
                index++;
                sb.Append(json[index]);
                index++;
                continue;
            }
            if (c == '"')
            {
                index++; // skip closing quote
                return sb.ToString();
            }
            sb.Append(c);
            index++;
        }
        return null;
    }

    // ── Code generation ───────────────────────────────────────────────

    private static string GenerateSClass(Dictionary<string, List<LocaleEntry>> namespaces)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Generated by Cascade.Generators — Localization Pipeline");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Strongly typed localization keys generated from JSON string files.");
        sb.AppendLine("/// Use <c>S.Namespace.Key</c> wherever a <see cref=\"global::Cascade.UI.LocKey\"/> is expected.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static partial class S");
        sb.AppendLine("{");

        bool firstNamespace = true;
        foreach (var kvp in namespaces)
        {
            if (!firstNamespace)
            {
                sb.AppendLine();
            }

            sb.AppendLine($"    public static partial class {kvp.Key}");
            sb.AppendLine("    {");

            for (int i = 0; i < kvp.Value.Count; i++)
            {
                var entry = kvp.Value[i];
                sb.AppendLine($"        public static global::Cascade.UI.LocKey {entry.Key} => new global::Cascade.UI.LocKey(\"{kvp.Key}.{entry.Key}\");");
            }

            sb.AppendLine("    }");
            firstNamespace = false;
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    // ── Pipeline data types ───────────────────────────────────────────

    internal readonly struct LocaleFileModel : System.IEquatable<LocaleFileModel>
    {
        public string? Path { get; }
        public string Content { get; }

        public LocaleFileModel(string path, string content)
        {
            Path = path;
            Content = content;
        }

        public bool Equals(LocaleFileModel other)
        {
            return string.Equals(Path, other.Path, System.StringComparison.Ordinal)
                && string.Equals(Content, other.Content, System.StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is LocaleFileModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Path?.GetHashCode() ?? 0) * 397) ^ (Content?.GetHashCode() ?? 0);
            }
        }
    }

    internal readonly struct LocaleEntry
    {
        public string Key { get; }
        public string Value { get; }
        public int PlaceholderCount { get; }

        public LocaleEntry(string key, string value, int placeholderCount)
        {
            Key = key;
            Value = value;
            PlaceholderCount = placeholderCount;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Cascade.Generators;

/// <summary>
/// Incremental source generator that produces a machine-readable API index
/// (<c>CascadeApiIndex.g.cs</c>) at compile time. The generated file contains
/// a complete reference of the framework API as used by the current project:
/// components, layout primitives, modifiers, theme tokens, localization keys,
/// icons, storage keys, and routes. Designed for AI agent consumption.
/// </summary>
internal static class ApiIndexGenerator
{
    /// <summary>
    /// Registers the API index pipeline with the incremental generator context.
    /// Called from <see cref="CascadeGenerator.Initialize"/>.
    /// </summary>
    public static void Register(IncrementalGeneratorInitializationContext context)
    {
        // Combine compilation (for type info) + additional texts (for locale/icon files)
        var combined = context.CompilationProvider.Combine(
            context.AdditionalTextsProvider.Collect());

        context.RegisterSourceOutput(combined, static (spc, data) =>
        {
            var (compilation, additionalTexts) = data;
            string markdown = GenerateApiIndex(compilation, additionalTexts);
            string source = WrapInCSharp(markdown);
            spc.AddSource("CascadeApiIndex.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    // ── Core generation ──────────────────────────────────────────────

    private static string GenerateApiIndex(
        Compilation compilation,
        ImmutableArray<AdditionalText> additionalTexts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Cascade API Index");
        sb.AppendLine();
        sb.AppendLine("Machine-readable API reference for the current project.");
        sb.AppendLine("Generated at compile time by Cascade.Generators.");
        sb.AppendLine();

        var cascadeAssembly = FindCascadeAssembly(compilation);

        AppendComponents(sb, compilation, cascadeAssembly);
        AppendLayoutPrimitives(sb, cascadeAssembly);
        AppendModifiers(sb, cascadeAssembly);
        AppendThemeTokens(sb, cascadeAssembly);
        AppendLocalizationKeys(sb, additionalTexts);
        AppendIcons(sb, additionalTexts);
        AppendStorageKeys(sb, compilation);
        AppendRoutes(sb, compilation);

        return sb.ToString();
    }

    // ── Assembly discovery ───────────────────────────────────────────

    private static IAssemblySymbol? FindCascadeAssembly(Compilation compilation)
    {
        foreach (var reference in compilation.References)
        {
            var symbol = compilation.GetAssemblyOrModuleSymbol(reference);
            if (symbol is IAssemblySymbol assembly && assembly.Name == "Cascade.UI")
            {
                return assembly;
            }
        }
        return null;
    }

    // ── Components ───────────────────────────────────────────────────

    private static void AppendComponents(
        StringBuilder sb, Compilation compilation, IAssemblySymbol? cascadeAssembly)
    {
        sb.AppendLine("## Components");
        sb.AppendLine();

        var componentBase = compilation.GetTypeByMetadataName("Cascade.UI.Component");
        if (componentBase is null)
        {
            sb.AppendLine("_Component base type not found in references._");
            sb.AppendLine();
            return;
        }

        var components = new List<ComponentInfo>();

        // Framework components from Cascade.UI assembly
        if (cascadeAssembly is not null)
        {
            CollectComponentsFromNamespace(cascadeAssembly.GlobalNamespace, componentBase, components);
        }

        // User-defined components from compilation
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();
            foreach (var classDecl in root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(classDecl) is INamedTypeSymbol symbol &&
                    InheritsFrom(symbol, componentBase) && !symbol.IsAbstract)
                {
                    components.Add(ExtractComponentInfo(symbol, isFramework: false));
                }
            }
        }

        components.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

        if (components.Count == 0)
        {
            sb.AppendLine("_No components found._");
            sb.AppendLine();
            return;
        }

        // Framework components
        var framework = components.Where(c => c.IsFramework).ToList();
        if (framework.Count > 0)
        {
            sb.AppendLine("### Framework Components");
            sb.AppendLine();
            foreach (var comp in framework)
            {
                AppendComponentEntry(sb, comp);
            }
        }

        // User-defined components
        var user = components.Where(c => !c.IsFramework).ToList();
        if (user.Count > 0)
        {
            sb.AppendLine("### User-Defined Components");
            sb.AppendLine();
            foreach (var comp in user)
            {
                AppendComponentEntry(sb, comp);
            }
        }
    }

    private static void AppendComponentEntry(StringBuilder sb, ComponentInfo comp)
    {
        sb.Append("- **");
        sb.Append(comp.Name);
        sb.Append("**");
        if (comp.Namespace is not null)
        {
            sb.Append(" (`");
            sb.Append(comp.Namespace);
            sb.Append('`');
            sb.Append(')');
        }
        sb.AppendLine();

        if (comp.Properties.Count > 0)
        {
            sb.Append("  Properties: ");
            sb.AppendLine(string.Join(", ", comp.Properties.Select(p => $"`{p.Name}` ({p.Type})")));
        }
        sb.AppendLine();
    }

    private static void CollectComponentsFromNamespace(
        INamespaceSymbol ns, INamedTypeSymbol componentBase, List<ComponentInfo> components)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            if (type.DeclaredAccessibility == Accessibility.Public &&
                !type.IsAbstract && InheritsFrom(type, componentBase))
            {
                components.Add(ExtractComponentInfo(type, isFramework: true));
            }
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            CollectComponentsFromNamespace(child, componentBase, components);
        }
    }

    private static ComponentInfo ExtractComponentInfo(INamedTypeSymbol type, bool isFramework)
    {
        var properties = new List<PropertyInfo>();
        foreach (var member in type.GetMembers())
        {
            if (member is IPropertySymbol prop &&
                prop.DeclaredAccessibility == Accessibility.Public &&
                !prop.IsStatic && !prop.IsIndexer &&
                prop.SetMethod is not null)
            {
                properties.Add(new PropertyInfo(prop.Name, prop.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
        }

        return new ComponentInfo(
            type.Name,
            type.ContainingNamespace?.IsGlobalNamespace == true ? null : type.ContainingNamespace?.ToDisplayString(),
            isFramework,
            properties);
    }

    // ── Layout primitives ────────────────────────────────────────────

    private static void AppendLayoutPrimitives(StringBuilder sb, IAssemblySymbol? cascadeAssembly)
    {
        sb.AppendLine("## Layout Primitives");
        sb.AppendLine();

        if (cascadeAssembly is null)
        {
            sb.AppendLine("_Cascade.UI assembly not referenced._");
            sb.AppendLine();
            return;
        }

        string[] primitiveNames = { "Row", "Column", "Grid", "Stack", "Wrap", "ZStack", "Spacer", "Divider" };
        var found = new List<(string Name, List<string> Signatures)>();

        foreach (var name in primitiveNames)
        {
            var type = FindTypeInAssembly(cascadeAssembly, "Cascade.UI", name);
            if (type is null)
            {
                type = FindTypeInAssembly(cascadeAssembly, "Cascade.UI." + name, name);
            }

            if (type is not null)
            {
                var signatures = new List<string>();
                foreach (var member in type.GetMembers())
                {
                    if (member is IMethodSymbol method &&
                        method.DeclaredAccessibility == Accessibility.Public &&
                        method.IsStatic &&
                        method.MethodKind == MethodKind.Ordinary)
                    {
                        signatures.Add(FormatMethodSignature(method));
                    }
                }

                // Also check for public constructors
                foreach (var ctor in type.Constructors)
                {
                    if (ctor.DeclaredAccessibility == Accessibility.Public && !ctor.IsImplicitlyDeclared)
                    {
                        signatures.Add(FormatCtorSignature(type.Name, ctor));
                    }
                }

                if (signatures.Count > 0)
                {
                    found.Add((name, signatures));
                }
            }
        }

        // Also look for factory methods in a LayoutFactory or similar class
        var layoutFactory = FindTypeInAssembly(cascadeAssembly, "Cascade.UI", "LayoutFactory");
        if (layoutFactory is not null)
        {
            foreach (var member in layoutFactory.GetMembers())
            {
                if (member is IMethodSymbol method &&
                    method.DeclaredAccessibility == Accessibility.Public &&
                    method.IsStatic)
                {
                    found.Add((method.Name, new List<string> { FormatMethodSignature(method) }));
                }
            }
        }

        if (found.Count == 0)
        {
            sb.AppendLine("_No layout primitives found._");
            sb.AppendLine();
            return;
        }

        foreach (var (name, sigs) in found)
        {
            sb.Append("### ");
            sb.AppendLine(name);
            foreach (var sig in sigs)
            {
                sb.Append("- `");
                sb.Append(sig);
                sb.AppendLine("`");
            }
            sb.AppendLine();
        }
    }

    // ── Modifiers ────────────────────────────────────────────────────

    private static void AppendModifiers(StringBuilder sb, IAssemblySymbol? cascadeAssembly)
    {
        sb.AppendLine("## Modifiers");
        sb.AppendLine();

        if (cascadeAssembly is null)
        {
            sb.AppendLine("_Cascade.UI assembly not referenced._");
            sb.AppendLine();
            return;
        }

        var modifiers = new List<(string Name, string Signature)>();

        // Find extension method classes
        string[] extensionClasses = { "LayoutModifiers", "VisualModifiers", "AccessibilityModifiers", "ResponsiveModifiers" };
        foreach (var className in extensionClasses)
        {
            var type = FindTypeInAssembly(cascadeAssembly, "Cascade.UI", className);
            if (type is null)
            {
                continue;
            }

            foreach (var member in type.GetMembers())
            {
                if (member is IMethodSymbol method &&
                    method.IsExtensionMethod &&
                    method.DeclaredAccessibility == Accessibility.Public)
                {
                    modifiers.Add((method.Name, FormatExtensionSignature(method)));
                }
            }
        }

        if (modifiers.Count == 0)
        {
            sb.AppendLine("_No modifiers found._");
            sb.AppendLine();
            return;
        }

        // Deduplicate by name (overloads share the same modifier name)
        var grouped = modifiers.GroupBy(m => m.Name).OrderBy(g => g.Key);

        sb.AppendLine("| Modifier | Signatures |");
        sb.AppendLine("|----------|------------|");
        foreach (var group in grouped)
        {
            var sigs = string.Join(", ", group.Select(g => $"`{g.Signature}`"));
            sb.Append("| ");
            sb.Append(group.Key);
            sb.Append(" | ");
            sb.Append(sigs);
            sb.AppendLine(" |");
        }
        sb.AppendLine();
    }

    // ── Theme tokens ─────────────────────────────────────────────────

    private static void AppendThemeTokens(StringBuilder sb, IAssemblySymbol? cascadeAssembly)
    {
        sb.AppendLine("## Theme Tokens");
        sb.AppendLine();

        if (cascadeAssembly is null)
        {
            sb.AppendLine("_Cascade.UI assembly not referenced._");
            sb.AppendLine();
            return;
        }

        var themeBase = FindTypeInAssembly(cascadeAssembly, "Cascade.UI", "CascadeTheme");
        if (themeBase is null)
        {
            sb.AppendLine("_CascadeTheme base type not found._");
            sb.AppendLine();
            return;
        }

        var themes = new List<INamedTypeSymbol>();
        CollectThemeSubclasses(cascadeAssembly.GlobalNamespace, themeBase, themes);

        if (themes.Count == 0)
        {
            sb.AppendLine("_No theme implementations found._");
            sb.AppendLine();
            return;
        }

        foreach (var theme in themes.OrderBy(t => t.Name))
        {
            sb.Append("### ");
            sb.AppendLine(theme.Name);
            sb.AppendLine();

            var tokens = new List<(string Name, string Type)>();
            foreach (var member in theme.GetMembers())
            {
                if (member is IPropertySymbol prop &&
                    prop.DeclaredAccessibility == Accessibility.Public &&
                    !prop.IsStatic && !prop.IsIndexer)
                {
                    tokens.Add((prop.Name, prop.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                }
            }

            if (tokens.Count == 0)
            {
                sb.AppendLine("_No tokens._");
            }
            else
            {
                sb.AppendLine("| Token | Type |");
                sb.AppendLine("|-------|------|");
                foreach (var (name, type) in tokens)
                {
                    sb.Append("| ");
                    sb.Append(name);
                    sb.Append(" | `");
                    sb.Append(type);
                    sb.AppendLine("` |");
                }
            }
            sb.AppendLine();
        }
    }

    private static void CollectThemeSubclasses(
        INamespaceSymbol ns, INamedTypeSymbol themeBase, List<INamedTypeSymbol> themes)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            if (type.DeclaredAccessibility == Accessibility.Public &&
                !type.IsAbstract && InheritsFrom(type, themeBase))
            {
                themes.Add(type);
            }
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            CollectThemeSubclasses(child, themeBase, themes);
        }
    }

    // ── Localization keys ────────────────────────────────────────────

    private static void AppendLocalizationKeys(StringBuilder sb, ImmutableArray<AdditionalText> additionalTexts)
    {
        sb.AppendLine("## Localization Keys");
        sb.AppendLine();

        // Find the reference locale file (en.json in strings/)
        AdditionalText? referenceFile = null;
        foreach (var text in additionalTexts)
        {
            if (text.Path is null)
            {
                continue;
            }

            string normalized = text.Path.Replace('\\', '/');
            if (normalized.EndsWith("/strings/en.json", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("/Strings/en.json", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("strings/en.json", StringComparison.OrdinalIgnoreCase))
            {
                referenceFile = text;
                break;
            }
        }

        if (referenceFile is null)
        {
            sb.AppendLine("_No strings/en.json found._");
            sb.AppendLine();
            return;
        }

        var content = referenceFile.GetText()?.ToString();
        if (string.IsNullOrWhiteSpace(content))
        {
            sb.AppendLine("_Empty locale file._");
            sb.AppendLine();
            return;
        }

        var keys = ParseLocaleKeys(content!);
        if (keys.Count == 0)
        {
            sb.AppendLine("_No keys found._");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| Key | Default Value |");
        sb.AppendLine("|-----|---------------|");
        foreach (var (key, value) in keys)
        {
            sb.Append("| S.");
            sb.Append(key);
            sb.Append(" | ");
            sb.Append(EscapeMarkdownCell(value));
            sb.AppendLine(" |");
        }
        sb.AppendLine();
    }

    /// <summary>
    /// Minimal JSON key parser for locale files. Handles nested objects
    /// producing dot-separated key paths (e.g., "Common.Save").
    /// </summary>
    private static List<(string Key, string Value)> ParseLocaleKeys(string json)
    {
        var keys = new List<(string, string)>();
        var path = new Stack<string>();
        int i = 0;

        while (i < json.Length)
        {
            if (json[i] == '"')
            {
                string key = ReadJsonString(json, ref i);
                SkipWhitespace(json, ref i);

                if (i < json.Length && json[i] == ':')
                {
                    i++;
                    SkipWhitespace(json, ref i);

                    if (i < json.Length && json[i] == '{')
                    {
                        path.Push(key);
                        i++;
                    }
                    else if (i < json.Length && json[i] == '"')
                    {
                        string value = ReadJsonString(json, ref i);
                        string fullKey = path.Count > 0
                            ? string.Join(".", path.Reverse()) + "." + key
                            : key;
                        keys.Add((fullKey, value));
                    }
                    else
                    {
                        // Skip non-string values
                        while (i < json.Length && json[i] != ',' && json[i] != '}')
                        {
                            i++;
                        }
                    }
                }
            }
            else if (json[i] == '}' && path.Count > 0)
            {
                path.Pop();
                i++;
            }
            else
            {
                i++;
            }
        }

        return keys;
    }

    private static string ReadJsonString(string json, ref int i)
    {
        i++; // skip opening quote
        var sb = new StringBuilder();
        while (i < json.Length && json[i] != '"')
        {
            if (json[i] == '\\' && i + 1 < json.Length)
            {
                i++;
                sb.Append(json[i]);
            }
            else
            {
                sb.Append(json[i]);
            }
            i++;
        }
        if (i < json.Length)
        {
            i++; // skip closing quote
        }
        return sb.ToString();
    }

    private static void SkipWhitespace(string json, ref int i)
    {
        while (i < json.Length && char.IsWhiteSpace(json[i]))
        {
            i++;
        }
    }

    // ── Icons ────────────────────────────────────────────────────────

    private static void AppendIcons(StringBuilder sb, ImmutableArray<AdditionalText> additionalTexts)
    {
        sb.AppendLine("## Icons");
        sb.AppendLine();

        var icons = new List<(string Pack, string Name)>();

        foreach (var text in additionalTexts)
        {
            if (text.Path is null)
            {
                continue;
            }

            string normalized = text.Path.Replace('\\', '/');
            if (!normalized.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Extract pack name and icon name from path: icons/{pack}/{name}.svg
            // Handle both absolute (*/icons/pack/name.svg) and relative (icons/pack/name.svg)
            int iconsIdx = normalized.LastIndexOf("/icons/", StringComparison.OrdinalIgnoreCase);
            string relative;
            if (iconsIdx >= 0)
            {
                relative = normalized.Substring(iconsIdx + "/icons/".Length);
            }
            else if (normalized.StartsWith("icons/", StringComparison.OrdinalIgnoreCase))
            {
                relative = normalized.Substring("icons/".Length);
            }
            else
            {
                continue;
            }
            int slashIdx = relative.IndexOf('/');
            if (slashIdx < 0)
            {
                continue;
            }

            string pack = relative.Substring(0, slashIdx);
            string fileName = relative.Substring(slashIdx + 1);
            string iconName = fileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                ? fileName.Substring(0, fileName.Length - 4)
                : fileName;

            icons.Add((pack, iconName));
        }

        if (icons.Count == 0)
        {
            sb.AppendLine("_No icon packs found._");
            sb.AppendLine();
            return;
        }

        var grouped = icons.GroupBy(i => i.Pack).OrderBy(g => g.Key);
        foreach (var group in grouped)
        {
            string packClass = ToPascalCase(group.Key) + "Icons";
            sb.Append("### ");
            sb.Append(packClass);
            sb.Append(" (");
            sb.Append(group.Count());
            sb.AppendLine(" icons)");
            sb.AppendLine();

            foreach (var icon in group.OrderBy(i => i.Name))
            {
                sb.Append("- `");
                sb.Append(packClass);
                sb.Append('.');
                sb.Append(ToPascalCase(icon.Name));
                sb.AppendLine("`");
            }
            sb.AppendLine();
        }
    }

    // ── Storage keys ─────────────────────────────────────────────────

    private static void AppendStorageKeys(StringBuilder sb, Compilation compilation)
    {
        sb.AppendLine("## Storage Keys");
        sb.AppendLine();

        var storageKeysAttr = compilation.GetTypeByMetadataName("Cascade.UI.StorageKeysAttribute");
        var storageKeyType = compilation.GetTypeByMetadataName("Cascade.UI.StorageKey`1");

        if (storageKeysAttr is null || storageKeyType is null)
        {
            sb.AppendLine("_Storage types not found in references._");
            sb.AppendLine();
            return;
        }

        var keys = new List<(string ClassName, string FieldName, string KeyValue, string ValueType)>();

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var classDecl in root.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
                {
                    continue;
                }

                bool hasStorageKeysAttr = false;
                foreach (var attr in classSymbol.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, storageKeysAttr))
                    {
                        hasStorageKeysAttr = true;
                        break;
                    }
                }

                if (!hasStorageKeysAttr)
                {
                    continue;
                }

                foreach (var member in classSymbol.GetMembers())
                {
                    if (member is IFieldSymbol field &&
                        field.Type is INamedTypeSymbol fieldType &&
                        fieldType.IsGenericType &&
                        SymbolEqualityComparer.Default.Equals(fieldType.OriginalDefinition, storageKeyType))
                    {
                        string valueType = fieldType.TypeArguments[0].ToDisplayString(
                            SymbolDisplayFormat.MinimallyQualifiedFormat);
                        string keyValue = field.Name; // Best guess — actual key is runtime
                        keys.Add((classSymbol.Name, field.Name, keyValue, valueType));
                    }
                }
            }
        }

        if (keys.Count == 0)
        {
            sb.AppendLine("_No storage keys declared._");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| Class | Field | Type |");
        sb.AppendLine("|-------|-------|------|");
        foreach (var (className, fieldName, _, valueType) in keys)
        {
            sb.Append("| ");
            sb.Append(className);
            sb.Append(" | `");
            sb.Append(fieldName);
            sb.Append("` | `");
            sb.Append(valueType);
            sb.AppendLine("` |");
        }
        sb.AppendLine();
    }

    // ── Routes ───────────────────────────────────────────────────────

    private static void AppendRoutes(StringBuilder sb, Compilation compilation)
    {
        sb.AppendLine("## Routes");
        sb.AppendLine();

        var routeAttr = compilation.GetTypeByMetadataName("Cascade.UI.RouteAttribute");
        if (routeAttr is null)
        {
            sb.AppendLine("_RouteAttribute not found in references._");
            sb.AppendLine();
            return;
        }

        var routes = new List<(string Pattern, string ComponentName, string? Namespace)>();

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var classDecl in root.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
                {
                    continue;
                }

                foreach (var attr in classSymbol.GetAttributes())
                {
                    if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, routeAttr))
                    {
                        continue;
                    }

                    if (attr.ConstructorArguments.Length > 0 &&
                        attr.ConstructorArguments[0].Value is string pattern)
                    {
                        string? ns = classSymbol.ContainingNamespace?.IsGlobalNamespace == true
                            ? null
                            : classSymbol.ContainingNamespace?.ToDisplayString();
                        routes.Add((pattern, classSymbol.Name, ns));
                    }
                }
            }
        }

        if (routes.Count == 0)
        {
            sb.AppendLine("_No routes declared._");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| Pattern | Component | Namespace |");
        sb.AppendLine("|---------|-----------|-----------|");
        foreach (var (pattern, name, ns) in routes.OrderBy(r => r.Pattern))
        {
            sb.Append("| `");
            sb.Append(pattern);
            sb.Append("` | ");
            sb.Append(name);
            sb.Append(" | ");
            sb.Append(ns ?? "_global_");
            sb.AppendLine(" |");
        }
        sb.AppendLine();
    }

    // ── C# wrapper ───────────────────────────────────────────────────

    private static string WrapInCSharp(string markdown)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Machine-readable API index generated by Cascade.Generators.");
        sb.AppendLine("// Regenerated on every build. Do not edit.");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Cascade.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Complete API reference for the current project, formatted as Markdown.");
        sb.AppendLine("    /// Designed for AI agent consumption. Read <see cref=\"Content\"/> to get");
        sb.AppendLine("    /// the full API index.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    internal static class CascadeApiIndex");
        sb.AppendLine("    {");
        sb.Append("        internal const string Content = @\"");
        sb.Append(markdown.Replace("\"", "\"\""));
        sb.AppendLine("\";");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
            current = current.BaseType;
        }
        return false;
    }

    private static INamedTypeSymbol? FindTypeInAssembly(
        IAssemblySymbol assembly, string namespaceName, string typeName)
    {
        var ns = FindNamespace(assembly.GlobalNamespace, namespaceName);
        if (ns is null)
        {
            return null;
        }

        return ns.GetTypeMembers(typeName).FirstOrDefault();
    }

    private static INamespaceSymbol? FindNamespace(INamespaceSymbol root, string fullyQualified)
    {
        var parts = fullyQualified.Split('.');
        var current = root;
        foreach (var part in parts)
        {
            var next = current.GetNamespaceMembers().FirstOrDefault(n => n.Name == part);
            if (next is null)
            {
                return null;
            }
            current = next;
        }
        return current;
    }

    private static string FormatMethodSignature(IMethodSymbol method)
    {
        var sb = new StringBuilder();
        sb.Append(method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        sb.Append(' ');
        sb.Append(method.Name);
        sb.Append('(');
        sb.Append(string.Join(", ", method.Parameters.Select(p =>
            $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}")));
        sb.Append(')');
        return sb.ToString();
    }

    private static string FormatCtorSignature(string typeName, IMethodSymbol ctor)
    {
        var sb = new StringBuilder();
        sb.Append("new ");
        sb.Append(typeName);
        sb.Append('(');
        sb.Append(string.Join(", ", ctor.Parameters.Select(p =>
            $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}")));
        sb.Append(')');
        return sb.ToString();
    }

    private static string FormatExtensionSignature(IMethodSymbol method)
    {
        // Skip the 'this' parameter
        var parms = method.Parameters.Skip(1).Select(p =>
            $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}");
        return $".{method.Name}({string.Join(", ", parms)})";
    }

    private static string EscapeMarkdownCell(string value)
    {
        return value.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
    }

    private static string ToPascalCase(string kebab)
    {
        var sb = new StringBuilder();
        bool capitalizeNext = true;
        foreach (char c in kebab)
        {
            if (c == '-' || c == '_')
            {
                capitalizeNext = true;
            }
            else if (capitalizeNext)
            {
                sb.Append(char.ToUpperInvariant(c));
                capitalizeNext = false;
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    // ── Model types ──────────────────────────────────────────────────

    private sealed class ComponentInfo
    {
        public string Name { get; }
        public string? Namespace { get; }
        public bool IsFramework { get; }
        public List<PropertyInfo> Properties { get; }

        public ComponentInfo(string name, string? ns, bool isFramework, List<PropertyInfo> properties)
        {
            Name = name;
            Namespace = ns;
            IsFramework = isFramework;
            Properties = properties;
        }
    }

    private sealed class PropertyInfo
    {
        public string Name { get; }
        public string Type { get; }

        public PropertyInfo(string name, string type)
        {
            Name = name;
            Type = type;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Cascade.Generators;

/// <summary>
/// Incremental source generator that validates StorageKey declarations in
/// [StorageKeys]-attributed classes. Checks for duplicate keys, invalid key
/// formats, and unsupported type parameters. Generates validated StorageKey
/// instances with compile-time key checking.
/// </summary>
internal static class StorageKeyGenerator
{
    private static readonly Regex ValidKeyPattern = new Regex(
        @"^[a-zA-Z0-9._\-]+$",
        RegexOptions.Compiled);

    /// <summary>
    /// Registers the storage key validation pipeline with the incremental generator context.
    /// Called from <see cref="CascadeGenerator.Initialize"/>.
    /// </summary>
    public static void Register(IncrementalGeneratorInitializationContext context)
    {
        // Stage 1: Find class declarations with [StorageKeys] attribute
        var storageKeyClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: IsStorageKeysCandidate,
                transform: AnalyzeStorageKeysClass)
            .Where(model => model is not null);

        // Stage 2: Find classes with StorageKey<T> fields but no [StorageKeys] attribute
        var unattributedClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: IsClassWithFields,
                transform: FindUnattributedStorageKeyClasses)
            .Where(model => model is not null);

        // Stage 3: Report diagnostics and generate validation code
        context.RegisterSourceOutput(storageKeyClasses, static (spc, model) =>
        {
            if (model is null)
            {
                return;
            }

            ReportDiagnostics(spc, model);

            if (model.Fields.Count > 0 && model.Diagnostics.Count == 0)
            {
                var source = GenerateValidatedKeys(model);
                var hintName = $"{model.ClassName}.StorageKeys.g.cs";
                spc.AddSource(hintName, source);
            }
        });

        context.RegisterSourceOutput(unattributedClasses, static (spc, model) =>
        {
            if (model is null)
            {
                return;
            }

            spc.ReportDiagnostic(Diagnostic.Create(
                StorageDiagnostics.MissingAttribute,
                model.Location,
                model.ClassName));
        });
    }

    // ── Predicates ────────────────────────────────────────────────────

    private static bool IsStorageKeysCandidate(SyntaxNode node, CancellationToken ct)
    {
        return node is ClassDeclarationSyntax classDecl
            && classDecl.AttributeLists.Count > 0;
    }

    private static bool IsClassWithFields(SyntaxNode node, CancellationToken ct)
    {
        return node is ClassDeclarationSyntax classDecl
            && classDecl.Members.Any(m => m is FieldDeclarationSyntax);
    }

    // ── Analysis ──────────────────────────────────────────────────────

    private static StorageKeysModel? AnalyzeStorageKeysClass(
        GeneratorSyntaxContext context, CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl, ct);

        if (classSymbol is null)
        {
            return null;
        }

        // Check for [StorageKeys] attribute
        bool hasAttribute = classSymbol.GetAttributes().Any(attr =>
            attr.AttributeClass?.Name == "StorageKeysAttribute"
            && attr.AttributeClass.ContainingNamespace?.ToDisplayString() == "Cascade.UI");

        if (!hasAttribute)
        {
            return null;
        }

        var fields = new List<StorageKeyFieldInfo>();
        var diagnostics = new List<StorageKeyDiagnostic>();
        var seenKeys = new Dictionary<string, string>();

        foreach (var member in classDecl.Members)
        {
            if (member is not FieldDeclarationSyntax fieldDecl)
            {
                continue;
            }

            var fieldType = semanticModel.GetTypeInfo(fieldDecl.Declaration.Type, ct).Type;
            if (fieldType is not INamedTypeSymbol namedType)
            {
                continue;
            }

            if (namedType.Name != "StorageKey" || namedType.TypeArguments.Length != 1)
            {
                continue;
            }

            var containingNs = namedType.ContainingNamespace?.ToDisplayString();
            if (containingNs != "Cascade.UI")
            {
                continue;
            }

            var typeArg = namedType.TypeArguments[0];

            foreach (var variable in fieldDecl.Declaration.Variables)
            {
                string fieldName = variable.Identifier.Text;
                string? keyValue = ExtractKeyString(variable, semanticModel, ct);

                if (keyValue is null)
                {
                    continue;
                }

                // Validate key format
                if (keyValue.Length == 0 || !ValidKeyPattern.IsMatch(keyValue))
                {
                    diagnostics.Add(new StorageKeyDiagnostic(
                        StorageKeyDiagnosticKind.InvalidFormat,
                        keyValue,
                        fieldName,
                        null,
                        variable.GetLocation()));
                    continue;
                }

                // Check for duplicate keys
                if (seenKeys.TryGetValue(keyValue, out var existingField))
                {
                    diagnostics.Add(new StorageKeyDiagnostic(
                        StorageKeyDiagnosticKind.Duplicate,
                        keyValue,
                        existingField,
                        fieldName,
                        variable.GetLocation()));
                    continue;
                }

                seenKeys[keyValue] = fieldName;

                // Check for unsupported type
                if (!IsSupportedStorageType(typeArg))
                {
                    diagnostics.Add(new StorageKeyDiagnostic(
                        StorageKeyDiagnosticKind.UnsupportedType,
                        typeArg.ToDisplayString(),
                        fieldName,
                        null,
                        variable.GetLocation()));
                }

                fields.Add(new StorageKeyFieldInfo(
                    fieldName,
                    keyValue,
                    typeArg.ToDisplayString()));
            }
        }

        string? ns = classSymbol.ContainingNamespace?.IsGlobalNamespace == true
            ? null
            : classSymbol.ContainingNamespace?.ToDisplayString();

        return new StorageKeysModel(
            classSymbol.Name,
            ns,
            fields,
            diagnostics);
    }

    private static UnattributedStorageKeyModel? FindUnattributedStorageKeyClasses(
        GeneratorSyntaxContext context, CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl, ct);

        if (classSymbol is null)
        {
            return null;
        }

        // Skip if already has [StorageKeys]
        bool hasAttribute = classSymbol.GetAttributes().Any(attr =>
            attr.AttributeClass?.Name == "StorageKeysAttribute"
            && attr.AttributeClass.ContainingNamespace?.ToDisplayString() == "Cascade.UI");

        if (hasAttribute)
        {
            return null;
        }

        // Check if any field is a StorageKey<T>
        foreach (var member in classDecl.Members)
        {
            if (member is not FieldDeclarationSyntax fieldDecl)
            {
                continue;
            }

            var fieldType = semanticModel.GetTypeInfo(fieldDecl.Declaration.Type, ct).Type;
            if (fieldType is not INamedTypeSymbol namedType)
            {
                continue;
            }

            if (namedType.Name == "StorageKey"
                && namedType.TypeArguments.Length == 1
                && namedType.ContainingNamespace?.ToDisplayString() == "Cascade.UI")
            {
                return new UnattributedStorageKeyModel(
                    classSymbol.Name,
                    classDecl.GetLocation());
            }
        }

        return null;
    }

    private static string? ExtractKeyString(
        VariableDeclaratorSyntax variable,
        SemanticModel semanticModel,
        CancellationToken ct)
    {
        if (variable.Initializer?.Value is null)
        {
            return null;
        }

        var initValue = variable.Initializer.Value;

        // Handle: new StorageKey<T>("key") or new StorageKey<T>("key", fallback)
        if (initValue is ObjectCreationExpressionSyntax creation
            && creation.ArgumentList?.Arguments.Count > 0)
        {
            var firstArg = creation.ArgumentList.Arguments[0].Expression;
            var constValue = semanticModel.GetConstantValue(firstArg, ct);
            if (constValue.HasValue && constValue.Value is string s)
            {
                return s;
            }
        }

        // Handle: new("key") or new("key", fallback)
        if (initValue is ImplicitObjectCreationExpressionSyntax implicitCreation
            && implicitCreation.ArgumentList?.Arguments.Count > 0)
        {
            var firstArg = implicitCreation.ArgumentList.Arguments[0].Expression;
            var constValue = semanticModel.GetConstantValue(firstArg, ct);
            if (constValue.HasValue && constValue.Value is string s)
            {
                return s;
            }
        }

        return null;
    }

    private static bool IsSupportedStorageType(ITypeSymbol type)
    {
        // Primitives and string are always supported
        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
            case SpecialType.System_String:
            case SpecialType.System_DateTime:
                return true;
        }

        // Enums are supported
        if (type.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        // Types implementing IStorageSerializable<T> are supported
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.Name == "IStorageSerializable"
                && iface.ContainingNamespace?.ToDisplayString() == "Cascade.UI")
            {
                return true;
            }
        }

        // Arrays and lists of supported types
        if (type is IArrayTypeSymbol arrayType)
        {
            return IsSupportedStorageType(arrayType.ElementType);
        }

        if (type is INamedTypeSymbol namedType
            && namedType.IsGenericType
            && namedType.TypeArguments.Length == 1)
        {
            string name = namedType.Name;
            if (name == "List" || name == "IReadOnlyList" || name == "IList")
            {
                return IsSupportedStorageType(namedType.TypeArguments[0]);
            }
        }

        return false;
    }

    // ── Diagnostics ───────────────────────────────────────────────────

    private static void ReportDiagnostics(SourceProductionContext spc, StorageKeysModel model)
    {
        foreach (var diag in model.Diagnostics)
        {
            switch (diag.Kind)
            {
                case StorageKeyDiagnosticKind.Duplicate:
                    spc.ReportDiagnostic(Diagnostic.Create(
                        StorageDiagnostics.DuplicateKey,
                        diag.Location,
                        diag.KeyOrType,
                        diag.FieldName,
                        diag.SecondFieldName));
                    break;

                case StorageKeyDiagnosticKind.InvalidFormat:
                    spc.ReportDiagnostic(Diagnostic.Create(
                        StorageDiagnostics.InvalidKeyFormat,
                        diag.Location,
                        diag.KeyOrType,
                        diag.FieldName));
                    break;

                case StorageKeyDiagnosticKind.UnsupportedType:
                    spc.ReportDiagnostic(Diagnostic.Create(
                        StorageDiagnostics.UnsupportedType,
                        diag.Location,
                        diag.KeyOrType,
                        diag.FieldName));
                    break;
            }
        }
    }

    // ── Code generation ───────────────────────────────────────────────

    private static string GenerateValidatedKeys(StorageKeysModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Generated by Cascade.Generators — StorageKey Validation Pipeline");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (model.Namespace is not null)
        {
            sb.AppendLine($"namespace {model.Namespace}");
            sb.AppendLine("{");
        }

        string indent = model.Namespace is not null ? "    " : "";
        sb.AppendLine($"{indent}partial class {model.ClassName}");
        sb.AppendLine($"{indent}{{");

        // Generate a static validation method that ensures uniqueness at compile time
        sb.AppendLine($"{indent}    /// <summary>");
        sb.AppendLine($"{indent}    /// Compile-time validated storage key set. All keys verified unique.");
        sb.AppendLine($"{indent}    /// </summary>");
        sb.AppendLine($"{indent}    internal static readonly int __StorageKeyCount = {model.Fields.Count};");

        sb.AppendLine($"{indent}}}");

        if (model.Namespace is not null)
        {
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    // ── Model types ───────────────────────────────────────────────────

    private sealed class StorageKeysModel
    {
        public string ClassName { get; }
        public string? Namespace { get; }
        public List<StorageKeyFieldInfo> Fields { get; }
        public List<StorageKeyDiagnostic> Diagnostics { get; }

        public StorageKeysModel(
            string className,
            string? ns,
            List<StorageKeyFieldInfo> fields,
            List<StorageKeyDiagnostic> diagnostics)
        {
            ClassName = className;
            Namespace = ns;
            Fields = fields;
            Diagnostics = diagnostics;
        }
    }

    private sealed class StorageKeyFieldInfo
    {
        public string FieldName { get; }
        public string KeyValue { get; }
        public string TypeArgument { get; }

        public StorageKeyFieldInfo(string fieldName, string keyValue, string typeArgument)
        {
            FieldName = fieldName;
            KeyValue = keyValue;
            TypeArgument = typeArgument;
        }
    }

    private enum StorageKeyDiagnosticKind
    {
        Duplicate,
        InvalidFormat,
        UnsupportedType
    }

    private sealed class StorageKeyDiagnostic
    {
        public StorageKeyDiagnosticKind Kind { get; }
        public string KeyOrType { get; }
        public string FieldName { get; }
        public string? SecondFieldName { get; }
        public Location Location { get; }

        public StorageKeyDiagnostic(
            StorageKeyDiagnosticKind kind,
            string keyOrType,
            string fieldName,
            string? secondFieldName,
            Location location)
        {
            Kind = kind;
            KeyOrType = keyOrType;
            FieldName = fieldName;
            SecondFieldName = secondFieldName;
            Location = location;
        }
    }

    private sealed class UnattributedStorageKeyModel
    {
        public string ClassName { get; }
        public Location Location { get; }

        public UnattributedStorageKeyModel(string className, Location location)
        {
            ClassName = className;
            Location = location;
        }
    }
}

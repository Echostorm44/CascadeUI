using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cascade.Generators;

/// <summary>
/// Incremental source generator that processes [PersistState]-attributed fields
/// in Component subclasses. Generates save/restore code that serializes the field
/// to LocalStorage on changes, with configurable debouncing. Restore happens in
/// a generated partial OnMounted() method.
/// </summary>
internal static class PersistStateGenerator
{
    /// <summary>
    /// Registers the persist state pipeline with the incremental generator context.
    /// Called from <see cref="CascadeGenerator.Initialize"/>.
    /// </summary>
    public static void Register(IncrementalGeneratorInitializationContext context)
    {
        var componentModels = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: IsPersistStateCandidate,
                transform: AnalyzePersistStateFields)
            .Where(model => model is not null);

        context.RegisterSourceOutput(componentModels, static (spc, model) =>
        {
            if (model is null)
            {
                return;
            }

            ReportDiagnostics(spc, model);

            if (model.Fields.Count > 0 && !model.HasErrors)
            {
                var source = GeneratePersistenceCode(model);
                var hintName = $"{model.ClassName}.PersistState.g.cs";
                spc.AddSource(hintName, source);
            }
        });
    }

    // ── Predicates ────────────────────────────────────────────────────

    private static bool IsPersistStateCandidate(SyntaxNode node, CancellationToken ct)
    {
        // Look for class declarations that have fields with attributes
        if (node is not ClassDeclarationSyntax classDecl)
        {
            return false;
        }

        // Quick check: any field has an attribute list
        foreach (var member in classDecl.Members)
        {
            if (member is FieldDeclarationSyntax fieldDecl
                && fieldDecl.AttributeLists.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    // ── Analysis ──────────────────────────────────────────────────────

    private static PersistStateModel? AnalyzePersistStateFields(
        GeneratorSyntaxContext context, CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl, ct);

        if (classSymbol is null)
        {
            return null;
        }

        // Check if it's a Component subclass
        if (!IsComponentSubclass(classSymbol))
        {
            return null;
        }

        var fields = new List<PersistFieldInfo>();
        var diagnostics = new List<PersistDiagnosticInfo>();
        bool hasErrors = false;

        // Collect all field names for key reference validation
        var allFieldNames = new HashSet<string>();
        foreach (var member in classSymbol.GetMembers())
        {
            if (member is IFieldSymbol)
            {
                allFieldNames.Add(member.Name);
            }
        }

        foreach (var member in classDecl.Members)
        {
            if (member is not FieldDeclarationSyntax fieldDecl)
            {
                continue;
            }

            // Check for [PersistState] attribute
            var persistAttr = GetPersistStateAttribute(fieldDecl, semanticModel, ct);
            if (persistAttr is null)
            {
                continue;
            }

            foreach (var variable in fieldDecl.Declaration.Variables)
            {
                string fieldName = variable.Identifier.Text;
                var fieldSymbol = semanticModel.GetDeclaredSymbol(variable, ct) as IFieldSymbol;

                if (fieldSymbol is null)
                {
                    continue;
                }

                // CS-CASCADE-PERS-003: readonly field
                if (fieldSymbol.IsReadOnly)
                {
                    diagnostics.Add(new PersistDiagnosticInfo(
                        PersistDiagnosticKind.ReadonlyField,
                        fieldName,
                        null,
                        variable.GetLocation()));
                    hasErrors = true;
                    continue;
                }

                string typeName = fieldSymbol.Type.ToDisplayString();

                // CS-CASCADE-PERS-001: non-serializable type
                if (!IsSupportedPersistType(fieldSymbol.Type))
                {
                    diagnostics.Add(new PersistDiagnosticInfo(
                        PersistDiagnosticKind.NonSerializableType,
                        fieldName,
                        typeName,
                        variable.GetLocation()));
                    hasErrors = true;
                    continue;
                }

                // Extract Key and When from the attribute
                string? keyExpression = ExtractNamedArgument(persistAttr, "Key");
                string persistWhen = ExtractNamedArgument(persistAttr, "When") ?? "Immediate";

                // CS-CASCADE-PERS-002: missing key field reference
                if (keyExpression is not null && !allFieldNames.Contains(keyExpression))
                {
                    diagnostics.Add(new PersistDiagnosticInfo(
                        PersistDiagnosticKind.MissingKey,
                        fieldName,
                        keyExpression,
                        variable.GetLocation()));
                    hasErrors = true;
                    continue;
                }

                fields.Add(new PersistFieldInfo(
                    fieldName,
                    typeName,
                    keyExpression,
                    persistWhen));
            }
        }

        if (fields.Count == 0 && diagnostics.Count == 0)
        {
            return null;
        }

        string? ns = classSymbol.ContainingNamespace?.IsGlobalNamespace == true
            ? null
            : classSymbol.ContainingNamespace?.ToDisplayString();

        return new PersistStateModel(
            classSymbol.Name,
            ns,
            fields,
            diagnostics,
            hasErrors);
    }

    private static bool IsComponentSubclass(INamedTypeSymbol classSymbol)
    {
        var baseType = classSymbol.BaseType;
        while (baseType is not null)
        {
            if (baseType.Name == "Component"
                && baseType.ContainingNamespace?.ToDisplayString() == "Cascade.UI")
            {
                return true;
            }
            baseType = baseType.BaseType;
        }
        return false;
    }

    private static AttributeData? GetPersistStateAttribute(
        FieldDeclarationSyntax fieldDecl,
        SemanticModel semanticModel,
        CancellationToken ct)
    {
        foreach (var variable in fieldDecl.Declaration.Variables)
        {
            var fieldSymbol = semanticModel.GetDeclaredSymbol(variable, ct) as IFieldSymbol;
            if (fieldSymbol is null)
            {
                continue;
            }

            foreach (var attr in fieldSymbol.GetAttributes())
            {
                if (attr.AttributeClass?.Name == "PersistStateAttribute"
                    && attr.AttributeClass.ContainingNamespace?.ToDisplayString() == "Cascade.UI")
                {
                    return attr;
                }
            }
        }

        return null;
    }

    private static string? ExtractNamedArgument(AttributeData attribute, string name)
    {
        foreach (var namedArg in attribute.NamedArguments)
        {
            if (namedArg.Key == name && namedArg.Value.Value is not null)
            {
                var value = namedArg.Value.Value;
                if (value is string s)
                {
                    return s;
                }
                // For enum values, return the name portion
                return value.ToString();
            }
        }
        return null;
    }

    private static bool IsSupportedPersistType(ITypeSymbol type)
    {
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

        if (type.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        foreach (var iface in type.AllInterfaces)
        {
            if (iface.Name == "IStorageSerializable"
                && iface.ContainingNamespace?.ToDisplayString() == "Cascade.UI")
            {
                return true;
            }
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return IsSupportedPersistType(arrayType.ElementType);
        }

        if (type is INamedTypeSymbol namedType
            && namedType.IsGenericType
            && namedType.TypeArguments.Length == 1)
        {
            string name = namedType.Name;
            if (name == "List" || name == "IReadOnlyList" || name == "IList")
            {
                return IsSupportedPersistType(namedType.TypeArguments[0]);
            }
        }

        return false;
    }

    // ── Diagnostics ───────────────────────────────────────────────────

    private static void ReportDiagnostics(SourceProductionContext spc, PersistStateModel model)
    {
        foreach (var diag in model.Diagnostics)
        {
            switch (diag.Kind)
            {
                case PersistDiagnosticKind.NonSerializableType:
                    spc.ReportDiagnostic(Diagnostic.Create(
                        PersistDiagnostics.NonSerializableType,
                        diag.Location,
                        diag.FieldName,
                        diag.Detail ?? "unknown"));
                    break;

                case PersistDiagnosticKind.MissingKey:
                    spc.ReportDiagnostic(Diagnostic.Create(
                        PersistDiagnostics.MissingKey,
                        diag.Location,
                        diag.FieldName,
                        diag.Detail ?? "unknown"));
                    break;

                case PersistDiagnosticKind.ReadonlyField:
                    spc.ReportDiagnostic(Diagnostic.Create(
                        PersistDiagnostics.ReadonlyField,
                        diag.Location,
                        diag.FieldName));
                    break;
            }
        }
    }

    // ── Code generation ───────────────────────────────────────────────

    private static string GeneratePersistenceCode(PersistStateModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Generated by Cascade.Generators — PersistState Pipeline");
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

        // Generate debounce delay constant
        sb.AppendLine($"{indent}    private const int __PersistDebounceMs = 250;");
        sb.AppendLine();

        // Generate storage key constants
        foreach (var field in model.Fields)
        {
            string keyBase = $"cascade.persist.{model.ClassName}.{field.FieldName}";
            if (field.KeyExpression is not null)
            {
                sb.AppendLine($"{indent}    private string __PersistKey_{field.FieldName} => $\"{keyBase}.{{{field.KeyExpression}}}\";");
            }
            else
            {
                sb.AppendLine($"{indent}    private const string __PersistKey_{field.FieldName} = \"{keyBase}\";");
            }
        }

        sb.AppendLine();

        // Generate debounce timer fields for Immediate persist
        foreach (var field in model.Fields)
        {
            if (field.PersistWhen == "Immediate")
            {
                sb.AppendLine($"{indent}    private global::System.Threading.CancellationTokenSource? __persistCts_{field.FieldName};");
            }
        }

        sb.AppendLine();

        // Generate save methods for each field
        foreach (var field in model.Fields)
        {
            sb.AppendLine($"{indent}    private void __PersistSave_{field.FieldName}()");
            sb.AppendLine($"{indent}    {{");

            if (field.PersistWhen == "Immediate")
            {
                // Debounced save
                sb.AppendLine($"{indent}        __persistCts_{field.FieldName}?.Cancel();");
                sb.AppendLine($"{indent}        __persistCts_{field.FieldName} = new global::System.Threading.CancellationTokenSource();");
                sb.AppendLine($"{indent}        var token = __persistCts_{field.FieldName}.Token;");
                sb.AppendLine($"{indent}        global::System.Threading.Tasks.Task.Run(async () =>");
                sb.AppendLine($"{indent}        {{");
                sb.AppendLine($"{indent}            await global::System.Threading.Tasks.Task.Delay(__PersistDebounceMs, token).ConfigureAwait(false);");
                sb.AppendLine($"{indent}            if (!token.IsCancellationRequested)");
                sb.AppendLine($"{indent}            {{");
                sb.AppendLine($"{indent}                global::Cascade.UI.LocalStorage.Set<{field.TypeName}>(__PersistKey_{field.FieldName}, {field.FieldName});");
                sb.AppendLine($"{indent}            }}");
                sb.AppendLine($"{indent}        }}, token);");
            }
            else
            {
                sb.AppendLine($"{indent}        global::Cascade.UI.LocalStorage.Set<{field.TypeName}>(__PersistKey_{field.FieldName}, {field.FieldName});");
            }

            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
        }

        // Generate restore method (partial OnMounted hook)
        sb.AppendLine($"{indent}    partial void __RestorePersistedState();");
        sb.AppendLine();

        // Generate the implementation of the restore method
        sb.AppendLine($"{indent}    private void __RestorePersistedStateImpl()");
        sb.AppendLine($"{indent}    {{");
        foreach (var field in model.Fields)
        {
            sb.AppendLine($"{indent}        {field.FieldName} = global::Cascade.UI.LocalStorage.Get<{field.TypeName}>(__PersistKey_{field.FieldName}, {field.FieldName});");
        }
        sb.AppendLine($"{indent}    }}");

        sb.AppendLine($"{indent}}}");

        if (model.Namespace is not null)
        {
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    // ── Model types ───────────────────────────────────────────────────

    private sealed class PersistStateModel
    {
        public string ClassName { get; }
        public string? Namespace { get; }
        public List<PersistFieldInfo> Fields { get; }
        public List<PersistDiagnosticInfo> Diagnostics { get; }
        public bool HasErrors { get; }

        public PersistStateModel(
            string className,
            string? ns,
            List<PersistFieldInfo> fields,
            List<PersistDiagnosticInfo> diagnostics,
            bool hasErrors)
        {
            ClassName = className;
            Namespace = ns;
            Fields = fields;
            Diagnostics = diagnostics;
            HasErrors = hasErrors;
        }
    }

    private sealed class PersistFieldInfo
    {
        public string FieldName { get; }
        public string TypeName { get; }
        public string? KeyExpression { get; }
        public string PersistWhen { get; }

        public PersistFieldInfo(string fieldName, string typeName, string? keyExpression, string persistWhen)
        {
            FieldName = fieldName;
            TypeName = typeName;
            KeyExpression = keyExpression;
            PersistWhen = persistWhen;
        }
    }

    private enum PersistDiagnosticKind
    {
        NonSerializableType,
        MissingKey,
        ReadonlyField
    }

    private sealed class PersistDiagnosticInfo
    {
        public PersistDiagnosticKind Kind { get; }
        public string FieldName { get; }
        public string? Detail { get; }
        public Location Location { get; }

        public PersistDiagnosticInfo(PersistDiagnosticKind kind, string fieldName, string? detail, Location location)
        {
            Kind = kind;
            FieldName = fieldName;
            Detail = detail;
            Location = location;
        }
    }
}

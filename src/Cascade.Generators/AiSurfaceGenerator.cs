using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cascade.Generators;

/// <summary>
/// Incremental source generator that processes [AiSurface] classes and
/// [AiCapability] methods. Generates MCP tool registration code that exposes
/// annotated methods as AI-callable tools with name, description, and parameter
/// schema derived from the method signature. A static AiToolRegistry is generated
/// containing all discovered capabilities.
/// </summary>
internal static class AiSurfaceGenerator
{
    /// <summary>
    /// Registers the AI surface pipeline with the incremental generator context.
    /// Called from <see cref="CascadeGenerator.Initialize"/>.
    /// </summary>
    public static void Register(IncrementalGeneratorInitializationContext context)
    {
        // Stage 1: Find classes with [AiSurface] or methods with [AiCapability]
        var aiModels = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: IsAiSurfaceCandidate,
                transform: AnalyzeAiSurface)
            .Where(model => model is not null);

        // Collect all models for cross-class duplicate detection
        var collected = aiModels.Collect();

        context.RegisterSourceOutput(collected, static (spc, models) =>
        {
            var allTools = new List<AiToolInfo>();
            var toolNameToSource = new Dictionary<string, string>();

            foreach (var model in models)
            {
                if (model is null)
                {
                    continue;
                }

                ReportDiagnostics(spc, model);

                foreach (var tool in model.Tools)
                {
                    // Check for cross-class duplicate tool names
                    string qualifiedSource = $"{model.FullyQualifiedName}.{tool.MethodName}";
                    if (toolNameToSource.TryGetValue(tool.ToolName, out var existingSource))
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            AiDiagnostics.DuplicateToolName,
                            tool.Location,
                            tool.ToolName,
                            existingSource,
                            qualifiedSource));
                    }
                    else
                    {
                        toolNameToSource[tool.ToolName] = qualifiedSource;
                        allTools.Add(tool);
                    }
                }
            }

            if (allTools.Count > 0)
            {
                var source = GenerateRegistry(allTools);
                spc.AddSource("AiToolRegistry.g.cs", source);
            }
        });
    }

    // ── Predicates ────────────────────────────────────────────────────

    private static bool IsAiSurfaceCandidate(SyntaxNode node, CancellationToken ct)
    {
        if (node is not ClassDeclarationSyntax classDecl)
        {
            return false;
        }

        // Has class-level attributes (possibly [AiSurface])
        if (classDecl.AttributeLists.Count > 0)
        {
            return true;
        }

        // Or has methods with attributes (possibly [AiCapability])
        foreach (var member in classDecl.Members)
        {
            if (member is MethodDeclarationSyntax method
                && method.AttributeLists.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    // ── Analysis ──────────────────────────────────────────────────────

    private static AiSurfaceModel? AnalyzeAiSurface(
        GeneratorSyntaxContext context, CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl, ct);

        if (classSymbol is null)
        {
            return null;
        }

        // Check for [AiSurface] attribute on class
        bool hasAiSurface = classSymbol.GetAttributes().Any(attr =>
            attr.AttributeClass?.Name == "AiSurfaceAttribute"
            && attr.AttributeClass.ContainingNamespace?.ToDisplayString() == "Cascade.UI");

        string? surfaceDescription = null;
        if (hasAiSurface)
        {
            var surfaceAttr = classSymbol.GetAttributes().First(attr =>
                attr.AttributeClass?.Name == "AiSurfaceAttribute"
                && attr.AttributeClass.ContainingNamespace?.ToDisplayString() == "Cascade.UI");
            foreach (var namedArg in surfaceAttr.NamedArguments)
            {
                if (namedArg.Key == "Description" && namedArg.Value.Value is string desc)
                {
                    surfaceDescription = desc;
                }
            }
        }

        var tools = new List<AiToolInfo>();
        var diagnostics = new List<AiDiagnosticInfo>();

        foreach (var member in classDecl.Members)
        {
            if (member is not MethodDeclarationSyntax methodDecl)
            {
                continue;
            }

            var methodSymbol = semanticModel.GetDeclaredSymbol(methodDecl, ct);
            if (methodSymbol is null)
            {
                continue;
            }

            var capabilityAttr = methodSymbol.GetAttributes().FirstOrDefault(attr =>
                attr.AttributeClass?.Name == "AiCapabilityAttribute"
                && attr.AttributeClass.ContainingNamespace?.ToDisplayString() == "Cascade.UI");

            if (capabilityAttr is null)
            {
                continue;
            }

            // CS-CASCADE-AI-001: [AiCapability] in non-[AiSurface] class
            if (!hasAiSurface)
            {
                diagnostics.Add(new AiDiagnosticInfo(
                    AiDiagnosticKind.MissingSurface,
                    methodSymbol.Name,
                    classSymbol.Name,
                    null,
                    methodDecl.GetLocation()));
                continue;
            }

            // CS-CASCADE-AI-004: must be public
            if (methodSymbol.DeclaredAccessibility != Accessibility.Public)
            {
                diagnostics.Add(new AiDiagnosticInfo(
                    AiDiagnosticKind.InvalidAccessibility,
                    methodSymbol.Name,
                    null,
                    null,
                    methodDecl.GetLocation()));
                continue;
            }

            // Extract tool name and description from attribute.
            // Description comes from the constructor argument (position 0) since
            // the property setter is private. Name is always a named argument.
            string toolName = methodSymbol.Name;
            string? toolDescription = null;

            if (capabilityAttr.ConstructorArguments.Length > 0
                && capabilityAttr.ConstructorArguments[0].Value is string ctorDesc)
            {
                toolDescription = ctorDesc;
            }

            foreach (var namedArg in capabilityAttr.NamedArguments)
            {
                if (namedArg.Key == "Name" && namedArg.Value.Value is string name)
                {
                    toolName = name;
                }
                else if (namedArg.Key == "Description" && namedArg.Value.Value is string desc)
                {
                    toolDescription = desc;
                }
            }

            // Build parameter schema
            var parameters = new List<AiToolParameterInfo>();
            bool hasUnsupportedParam = false;

            foreach (var param in methodSymbol.Parameters)
            {
                string paramTypeName = param.Type.ToDisplayString();
                string schemaType = GetJsonSchemaType(param.Type);

                if (schemaType == "unsupported")
                {
                    diagnostics.Add(new AiDiagnosticInfo(
                        AiDiagnosticKind.UnsupportedParameterType,
                        methodSymbol.Name,
                        param.Name,
                        paramTypeName,
                        methodDecl.GetLocation()));
                    hasUnsupportedParam = true;
                    continue;
                }

                parameters.Add(new AiToolParameterInfo(
                    param.Name,
                    paramTypeName,
                    schemaType,
                    param.HasExplicitDefaultValue));
            }

            if (hasUnsupportedParam)
            {
                continue;
            }

            // Determine return type
            string returnTypeName = methodSymbol.ReturnType.ToDisplayString();
            bool isAsync = returnTypeName.StartsWith("System.Threading.Tasks.Task", System.StringComparison.Ordinal)
                || returnTypeName.StartsWith("global::System.Threading.Tasks.Task", System.StringComparison.Ordinal);

            tools.Add(new AiToolInfo(
                toolName,
                toolDescription,
                methodSymbol.Name,
                classSymbol.ToDisplayString(),
                methodSymbol.IsStatic,
                parameters,
                returnTypeName,
                isAsync,
                methodDecl.GetLocation()));
        }

        if (tools.Count == 0 && diagnostics.Count == 0)
        {
            return null;
        }

        string? ns = classSymbol.ContainingNamespace?.IsGlobalNamespace == true
            ? null
            : classSymbol.ContainingNamespace?.ToDisplayString();

        return new AiSurfaceModel(
            classSymbol.Name,
            classSymbol.ToDisplayString(),
            ns,
            surfaceDescription,
            tools,
            diagnostics);
    }

    private static string GetJsonSchemaType(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_String:
                return "string";
            case SpecialType.System_Boolean:
                return "boolean";
            case SpecialType.System_Int16:
            case SpecialType.System_Int32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt16:
            case SpecialType.System_UInt32:
            case SpecialType.System_UInt64:
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
                return "integer";
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
                return "number";
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return "string";
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            string elementType = GetJsonSchemaType(arrayType.ElementType);
            if (elementType != "unsupported")
            {
                return "array";
            }
        }

        if (type is INamedTypeSymbol namedType
            && namedType.IsGenericType
            && namedType.TypeArguments.Length == 1)
        {
            string name = namedType.Name;
            if (name == "List" || name == "IReadOnlyList" || name == "IList"
                || name == "IEnumerable")
            {
                string elementType = GetJsonSchemaType(namedType.TypeArguments[0]);
                if (elementType != "unsupported")
                {
                    return "array";
                }
            }
        }

        return "unsupported";
    }

    // ── Diagnostics ───────────────────────────────────────────────────

    private static void ReportDiagnostics(SourceProductionContext spc, AiSurfaceModel model)
    {
        foreach (var diag in model.Diagnostics)
        {
            switch (diag.Kind)
            {
                case AiDiagnosticKind.MissingSurface:
                    spc.ReportDiagnostic(Diagnostic.Create(
                        AiDiagnostics.MissingSurface,
                        diag.Location,
                        diag.MethodName,
                        diag.ClassName ?? "unknown"));
                    break;

                case AiDiagnosticKind.DuplicateToolName:
                    spc.ReportDiagnostic(Diagnostic.Create(
                        AiDiagnostics.DuplicateToolName,
                        diag.Location,
                        diag.MethodName,
                        diag.ClassName ?? "",
                        diag.Detail ?? ""));
                    break;

                case AiDiagnosticKind.UnsupportedParameterType:
                    spc.ReportDiagnostic(Diagnostic.Create(
                        AiDiagnostics.UnsupportedParameterType,
                        diag.Location,
                        diag.MethodName,
                        diag.ClassName ?? "",
                        diag.Detail ?? ""));
                    break;

                case AiDiagnosticKind.InvalidAccessibility:
                    spc.ReportDiagnostic(Diagnostic.Create(
                        AiDiagnostics.InvalidAccessibility,
                        diag.Location,
                        diag.MethodName));
                    break;
            }
        }
    }

    // ── Code generation ───────────────────────────────────────────────

    private static string GenerateRegistry(List<AiToolInfo> tools)
    {
        string capabilityHash = ComputeCapabilityHash(tools);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Generated by Cascade.Generators — AI Surface Pipeline");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Cascade.UI.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Auto-generated registry of all AI-callable tools discovered");
        sb.AppendLine("    /// from [AiSurface] classes and [AiCapability] methods.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    internal static class AiToolRegistry");
        sb.AppendLine("    {");
        sb.AppendLine($"        /// <summary>Deterministic SHA-256 hash of all capability signatures for cache invalidation.</summary>");
        sb.AppendLine($"        public static string CapabilityHash => \"sha256:{capabilityHash}\";");
        sb.AppendLine();
        sb.AppendLine("        internal static readonly AiToolDefinition[] Tools = new AiToolDefinition[]");
        sb.AppendLine("        {");

        for (int i = 0; i < tools.Count; i++)
        {
            var tool = tools[i];
            string comma = i < tools.Count - 1 ? "," : "";
            string desc = tool.Description is not null
                ? $"\"{EscapeString(tool.Description)}\""
                : "null";

            sb.AppendLine($"            new AiToolDefinition(");
            sb.AppendLine($"                name: \"{EscapeString(tool.ToolName)}\",");
            sb.AppendLine($"                description: {desc},");
            sb.AppendLine($"                declaringType: \"{EscapeString(tool.DeclaringType)}\",");
            sb.AppendLine($"                methodName: \"{EscapeString(tool.MethodName)}\",");
            sb.AppendLine($"                isStatic: {(tool.IsStatic ? "true" : "false")},");
            sb.AppendLine($"                parameters: new AiToolParameterDefinition[]");
            sb.AppendLine($"                {{");

            for (int j = 0; j < tool.Parameters.Count; j++)
            {
                var param = tool.Parameters[j];
                string paramComma = j < tool.Parameters.Count - 1 ? "," : "";
                sb.AppendLine($"                    new AiToolParameterDefinition(\"{EscapeString(param.Name)}\", \"{EscapeString(param.SchemaType)}\", {(param.IsOptional ? "true" : "false")}){paramComma}");
            }

            sb.AppendLine($"                }}){comma}");
        }

        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate the definition types
        sb.AppendLine("    internal sealed class AiToolDefinition");
        sb.AppendLine("    {");
        sb.AppendLine("        public string Name { get; }");
        sb.AppendLine("        public string? Description { get; }");
        sb.AppendLine("        public string DeclaringType { get; }");
        sb.AppendLine("        public string MethodName { get; }");
        sb.AppendLine("        public bool IsStatic { get; }");
        sb.AppendLine("        public AiToolParameterDefinition[] Parameters { get; }");
        sb.AppendLine();
        sb.AppendLine("        public AiToolDefinition(string name, string? description, string declaringType, string methodName, bool isStatic, AiToolParameterDefinition[] parameters)");
        sb.AppendLine("        {");
        sb.AppendLine("            Name = name;");
        sb.AppendLine("            Description = description;");
        sb.AppendLine("            DeclaringType = declaringType;");
        sb.AppendLine("            MethodName = methodName;");
        sb.AppendLine("            IsStatic = isStatic;");
        sb.AppendLine("            Parameters = parameters;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    internal sealed class AiToolParameterDefinition");
        sb.AppendLine("    {");
        sb.AppendLine("        public string Name { get; }");
        sb.AppendLine("        public string SchemaType { get; }");
        sb.AppendLine("        public bool IsOptional { get; }");
        sb.AppendLine();
        sb.AppendLine("        public AiToolParameterDefinition(string name, string schemaType, bool isOptional)");
        sb.AppendLine("        {");
        sb.AppendLine("            Name = name;");
        sb.AppendLine("            SchemaType = schemaType;");
        sb.AppendLine("            IsOptional = isOptional;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string ComputeCapabilityHash(List<AiToolInfo> tools)
    {
        // Build a deterministic canonical string from all capability signatures.
        // Sorted by tool name to ensure order-independence.
        var sorted = tools.OrderBy(t => t.ToolName, StringComparer.Ordinal).ToList();
        var canonical = new StringBuilder();

        foreach (var tool in sorted)
        {
            canonical.Append("tool:");
            canonical.Append(tool.ToolName);
            canonical.Append('\n');

            if (tool.Description is not null)
            {
                canonical.Append("desc:");
                canonical.Append(tool.Description);
                canonical.Append('\n');
            }

            canonical.Append("method:");
            canonical.Append(tool.DeclaringType);
            canonical.Append('.');
            canonical.Append(tool.MethodName);
            canonical.Append('\n');

            foreach (var param in tool.Parameters)
            {
                canonical.Append("param:");
                canonical.Append(param.Name);
                canonical.Append(':');
                canonical.Append(param.SchemaType);
                canonical.Append(':');
                canonical.Append(param.IsOptional ? "optional" : "required");
                canonical.Append('\n');
            }
        }

        byte[] inputBytes = Encoding.UTF8.GetBytes(canonical.ToString());
        using (var sha = SHA256.Create())
        {
            byte[] hashBytes = sha.ComputeHash(inputBytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();
        }
    }

    private static string EscapeString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    // ── Model types ───────────────────────────────────────────────────

    private sealed class AiSurfaceModel
    {
        public string ClassName { get; }
        public string FullyQualifiedName { get; }
        public string? Namespace { get; }
        public string? Description { get; }
        public List<AiToolInfo> Tools { get; }
        public List<AiDiagnosticInfo> Diagnostics { get; }

        public AiSurfaceModel(
            string className,
            string fullyQualifiedName,
            string? ns,
            string? description,
            List<AiToolInfo> tools,
            List<AiDiagnosticInfo> diagnostics)
        {
            ClassName = className;
            FullyQualifiedName = fullyQualifiedName;
            Namespace = ns;
            Description = description;
            Tools = tools;
            Diagnostics = diagnostics;
        }
    }

    private sealed class AiToolInfo
    {
        public string ToolName { get; }
        public string? Description { get; }
        public string MethodName { get; }
        public string DeclaringType { get; }
        public bool IsStatic { get; }
        public List<AiToolParameterInfo> Parameters { get; }
        public string ReturnType { get; }
        public bool IsAsync { get; }
        public Location Location { get; }

        public AiToolInfo(
            string toolName,
            string? description,
            string methodName,
            string declaringType,
            bool isStatic,
            List<AiToolParameterInfo> parameters,
            string returnType,
            bool isAsync,
            Location location)
        {
            ToolName = toolName;
            Description = description;
            MethodName = methodName;
            DeclaringType = declaringType;
            IsStatic = isStatic;
            Parameters = parameters;
            ReturnType = returnType;
            IsAsync = isAsync;
            Location = location;
        }
    }

    private sealed class AiToolParameterInfo
    {
        public string Name { get; }
        public string TypeName { get; }
        public string SchemaType { get; }
        public bool IsOptional { get; }

        public AiToolParameterInfo(string name, string typeName, string schemaType, bool isOptional)
        {
            Name = name;
            TypeName = typeName;
            SchemaType = schemaType;
            IsOptional = isOptional;
        }
    }

    private enum AiDiagnosticKind
    {
        MissingSurface,
        DuplicateToolName,
        UnsupportedParameterType,
        InvalidAccessibility
    }

    private sealed class AiDiagnosticInfo
    {
        public AiDiagnosticKind Kind { get; }
        public string MethodName { get; }
        public string? ClassName { get; }
        public string? Detail { get; }
        public Location Location { get; }

        public AiDiagnosticInfo(
            AiDiagnosticKind kind,
            string methodName,
            string? className,
            string? detail,
            Location location)
        {
            Kind = kind;
            MethodName = methodName;
            ClassName = className;
            Detail = detail;
            Location = location;
        }
    }
}

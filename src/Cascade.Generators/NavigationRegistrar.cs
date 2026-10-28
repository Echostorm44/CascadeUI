using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Cascade.Generators;

/// <summary>
/// Incremental source generator that finds all classes decorated with
/// <c>[Route("path")]</c> and generates a static registration method
/// that maps routes to component factories, enabling compile-time
/// route validation and deep link support. It also emits a module initializer
/// that hands each route and a reflection-free factory to
/// <c>Cascade.UI.GeneratedRegistrations</c>, which is what lets
/// <c>Navigate(path)</c> work in trimmed and Native AOT apps.
/// </summary>
internal static class NavigationRegistrar
{
    private const string RouteAttributeFullName = "Cascade.UI.RouteAttribute";
    private const string ComponentFullName = "Cascade.UI.Component";
    private const string RegistrationsMetadataName = "Cascade.UI.GeneratedRegistrations";
    private const string ModuleInitializerMetadataName = "System.Runtime.CompilerServices.ModuleInitializerAttribute";

    /// <summary>
    /// Registers the navigation route pipeline with the incremental generator context.
    /// Called from <see cref="CascadeGenerator.Initialize"/>.
    /// </summary>
    public static void Register(IncrementalGeneratorInitializationContext context)
    {
        // Stage 1: Syntax filter — look for class declarations with attributes.
        // Stage 2: Semantic enrichment — verify [Route] attribute and extract pattern.
        var routeModels = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: IsRouteCandidate,
                transform: ExtractRouteModel)
            .Where(model => model is not null);

        var collected = routeModels.Collect();

        // The runtime registration needs Cascade.UI's hook and C# module initializers (.NET 5+).
        var canRegister = context.CompilationProvider.Select(static (compilation, _) =>
            compilation.GetTypeByMetadataName(RegistrationsMetadataName) is not null
            && compilation.GetTypeByMetadataName(ModuleInitializerMetadataName) is not null);

        // Stage 3: Generate the route registration table.
        context.RegisterSourceOutput(collected, static (spc, routes) =>
        {
            if (routes.Length == 0)
            {
                return;
            }

            // De-duplicate by route pattern. Two components claiming the same path is a
            // CASCADENAV001 error (and would otherwise emit a dictionary with duplicate
            // keys — a runtime ArgumentException). Report the collision and keep the first.
            var validRoutes = new List<RouteModel>();
            var seenPatterns = new Dictionary<string, string>(System.StringComparer.Ordinal);
            foreach (var route in routes)
            {
                if (route is null)
                {
                    continue;
                }

                var r = route.Value;
                if (seenPatterns.TryGetValue(r.Pattern, out var firstClass))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        NavigationDiagnostics.DuplicateRoute,
                        Location.None,
                        r.Pattern,
                        firstClass));
                    continue;
                }

                seenPatterns[r.Pattern] = r.ClassName;
                if (r.Registrable)
                {
                    validRoutes.Add(r); // typeof() of a private nested or generic class wouldn't compile
                }
            }

            if (validRoutes.Count == 0)
            {
                return;
            }

            var source = GenerateRouteRegistration(validRoutes);
            spc.AddSource("RouteRegistration.g.cs", SourceText.From(source, Encoding.UTF8));
        });

        context.RegisterSourceOutput(collected.Combine(canRegister), static (spc, data) =>
        {
            var (routes, canRegisterRoutes) = data;
            if (!canRegisterRoutes)
            {
                return;
            }

            var registrable = new List<RouteModel>();
            var seenPatterns = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var route in routes)
            {
                if (route is { } r && r.Registrable && seenPatterns.Add(r.Pattern))
                {
                    registrable.Add(r);
                }
            }

            if (registrable.Count > 0)
            {
                spc.AddSource("RouteTable.g.cs", SourceText.From(GenerateRouteTable(registrable), Encoding.UTF8));
            }
        });

        // CASCADENAV002: a typed route parameter {name:type} must match the type of the
        // target component's same-named property (the property the router binds it to).
        var paramFindings = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: IsRouteCandidate,
                transform: AnalyzeRouteParams)
            .Where(m => m is not null);

        context.RegisterSourceOutput(paramFindings, static (spc, model) =>
        {
            if (model is null)
            {
                return;
            }

            foreach (var mm in model.Mismatches)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    NavigationDiagnostics.RouteParameterTypeMismatch,
                    mm.Location,
                    mm.ParamName, mm.ParamType, mm.PropertyName, mm.PropertyType));
            }
        });
    }

    // ── CASCADENAV002 route-parameter type checking ───────────────────

    private sealed class RouteParamModel : System.IEquatable<RouteParamModel>
    {
        public RouteParamModel(List<ParamMismatch> mismatches) => Mismatches = mismatches;
        public List<ParamMismatch> Mismatches { get; }
        public bool Equals(RouteParamModel? other) => other is not null && Mismatches.SequenceEqual(other.Mismatches);
        public override bool Equals(object? o) => Equals(o as RouteParamModel);
        public override int GetHashCode()
        {
            int h = 17;
            foreach (var m in Mismatches)
            {
                h = (h * 31) + m.GetHashCode();
            }
            return h;
        }
    }

    private readonly struct ParamMismatch : System.IEquatable<ParamMismatch>
    {
        public ParamMismatch(string paramName, string paramType, string propName, string propType, Location loc)
        {
            ParamName = paramName;
            ParamType = paramType;
            PropertyName = propName;
            PropertyType = propType;
            Location = loc;
        }

        public string ParamName { get; }
        public string ParamType { get; }
        public string PropertyName { get; }
        public string PropertyType { get; }
        public Location Location { get; }

        public bool Equals(ParamMismatch o) =>
            ParamName == o.ParamName && ParamType == o.ParamType
            && PropertyName == o.PropertyName && PropertyType == o.PropertyType && Location.Equals(o.Location);
        public override bool Equals(object? o) => o is ParamMismatch m && Equals(m);
        public override int GetHashCode() =>
            ((ParamName.GetHashCode() * 397) ^ PropertyName.GetHashCode()) ^ Location.GetHashCode();
    }

    private static RouteParamModel? AnalyzeRouteParams(
        GeneratorSyntaxContext ctx, System.Threading.CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        var classSymbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct);
        if (classSymbol is null)
        {
            return null;
        }

        string? pattern = null;
        Location? attrLocation = null;
        foreach (var attrList in classDecl.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var symbol = ctx.SemanticModel.GetSymbolInfo(attr, ct).Symbol;
                if (symbol?.ContainingType?.ToDisplayString() != RouteAttributeFullName)
                {
                    continue;
                }

                var arg = attr.ArgumentList?.Arguments.FirstOrDefault();
                if (arg?.Expression is LiteralExpressionSyntax lit
                    && lit.Token.IsKind(SyntaxKind.StringLiteralToken))
                {
                    pattern = lit.Token.ValueText;
                    attrLocation = attr.GetLocation();
                }
                break;
            }
        }

        if (pattern is null)
        {
            return null;
        }

        var location = attrLocation ?? classDecl.Identifier.GetLocation();
        var mismatches = new List<ParamMismatch>();
        foreach (var (paramName, paramType) in ParseTypedParams(pattern))
        {
            var prop = classSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .FirstOrDefault(p => string.Equals(p.Name, paramName, System.StringComparison.OrdinalIgnoreCase));
            if (prop is null)
            {
                continue; // no bound property — nothing to type-check (ctor-arg route)
            }

            var effective = (prop.Type is INamedTypeSymbol nt
                && nt.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                ? nt.TypeArguments[0]
                : prop.Type;

            if (!RouteTypeMatches(effective, paramType))
            {
                mismatches.Add(new ParamMismatch(
                    paramName, paramType, prop.Name, effective.ToDisplayString(), location));
            }
        }

        return mismatches.Count == 0 ? null : new RouteParamModel(mismatches);
    }

    private static IEnumerable<(string Name, string Type)> ParseTypedParams(string pattern)
    {
        foreach (var seg in pattern.Split('/'))
        {
            if (seg.Length > 2 && seg[0] == '{' && seg[seg.Length - 1] == '}')
            {
                var inner = seg.Substring(1, seg.Length - 2);
                int colon = inner.IndexOf(':');
                if (colon > 0 && colon < inner.Length - 1)
                {
                    yield return (inner.Substring(0, colon), inner.Substring(colon + 1));
                }
            }
        }
    }

    // Returns true if the property type is compatible with the declared route-param type,
    // or if the route type is unrecognised (then we don't flag — the constraint is free-form).
    private static bool RouteTypeMatches(ITypeSymbol propType, string routeType)
    {
        switch (routeType.ToUpperInvariant())
        {
            case "INT": return propType.SpecialType == SpecialType.System_Int32;
            case "LONG": return propType.SpecialType == SpecialType.System_Int64;
            case "BOOL": return propType.SpecialType == SpecialType.System_Boolean;
            case "DOUBLE": return propType.SpecialType == SpecialType.System_Double;
            case "FLOAT": return propType.SpecialType == SpecialType.System_Single;
            case "STRING": return propType.SpecialType == SpecialType.System_String;
            case "GUID": return propType.Name == "Guid" && propType.ContainingNamespace?.ToDisplayString() == "System";
            default: return true;
        }
    }

    // ── Syntax filtering ──────────────────────────────────────────────

    private static bool IsRouteCandidate(SyntaxNode node, System.Threading.CancellationToken ct)
    {
        // Fast filter: only class declarations with at least one attribute
        return node is ClassDeclarationSyntax classDecl
            && classDecl.AttributeLists.Count > 0;
    }

    // ── Semantic analysis ─────────────────────────────────────────────

    private static RouteModel? ExtractRouteModel(
        GeneratorSyntaxContext context,
        System.Threading.CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl, ct);

        if (classSymbol is null)
        {
            return null;
        }

        // Find the [Route] attribute
        AttributeData? routeAttribute = null;
        foreach (var attr in classSymbol.GetAttributes())
        {
            if (attr.AttributeClass is not null &&
                attr.AttributeClass.ToDisplayString() == RouteAttributeFullName)
            {
                routeAttribute = attr;
                break;
            }
        }

        if (routeAttribute is null)
        {
            return null;
        }

        // Extract the pattern from the constructor argument
        if (routeAttribute.ConstructorArguments.Length == 0 ||
            routeAttribute.ConstructorArguments[0].Value is not string pattern)
        {
            return null;
        }

        var fullTypeName = classSymbol.ToDisplayString();
        var namespaceName = classSymbol.ContainingNamespace?.IsGlobalNamespace == true
            ? null
            : classSymbol.ContainingNamespace?.ToDisplayString();

        bool registrable = IsRegistrable(classSymbol);
        string? factory = registrable ? BuildFactory(classSymbol, pattern) : null;

        return new RouteModel(pattern, fullTypeName, classSymbol.Name, namespaceName,
            classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), registrable, factory);
    }

    // ── Reflection-free factories ─────────────────────────────────────

    // Generated code can only name a concrete, non-generic Component that is visible from the
    // assembly's own top level. Anything else is left to the reflection fallback.
    private static bool IsRegistrable(INamedTypeSymbol classSymbol)
    {
        if (classSymbol.IsAbstract || !DerivesFromComponent(classSymbol))
        {
            return false;
        }

        for (INamedTypeSymbol? t = classSymbol; t is not null; t = t.ContainingType)
        {
            if (t.IsGenericType || !IsVisibleInAssembly(t.DeclaredAccessibility))
            {
                return false;
            }
        }

        return true;
    }

    private static bool DerivesFromComponent(INamedTypeSymbol classSymbol)
    {
        for (var t = classSymbol.BaseType; t is not null; t = t.BaseType)
        {
            if (t.ToDisplayString() == ComponentFullName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsVisibleInAssembly(Accessibility accessibility) =>
        accessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;

    // Mirrors Navigator.CreateComponentFromRoute: default-construct and bind every route value
    // (path or query) to a same-named settable property, converting to the property's type and
    // skipping values that don't parse. Without a parameterless constructor, a constructor taking
    // one string per path parameter gets them in pattern order. Returns null when neither exists.
    private static string? BuildFactory(INamedTypeSymbol classSymbol, string pattern)
    {
        string typeName = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var constructors = classSymbol.InstanceConstructors
            .Where(c => IsVisibleInAssembly(c.DeclaredAccessibility))
            .ToList();

        if (constructors.Any(c => c.Parameters.Length == 0))
        {
            var bindings = BuildPropertyBindings(classSymbol);
            if (bindings.Length == 0)
            {
                return $"static parameters => new {typeName}()";
            }

            return "static parameters =>\n"
                + "            {\n"
                + $"                var component = new {typeName}();\n"
                + "                foreach (var parameter in parameters)\n"
                + "                {\n"
                + "                    switch (parameter.Key.ToUpperInvariant())\n"
                + "                    {\n"
                + bindings
                + "                    }\n"
                + "                }\n"
                + "                return component;\n"
                + "            }";
        }

        var names = PathParameterNames(pattern);
        var stringConstructor = constructors.FirstOrDefault(c =>
            c.Parameters.Length == names.Count
            && c.Parameters.All(p => p.Type.SpecialType == SpecialType.System_String));
        if (stringConstructor is null)
        {
            return null;
        }

        var arguments = names.Select((name, i) =>
            $"(parameters.TryGetValue(\"{EscapeString(name)}\", out var argument{i}) ? argument{i} : \"\")");
        return $"static parameters => new {typeName}({string.Join(", ", arguments)})";
    }

    private static string BuildPropertyBindings(INamedTypeSymbol classSymbol)
    {
        var sb = new StringBuilder();
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        int index = 0;
        for (var t = classSymbol; t is not null && t.ToDisplayString() != ComponentFullName; t = t.BaseType)
        {
            foreach (var property in t.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.IsStatic || property.IsIndexer || property.DeclaredAccessibility != Accessibility.Public
                    || property.SetMethod is not { IsInitOnly: false } setter
                    || !CanSet(setter, classSymbol))
                {
                    continue;
                }

                string key = property.Name.ToUpperInvariant();
                if (!seen.Add(key))
                {
                    continue; // an override or a case-only twin: the most derived one wins
                }

                string? assign = BuildAssignment(property.Type, "@" + property.Name, index++);
                if (assign is null)
                {
                    continue;
                }

                sb.Append("                        case \"").Append(EscapeString(key)).Append("\":\n");
                sb.Append("                            ").Append(assign).Append('\n');
                sb.Append("                            break;\n");
            }
        }

        return sb.ToString();
    }

    private static bool CanSet(IMethodSymbol setter, INamedTypeSymbol classSymbol) =>
        setter.DeclaredAccessibility == Accessibility.Public
        || (setter.DeclaredAccessibility is Accessibility.Internal or Accessibility.ProtectedOrInternal
            && SymbolEqualityComparer.Default.Equals(setter.ContainingAssembly, classSymbol.ContainingAssembly));

    private static string? BuildAssignment(ITypeSymbol type, string property, int index)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return $"component.{property} = parameter.Value;";
        }

        var effective = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;
        string value = "value" + index;
        string invariant = "global::System.Globalization.CultureInfo.InvariantCulture";
        string? parse = effective.SpecialType switch
        {
            SpecialType.System_Boolean => $"bool.TryParse(parameter.Value, out var {value})",
            SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16
                or SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64
                => $"{effective.ToDisplayString()}.TryParse(parameter.Value, global::System.Globalization.NumberStyles.Integer, {invariant}, out var {value})",
            SpecialType.System_Single or SpecialType.System_Double
                => $"{effective.ToDisplayString()}.TryParse(parameter.Value, global::System.Globalization.NumberStyles.Float | global::System.Globalization.NumberStyles.AllowThousands, {invariant}, out var {value})",
            SpecialType.System_Decimal
                => $"decimal.TryParse(parameter.Value, global::System.Globalization.NumberStyles.Number, {invariant}, out var {value})",
            SpecialType.System_DateTime
                => $"global::System.DateTime.TryParse(parameter.Value, {invariant}, global::System.Globalization.DateTimeStyles.None, out var {value})",
            SpecialType.System_Char => $"char.TryParse(parameter.Value, out var {value})",
            _ => null,
        };

        if (parse is null && effective.Name == "Guid" && effective.ContainingNamespace?.ToDisplayString() == "System")
        {
            parse = $"global::System.Guid.TryParse(parameter.Value, out var {value})";
        }
        else if (parse is null && effective.TypeKind == TypeKind.Enum)
        {
            parse = $"global::System.Enum.TryParse<{effective.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>(parameter.Value, true, out var {value})";
        }

        return parse is null ? null : $"if ({parse}) {{ component.{property} = {value}; }}";
    }

    private static List<string> PathParameterNames(string pattern)
    {
        var names = new List<string>();
        foreach (var seg in pattern.Split('/'))
        {
            if (seg.Length > 2 && seg[0] == '{' && seg[seg.Length - 1] == '}')
            {
                var inner = seg.Substring(1, seg.Length - 2);
                int colon = inner.IndexOf(':');
                names.Add(colon < 0 ? inner : inner.Substring(0, colon));
            }
        }

        return names;
    }

    // ── Code generation ───────────────────────────────────────────────

    private static string GenerateRouteRegistration(List<RouteModel> routes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Generated by Cascade.Generators — Navigation Route Pipeline");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Auto-generated route registration mapping [Route] patterns to component factories.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static partial class RouteRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Returns all registered route patterns and their corresponding component type names.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static global::System.Collections.Generic.Dictionary<string, global::System.Type> GetRoutes()");
        sb.AppendLine("    {");
        sb.AppendLine("        return new global::System.Collections.Generic.Dictionary<string, global::System.Type>");
        sb.AppendLine("        {");

        for (int i = 0; i < routes.Count; i++)
        {
            var route = routes[i];
            var separator = i < routes.Count - 1 ? "," : "";
            sb.AppendLine($"            {{ \"{EscapeString(route.Pattern)}\", typeof({route.QualifiedTypeName}) }}{separator}");
        }

        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateRouteTable(List<RouteModel> routes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Generated by Cascade.Generators — Navigation Route Pipeline");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Cascade.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    internal static class CascadeRouteTable");
        sb.AppendLine("    {");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("        internal static void Register()");
        sb.AppendLine("        {");
        foreach (var route in routes)
        {
            sb.AppendLine($"            global::Cascade.UI.GeneratedRegistrations.RegisterRoute(\"{EscapeString(route.Pattern)}\", typeof({route.QualifiedTypeName}),");
            sb.AppendLine($"            {route.FactoryCode ?? "null"});");
        }
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string EscapeString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    // ── Pipeline data types ───────────────────────────────────────────

    internal readonly struct RouteModel : System.IEquatable<RouteModel>
    {
        public string Pattern { get; }
        public string FullTypeName { get; }
        public string ClassName { get; }
        public string? Namespace { get; }
        public string QualifiedTypeName { get; }
        public bool Registrable { get; }
        public string? FactoryCode { get; }

        public RouteModel(string pattern, string fullTypeName, string className, string? namespaceName,
            string qualifiedTypeName, bool registrable, string? factoryCode)
        {
            Pattern = pattern;
            FullTypeName = fullTypeName;
            ClassName = className;
            Namespace = namespaceName;
            QualifiedTypeName = qualifiedTypeName;
            Registrable = registrable;
            FactoryCode = factoryCode;
        }

        public bool Equals(RouteModel other)
        {
            return string.Equals(Pattern, other.Pattern, System.StringComparison.Ordinal)
                && string.Equals(FullTypeName, other.FullTypeName, System.StringComparison.Ordinal)
                && Registrable == other.Registrable
                && string.Equals(FactoryCode, other.FactoryCode, System.StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is RouteModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Pattern?.GetHashCode() ?? 0) * 397) ^ (FullTypeName?.GetHashCode() ?? 0);
            }
        }
    }
}

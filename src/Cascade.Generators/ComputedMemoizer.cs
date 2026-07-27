using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cascade.Generators;

/// <summary>
/// Metadata about a computed (memoized) property in a Component subclass.
/// Computed properties are expression-bodied properties whose values depend
/// on reactive fields and are automatically cached with dirty-flag memoization.
/// </summary>
internal sealed class ComputedPropertyInfo : IEquatable<ComputedPropertyInfo>
{
    public string Name { get; }
    public string TypeName { get; }
    public string Expression { get; }
    public EquatableArray<string> FieldDependencies { get; }
    public EquatableArray<string> ComputedDependencies { get; }

    public ComputedPropertyInfo(
        string name,
        string typeName,
        string expression,
        EquatableArray<string> fieldDependencies,
        EquatableArray<string> computedDependencies)
    {
        Name = name;
        TypeName = typeName;
        Expression = expression;
        FieldDependencies = fieldDependencies;
        ComputedDependencies = computedDependencies;
    }

    public bool Equals(ComputedPropertyInfo? other)
    {
        if (other is null)
        {
            return false;
        }
        return Name == other.Name
            && TypeName == other.TypeName
            && Expression == other.Expression
            && FieldDependencies.Equals(other.FieldDependencies)
            && ComputedDependencies.Equals(other.ComputedDependencies);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ComputedPropertyInfo);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Name.GetHashCode();
            hash = hash * 397 + TypeName.GetHashCode();
            hash = hash * 397 + Expression.GetHashCode();
            hash = hash * 397 + FieldDependencies.GetHashCode();
            hash = hash * 397 + ComputedDependencies.GetHashCode();
            return hash;
        }
    }
}

/// <summary>
/// Identifies computed properties in Component subclasses, tracks their
/// dependencies on reactive fields and other computed properties, and
/// generates memoization code with dirty flags.
/// </summary>
internal static class ComputedMemoizer
{
    private const string ComputedAttributeName = "Cascade.UI.ComputedAttribute";

    /// <summary>
    /// Finds all computed properties in a component class.
    /// A property is computed if it is expression-bodied and reads at least one
    /// reactive field, or if it has the [Computed] attribute.
    /// </summary>
    public static ComputedPropertyInfo[] AnalyzeComputedProperties(
        ClassDeclarationSyntax classDecl,
        SemanticModel semanticModel,
        INamedTypeSymbol classSymbol,
        List<ReactiveFieldInfo> reactiveFields,
        CancellationToken ct)
    {
        var reactiveFieldNames = new HashSet<string>(reactiveFields.Select(f => f.Name));

        // First pass: collect candidates with their expression syntax nodes
        var candidates = new List<(
            IPropertySymbol symbol,
            SyntaxNode expressionNode,
            string expression,
            string[] fieldDeps)>();

        foreach (var member in classDecl.Members)
        {
            ct.ThrowIfCancellationRequested();

            if (member is not PropertyDeclarationSyntax propertyDecl)
            {
                continue;
            }

            var propSymbol = semanticModel.GetDeclaredSymbol(propertyDecl, ct) as IPropertySymbol;
            if (propSymbol is null)
            {
                continue;
            }

            bool hasComputedAttribute = propSymbol.GetAttributes()
                .Any(a => a.AttributeClass?.ToDisplayString() == ComputedAttributeName);

            // Get the expression node (from expression body or getter body)
            SyntaxNode? expressionNode = null;
            if (propertyDecl.ExpressionBody is not null)
            {
                expressionNode = propertyDecl.ExpressionBody.Expression;
            }
            else if (hasComputedAttribute && propertyDecl.AccessorList is not null)
            {
                var getter = propertyDecl.AccessorList.Accessors
                    .FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
                if (getter?.ExpressionBody is not null)
                {
                    expressionNode = getter.ExpressionBody.Expression;
                }
            }

            if (expressionNode is null)
            {
                continue;
            }

            // Find which reactive fields this expression reads
            var fieldDeps = FindFieldDependencies(
                expressionNode, semanticModel, classSymbol, reactiveFieldNames);

            if (fieldDeps.Length == 0 && !hasComputedAttribute)
            {
                continue;
            }

            candidates.Add((propSymbol, expressionNode, "", fieldDeps));
        }

        // Second pass: detect computed → computed dependencies
        var computedNames = new HashSet<string>(candidates.Select(c => c.symbol.Name));
        var results = new List<ComputedPropertyInfo>();

        foreach (var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();

            var computedDeps = new List<string>();
            foreach (var identifier in candidate.expressionNode
                .DescendantNodesAndSelf()
                .OfType<IdentifierNameSyntax>())
            {
                var sym = semanticModel.GetSymbolInfo(identifier, ct).Symbol;
                if (sym is IPropertySymbol prop
                    && SymbolEqualityComparer.Default.Equals(prop.ContainingType, classSymbol)
                    && computedNames.Contains(prop.Name)
                    && prop.Name != candidate.symbol.Name)
                {
                    computedDeps.Add(prop.Name);
                }
            }

            // Rewrite field references in the expression to use __ backing fields
            var rewrittenExpression = RewriteFieldReferences(
                candidate.expressionNode, reactiveFieldNames);

            results.Add(new ComputedPropertyInfo(
                candidate.symbol.Name,
                candidate.symbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                rewrittenExpression,
                new EquatableArray<string>(candidate.fieldDeps),
                new EquatableArray<string>(computedDeps.Distinct().ToArray())));
        }

        return results.ToArray();
    }

    /// <summary>
    /// Generates the memoization code block for a computed property:
    /// dirty flag, cached value, compute method, invalidation method, and getter.
    /// </summary>
    public static void EmitMemoization(
        StringBuilder sb,
        ComputedPropertyInfo computed,
        Dictionary<string, List<string>> computedReverseDeps)
    {
        string lowerName = char.ToLowerInvariant(computed.Name[0]) + computed.Name.Substring(1);

        sb.AppendLine($"    private bool __{lowerName}_dirty = true;");
        sb.AppendLine($"    private {computed.TypeName} __{lowerName}_cached = default!;");
        sb.AppendLine();

        // Compute method — evaluates the expression using backing fields
        sb.AppendLine($"    private {computed.TypeName} __Compute_{computed.Name}()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return {computed.Expression};");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Invalidation method — marks dirty and cascades to dependents
        sb.AppendLine($"    private void __Invalidate_{computed.Name}()");
        sb.AppendLine("    {");
        sb.AppendLine($"        __{lowerName}_dirty = true;");
        if (computedReverseDeps.TryGetValue(computed.Name, out var dependents))
        {
            foreach (var dep in dependents)
            {
                sb.AppendLine($"        __Invalidate_{dep}();");
            }
        }
        sb.AppendLine("    }");
        sb.AppendLine();

        // Memoized getter — recomputes only when dirty
        sb.AppendLine($"    private {computed.TypeName} __Get_{computed.Name}()");
        sb.AppendLine("    {");
        sb.AppendLine($"        if (__{lowerName}_dirty)");
        sb.AppendLine("        {");
        sb.AppendLine($"            __{lowerName}_cached = __Compute_{computed.Name}();");
        sb.AppendLine($"            __{lowerName}_dirty = false;");
        sb.AppendLine("        }");
        sb.AppendLine($"        return __{lowerName}_cached;");
        sb.AppendLine("    }");
    }

    // ── Private helpers ───────────────────────────────────────────────

    private static string[] FindFieldDependencies(
        SyntaxNode expression,
        SemanticModel semanticModel,
        INamedTypeSymbol classSymbol,
        HashSet<string> reactiveFieldNames)
    {
        var dependencies = new HashSet<string>();

        foreach (var identifier in expression
            .DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>())
        {
            var symbolInfo = semanticModel.GetSymbolInfo(identifier);
            if (symbolInfo.Symbol is IFieldSymbol field
                && SymbolEqualityComparer.Default.Equals(field.ContainingType, classSymbol)
                && reactiveFieldNames.Contains(field.Name))
            {
                dependencies.Add(field.Name);
            }
        }

        return dependencies.ToArray();
    }

    /// <summary>
    /// Rewrites reactive field references in an expression to use the __ backing prefix.
    /// For example, <c>email.Contains('@')</c> becomes <c>__email.Contains('@')</c>.
    /// </summary>
    private static string RewriteFieldReferences(
        SyntaxNode expression,
        HashSet<string> reactiveFieldNames)
    {
        var identifiersToReplace = expression
            .DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Where(id => reactiveFieldNames.Contains(id.Identifier.Text))
            .ToList();

        if (identifiersToReplace.Count == 0)
        {
            return expression.ToFullString().Trim();
        }

        var rewritten = expression.ReplaceNodes(
            identifiersToReplace,
            (original, _) => SyntaxFactory.IdentifierName("__" + original.Identifier.Text)
                .WithTriviaFrom(original));

        return rewritten.ToFullString().Trim();
    }
}

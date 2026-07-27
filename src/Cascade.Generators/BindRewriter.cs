using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cascade.Generators;

/// <summary>
/// Metadata about a Bind() call detected in a Render() method.
/// If <see cref="IsReadonly"/> is true, this call is invalid and
/// the generator emits CS-CASCADE-002.
/// </summary>
internal sealed class BindCallInfo : IEquatable<BindCallInfo>
{
    public string FieldName { get; }
    public string TypeName { get; }
    public bool IsReadonly { get; }

    public BindCallInfo(string fieldName, string typeName, bool isReadonly)
    {
        FieldName = fieldName;
        TypeName = typeName;
        IsReadonly = isReadonly;
    }

    public bool Equals(BindCallInfo? other)
    {
        if (other is null)
        {
            return false;
        }
        return FieldName == other.FieldName
            && TypeName == other.TypeName
            && IsReadonly == other.IsReadonly;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as BindCallInfo);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = FieldName.GetHashCode();
            hash = hash * 397 + TypeName.GetHashCode();
            hash = hash * 397 + IsReadonly.GetHashCode();
            return hash;
        }
    }
}

/// <summary>
/// Identifies <c>Bind(field)</c> calls in Render() methods, validates that
/// the target field is writable, and generates <c>Bindable&lt;T&gt;</c>
/// helper methods for two-way binding.
/// </summary>
internal static class BindRewriter
{
    /// <summary>
    /// Finds all Bind() calls in a Render() method body.
    /// Returns metadata for each call, including whether the target field is readonly.
    /// </summary>
    public static BindCallInfo[] AnalyzeBindCalls(
        MethodDeclarationSyntax renderMethod,
        SemanticModel semanticModel,
        INamedTypeSymbol classSymbol,
        CancellationToken ct)
    {
        SyntaxNode? body = (SyntaxNode?)renderMethod.Body ?? renderMethod.ExpressionBody;
        if (body is null)
        {
            return Array.Empty<BindCallInfo>();
        }

        var results = new List<BindCallInfo>();

        foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            // Check if this is a call to "Bind"
            string? methodName = null;
            if (invocation.Expression is IdentifierNameSyntax simpleName)
            {
                methodName = simpleName.Identifier.Text;
            }
            else if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                methodName = memberAccess.Name.Identifier.Text;
            }

            if (methodName != "Bind")
            {
                continue;
            }

            // Bind() takes exactly one argument — the field to bind
            if (invocation.ArgumentList.Arguments.Count != 1)
            {
                continue;
            }

            var argument = invocation.ArgumentList.Arguments[0].Expression;
            if (argument is not IdentifierNameSyntax fieldIdentifier)
            {
                continue;
            }

            // Resolve the argument to a field symbol
            var symbolInfo = semanticModel.GetSymbolInfo(fieldIdentifier, ct);
            if (symbolInfo.Symbol is not IFieldSymbol field)
            {
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(field.ContainingType, classSymbol))
            {
                continue;
            }

            results.Add(new BindCallInfo(
                field.Name,
                field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                field.IsReadOnly));
        }

        // Deduplicate — multiple Bind(email) calls produce one helper
        return results
            .GroupBy(b => b.FieldName)
            .Select(g => g.First())
            .ToArray();
    }

    /// <summary>
    /// Generates a <c>__Bind_{fieldName}()</c> helper method that returns a
    /// <c>Bindable&lt;T&gt;</c> wrapping the backing field's getter and setter.
    /// </summary>
    public static void EmitBindHelper(StringBuilder sb, string fieldName, string typeName)
    {
        sb.AppendLine($"    private global::Cascade.UI.Bindable<{typeName}> __Bind_{fieldName}()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return new global::Cascade.UI.Bindable<{typeName}>(__{fieldName}, __Set_{fieldName});");
        sb.AppendLine("    }");
    }
}

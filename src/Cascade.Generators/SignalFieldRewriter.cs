using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cascade.Generators;

// ═══════════════════════════════════════════════════════════════════════
// Pipeline data types — all implement IEquatable<T> for incremental caching
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Array wrapper with structural equality for incremental generator caching.
/// <see cref="System.Collections.Immutable.ImmutableArray{T}"/> does not
/// implement structural equality, so we need this wrapper.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    public static readonly EquatableArray<T> Empty = new(Array.Empty<T>());

    private readonly T[]? items;

    public EquatableArray(T[] items)
    {
        this.items = items;
    }

    public int Length => items?.Length ?? 0;

    public T this[int index] => (items ?? Array.Empty<T>())[index];

    public bool Equals(EquatableArray<T> other)
    {
        var a = items ?? Array.Empty<T>();
        var b = other.items ?? Array.Empty<T>();
        if (a.Length != b.Length)
        {
            return false;
        }
        for (int i = 0; i < a.Length; i++)
        {
            if (!a[i].Equals(b[i]))
            {
                return false;
            }
        }
        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        var arr = items ?? Array.Empty<T>();
        unchecked
        {
            int hash = 17;
            foreach (var item in arr)
            {
                hash = hash * 31 + item.GetHashCode();
            }
            return hash;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)(items ?? Array.Empty<T>())).GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>
/// Records a field write detected inside Render() for CASCADE001 reporting.
/// </summary>
internal sealed class RenderWriteInfo : IEquatable<RenderWriteInfo>
{
    public string FieldName { get; }
    public int LineNumber { get; }
    public string FilePath { get; }

    public RenderWriteInfo(string fieldName, int lineNumber, string filePath)
    {
        FieldName = fieldName;
        LineNumber = lineNumber;
        FilePath = filePath;
    }

    public bool Equals(RenderWriteInfo? other)
    {
        if (other is null)
        {
            return false;
        }
        return FieldName == other.FieldName
            && LineNumber == other.LineNumber
            && FilePath == other.FilePath;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as RenderWriteInfo);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = FieldName.GetHashCode();
            hash = hash * 397 + LineNumber;
            hash = hash * 397 + FilePath.GetHashCode();
            return hash;
        }
    }
}

/// <summary>
/// Reactivity diagnostic model for a single Component subclass — the equatable data
/// flowing through the incremental pipeline. Reactivity is a diagnostics-only pass
/// (no code is generated), so the model carries only the writes-in-Render used for
/// CASCADE001.
/// </summary>
internal sealed class ComponentReactivityModel : IEquatable<ComponentReactivityModel>
{
    public EquatableArray<RenderWriteInfo> RenderWrites { get; }

    public ComponentReactivityModel(EquatableArray<RenderWriteInfo> renderWrites)
    {
        RenderWrites = renderWrites;
    }

    public bool Equals(ComponentReactivityModel? other)
    {
        if (other is null)
        {
            return false;
        }
        return RenderWrites.Equals(other.RenderWrites);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ComponentReactivityModel);
    }

    public override int GetHashCode()
    {
        return RenderWrites.GetHashCode();
    }
}

// ═══════════════════════════════════════════════════════════════════════
// Analysis logic
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Analyzes Component subclasses for the one reactivity rule the generator enforces:
/// CASCADE001 — a mutable field read in Render() must not also be written inside Render()
/// (Render must be pure). Field reads/writes are followed transitively through the Render()
/// call graph; writes inside deferred event-handler lambdas are excluded.
/// </summary>
internal static class SignalFieldRewriter
{
    private const string ComponentFullName = "Cascade.UI.Component";
    private const string NotReactiveAttributeName = "Cascade.UI.NotReactiveAttribute";

    /// <summary>
    /// Fast syntax-only predicate: does this class declaration have a base type?
    /// This runs on every edit and must be allocation-minimal. It filters candidates
    /// before the more expensive semantic analysis phase.
    /// </summary>
    public static bool IsComponentCandidate(SyntaxNode node, CancellationToken ct)
    {
        return node is ClassDeclarationSyntax classDecl
            && classDecl.BaseList is not null
            && classDecl.BaseList.Types.Count > 0;
    }

    /// <summary>
    /// Semantic analysis: builds the reactivity diagnostic model for a Component subclass.
    /// Returns null if the class does not inherit from Component or has no Render() method.
    /// </summary>
    public static ComponentReactivityModel? Analyze(
        GeneratorSyntaxContext context,
        CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl, ct);
        if (classSymbol is null)
        {
            return null;
        }

        if (!InheritsFromComponent(classSymbol, semanticModel.Compilation))
        {
            return null;
        }

        // Skip nested types.
        if (classSymbol.ContainingType is not null)
        {
            return null;
        }

        ct.ThrowIfCancellationRequested();

        // Find all non-static instance fields declared in this class.
        var allFields = classSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => !f.IsStatic && !f.IsImplicitlyDeclared)
            .ToList();

        var renderMethod = FindRenderMethod(classDecl);
        if (renderMethod is null)
        {
            return null;
        }

        // Walk the transitive Render() call graph to find field reads and writes.
        var fieldsReadInRender = new HashSet<string>();
        var fieldsWrittenInRender = new List<(string name, Location location)>();

        AnalyzeMethodBody(
            renderMethod,
            classDecl,
            semanticModel,
            classSymbol,
            fieldsReadInRender,
            fieldsWrittenInRender,
            new HashSet<string>(),
            ct);

        ct.ThrowIfCancellationRequested();

        // CASCADE001 targets mutable state fields that are read in Render: writing one during
        // Render is impure. Readonly and [NotReactive] fields are not state, so they are exempt.
        var stateFieldNames = new HashSet<string>();
        foreach (var field in allFields)
        {
            if (field.IsReadOnly || HasAttribute(field, NotReactiveAttributeName))
            {
                continue;
            }
            if (fieldsReadInRender.Contains(field.Name))
            {
                stateFieldNames.Add(field.Name);
            }
        }

        var renderWrites = fieldsWrittenInRender
            .Where(w => stateFieldNames.Contains(w.name))
            .Select(w =>
            {
                var lineSpan = w.location.GetLineSpan();
                return new RenderWriteInfo(
                    w.name,
                    lineSpan.StartLinePosition.Line + 1,
                    lineSpan.Path ?? "");
            })
            .ToArray();

        return new ComponentReactivityModel(
            new EquatableArray<RenderWriteInfo>(renderWrites));
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static bool InheritsFromComponent(INamedTypeSymbol type, Compilation compilation)
    {
        var componentType = compilation.GetTypeByMetadataName(ComponentFullName);
        if (componentType is null)
        {
            return false;
        }

        var current = type.BaseType;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, componentType))
            {
                return true;
            }
            current = current.BaseType;
        }
        return false;
    }

    private static MethodDeclarationSyntax? FindRenderMethod(ClassDeclarationSyntax classDecl)
    {
        return classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "Render"
                && m.Modifiers.Any(SyntaxKind.ProtectedKeyword)
                && m.Modifiers.Any(SyntaxKind.OverrideKeyword));
    }

    /// <summary>
    /// Walks a method body to find field reads and writes.
    /// Transitively follows calls to methods declared in the same class.
    /// </summary>
    private static void AnalyzeMethodBody(
        MethodDeclarationSyntax method,
        ClassDeclarationSyntax classDecl,
        SemanticModel semanticModel,
        INamedTypeSymbol classSymbol,
        HashSet<string> fieldsRead,
        List<(string name, Location location)> fieldsWritten,
        HashSet<string> visitedMethods,
        CancellationToken ct)
    {
        if (!visitedMethods.Add(method.Identifier.Text))
        {
            return;
        }

        ct.ThrowIfCancellationRequested();

        SyntaxNode? body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
        if (body is null)
        {
            return;
        }

        foreach (var node in body.DescendantNodes())
        {
            ct.ThrowIfCancellationRequested();

            // Code inside an event-handler lambda runs when the handler fires, NOT
            // during Render, so its writes/calls must not be attributed to Render.
            // Without this the documented pattern `new Button("+", () => { count++; })`
            // produced a false CASCADE001 ("reactive field written inside Render()").
            // Reads still count, so a field used in a handler stays classified reactive.
            bool deferred = IsInHandlerLambda(node, body);

            switch (node)
            {
                case IdentifierNameSyntax identifier:
                    AnalyzeIdentifier(
                        identifier, semanticModel, classSymbol,
                        fieldsRead, fieldsWritten, countWrites: !deferred);
                    break;

                case InvocationExpressionSyntax invocation when !deferred:
                    FollowMethodCall(
                        invocation, classDecl, semanticModel, classSymbol,
                        fieldsRead, fieldsWritten, visitedMethods, ct);
                    break;
            }
        }
    }

    /// <summary>
    /// True when <paramref name="node"/> sits inside a lambda or anonymous-method
    /// body nested within <paramref name="body"/> — i.e. deferred event-handler code
    /// that runs on the event, not while Render() is producing its node tree.
    /// </summary>
    private static bool IsInHandlerLambda(SyntaxNode node, SyntaxNode body)
    {
        for (var current = node.Parent; current is not null && current != body; current = current.Parent)
        {
            if (current is SimpleLambdaExpressionSyntax
                or ParenthesizedLambdaExpressionSyntax
                or AnonymousMethodExpressionSyntax)
            {
                return true;
            }
        }
        return false;
    }

    private static void AnalyzeIdentifier(
        IdentifierNameSyntax identifier,
        SemanticModel semanticModel,
        INamedTypeSymbol classSymbol,
        HashSet<string> fieldsRead,
        List<(string name, Location location)> fieldsWritten,
        bool countWrites)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(identifier);
        if (symbolInfo.Symbol is not IFieldSymbol field)
        {
            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(field.ContainingType, classSymbol))
        {
            return;
        }

        if (field.IsStatic)
        {
            return;
        }

        if (IsAssignmentTarget(identifier))
        {
            // Deferred (handler-lambda) writes are not Render-time writes — skip them.
            if (countWrites)
            {
                fieldsWritten.Add((field.Name, identifier.GetLocation()));
            }
        }
        else
        {
            fieldsRead.Add(field.Name);
        }
    }

    private static bool IsAssignmentTarget(IdentifierNameSyntax identifier)
    {
        var parent = identifier.Parent;

        // Direct or compound assignment (=, +=, -=, *=, etc.)
        if (parent is AssignmentExpressionSyntax assignment && assignment.Left == identifier)
        {
            return true;
        }

        // Prefix ++/--
        if (parent is PrefixUnaryExpressionSyntax prefix
            && (prefix.IsKind(SyntaxKind.PreIncrementExpression)
                || prefix.IsKind(SyntaxKind.PreDecrementExpression)))
        {
            return true;
        }

        // Postfix ++/--
        if (parent is PostfixUnaryExpressionSyntax postfix
            && (postfix.IsKind(SyntaxKind.PostIncrementExpression)
                || postfix.IsKind(SyntaxKind.PostDecrementExpression)))
        {
            return true;
        }

        // ref/out argument
        if (parent is ArgumentSyntax arg
            && (arg.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                || arg.RefKindKeyword.IsKind(SyntaxKind.OutKeyword)))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Follows a method call to a method declared in the same class,
    /// transitively analyzing its body for field access.
    /// </summary>
    private static void FollowMethodCall(
        InvocationExpressionSyntax invocation,
        ClassDeclarationSyntax classDecl,
        SemanticModel semanticModel,
        INamedTypeSymbol classSymbol,
        HashSet<string> fieldsRead,
        List<(string name, Location location)> fieldsWritten,
        HashSet<string> visitedMethods,
        CancellationToken ct)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(invocation, ct);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, classSymbol))
        {
            return;
        }

        var calledMethod = classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == method.Name);

        if (calledMethod is not null)
        {
            AnalyzeMethodBody(
                calledMethod, classDecl, semanticModel, classSymbol,
                fieldsRead, fieldsWritten, visitedMethods, ct);
        }
    }

    private static bool HasAttribute(ISymbol symbol, string attributeFullName)
    {
        return symbol.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == attributeFullName);
    }
}

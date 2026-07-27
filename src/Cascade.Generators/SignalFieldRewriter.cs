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
/// Metadata about a reactive field identified in a Component subclass.
/// </summary>
internal sealed class ReactiveFieldInfo : IEquatable<ReactiveFieldInfo>
{
    public string Name { get; }
    public string TypeName { get; }
    public string? Initializer { get; }

    public ReactiveFieldInfo(string name, string typeName, string? initializer)
    {
        Name = name;
        TypeName = typeName;
        Initializer = initializer;
    }

    public bool Equals(ReactiveFieldInfo? other)
    {
        if (other is null)
        {
            return false;
        }
        return Name == other.Name
            && TypeName == other.TypeName
            && Initializer == other.Initializer;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ReactiveFieldInfo);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Name.GetHashCode();
            hash = hash * 397 + TypeName.GetHashCode();
            hash = hash * 397 + (Initializer?.GetHashCode() ?? 0);
            return hash;
        }
    }
}

/// <summary>
/// Records a field write detected inside Render() for CS-CASCADE-001 reporting.
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
/// Records a heap allocation detected inside Render() for CS-CASCADE-PERF-001 reporting.
/// </summary>
internal sealed class RenderAllocationInfo : IEquatable<RenderAllocationInfo>
{
    public string TypeName { get; }
    public int LineNumber { get; }
    public string FilePath { get; }

    public RenderAllocationInfo(string typeName, int lineNumber, string filePath)
    {
        TypeName = typeName;
        LineNumber = lineNumber;
        FilePath = filePath;
    }

    public bool Equals(RenderAllocationInfo? other)
    {
        if (other is null)
        {
            return false;
        }
        return TypeName == other.TypeName
            && LineNumber == other.LineNumber
            && FilePath == other.FilePath;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as RenderAllocationInfo);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = TypeName.GetHashCode();
            hash = hash * 397 + LineNumber;
            hash = hash * 397 + FilePath.GetHashCode();
            return hash;
        }
    }
}

/// <summary>
/// Complete reactivity model for a single Component subclass.
/// This is the equatable data flowing through the incremental pipeline —
/// changes are detected by comparing models, and downstream stages only
/// re-run when the model changes.
/// </summary>
internal sealed class ComponentReactivityModel : IEquatable<ComponentReactivityModel>
{
    public string? Namespace { get; }
    public string ClassName { get; }
    public string FileName { get; }
    public EquatableArray<ReactiveFieldInfo> ReactiveFields { get; }
    public EquatableArray<ComputedPropertyInfo> ComputedProperties { get; }
    public EquatableArray<BindCallInfo> BindCalls { get; }
    public EquatableArray<RenderWriteInfo> RenderWrites { get; }
    public EquatableArray<RenderAllocationInfo> RenderAllocations { get; }

    /// <summary>Whether the user's class declaration has the <c>partial</c> modifier.</summary>
    public bool IsPartial { get; }

    /// <summary>File path of the class declaration, for the "must be partial" diagnostic.</summary>
    public string ClassFilePath { get; }

    /// <summary>1-based line of the class declaration, for the "must be partial" diagnostic.</summary>
    public int ClassLineNumber { get; }

    public ComponentReactivityModel(
        string? ns,
        string className,
        string fileName,
        EquatableArray<ReactiveFieldInfo> reactiveFields,
        EquatableArray<ComputedPropertyInfo> computedProperties,
        EquatableArray<BindCallInfo> bindCalls,
        EquatableArray<RenderWriteInfo> renderWrites,
        EquatableArray<RenderAllocationInfo> renderAllocations,
        bool isPartial,
        string classFilePath,
        int classLineNumber)
    {
        Namespace = ns;
        ClassName = className;
        FileName = fileName;
        ReactiveFields = reactiveFields;
        ComputedProperties = computedProperties;
        BindCalls = bindCalls;
        RenderWrites = renderWrites;
        RenderAllocations = renderAllocations;
        IsPartial = isPartial;
        ClassFilePath = classFilePath;
        ClassLineNumber = classLineNumber;
    }

    public bool Equals(ComponentReactivityModel? other)
    {
        if (other is null)
        {
            return false;
        }
        return Namespace == other.Namespace
            && ClassName == other.ClassName
            && FileName == other.FileName
            && IsPartial == other.IsPartial
            && ClassFilePath == other.ClassFilePath
            && ClassLineNumber == other.ClassLineNumber
            && ReactiveFields.Equals(other.ReactiveFields)
            && ComputedProperties.Equals(other.ComputedProperties)
            && BindCalls.Equals(other.BindCalls)
            && RenderWrites.Equals(other.RenderWrites)
            && RenderAllocations.Equals(other.RenderAllocations);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ComponentReactivityModel);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Namespace?.GetHashCode() ?? 0;
            hash = hash * 397 + ClassName.GetHashCode();
            hash = hash * 397 + FileName.GetHashCode();
            hash = hash * 397 + IsPartial.GetHashCode();
            hash = hash * 397 + ClassFilePath.GetHashCode();
            hash = hash * 397 + ClassLineNumber;
            hash = hash * 397 + ReactiveFields.GetHashCode();
            hash = hash * 397 + ComputedProperties.GetHashCode();
            hash = hash * 397 + BindCalls.GetHashCode();
            hash = hash * 397 + RenderWrites.GetHashCode();
            hash = hash * 397 + RenderAllocations.GetHashCode();
            return hash;
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// Analysis logic
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Identifies reactive fields in Component subclasses by analyzing field usage
/// within the Render() call graph. This is the core analysis engine for the
/// reactivity inference system.
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
    /// Semantic analysis: builds the complete reactivity model for a Component subclass.
    /// Returns null if the class does not inherit from Component or has no Render() method.
    /// </summary>
    public static ComponentReactivityModel? Analyze(
        GeneratorSyntaxContext context,
        CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        // Capture whether the user declared the class `partial` and where — the reactivity
        // pipeline augments the class with a generated partial, so a non-partial declaration
        // must produce a clear diagnostic (CS-CASCADE-003) rather than a CS0260 collision.
        bool isPartial = classDecl.Modifiers.Any(SyntaxKind.PartialKeyword);
        var classLineSpan = classDecl.Identifier.GetLocation().GetLineSpan();
        string classFilePath = classLineSpan.Path ?? string.Empty;
        int classLineNumber = classLineSpan.StartLinePosition.Line + 1;

        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl, ct);
        if (classSymbol is null)
        {
            return null;
        }

        if (!InheritsFromComponent(classSymbol, semanticModel.Compilation))
        {
            return null;
        }

        // Skip nested types — they require more complex partial class generation
        if (classSymbol.ContainingType is not null)
        {
            return null;
        }

        ct.ThrowIfCancellationRequested();

        // Find all non-static instance fields declared in this class
        var allFields = classSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => !f.IsStatic && !f.IsImplicitlyDeclared)
            .ToList();

        // Find the Render() method
        var renderMethod = FindRenderMethod(classDecl);
        if (renderMethod is null)
        {
            return null;
        }

        // Walk the transitive Render() call graph to find field reads and writes
        var fieldsReadInRender = new HashSet<string>();
        var fieldsWrittenInRender = new List<(string name, Location location)>();
        var allocationsInRender = new List<(string typeName, Location location)>();

        AnalyzeMethodBody(
            renderMethod,
            classDecl,
            semanticModel,
            classSymbol,
            fieldsReadInRender,
            fieldsWrittenInRender,
            allocationsInRender,
            new HashSet<string>(),
            ct);

        ct.ThrowIfCancellationRequested();

        // Classify fields: reactive = non-readonly, non-static, read in Render, not [NotReactive]
        var reactiveFields = new List<ReactiveFieldInfo>();
        foreach (var field in allFields)
        {
            if (field.IsReadOnly)
            {
                continue;
            }

            if (HasAttribute(field, NotReactiveAttributeName))
            {
                continue;
            }

            if (fieldsReadInRender.Contains(field.Name))
            {
                var initializer = GetFieldInitializer(field, classDecl);
                reactiveFields.Add(new ReactiveFieldInfo(
                    field.Name,
                    field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    initializer));
            }
        }

        // Analyze computed properties
        var computedProperties = ComputedMemoizer.AnalyzeComputedProperties(
            classDecl, semanticModel, classSymbol, reactiveFields, ct);

        // Analyze Bind() calls
        var bindCalls = BindRewriter.AnalyzeBindCalls(
            renderMethod, semanticModel, classSymbol, ct);

        ct.ThrowIfCancellationRequested();

        // Build diagnostic data for writes to reactive fields inside Render()
        var reactiveFieldNames = new HashSet<string>(reactiveFields.Select(f => f.Name));
        var renderWrites = fieldsWrittenInRender
            .Where(w => reactiveFieldNames.Contains(w.name))
            .Select(w =>
            {
                var lineSpan = w.location.GetLineSpan();
                return new RenderWriteInfo(
                    w.name,
                    lineSpan.StartLinePosition.Line + 1,
                    lineSpan.Path ?? "");
            })
            .ToArray();

        // Build diagnostic data for allocations inside Render()
        var renderAllocs = allocationsInRender
            .Select(a =>
            {
                var lineSpan = a.location.GetLineSpan();
                return new RenderAllocationInfo(
                    a.typeName,
                    lineSpan.StartLinePosition.Line + 1,
                    lineSpan.Path ?? "");
            })
            .ToArray();

        // Determine the file name for the generated output hint
        var filePath = classDecl.SyntaxTree.FilePath;
        var fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = classSymbol.Name;
        }

        var ns = classSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : classSymbol.ContainingNamespace.ToDisplayString();

        return new ComponentReactivityModel(
            ns,
            classSymbol.Name,
            fileName,
            new EquatableArray<ReactiveFieldInfo>(reactiveFields.ToArray()),
            new EquatableArray<ComputedPropertyInfo>(computedProperties),
            new EquatableArray<BindCallInfo>(bindCalls),
            new EquatableArray<RenderWriteInfo>(renderWrites),
            new EquatableArray<RenderAllocationInfo>(renderAllocs),
            isPartial,
            classFilePath,
            classLineNumber);
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
    /// Walks a method body to find field reads, field writes, and allocations.
    /// Transitively follows calls to methods declared in the same class.
    /// </summary>
    private static void AnalyzeMethodBody(
        MethodDeclarationSyntax method,
        ClassDeclarationSyntax classDecl,
        SemanticModel semanticModel,
        INamedTypeSymbol classSymbol,
        HashSet<string> fieldsRead,
        List<(string name, Location location)> fieldsWritten,
        List<(string typeName, Location location)> allocations,
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
            // during Render, so its writes/allocations/calls must not be attributed
            // to Render. Without this the documented pattern
            // `new Button("+", () => { count++; })` produced a false CASCADE001
            // ("reactive field written inside Render()") and CASCADEPERF001. Reads
            // still count, so a field used in a handler stays classified reactive.
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
                        fieldsRead, fieldsWritten, allocations, visitedMethods, ct);
                    break;

                case ObjectCreationExpressionSyntax creation when !deferred:
                    DetectAllocation(creation, semanticModel, allocations);
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
        List<(string typeName, Location location)> allocations,
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
                fieldsRead, fieldsWritten, allocations, visitedMethods, ct);
        }
    }

    /// <summary>
    /// Detects reference type allocations (new expressions) for the performance diagnostic.
    /// Value type allocations and throw expressions are not flagged.
    /// </summary>
    private static void DetectAllocation(
        ObjectCreationExpressionSyntax creation,
        SemanticModel semanticModel,
        List<(string typeName, Location location)> allocations)
    {
        // Skip allocations inside throw expressions — exception creation is not hot-path
        if (creation.Parent is ThrowExpressionSyntax
            || creation.Parent is ThrowStatementSyntax)
        {
            return;
        }

        var typeInfo = semanticModel.GetTypeInfo(creation);
        if (typeInfo.Type is null)
        {
            return;
        }

        if (typeInfo.Type.IsValueType)
        {
            return;
        }

        allocations.Add((typeInfo.Type.ToDisplayString(), creation.GetLocation()));
    }

    private static string? GetFieldInitializer(IFieldSymbol field, ClassDeclarationSyntax classDecl)
    {
        foreach (var member in classDecl.Members)
        {
            if (member is FieldDeclarationSyntax fieldDecl)
            {
                foreach (var variable in fieldDecl.Declaration.Variables)
                {
                    if (variable.Identifier.Text == field.Name
                        && variable.Initializer is not null)
                    {
                        return variable.Initializer.Value.ToFullString().Trim();
                    }
                }
            }
        }
        return null;
    }

    private static bool HasAttribute(ISymbol symbol, string attributeFullName)
    {
        return symbol.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == attributeFullName);
    }
}

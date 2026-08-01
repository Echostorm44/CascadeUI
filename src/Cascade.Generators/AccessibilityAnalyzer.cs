using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cascade.Generators;

/// <summary>
/// Flags interactive elements and images built without an accessible label so the
/// CASCADEA11Y001/002 checks actually fire (analyzer audit 2026-08-01). Conservative
/// to avoid a false-positive flood: only an INLINE <c>new IconButton(...)</c> /
/// <c>new Image(...)</c> whose fluent chain contains no <c>.AccessibleLabel(...)</c>
/// call is flagged. A creation stored in a local/field is skipped — it may be labeled
/// elsewhere. An icon-only button and an image are the two elements that carry no
/// intrinsic text label, which is why only these two types are targeted.
/// </summary>
internal static class AccessibilityAnalyzer
{
    private const string IconButtonFullName = "Cascade.UI.IconButton";
    private const string ImageFullName = "Cascade.UI.Image";

    public static void Register(IncrementalGeneratorInitializationContext context)
    {
        var findings = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidate(node),
                transform: static (ctx, ct) => Analyze(ctx, ct))
            .Where(static f => f is not null);

        context.RegisterSourceOutput(findings, static (spc, f) =>
        {
            if (f is null)
            {
                return;
            }

            var descriptor = f.Value.IsImage
                ? AccessibilityDiagnostics.MissingImageAltText
                : AccessibilityDiagnostics.MissingAccessibleLabel;

            spc.ReportDiagnostic(Diagnostic.Create(descriptor, f.Value.Location, f.Value.TypeName));
        });
    }

    private readonly struct Finding : System.IEquatable<Finding>
    {
        public Finding(bool isImage, string typeName, Location location)
        {
            IsImage = isImage;
            TypeName = typeName;
            Location = location;
        }

        public bool IsImage { get; }
        public string TypeName { get; }
        public Location Location { get; }

        public bool Equals(Finding other) =>
            IsImage == other.IsImage && TypeName == other.TypeName && Location.Equals(other.Location);

        public override bool Equals(object? obj) => obj is Finding f && Equals(f);
        public override int GetHashCode() =>
            (IsImage.GetHashCode() * 397) ^ (TypeName.GetHashCode() * 31) ^ Location.GetHashCode();
    }

    private static bool IsCandidate(SyntaxNode node)
    {
        if (node is not ObjectCreationExpressionSyntax oc)
        {
            return false;
        }

        string? name = LastTypeName(oc.Type);
        return name is "IconButton" or "Image";
    }

    private static Finding? Analyze(GeneratorSyntaxContext ctx, System.Threading.CancellationToken ct)
    {
        var oc = (ObjectCreationExpressionSyntax)ctx.Node;

        // Confirm it is really the Cascade type, not a same-named user type.
        var type = ctx.SemanticModel.GetTypeInfo(oc, ct).Type;
        if (type is null)
        {
            return null;
        }

        string fullName = type.ToDisplayString();
        bool isImage = fullName == ImageFullName;
        if (!isImage && fullName != IconButtonFullName)
        {
            return null;
        }

        // Walk the fluent chain rooted at this creation; if any call is AccessibleLabel,
        // it is labeled. Track the outermost expression to test how the value is used.
        SyntaxNode current = oc;
        while (current.Parent is MemberAccessExpressionSyntax ma
            && ma.Parent is InvocationExpressionSyntax inv)
        {
            if (ma.Name.Identifier.Text == "AccessibleLabel")
            {
                return null; // explicitly labeled (empty string = decorative, also fine)
            }

            current = inv;
        }

        // Stored in a local/field or assigned? It may be labeled elsewhere — skip to stay
        // conservative. Only flag values used inline (arguments, collection items, returns).
        if (current.Parent is EqualsValueClauseSyntax or AssignmentExpressionSyntax)
        {
            return null;
        }

        string typeName = isImage ? "Image" : "IconButton";
        return new Finding(isImage, typeName, oc.GetLocation());
    }

    private static string? LastTypeName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        QualifiedNameSyntax q => q.Right.Identifier.Text,
        GenericNameSyntax g => g.Identifier.Text,
        _ => null,
    };
}

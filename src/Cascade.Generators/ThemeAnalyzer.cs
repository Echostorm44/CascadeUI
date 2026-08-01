using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cascade.Generators;

/// <summary>
/// Fires CASCADETHEME001 when a concrete <c>CascadeTheme</c> subclass does not override
/// a base token marked <c>[RequiredThemeToken]</c> — so a theme author's "you must
/// customise this" contract is enforced (analyzer audit 2026-08-01). Cascade's own
/// required global tokens are <c>abstract</c> and already compiler-enforced; this covers
/// the opt-in virtual case a design-system base theme declares.
/// </summary>
internal static class ThemeAnalyzer
{
    private const string RequiredAttr = "RequiredThemeTokenAttribute";
    private const string ThemeBase = "CascadeTheme";
    private const string Ns = "Cascade.UI";

    public static void Register(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (n, _) =>
                    n is ClassDeclarationSyntax c && !c.Modifiers.Any(SyntaxKind.AbstractKeyword),
                transform: static (ctx, ct) => Analyze(ctx, ct))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(models, static (spc, m) =>
        {
            if (m is null)
            {
                return;
            }

            foreach (var token in m.MissingTokens)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    ThemeDiagnostics.MissingRequiredTokenOverride, m.Location, m.ClassName, token));
            }
        });
    }

    private sealed class ThemeModel : System.IEquatable<ThemeModel>
    {
        public ThemeModel(string className, IReadOnlyList<string> missingTokens, Location location)
        {
            ClassName = className;
            MissingTokens = missingTokens;
            Location = location;
        }

        public string ClassName { get; }
        public IReadOnlyList<string> MissingTokens { get; }
        public Location Location { get; }

        public bool Equals(ThemeModel? other) =>
            other is not null && ClassName == other.ClassName && MissingTokens.SequenceEqual(other.MissingTokens);

        public override bool Equals(object? obj) => Equals(obj as ThemeModel);
        public override int GetHashCode()
        {
            int h = ClassName.GetHashCode();
            foreach (var t in MissingTokens)
            {
                h = (h * 397) ^ t.GetHashCode();
            }
            return h;
        }
    }

    private static ThemeModel? Analyze(GeneratorSyntaxContext ctx, System.Threading.CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        var classSymbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct);
        if (classSymbol is null || classSymbol.IsAbstract || !InheritsCascadeTheme(classSymbol))
        {
            return null;
        }

        var missing = new List<string>();
        for (var baseType = classSymbol.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            foreach (var member in baseType.GetMembers())
            {
                if (!HasRequiredAttribute(member))
                {
                    continue;
                }

                if (!IsOverriddenBetween(classSymbol, baseType, member.Name))
                {
                    missing.Add(member.Name);
                }
            }
        }

        return missing.Count == 0
            ? null
            : new ThemeModel(classSymbol.Name, missing, classDecl.Identifier.GetLocation());
    }

    private static bool InheritsCascadeTheme(INamedTypeSymbol type)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
        {
            if (t.Name == ThemeBase && t.ContainingNamespace?.ToDisplayString() == Ns)
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasRequiredAttribute(ISymbol member) =>
        member.GetAttributes().Any(a =>
            a.AttributeClass?.Name == RequiredAttr
            && a.AttributeClass.ContainingNamespace?.ToDisplayString() == Ns);

    // True if some type between the concrete class (inclusive) and the type that declared
    // the required member (exclusive) declares an override of it.
    private static bool IsOverriddenBetween(INamedTypeSymbol concrete, INamedTypeSymbol declaringBase, string name)
    {
        for (var t = concrete; t is not null && !SymbolEqualityComparer.Default.Equals(t, declaringBase); t = t.BaseType)
        {
            if (t.GetMembers(name).Any(m => m.IsOverride))
            {
                return true;
            }
        }
        return false;
    }
}

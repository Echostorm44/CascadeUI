using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Cascade.Generators;

/// <summary>
/// Reactivity analysis pipeline for the Cascade source generator.
///
/// <para>
/// This is a <b>diagnostics-only</b> pass — it emits no runtime code. Reactivity at
/// runtime is provided entirely by <c>Component.Bind(value, setter)</c> plus the
/// <c>SignalTracker</c>/<c>RenderScheduler</c> machinery; there is no generated reactive
/// plumbing. (A source generator cannot intercept a plain <c>field = x</c> assignment, so
/// "automatic" field interception is impossible; and computed properties run their own
/// getter body — the generator cannot own it — so there is nothing to memoize from here.)
/// </para>
///
/// <para>
/// The single rule this pass enforces is <c>CASCADE001</c>: a reactive field must not be
/// written inside <see cref="object"/> <c>Render()</c>, which must stay pure.
/// </para>
/// </summary>
internal static class ReactivityGenerator
{
    /// <summary>
    /// Registers the reactivity diagnostic pipeline with the incremental generator context.
    /// Called from <see cref="CascadeGenerator.Initialize"/>.
    /// </summary>
    public static void Register(IncrementalGeneratorInitializationContext context)
    {
        // Stage 1: syntax filter for class declarations with a base type (allocation-minimal).
        // Stage 2: semantic analysis — resolves Component inheritance and finds writes-in-Render.
        var componentModels = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: SignalFieldRewriter.IsComponentCandidate,
                transform: SignalFieldRewriter.Analyze)
            .Where(model => model is not null);

        // Stage 3: report diagnostics. No source is produced.
        context.RegisterSourceOutput(componentModels, static (spc, model) =>
        {
            if (model is null)
            {
                return;
            }

            ReportDiagnostics(spc, model);
        });
    }

    // ── Diagnostics ───────────────────────────────────────────────────

    private static void ReportDiagnostics(SourceProductionContext spc, ComponentReactivityModel model)
    {
        // CASCADE001: reactive field written inside Render(). Render() must be pure.
        foreach (var write in model.RenderWrites)
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                ReactivityDiagnostics.WriteInRender,
                CreateLocation(write.FilePath, write.LineNumber),
                write.FieldName));
        }
    }

    private static Location CreateLocation(string filePath, int lineNumber)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return Location.None;
        }

        var linePosition = new LinePosition(lineNumber - 1, 0);
        return Location.Create(
            filePath,
            TextSpan.FromBounds(0, 0),
            new LinePositionSpan(linePosition, linePosition));
    }
}

using Microsoft.CodeAnalysis;

namespace Cascade.Generators;

/// <summary>
/// Root incremental source generator for Cascade UI.
/// Individual generator responsibilities (reactivity, localization, icons, etc.)
/// are implemented in separate files in Phase 2 work packages (WP-200, WP-201, WP-202).
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class CascadeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        ReactivityGenerator.Register(context);
        LocalizationGenerator.Register(context);
        IconPackGenerator.Register(context);
        FontVerifier.Register(context);
        NavigationRegistrar.Register(context);
        AiSurfaceGenerator.Register(context);
        ApiIndexGenerator.Register(context);
        PersistStateGenerator.Register(context);
        StorageKeyGenerator.Register(context);
    }
}

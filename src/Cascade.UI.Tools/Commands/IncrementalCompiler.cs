using System.Collections.Immutable;
using System.Reflection.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using IOPath = System.IO.Path;

#pragma warning disable IL3000 // Assembly.Location is empty in single-file — acceptable for dev tooling
#pragma warning disable CS0619 // CreateInitialBaseline overloads are obsolete but no replacement API exists yet

namespace Cascade.UI.Tools.Commands;

/// <summary>
/// Roslyn-based incremental compiler that produces Edit-and-Continue deltas
/// for hot reload. Maintains a CSharpCompilation across file changes and
/// uses EmitDifference to produce metadata/IL/PDB deltas.
/// </summary>
internal sealed class IncrementalCompiler : IDisposable
{
    private readonly string projectDir;
    private readonly string outputAssemblyPath;
    private CSharpCompilation? compilation;
    private EmitBaseline? baseline;
    private ModuleMetadata? moduleMetadata;
    private int generation;
    private bool disposed;

    // Map from file path to its syntax tree in the compilation
    private readonly Dictionary<string, SyntaxTree> syntaxTrees = new(StringComparer.OrdinalIgnoreCase);

    public IncrementalCompiler(string projectDir, string outputAssemblyPath)
    {
        this.projectDir = projectDir;
        this.outputAssemblyPath = outputAssemblyPath;
    }

    /// <summary>Number of successful incremental compilations performed.</summary>
    public int Generation => generation;

    /// <summary>Whether the compiler has been initialized with a baseline.</summary>
    public bool IsInitialized => baseline is not null;

    /// <summary>
    /// Initializes the compiler by loading the project's source files,
    /// resolving references, and creating the initial compilation + baseline.
    /// Call this after a successful full build.
    /// </summary>
    public bool Initialize()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        try
        {
            string sep = IOPath.DirectorySeparatorChar.ToString();

            // Parse all .cs source files
            var sourceFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(sep + "obj" + sep, StringComparison.OrdinalIgnoreCase) &&
                            !f.Contains(sep + "bin" + sep, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            syntaxTrees.Clear();
            foreach (string file in sourceFiles)
            {
                string sourceText = System.IO.File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(
                    sourceText,
                    CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                    path: file);
                syntaxTrees[file] = tree;
            }

            // Resolve metadata references from the build output directory
            string outputDir = IOPath.GetDirectoryName(outputAssemblyPath) ?? projectDir;
            var references = ResolveReferences(outputDir);

            string assemblyName = IOPath.GetFileNameWithoutExtension(outputAssemblyPath);

            compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees.Values,
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Debug)
                    .WithPlatform(Microsoft.CodeAnalysis.Platform.AnyCpu));

            // Create the initial baseline from the already-built assembly
            if (System.IO.File.Exists(outputAssemblyPath))
            {
                string pdbPath = IOPath.ChangeExtension(outputAssemblyPath, ".pdb");
                if (System.IO.File.Exists(pdbPath))
                {
                    using var peStream = System.IO.File.OpenRead(outputAssemblyPath);
                    moduleMetadata?.Dispose();
                    moduleMetadata = ModuleMetadata.CreateFromStream(
                        peStream,
                        System.Reflection.PortableExecutable.PEStreamOptions.LeaveOpen);
                    baseline = EmitBaseline.CreateInitialBaseline(
                        compilation,
                        moduleMetadata,
                        (MethodDefinitionHandle handle) => default,
                        (MethodDefinitionHandle handle) => default,
                        true);
                    generation = 0;
                    return true;
                }
            }

            // If no existing assembly, do an initial emit to create the baseline
            using var initialPeStream = new MemoryStream();
            using var initialPdbStream = new MemoryStream();
            var emitResult = compilation.Emit(initialPeStream, initialPdbStream);
            if (!emitResult.Success)
            {
                return false;
            }

            initialPeStream.Position = 0;
            moduleMetadata?.Dispose();
            moduleMetadata = ModuleMetadata.CreateFromStream(
                initialPeStream,
                System.Reflection.PortableExecutable.PEStreamOptions.LeaveOpen);
            baseline = EmitBaseline.CreateInitialBaseline(
                compilation,
                moduleMetadata,
                (MethodDefinitionHandle handle) => default,
                (MethodDefinitionHandle handle) => default,
                true);
            generation = 0;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Performs an incremental compilation after a source file change.
    /// Returns a DeltaResult with the metadata/IL/PDB delta bytes,
    /// or null if incremental compilation failed and a full rebuild is needed.
    /// </summary>
    public DeltaResult? CompileIncremental(string changedFile)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (compilation is null || baseline is null)
        {
            return null;
        }

        try
        {
            // Read the new file content
            string newSource = System.IO.File.ReadAllText(changedFile);
            var newTree = CSharpSyntaxTree.ParseText(
                newSource,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                path: changedFile);

            // Check if we have the old tree
            if (!syntaxTrees.TryGetValue(changedFile, out var oldTree))
            {
                // New file added — can't do incremental, need full rebuild
                return null;
            }

            // Update the compilation with the new syntax tree
            var newCompilation = compilation.ReplaceSyntaxTree(oldTree, newTree);

            // Find what symbols changed between old and new
            var edits = ComputeSemanticEdits(compilation, newCompilation, oldTree, newTree);
            if (edits is null)
            {
                // Can't compute edits — structural change, need full rebuild
                return null;
            }

            // Emit the difference
            using var metadataStream = new MemoryStream();
            using var ilStream = new MemoryStream();
            using var pdbStream = new MemoryStream();

            var emitResult = newCompilation.EmitDifference(
                baseline,
                edits,
                isAddedSymbol: _ => false,
                metadataStream,
                ilStream,
                pdbStream,
                cancellationToken: default);

            if (!emitResult.Success)
            {
                // Check if any errors are just diagnostics vs real failures
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToArray();

                if (errors.Length > 0)
                {
                    return new DeltaResult
                    {
                        Success = false,
                        Errors = errors.Select(e => new CompileError
                        {
                            File = e.Location.GetLineSpan().Path,
                            Line = e.Location.GetLineSpan().StartLinePosition.Line + 1,
                            Column = e.Location.GetLineSpan().StartLinePosition.Character + 1,
                            Message = e.GetMessage(),
                        }).ToArray(),
                    };
                }

                return null;
            }

            // Update state for next incremental compilation
            baseline = emitResult.Baseline;
            compilation = newCompilation;
            syntaxTrees[changedFile] = newTree;
            generation++;

            return new DeltaResult
            {
                Success = true,
                MetadataBytes = metadataStream.ToArray(),
                IlDelta = ilStream.ToArray(),
                PdbDelta = pdbStream.ToArray(),
                UpdatedTypes = FindUpdatedTypeNames(oldTree, newTree, newCompilation),
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Computes the semantic edits between two compilations for a changed file.
    /// Returns null if the change is structural (requires restart).
    /// </summary>
    private static ImmutableArray<SemanticEdit>? ComputeSemanticEdits(
        CSharpCompilation oldCompilation,
        CSharpCompilation newCompilation,
        SyntaxTree oldTree,
        SyntaxTree newTree)
    {
        var oldModel = oldCompilation.GetSemanticModel(oldTree);
        var newModel = newCompilation.GetSemanticModel(newTree);

        var oldRoot = oldTree.GetRoot();
        var newRoot = newTree.GetRoot();

        // Find changed methods by comparing method bodies
        var edits = new List<SemanticEdit>();

        var oldMethods = oldRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .ToArray();
        var newMethods = newRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .ToArray();

        // Build a map of method signature → method node for the old tree
        var oldMethodMap = new Dictionary<string, MethodDeclarationSyntax>();
        foreach (var method in oldMethods)
        {
            string key = GetMethodKey(method, oldModel);
            if (key.Length > 0)
            {
                oldMethodMap[key] = method;
            }
        }

        foreach (var newMethod in newMethods)
        {
            string key = GetMethodKey(newMethod, newModel);
            if (key.Length == 0)
            {
                continue;
            }

            if (!oldMethodMap.TryGetValue(key, out var oldMethod))
            {
                // New method added — structural change
                return null;
            }

            // Compare method bodies
            string oldBody = oldMethod.Body?.ToFullString() ?? oldMethod.ExpressionBody?.ToFullString() ?? "";
            string newBody = newMethod.Body?.ToFullString() ?? newMethod.ExpressionBody?.ToFullString() ?? "";

            if (!string.Equals(oldBody, newBody, StringComparison.Ordinal))
            {
                var newSymbol = newModel.GetDeclaredSymbol(newMethod);
                var oldSymbol = oldModel.GetDeclaredSymbol(oldMethod);
                if (newSymbol is not null && oldSymbol is not null)
                {
                    edits.Add(new SemanticEdit(
                        SemanticEditKind.Update,
                        oldSymbol,
                        newSymbol));
                }
            }
        }

        // Check if methods were removed
        var newMethodMap = new Dictionary<string, MethodDeclarationSyntax>();
        foreach (var method in newMethods)
        {
            string key = GetMethodKey(method, newModel);
            if (key.Length > 0)
            {
                newMethodMap[key] = method;
            }
        }

        foreach (string key in oldMethodMap.Keys)
        {
            if (!newMethodMap.ContainsKey(key))
            {
                // Method removed — structural change
                return null;
            }
        }

        // Also check properties, constructors, etc. for structural changes
        int oldTypeCount = oldRoot.DescendantNodes()
            .OfType<TypeDeclarationSyntax>().Count();
        int newTypeCount = newRoot.DescendantNodes()
            .OfType<TypeDeclarationSyntax>().Count();
        if (oldTypeCount != newTypeCount)
        {
            return null; // Type added or removed
        }

        int oldFieldCount = oldRoot.DescendantNodes()
            .OfType<FieldDeclarationSyntax>().Count();
        int newFieldCount = newRoot.DescendantNodes()
            .OfType<FieldDeclarationSyntax>().Count();
        if (oldFieldCount != newFieldCount)
        {
            return null; // Field added or removed
        }

        if (edits.Count == 0)
        {
            // No method body changes detected — might be whitespace/comment only
            return ImmutableArray<SemanticEdit>.Empty;
        }

        return edits.ToImmutableArray();
    }

    /// <summary>
    /// Gets a stable key for a method based on its containing type and signature.
    /// </summary>
    private static string GetMethodKey(
        MethodDeclarationSyntax method,
        SemanticModel model)
    {
        var symbol = model.GetDeclaredSymbol(method);
        if (symbol is null)
        {
            return "";
        }

        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    /// <summary>
    /// Finds the fully-qualified names of types that were updated.
    /// </summary>
    private static string[] FindUpdatedTypeNames(
        SyntaxTree oldTree,
        SyntaxTree newTree,
        CSharpCompilation newCompilation)
    {
        var newModel = newCompilation.GetSemanticModel(newTree);
        var types = new HashSet<string>();

        // Find all type declarations in the new tree that contain changed methods
        var oldMethods = oldTree.GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Select(m => m.Body?.ToFullString() ?? m.ExpressionBody?.ToFullString() ?? "")
            .ToArray();

        int i = 0;
        foreach (var method in newTree.GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>())
        {
            string newBody = method.Body?.ToFullString() ?? method.ExpressionBody?.ToFullString() ?? "";
            if (i < oldMethods.Length && !string.Equals(newBody, oldMethods[i], StringComparison.Ordinal))
            {
                var containingType = method.Parent as TypeDeclarationSyntax;
                if (containingType is not null)
                {
                    var typeSymbol = newModel.GetDeclaredSymbol(containingType);
                    if (typeSymbol is not null)
                    {
                        types.Add(typeSymbol.ToDisplayString());
                    }
                }
            }
            i++;
        }

        return types.ToArray();
    }

    /// <summary>
    /// Resolves metadata references from DLLs in the output directory
    /// and the .NET runtime directory.
    /// </summary>
    private static List<MetadataReference> ResolveReferences(string outputDir)
    {
        var references = new List<MetadataReference>();

        // Add core runtime references
        string runtimeDir = IOPath.GetDirectoryName(typeof(object).Assembly.Location)!;
        foreach (string dll in Directory.GetFiles(runtimeDir, "*.dll"))
        {
            string fileName = IOPath.GetFileName(dll);
            // Skip native/resource-only assemblies
            if (fileName.StartsWith("api-ms-", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("clr", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("hostfxr", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("hostpolicy", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                references.Add(MetadataReference.CreateFromFile(dll));
            }
            catch (Exception)
            {
                // Skip files that can't be loaded as metadata
            }
        }

        // Add project output references
        if (Directory.Exists(outputDir))
        {
            foreach (string dll in Directory.GetFiles(outputDir, "*.dll"))
            {
                string fullPath = IOPath.GetFullPath(dll);
                // Don't double-add runtime DLLs
                if (fullPath.StartsWith(runtimeDir, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    references.Add(MetadataReference.CreateFromFile(dll));
                }
                catch (Exception)
                {
                    // Skip
                }
            }
        }

        return references;
    }

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            syntaxTrees.Clear();
            compilation = null;
            baseline = null;
            moduleMetadata?.Dispose();
            moduleMetadata = null;
        }
    }
}

/// <summary>Result of an incremental compilation attempt.</summary>
internal sealed class DeltaResult
{
    public required bool Success { get; init; }
    public byte[] MetadataBytes { get; init; } = [];
    public byte[] IlDelta { get; init; } = [];
    public byte[] PdbDelta { get; init; } = [];
    public string[] UpdatedTypes { get; init; } = [];
    public CompileError[] Errors { get; init; } = [];
}

/// <summary>A compilation error with location.</summary>
internal sealed class CompileError
{
    public required string File { get; init; }
    public required int Line { get; init; }
    public required int Column { get; init; }
    public required string Message { get; init; }
}

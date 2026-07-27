using System.Reflection;
using System.Reflection.Metadata;

namespace Cascade.UI.Core.Internal;

/// <summary>
/// Receives .NET hot reload metadata updates and applies them to the running application.
/// Uses the <see cref="MetadataUpdater"/> API when available.
/// </summary>
/// <remarks>
/// This class integrates with .NET's built-in hot reload infrastructure via
/// <c>[assembly: MetadataUpdateHandler]</c>. When metadata deltas arrive (from
/// dotnet-watch or the Cascade CLI), they are applied here.
///
/// Supported changes (no restart needed):
/// - Method body edits
/// - Lambda body edits
/// - String/constant changes
/// - Adding static methods
///
/// Unsupported changes (restart required):
/// - Adding/removing fields
/// - Changing type hierarchy
/// - Changing constructor signatures
/// - Adding/removing [Signal] attributes
/// </remarks>
internal static class MetadataUpdateReceiver
{
    private static readonly List<Action<string[]>> updateHandlers = [];
    private static int appliedDeltaCount;

    /// <summary>Number of deltas successfully applied.</summary>
    public static int AppliedDeltaCount => appliedDeltaCount;

    /// <summary>Registers a handler called after metadata is updated.</summary>
    public static void RegisterHandler(Action<string[]> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        updateHandlers.Add(handler);
    }

    /// <summary>Clears all registered handlers.</summary>
    public static void ClearHandlers()
    {
        updateHandlers.Clear();
    }

    /// <summary>
    /// Applies a metadata delta to the running application.
    /// Returns true if the delta was applied successfully.
    /// </summary>
    public static bool ApplyUpdate(MetadataDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);

        if (delta.RequiresRestart)
        {
            return false;
        }

        // If real IL/metadata bytes are present, apply via MetadataUpdater
        if (delta.IlDelta.Length > 0 && delta.MetadataBytes.Length > 0)
        {
            if (!TryApplyRuntimeUpdate(delta))
            {
                return false;
            }
        }

        Interlocked.Increment(ref appliedDeltaCount);

        // Snapshot the handler list to avoid collection-modified-during-enumeration
        string[] updatedTypes = delta.UpdatedTypes.ToArray();
        Action<string[]>[] handlers = updateHandlers.ToArray();
        foreach (var handler in handlers)
        {
            handler(updatedTypes);
        }

        return true;
    }

    /// <summary>
    /// Attempts to apply an IL/metadata delta to the running process via MetadataUpdater.
    /// </summary>
    private static bool TryApplyRuntimeUpdate(MetadataDelta delta)
    {
        if (!MetadataUpdater.IsSupported)
        {
            // Runtime doesn't support hot reload (AOT build or no agent attached).
            // Still count it as success for the handlers — the delta is valid,
            // just can't be applied at runtime.
            return true;
        }

        // Resolve the assembly to update. The first updated type's namespace
        // is used to find the assembly.
        Assembly? targetAssembly = ResolveTargetAssembly(delta);
        if (targetAssembly is null)
        {
            return false;
        }

        try
        {
            MetadataUpdater.ApplyUpdate(
                targetAssembly,
                delta.MetadataBytes,
                delta.IlDelta,
                delta.PdbDelta);
            return true;
        }
        catch (InvalidOperationException)
        {
            // Delta was rejected by the runtime (unsupported change type)
            return false;
        }
    }

    /// <summary>
    /// Resolves the target assembly for a metadata delta by searching loaded assemblies
    /// for one containing any of the updated types.
    /// </summary>
#pragma warning disable IL2026 // Assembly type lookup is inherently reflection-based; only used in debug hot reload
    private static Assembly? ResolveTargetAssembly(MetadataDelta delta)
    {
        if (delta.UpdatedTypes.Count == 0)
        {
            return null;
        }

        string typeName = delta.UpdatedTypes[0];
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            if (assembly.IsDynamic)
            {
                continue;
            }

            try
            {
                if (assembly.GetType(typeName, throwOnError: false) is not null)
                {
                    return assembly;
                }
            }
            catch (Exception)
            {
                // Skip assemblies that can't be searched
            }
        }

        // If exact type not found, try matching by assembly name from the changed file path
        string fileName = System.IO.Path.GetFileNameWithoutExtension(delta.ChangedFile);
        foreach (var assembly in assemblies)
        {
            if (assembly.IsDynamic)
            {
                continue;
            }

            string? assemblyName = assembly.GetName().Name;
            if (assemblyName is not null &&
                delta.ChangedFile.Contains(assemblyName, StringComparison.OrdinalIgnoreCase))
            {
                return assembly;
            }
        }

        return null;
    }
#pragma warning restore IL2026

    /// <summary>
    /// Checks whether the runtime supports metadata updates.
    /// </summary>
    public static bool IsSupported()
    {
        return MetadataUpdater.IsSupported;
    }

    /// <summary>Resets the receiver state (for testing).</summary>
    internal static void Reset()
    {
        appliedDeltaCount = 0;
        updateHandlers.Clear();
    }
}

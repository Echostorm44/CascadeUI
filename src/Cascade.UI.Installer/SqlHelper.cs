#pragma warning disable CA1822 // SqlHelper is an instance API (ctx.Sql.For<T>()) by design.

namespace Cascade.UI.Installer;

/// <summary>
/// Install-time database operations, implemented by optional provider packages so that core
/// <c>Cascade.UI.Installer</c> takes no database dependency. Reference a provider (e.g.
/// <c>Cascade.UI.Installer.Sqlite</c>) and obtain it via <see cref="SqlHelper.For{TProvider}"/>.
/// </summary>
public interface ISqlInstallerProvider
{
    /// <summary>Opens a connection to verify it is reachable. Returns false on failure rather than throwing.</summary>
    Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default);

    /// <summary>Executes the SQL script at <paramref name="scriptPath"/> against the connection.</summary>
    Task RunSchemaAsync(string connectionString, string scriptPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <c>*.sql</c> migration files from <paramref name="migrationsDirectory"/> in lexicographic
    /// order, skipping those already applied (tracked in a <c>__migrations</c> table).
    /// </summary>
    Task RunMigrationsAsync(string connectionString, string migrationsDirectory, CancellationToken cancellationToken = default);
}

/// <summary>
/// Gateway to install-time database helpers (exposed as <see cref="InstallContext.Sql"/>). Core stays
/// dependency-free; reference a provider package and call
/// <c>ctx.Sql.For&lt;SqliteInstallerProvider&gt;().RunSchemaAsync(...)</c>.
/// </summary>
public sealed class SqlHelper
{
    /// <summary>Creates the requested install-time database provider.</summary>
    public TProvider For<TProvider>() where TProvider : ISqlInstallerProvider, new() => new();
}

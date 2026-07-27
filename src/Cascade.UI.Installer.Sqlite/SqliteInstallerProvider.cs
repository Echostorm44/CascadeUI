using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using IOPath = System.IO.Path;

namespace Cascade.UI.Installer.Sqlite;

/// <summary>
/// The SQLite implementation of <see cref="ISqlInstallerProvider"/>. Use via
/// <c>ctx.Sql.For&lt;SqliteInstallerProvider&gt;()</c>. Connection strings are SQLite ones
/// (e.g. <c>Data Source=C:\path\app.db</c>); <see cref="DefaultConnectionString"/> builds one in AppData.
/// </summary>
public sealed class SqliteInstallerProvider : ISqlInstallerProvider
{
    public async Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    public async Task RunSchemaAsync(string connectionString, string scriptPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        ArgumentException.ThrowIfNullOrEmpty(scriptPath);
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("Schema script not found.", scriptPath);
        }

        string sql = await File.ReadAllTextAsync(scriptPath, cancellationToken).ConfigureAwait(false);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, sql, cancellationToken).ConfigureAwait(false);
    }

    public async Task RunMigrationsAsync(string connectionString, string migrationsDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        ArgumentException.ThrowIfNullOrEmpty(migrationsDirectory);
        if (!Directory.Exists(migrationsDirectory))
        {
            return;
        }

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS __migrations (name TEXT PRIMARY KEY, appliedAt TEXT NOT NULL);", cancellationToken).ConfigureAwait(false);

        var applied = new HashSet<string>(StringComparer.Ordinal);
        await using (SqliteCommand query = connection.CreateCommand())
        {
            query.CommandText = "SELECT name FROM __migrations;";
            await using SqliteDataReader reader = await query.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                applied.Add(reader.GetString(0));
            }
        }

        foreach (string file in Directory.GetFiles(migrationsDirectory, "*.sql").OrderBy(IOPath.GetFileName, StringComparer.Ordinal))
        {
            string name = IOPath.GetFileName(file);
            if (applied.Contains(name))
            {
                continue;
            }

            string sql = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(connection, sql, cancellationToken).ConfigureAwait(false);

            await using SqliteCommand insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO __migrations (name, appliedAt) VALUES ($name, $time);";
            insert.Parameters.AddWithValue("$name", name);
            insert.Parameters.AddWithValue("$time", DateTimeOffset.UtcNow.ToString("O"));
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Builds a SQLite connection string for a per-user database in <c>%APPDATA%\{appName}\{appName}.db</c>.</summary>
    public static string DefaultConnectionString(string appName)
    {
        ArgumentException.ThrowIfNullOrEmpty(appName);
        string dir = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);
        Directory.CreateDirectory(dir);
        return "Data Source=" + IOPath.Combine(dir, appName + ".db");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100",
        Justification = "SQL is a developer-authored schema/migration script bundled in the installer, not user input.")]
    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

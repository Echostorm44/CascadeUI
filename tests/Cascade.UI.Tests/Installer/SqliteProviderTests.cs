using System;
using System.IO;
using System.Threading.Tasks;
using Cascade.UI.Installer.Sqlite;
using Microsoft.Data.Sqlite;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using IOPath = System.IO.Path;

namespace Cascade.UI.Tests.Installer;

/// <summary>The SQLite install-time provider: schema execution, idempotent migrations, connection test.</summary>
public sealed class SqliteProviderTests
{
    [Test]
    public async Task RunSchema_ExecutesScript_AndTestConnection()
    {
        string dir = NewTempDir();
        string cs = "Data Source=" + IOPath.Combine(dir, "app.db");
        string schema = IOPath.Combine(dir, "schema.sql");
        await File.WriteAllTextAsync(schema, "CREATE TABLE items (id INTEGER PRIMARY KEY, name TEXT); INSERT INTO items (id, name) VALUES (1, 'hello');");

        try
        {
            var provider = new SqliteInstallerProvider();
            await provider.RunSchemaAsync(cs, schema);

            await Assert.That(await provider.TestConnectionAsync(cs)).IsTrue();
            await Assert.That(await ScalarAsync(cs, "SELECT name FROM items WHERE id = 1;")).IsEqualTo("hello");
        }
        finally
        {
            CleanUp(dir);
        }
    }

    [Test]
    public async Task RunMigrations_AppliesEachOnce_AndIsIdempotent()
    {
        string dir = NewTempDir();
        string cs = "Data Source=" + IOPath.Combine(dir, "app.db");
        string migrations = IOPath.Combine(dir, "migrations");
        Directory.CreateDirectory(migrations);
        await File.WriteAllTextAsync(IOPath.Combine(migrations, "001_init.sql"), "CREATE TABLE a (x INTEGER);");
        await File.WriteAllTextAsync(IOPath.Combine(migrations, "002_more.sql"), "CREATE TABLE b (y INTEGER);");

        try
        {
            var provider = new SqliteInstallerProvider();
            await provider.RunMigrationsAsync(cs, migrations);
            await provider.RunMigrationsAsync(cs, migrations); // re-run must not re-apply (no "table exists" error)

            await Assert.That(Convert.ToInt64(await ScalarAsync(cs, "SELECT COUNT(*) FROM __migrations;"))).IsEqualTo(2L);
            // Both tables exist (querying them would throw if missing).
            await Assert.That(await ScalarAsync(cs, "SELECT COUNT(*) FROM a;")).IsNotNull();
            await Assert.That(await ScalarAsync(cs, "SELECT COUNT(*) FROM b;")).IsNotNull();
        }
        finally
        {
            CleanUp(dir);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100", Justification = "Test SQL is hardcoded, not user input.")]
    private static async Task<object?> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private static string NewTempDir()
    {
        string dir = IOPath.Combine(IOPath.GetTempPath(), "cascade-sqlite-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CleanUp(string dir)
    {
        SqliteConnection.ClearAllPools(); // release the db file handle so the dir can be deleted
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}

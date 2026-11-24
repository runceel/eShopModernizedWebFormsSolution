using System.Diagnostics;
using System.Reflection;
using eShop.CatalogData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace eShop.CatalogApi.Tests;

[CollectionDefinition("PostgreSQL")]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlContainer>;

// The only server these tests access is this disposable, test-owned container.
public sealed class PostgreSqlContainer : IAsyncLifetime
{
    private readonly string name = "catalog-api-tests-" + Guid.NewGuid().ToString("N");
    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        try
        {
            await Run("run", "-d", "--name", name, "-e", "POSTGRES_PASSWORD=test-only",
                "-p", "127.0.0.1::5432", "docker.io/library/postgres:17");
            var portText = await Run("port", name, "5432/tcp");
            var port = int.Parse(portText.Trim().Split(':').Last());
            ConnectionString = new NpgsqlConnectionStringBuilder
            {
                Host = "127.0.0.1", Port = port, Database = "postgres",
                Username = "postgres", Password = "test-only", Pooling = false, Timeout = 2
            }.ConnectionString;

            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (true)
            {
                try
                {
                    await using var connection = new NpgsqlConnection(ConnectionString);
                    await connection.OpenAsync();
                    break;
                }
                catch (Exception ex) when (ex is NpgsqlException or System.Net.Sockets.SocketException)
                {
                    if (DateTime.UtcNow >= deadline)
                        throw new InvalidOperationException("Test-owned PostgreSQL did not become ready.", ex);
                    await Task.Delay(500);
                }
            }
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        try { await Run("rm", "-f", name); }
        catch (InvalidOperationException) { /* Container may never have been created. */ }
    }

    private static async Task<string> Run(params string[] args)
    {
        var start = new ProcessStartInfo("podman") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start podman.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var result = await stdout;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"podman {args[0]} failed: {await stderr}");
        return result;
    }
}

[Collection("PostgreSQL")]
public abstract class CatalogTestBase(PostgreSqlContainer container) : IAsyncLifetime
{
    private readonly string databaseName = "test_" + Guid.NewGuid().ToString("N");
    private IDisposable? factory;
    protected HttpClient Client { get; private set; } = null!;
    protected string ConnectionString { get; private set; } = "";
    protected virtual bool InitializeDatabase => true;

    public async Task InitializeAsync()
    {
        await using (var admin = new NpgsqlConnection(container.ConnectionString))
        {
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin);
            await command.ExecuteNonQueryAsync();
        }
        ConnectionString = new NpgsqlConnectionStringBuilder(container.ConnectionString)
        {
            Database = databaseName
        }.ConnectionString;
        if (InitializeDatabase)
        {
            await using var db = CreateDbContext();
            await CatalogSeeder.InitializeAsync(db);
        }

        var entryPoint = Assembly.Load("eShop.CatalogApi").EntryPoint!.DeclaringType!;
        var create = typeof(CatalogTestBase).GetMethod(nameof(CreateFactory), BindingFlags.Static | BindingFlags.NonPublic)!;
        (factory, Client) = ((IDisposable, HttpClient))create.MakeGenericMethod(entryPoint).Invoke(null, [ConnectionString])!;
    }

    private static (IDisposable, HttpClient) CreateFactory<TEntryPoint>(string connectionString) where TEntryPoint : class
    {
        var app = new WebApplicationFactory<TEntryPoint>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:catalog", connectionString);
        });
        return (app, app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }));
    }

    protected CatalogDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(ConnectionString).Options);

    public Task DisposeAsync()
    {
        Client?.Dispose();
        factory?.Dispose();
        // All databases disappear when the test-owned container is removed.
        return Task.CompletedTask;
    }
}

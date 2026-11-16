using Npgsql;

namespace eShop.CatalogData;

public sealed class CatalogDatabase(NpgsqlDataSource dataSource)
{
    public async Task CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT 1", connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);

        if (result is not int value || value != 1)
        {
            throw new InvalidOperationException("PostgreSQL readiness query returned an unexpected result.");
        }
    }
}

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Pervaxis.Vpa.Vending.Infrastructure.HealthChecks;

public sealed class PostgresHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("VendingDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Unhealthy(
                "PostgreSQL connection string is not configured.");
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            var result = await command.ExecuteScalarAsync(cancellationToken);

            return Equals(result, 1)
                ? HealthCheckResult.Healthy("PostgreSQL connection succeeded.")
                : HealthCheckResult.Unhealthy("PostgreSQL test query returned an unexpected result.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "PostgreSQL connection failed.",
                exception);
        }
    }
}

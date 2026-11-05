using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Pervaxis.Vpa.Vending.Infrastructure.Database;

namespace Pervaxis.Vpa.Vending.Infrastructure.HealthChecks;

public sealed class PostgresHealthCheck(
    IServiceProvider services,
    ILogger<PostgresHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dataSource = services.GetRequiredService<NpgsqlDataSource>();
            await using var connection = dataSource.CreateConnection();
            await services.GetRequiredService<DatabaseConnectionOpener>().OpenAsync(connection, cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            var result = await command.ExecuteScalarAsync(cancellationToken);

            return Equals(result, 1)
                ? HealthCheckResult.Healthy("PostgreSQL connection succeeded.")
                : HealthCheckResult.Unhealthy("PostgreSQL test query returned an unexpected result.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "PostgreSQL readiness check failed.");
            return HealthCheckResult.Unhealthy(
                "PostgreSQL connection failed.",
                exception);
        }
    }
}

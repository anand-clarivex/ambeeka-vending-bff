using Amazon.SecretsManager;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pervaxis.Vpa.Vending.Data;

namespace Pervaxis.Vpa.Vending.Infrastructure.Database;

public static class DatabaseServiceCollectionExtensions
{
    public static IServiceCollection AddVendingDatabase(
        this IServiceCollection services, IConfiguration configuration)
    {
        var secretId = configuration["Database:SecretId"];
        if (!string.IsNullOrWhiteSpace(secretId))
        {
            services.AddSingleton(provider => new DatabaseSecretReader(
                provider.GetRequiredService<IAmazonSecretsManager>(), secretId,
                configuration["Database:RootCertificate"], configuration["Database:CredentialsSecretId"]));
        }

        services.AddSingleton<NpgsqlDataSource>(provider =>
        {
            if (!string.IsNullOrWhiteSpace(secretId))
            {
                // Singleton factories are synchronous; secret lookup stays lazy until first database use.
                var reader = provider.GetRequiredService<DatabaseSecretReader>();
                var initial = reader.ReadAsync(CancellationToken.None).GetAwaiter().GetResult();
                return NpgsqlDataSource.Create(initial.ConnectionString);
            }

            var connectionString = configuration.GetConnectionString("VendingDatabase");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("PostgreSQL connection settings are not configured.");
            return NpgsqlDataSource.Create(connectionString);
        });
        services.AddSingleton(provider => new DatabaseConnectionOpener(
            provider.GetRequiredService<NpgsqlDataSource>(),
            string.IsNullOrWhiteSpace(secretId)
                ? null
                : provider.GetRequiredService<DatabaseSecretReader>().ReadAsync));
        services.AddSingleton<DatabaseConnectionInterceptor>();
        services.AddDbContext<VendingDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>())
                .AddInterceptors(provider.GetRequiredService<DatabaseConnectionInterceptor>()));

        return services;
    }
}

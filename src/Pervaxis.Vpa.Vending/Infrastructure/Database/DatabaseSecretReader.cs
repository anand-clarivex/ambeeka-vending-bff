using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Npgsql;
using System.Text.Json;

namespace Pervaxis.Vpa.Vending.Infrastructure.Database;

public sealed class DatabaseSecretReader(
    IAmazonSecretsManager client, string secretId, string? rootCertificate = null,
    string? credentialsSecretId = null)
{
    public async Task<NpgsqlConnectionStringBuilder> ReadAsync(CancellationToken cancellationToken)
    {
        var response = await client.GetSecretValueAsync(new GetSecretValueRequest
        {
            SecretId = secretId,
            VersionStage = "AWSCURRENT"
        }, cancellationToken).ConfigureAwait(false);
        var connection = Parse(response.SecretString);
        if (!string.IsNullOrWhiteSpace(credentialsSecretId))
        {
            var credentials = await client.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = credentialsSecretId,
                VersionStage = "AWSCURRENT"
            }, cancellationToken).ConfigureAwait(false);
            ApplyCredentials(connection, credentials.SecretString);
        }
        if (!string.IsNullOrWhiteSpace(rootCertificate))
            connection.RootCertificate = rootCertificate;
        return connection;
    }

    public static void ApplyCredentials(NpgsqlConnectionStringBuilder connection, string? secret)
    {
        try
        {
            using var document = JsonDocument.Parse(secret ?? "{}");
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("username", out var username)
                || !root.TryGetProperty("password", out var password)
                || username.ValueKind != JsonValueKind.String || password.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(username.GetString()) || string.IsNullOrWhiteSpace(password.GetString()))
                throw new InvalidOperationException("The database credentials secret requires username and password.");
            connection.Username = username.GetString();
            connection.Password = password.GetString();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("The database credentials secret has an invalid format.");
        }
    }

    public static NpgsqlConnectionStringBuilder Parse(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("The database secret must contain a JSON string value.");

        try
        {
            using var document = JsonDocument.Parse(secret);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("ConnectionStrings__VendingDatabase", out var value)
                || value.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException("The database secret requires ConnectionStrings__VendingDatabase.");

            var connection = new NpgsqlConnectionStringBuilder(value.GetString());
            if (string.IsNullOrWhiteSpace(connection.Host)
                || string.IsNullOrWhiteSpace(connection.Database)
                || string.IsNullOrWhiteSpace(connection.Username)
                || string.IsNullOrWhiteSpace(connection.Password))
                throw new InvalidOperationException("The database secret requires host, database, username and password.");
            return connection;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            // Do not expose malformed connection strings or secret values in exceptions.
            throw new InvalidOperationException("The database secret has an invalid format.");
        }
    }

    public static string GetRefreshedPassword(
        NpgsqlConnectionStringBuilder original, NpgsqlConnectionStringBuilder refreshed)
    {
        var originalSettings = new NpgsqlConnectionStringBuilder(original.ConnectionString);
        var refreshedSettings = new NpgsqlConnectionStringBuilder(refreshed.ConnectionString);
        originalSettings.Remove("Password");
        refreshedSettings.Remove("Password");
        if (!originalSettings.EquivalentTo(refreshedSettings))
            throw new InvalidOperationException("Database connection settings changed. Restart the API to load the new settings.");
        return refreshed.Password!;
    }
}

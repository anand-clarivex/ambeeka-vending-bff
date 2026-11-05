using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Moq;
using Npgsql;
using Pervaxis.Vpa.Vending.Infrastructure.Database;
using System.Text.Json;
using Xunit;

namespace Pervaxis.Vpa.Vending.Tests;

public sealed class DatabaseSecretReaderTests
{
    [Fact]
    public async Task UsesRdsManagedCredentialsInsteadOfStaleConnectionPassword()
    {
        var client = new Mock<IAmazonSecretsManager>();
        client.Setup(x => x.GetSecretValueAsync(
                It.Is<GetSecretValueRequest>(r => r.SecretId == "connection" && r.VersionStage == "AWSCURRENT"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = Secret("stale") });
        client.Setup(x => x.GetSecretValueAsync(
                It.Is<GetSecretValueRequest>(r => r.SecretId == "rds" && r.VersionStage == "AWSCURRENT"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = "{\"username\":\"postgres\",\"password\":\"current\"}" });
        var reader = new DatabaseSecretReader(client.Object, "connection", "local.pem", "rds");
        var connection = await reader.ReadAsync(CancellationToken.None);
        Assert.Equal("current", connection.Password);
        Assert.Equal("postgres", connection.Username);
        Assert.Equal("local.pem", connection.RootCertificate);
    }

    [Fact]
    public async Task ReadsCurrentSecretAgainAfterPasswordChanges()
    {
        var client = new Mock<IAmazonSecretsManager>();
        client.SetupSequence(x => x.GetSecretValueAsync(
                It.Is<GetSecretValueRequest>(r => r.SecretId == "database" && r.VersionStage == "AWSCURRENT"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = Secret("first") })
            .ReturnsAsync(new GetSecretValueResponse { SecretString = Secret("rotated;=password") });
        var reader = new DatabaseSecretReader(client.Object, "database");
        var initial = await reader.ReadAsync(CancellationToken.None);
        var updated = await reader.ReadAsync(CancellationToken.None);

        Assert.Equal("rotated;=password", DatabaseSecretReader.GetRefreshedPassword(initial, updated));
    }

    [Theory]
    [InlineData("not-json-private-value")]
    [InlineData("{\"ConnectionStrings__VendingDatabase\":\"bad-private-value\"}")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("\"private-value\"")]
    [InlineData("null")]
    public void RejectsInvalidSecretsWithoutLeakingTheirContents(string secret)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => DatabaseSecretReader.Parse(secret));
        Assert.DoesNotContain("private-value", exception.ToString());
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"username\":\"postgres\",\"password\":123}")]
    [InlineData("{\"username\":\"postgres\",\"password\":\"\"}")]
    [InlineData("private-value")]
    public void RejectsInvalidManagedCredentialsWithoutLeakingTheirContents(string secret)
    {
        var connection = new NpgsqlConnectionStringBuilder();
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DatabaseSecretReader.ApplyCredentials(connection, secret));
        Assert.DoesNotContain("private-value", exception.ToString());
    }

    [Fact]
    public void RequiresRestartIfSettingsOtherThanPasswordChange()
    {
        var original = DatabaseSecretReader.Parse(Secret("first"));
        var updated = DatabaseSecretReader.Parse(Secret("second"));
        updated.Username = "different-user";
        Assert.Throws<InvalidOperationException>(() => DatabaseSecretReader.GetRefreshedPassword(original, updated));
    }

    private static string Secret(string password) => JsonSerializer.Serialize(new Dictionary<string, string>
    {
        ["ConnectionStrings__VendingDatabase"] = new NpgsqlConnectionStringBuilder
        {
            Host = "localhost", Database = "test", Username = "test", Password = password
        }.ConnectionString
    });
}

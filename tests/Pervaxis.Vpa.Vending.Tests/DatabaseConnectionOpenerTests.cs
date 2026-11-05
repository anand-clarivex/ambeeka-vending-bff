using System.Data.Common;
using Moq;
using Npgsql;
using Pervaxis.Vpa.Vending.Infrastructure.Database;
using Xunit;

namespace Pervaxis.Vpa.Vending.Tests;

public sealed class DatabaseConnectionOpenerTests
{
    private const string Settings = "Host=localhost;Database=test;Username=test;Password=old";

    [Fact]
    public async Task SuccessfulOpenDoesNotReadSecrets()
    {
        using var source = NpgsqlDataSource.Create(Settings);
        var refreshes = 0;
        using var opener = new DatabaseConnectionOpener(source, _ =>
        {
            refreshes++;
            return Task.FromResult(new NpgsqlConnectionStringBuilder(Settings));
        });
        var connection = new Mock<DbConnection>();
        connection.Setup(x => x.OpenAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await opener.OpenAsync(connection.Object);
        Assert.Equal(0, refreshes);
        connection.Verify(x => x.OpenAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticationFailureRefreshesAndRetriesExactlyOnce()
    {
        using var source = NpgsqlDataSource.Create(Settings);
        var refreshes = 0;
        using var opener = new DatabaseConnectionOpener(source, _ =>
        {
            refreshes++;
            return Task.FromResult(new NpgsqlConnectionStringBuilder(Settings) { Password = "new" });
        });
        var connection = new Mock<DbConnection>();
        connection.SetupSequence(x => x.OpenAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(InvalidPassword()).Returns(Task.CompletedTask);
        await opener.OpenAsync(connection.Object);
        Assert.Equal(1, refreshes);
        connection.Verify(x => x.OpenAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData("08006")]
    [InlineData("42501")]
    public async Task OtherPostgresErrorsDoNotRefreshOrRetry(string sqlState)
    {
        using var source = NpgsqlDataSource.Create(Settings);
        var refreshes = 0;
        using var opener = new DatabaseConnectionOpener(source, _ =>
        {
            refreshes++;
            return Task.FromResult(new NpgsqlConnectionStringBuilder(Settings));
        });
        var connection = new Mock<DbConnection>();
        connection.Setup(x => x.OpenAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PostgresException("failure", "ERROR", "ERROR", sqlState));
        await Assert.ThrowsAsync<PostgresException>(() => opener.OpenAsync(connection.Object));
        Assert.Equal(0, refreshes);
        connection.Verify(x => x.OpenAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConcurrentFailuresShareOneRefresh()
    {
        using var source = NpgsqlDataSource.Create(Settings);
        var clock = new TestClock();
        var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshes = 0;
        using var opener = new DatabaseConnectionOpener(source, async _ =>
        {
            Interlocked.Increment(ref refreshes);
            refreshStarted.SetResult();
            await releaseRefresh.Task;
            return new NpgsqlConnectionStringBuilder(Settings) { Password = "new" };
        }, clock);
        var started = 0;
        var connections = Enumerable.Range(0, 10).Select(_ =>
        {
            var connection = new Mock<DbConnection>();
            var calls = 0;
            connection.Setup(x => x.OpenAsync(It.IsAny<CancellationToken>())).Returns(async () =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    if (Interlocked.Increment(ref started) == 10)
                        allStarted.SetResult();
                    await allStarted.Task;
                    throw InvalidPassword();
                }
            });
            return connection;
        }).ToArray();
        var attempts = connections.Select(x => opener.OpenAsync(x.Object)).ToArray();
        await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        releaseRefresh.SetResult();
        await Task.WhenAll(attempts);
        Assert.Equal(1, refreshes);
        foreach (var connection in connections)
            connection.Verify(x => x.OpenAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RejectedRefreshedPasswordIsRetriedOnceAndRefreshIsThrottled()
    {
        using var source = NpgsqlDataSource.Create(Settings);
        var clock = new TestClock();
        var refreshes = 0;
        using var opener = new DatabaseConnectionOpener(source, _ =>
        {
            refreshes++;
            return Task.FromResult(new NpgsqlConnectionStringBuilder(Settings));
        }, clock);
        var connection = new Mock<DbConnection>();
        connection.Setup(x => x.OpenAsync(It.IsAny<CancellationToken>())).ThrowsAsync(InvalidPassword());
        await Assert.ThrowsAsync<PostgresException>(() => opener.OpenAsync(connection.Object));
        await Assert.ThrowsAsync<PostgresException>(() => opener.OpenAsync(connection.Object));
        Assert.Equal(1, refreshes);
        connection.Verify(x => x.OpenAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
        clock.Advance();
        await Assert.ThrowsAsync<PostgresException>(() => opener.OpenAsync(connection.Object));
        Assert.Equal(2, refreshes);
    }

    [Fact]
    public async Task SecretLookupFailuresAreThrottled()
    {
        using var source = NpgsqlDataSource.Create(Settings);
        var refreshes = 0;
        using var opener = new DatabaseConnectionOpener(source, _ =>
        {
            refreshes++;
            throw new InvalidOperationException("Secret unavailable");
        }, new TestClock());
        var connection = new Mock<DbConnection>();
        connection.Setup(x => x.OpenAsync(It.IsAny<CancellationToken>())).ThrowsAsync(InvalidPassword());
        await Assert.ThrowsAsync<InvalidOperationException>(() => opener.OpenAsync(connection.Object));
        await Assert.ThrowsAsync<PostgresException>(() => opener.OpenAsync(connection.Object));
        Assert.Equal(1, refreshes);
        connection.Verify(x => x.OpenAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CancellationDoesNotRefreshOrRetry()
    {
        using var source = NpgsqlDataSource.Create(Settings);
        var refreshes = 0;
        using var opener = new DatabaseConnectionOpener(source, _ =>
        {
            refreshes++;
            return Task.FromResult(new NpgsqlConnectionStringBuilder(Settings));
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var connection = new Mock<DbConnection>();
        connection.Setup(x => x.OpenAsync(cancellation.Token)).Returns(Task.FromCanceled(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opener.OpenAsync(connection.Object, cancellation.Token));
        Assert.Equal(0, refreshes);
    }

    private static PostgresException InvalidPassword() => new("Invalid password", "ERROR", "ERROR", "28P01");

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance() => _now = _now.AddSeconds(6);
    }
}

using System.Data.Common;
using Npgsql;

namespace Pervaxis.Vpa.Vending.Infrastructure.Database;

/// <summary>Retries only connection authentication, before any SQL is executed.</summary>
public sealed class DatabaseConnectionOpener(
    NpgsqlDataSource dataSource,
    Func<CancellationToken, Task<NpgsqlConnectionStringBuilder>>? refreshCredentials,
    TimeProvider? timeProvider = null) : IDisposable
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private readonly NpgsqlConnectionStringBuilder _settings = new(dataSource.ConnectionString);
    private DateTimeOffset _nextRefreshAllowed = DateTimeOffset.MinValue;
    private long _generation;

    public async Task OpenAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        var generation = Interlocked.Read(ref _generation);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.InvalidPassword && refreshCredentials is not null)
        {
            if (!await RefreshAsync(generation, cancellationToken).ConfigureAwait(false))
                throw;
        }

        // A second failure propagates. Do not replay commands or transactions.
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> RefreshAsync(long failedGeneration, CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another caller already updated the password used by this attempt.
            if (failedGeneration != Interlocked.Read(ref _generation))
                return true;
            if (_clock.GetUtcNow() < _nextRefreshAllowed)
                return false;

            try
            {
                var updated = await refreshCredentials!(cancellationToken).ConfigureAwait(false);
                dataSource.Password = DatabaseSecretReader.GetRefreshedPassword(_settings, updated);
                Interlocked.Increment(ref _generation);
                return true;
            }
            finally
            {
                // Also throttle failed lookups and unchanged/incorrect passwords.
                _nextRefreshAllowed = _clock.GetUtcNow().AddSeconds(5);
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Dispose() => _refreshLock.Dispose();
}

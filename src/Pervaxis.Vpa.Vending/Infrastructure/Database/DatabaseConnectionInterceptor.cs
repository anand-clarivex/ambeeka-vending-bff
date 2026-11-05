using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Pervaxis.Vpa.Vending.Infrastructure.Database;

public sealed class DatabaseConnectionInterceptor(DatabaseConnectionOpener opener) : DbConnectionInterceptor
{
    public override InterceptionResult ConnectionOpening(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        if (result.IsSuppressed)
            return result;
        opener.OpenAsync(connection).GetAwaiter().GetResult();
        return InterceptionResult.Suppress();
    }

    public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (result.IsSuppressed)
            return result;
        await opener.OpenAsync(connection, cancellationToken).ConfigureAwait(false);
        return InterceptionResult.Suppress();
    }
}

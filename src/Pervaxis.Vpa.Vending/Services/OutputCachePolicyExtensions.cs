using Microsoft.AspNetCore.OutputCaching;

namespace Pervaxis.Vpa.Vending.Services;

public static class OutputCachePolicyExtensions
{
    public static void AddForgeDefaultOutputCache(this OutputCachePolicyBuilder policy)
    {
        policy.Expire(TimeSpan.FromSeconds(60));
        policy.Tag("forge-default");
        policy.SetVaryByQuery("page", "pageSize", "sort");
    }
}

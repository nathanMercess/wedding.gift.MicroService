using Microsoft.Extensions.Caching.Memory;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Services.Implementations;

public sealed class ApplicationCacheService(IMemoryCache cache) : IApplicationCacheService
{
    private const string CacheVersionKey = "app:cache:version";

    public string CurrentVersion => cache.GetOrCreate(CacheVersionKey, entry =>
    {
        entry.Priority = CacheItemPriority.NeverRemove;
        return NewVersion();
    })!;

    public void Invalidate()
        => cache.Set(CacheVersionKey, NewVersion(), new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.NeverRemove
        });

    private static string NewVersion()
        => Guid.NewGuid().ToString("N");
}

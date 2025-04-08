namespace DataFlow.Dataverse.Caching;

using Microsoft.Extensions.Caching.Memory;

public interface ISafeMemoryCache : IDisposable
{
    void Add<T>(string key, T item, MemoryCacheEntryOptions policy);

    T? Get<T>(string key);

    Task<T?> GetAsync<T>(string key);

    bool TryGetValue<T>(string key, out T? value);

    T? GetOrAdd<T>(string key, Func<ICacheEntry, T?> itemFactory);

    T? GetOrAdd<T>(string key, Func<ICacheEntry, T?> itemFactory, MemoryCacheEntryOptions? policy);

    void Remove(string key);

    Task<T?> GetOrAddAsync<T>(string key, Func<ICacheEntry, Task<T?>> itemFactory);

    Task<T?> GetOrAddAsync<T>(string key, Func<ICacheEntry, Task<T?>> itemFactory, MemoryCacheEntryOptions? policy);
}

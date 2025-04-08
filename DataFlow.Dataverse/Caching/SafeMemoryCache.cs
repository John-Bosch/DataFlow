namespace DataFlow.Dataverse.Caching;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

internal class SafeMemoryCache : ISafeMemoryCache
{
    private readonly int[] keyLocks;
    private readonly MemoryCache cache;
    private bool disposedValue;

    public SafeMemoryCache(MemoryCacheOptions options)
    {
        var lockCount = Math.Max(Environment.ProcessorCount * 8, 32);
        keyLocks = new int[lockCount];

        cache = new MemoryCache(options);
    }

    public void Add<T>(string key, T item, MemoryCacheEntryOptions policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(item);

        cache.Set(key, item, policy);
    }

    public T? Get<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var item = cache.Get(key);
        return GetValueFromLazy<T>(item);
    }

    public Task<T?> GetAsync<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var item = cache.Get(key);
        return GetValueFromLazyAsync<T>(item);
    }

    public bool TryGetValue<T>(string key, out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return cache.TryGetValue(key, out value);
    }

    public T? GetOrAdd<T>(string key, Func<ICacheEntry, T?> itemFactory)
    {
        return GetOrAdd(key, itemFactory, null);
    }

    public T? GetOrAdd<T>(string key, Func<ICacheEntry, T?> itemFactory, MemoryCacheEntryOptions? policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(itemFactory);

        object? CacheFactory(ICacheEntry entry)
        {
            return new Lazy<T?>(() =>
            {
                var item = itemFactory(entry);
                return item;
            });
        }

        object? cacheItem;

        // Ensure only one thread can place an item into the cache provider at a time.
        // itemFactory is not called here - that happens outside the lock, and is guarded using the lazy.
        // Here we just ensure only one thread can place the Lazy into the cache at a time

        // Lock the key while creating the new item
        uint hash = (uint)key.GetHashCode() % (uint)keyLocks.Length;
        while (Interlocked.CompareExchange(ref keyLocks[hash], 1, 0) == 1)
        {
            Thread.Yield();
        }

        try
        {
            cacheItem = GetOrCreate<object?>(key, CacheFactory, policy);
        }
        finally
        {
            keyLocks[hash] = 0;
        }

        try
        {
            var result = GetValueFromLazy<T>(cacheItem);
            return result;
        }
        catch
        {
            cache.Remove(key);
            throw;
        }
    }

    public void Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        cache.Remove(key);
    }

    public Task<T?> GetOrAddAsync<T>(string key, Func<ICacheEntry, Task<T?>> itemFactory)
    {
        return GetOrAddAsync<T>(key, itemFactory, null);
    }

    public async Task<T?> GetOrAddAsync<T>(string key, Func<ICacheEntry, Task<T?>> itemFactory, MemoryCacheEntryOptions? policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        object? cacheItem;

        object? CacheFactory(ICacheEntry entry)
        {
            return new AsyncLazy<T?>(async () =>
            {
                var item = await itemFactory(entry);
                return item;
            });
        }

        // Ensure only one thread can place an item into the cache provider at a time.
        // itemFactory is not called here - that happens outside the lock, and is guarded using the async lazy.
        // Here we just ensure only one thread can place the AsyncLazy into the cache at a time

        // acquire lock
        uint hash = (uint)key.GetHashCode() % (uint)keyLocks.Length;
        while (Interlocked.CompareExchange(ref keyLocks[hash], 1, 0) == 1)
        {
            Thread.Yield();
        }

        try
        {
            cacheItem = GetOrCreate<object?>(key, CacheFactory, policy);
        }
        finally
        {
            keyLocks[hash] = 0;
        }

        try
        {
            return await GetValueFromLazyAsync<T>(cacheItem);
        }
        catch
        {
            cache.Remove(key);
            throw;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                cache.Dispose();
            }

            disposedValue = true;
        }
    }

    private static T? GetValueFromLazy<T>(object? item)
    {
        return item switch
        {
            Lazy<T> lazy => lazy.Value,
            T variable => variable,
            AsyncLazy<T> lazy => lazy.Value.ConfigureAwait(false).GetAwaiter().GetResult(),
            Task<T> task => task.Result,
            _ => throw new InvalidOperationException("Found an unexpected object type in the cache")
        };
    }

    private static Task<T?> GetValueFromLazyAsync<T>(object? item)
    {
        return item switch
        {
            AsyncLazy<T?> lazy => lazy.Value,
            Task<T?> task => task,
            Lazy<T?> lazy => Task.FromResult(lazy.Value),
            T variable => Task.FromResult<T?>(variable),
            _ => throw new InvalidOperationException("Found an unexpected object type in the cache")
        };
    }

    private object? GetOrCreate<T>(string key, Func<ICacheEntry, T?> itemFactory, MemoryCacheEntryOptions? policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (policy == null)
        {
            return cache.GetOrCreate(key, itemFactory);
        }

        if (!cache.TryGetValue(key, out var result))
        {
            var entry = cache.CreateEntry(key);

            // Set the initial options before the factory is fired so that any callbacks
            // that need to be wired up are still added.
            entry.SetOptions(policy);

            if (policy is SafeMemoryCacheEntryOptions safeOptions && safeOptions.ExpirationMode != ExpirationMode.LazyExpiration)
            {
                var tokenSource = new CancellationTokenSource();
                var token = new CancellationChangeToken(tokenSource.Token);
                entry.AddExpirationToken(token);

                entry.RegisterPostEvictionCallback((key, value, reason, state) =>
                {
                    tokenSource.Dispose();
                });

                result = itemFactory(entry);
                tokenSource.CancelAfter(safeOptions.ImmediateExpirationTimespan);
            }
            else
            {
                result = itemFactory(entry);
            }

            entry.SetValue(result);
            entry.Dispose();
        }

        return (T?)result;
    }    
}

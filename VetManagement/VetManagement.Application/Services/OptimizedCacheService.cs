using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Memory;

namespace VetManagement.Application.Services;

public sealed class OptimizedCacheService(IMemoryCache memoryCache)
{
    private readonly IMemoryCache _cache = memoryCache;
    private static readonly MemoryCacheEntryOptions _defaultOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
        SlidingExpiration = TimeSpan.FromMinutes(10),
        Priority = CacheItemPriority.Normal
    };

    public async Task<TValue> GetOrCreateAsync<TValue>(
        string key,
        Func<CancellationToken, Task<TValue>> factory,
        MemoryCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out TValue? cachedValue) && cachedValue is not null)
        {
            return cachedValue;
        }

        var value = await factory(cancellationToken);
        _cache.Set(key, value, options ?? _defaultOptions);
        return value;
    }

    public async Task<FrozenDictionary<TKey, TValue>> GetOrCreateFrozenDictionaryAsync<TKey, TValue>(
        string key,
        Func<CancellationToken, Task<IEnumerable<KeyValuePair<TKey, TValue>>>> factory,
        MemoryCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        if (_cache.TryGetValue(key, out FrozenDictionary<TKey, TValue>? cachedValue) && cachedValue is not null)
        {
            return cachedValue;
        }

        var data = await factory(cancellationToken);
        var frozenDict = data.ToFrozenDictionary();
        _cache.Set(key, frozenDict, options ?? _defaultOptions);
        return frozenDict;
    }

    public async Task<FrozenSet<T>> GetOrCreateFrozenSetAsync<T>(
        string key,
        Func<CancellationToken, Task<IEnumerable<T>>> factory,
        MemoryCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out FrozenSet<T>? cachedValue) && cachedValue is not null)
        {
            return cachedValue;
        }

        var data = await factory(cancellationToken);
        var frozenSet = data.ToFrozenSet();
        _cache.Set(key, frozenSet, options ?? _defaultOptions);
        return frozenSet;
    }

    public async IAsyncEnumerable<T> StreamAsync<T>(
        string key,
        Func<CancellationToken, IAsyncEnumerable<T>> factory,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out List<T>? cachedList) && cachedList is not null)
        {
            foreach (var item in cachedList)
            {
                yield return item;
            }
            yield break;
        }

        var list = new List<T>();
        await foreach (var item in factory(cancellationToken).WithCancellation(cancellationToken))
        {
            list.Add(item);
            yield return item;
        }

        _cache.Set(key, list, _defaultOptions);
    }

    public void Remove(string key) => _cache.Remove(key);

    /// <summary>
    /// Clear all cache entries.
    /// </summary>
    public void Clear()
    {
        if (_cache is MemoryCache concreteCache)
        {
            concreteCache.Compact(1.0);
        }
    }
}

using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Shared.Models.Core;

namespace VetManagement.Application.Services;

public sealed class OptimizedQueryService(IUnitOfWork unitOfWork, OptimizedCacheService cacheService)
{
    public async IAsyncEnumerable<Item> StreamItemsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var items = await unitOfWork.Items.GetAllAsync();

        foreach (var item in items)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            yield return item;
        }
    }

    public async IAsyncEnumerable<IReadOnlyList<Item>> StreamItemsInBatchesAsync(
        int batchSize = 100,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var allItems = await unitOfWork.Items.GetAllAsync();
        var batches = allItems.Chunk(batchSize);

        foreach (var batch in batches)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            yield return batch.ToList().AsReadOnly();

            await Task.Yield();
        }
    }

    public async Task<List<Item>> GetItemsCachedAsync(
        CancellationToken cancellationToken = default)
    {
        return await cacheService.GetOrCreateAsync(
            "AllItems",
            async ct => await unitOfWork.Items.GetAllAsync(),
            cancellationToken: cancellationToken
        );
    }

    public async IAsyncEnumerable<Item> SearchItemsStreamAsync(
        string searchTerm,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var normalizedSearch = searchTerm.ToLowerInvariant();

        await foreach (var item in StreamItemsAsync(cancellationToken))
        {
            if (item.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                (item.Barcode?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                yield return item;
            }
        }
    }

    public async Task<FrozenDictionary<int, Item>> GetLowStockItemsDictionaryAsync(
        CancellationToken cancellationToken = default)
    {
        return await cacheService.GetOrCreateFrozenDictionaryAsync(
            "LowStockItems",
            async ct =>
            {
                var items = await unitOfWork.Items.GetLowStockAsync();
                return items.Select(item => new KeyValuePair<int, Item>(item.Id, item));
            },
            cancellationToken: cancellationToken
        );
    }

    public async Task<List<TResult>> ProcessItemsInParallelAsync<TResult>(
        Func<Item, CancellationToken, Task<TResult>> processor,
        int maxConcurrency = 4,
        CancellationToken cancellationToken = default)
    {
        var items = await unitOfWork.Items.GetAllAsync();
        var semaphore = new SemaphoreSlim(maxConcurrency);

        var tasks = items.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await processor(item, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        return [.. await Task.WhenAll(tasks)];
    }

    public void InvalidateCache(params string[] keys)
    {
        foreach (var key in keys)
        {
            cacheService.Remove(key);
        }
    }

    public void InvalidateAllItemCaches()
    {
        cacheService.Remove("AllItems");
        cacheService.Remove("LowStockItems");
    }
}

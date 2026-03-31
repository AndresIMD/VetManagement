using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace VetManagement.Application.Helpers;

public static class CollectionHelper
{
    public static FrozenDictionary<TKey, TValue> ToOptimizedDictionary<TKey, TValue>(
        this IEnumerable<TValue> source,
        Func<TValue, TKey> keySelector)
        where TKey : notnull
    {
        return source.ToFrozenDictionary(keySelector);
    }

    public static async IAsyncEnumerable<IReadOnlyList<T>> BatchAsync<T>(
        this IAsyncEnumerable<T> source,
        int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<T> batch = [];

        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            batch.Add(item);

            if (batch.Count >= batchSize)
            {
                yield return batch.AsReadOnly();
                batch = [];
            }
        }

        if (batch.Count > 0)
        {
            yield return batch.AsReadOnly();
        }
    }

    public static IEnumerable<TResult> FilterAndMap<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, bool> predicate,
        Func<TSource, TResult> selector)
    {
        foreach (var item in source)
        {
            if (predicate(item))
            {
                yield return selector(item);
            }
        }
    }

    public static (List<T> Matching, List<T> NotMatching) Partition<T>(
        this IEnumerable<T> source,
        Func<T, bool> predicate)
    {
        List<T> matching = [];
        List<T> notMatching = [];

        foreach (var item in source)
        {
            if (predicate(item))
                matching.Add(item);
            else
                notMatching.Add(item);
        }

        return (matching, notMatching);
    }

    public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this IEnumerable<T>? source)
    {
        return source switch
        {
            null => true,
            ICollection<T> collection => collection.Count == 0,
            IReadOnlyCollection<T> readOnlyCollection => readOnlyCollection.Count == 0,
            _ => !source.Any()
        };
    }

    public static T? SafeFirstOrDefault<T>(this IEnumerable<T>? source, Func<T, bool>? predicate = null)
    {
        if (source.IsNullOrEmpty())
            return default;

        return predicate is null
            ? source.FirstOrDefault()
            : source.FirstOrDefault(predicate);
    }

    public static async Task<List<TResult>> ParallelSelectAsync<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, CancellationToken, Task<TResult>> selector,
        int maxDegreeOfParallelism = 4,
        CancellationToken cancellationToken = default)
    {
        var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
        var tasks = source.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await selector(item, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        return [.. await Task.WhenAll(tasks)];
    }

    public static IReadOnlyList<T> ToReadOnlyList<T>(this IEnumerable<T> source)
    {
        return source.ToList().AsReadOnly();
    }

    public static IEnumerable<List<T>> ChunkOptimized<T>(this IEnumerable<T> source, int size)
    {
        List<T> chunk = new(size);

        foreach (var item in source)
        {
            chunk.Add(item);
            if (chunk.Count >= size)
            {
                yield return chunk;
                chunk = new(size);
            }
        }

        if (chunk.Count > 0)
        {
            yield return chunk;
        }
    }
}

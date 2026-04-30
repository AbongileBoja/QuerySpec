using System;
using System.Collections.Concurrent;
using System.Threading;

namespace QuerySpec.EFCore;

/// <summary>
/// Bounded, lock-free cache with generation-based LRU eviction. Reads are fully lock-free
/// via <see cref="ConcurrentDictionary{TKey,TValue}"/>. Eviction is triggered when the entry
/// count exceeds the configured capacity and runs at most once at a time (coordinated by a
/// compare-exchange flag). Each eviction sweep removes the bottom 25% of entries by last-access
/// epoch, preserving at least 75% of cached entries and preventing the thundering-herd re-fill
/// that a bulk <c>Clear()</c> causes under concurrent load.
/// </summary>
internal sealed class GenerationCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly int _evictCount;
    private readonly ConcurrentDictionary<TKey, Entry> _store;
    private long _ticks;
    private int _evicting;

    internal GenerationCache(int capacity)
    {
        _capacity = capacity;
        _evictCount = Math.Max(1, capacity / 4);
        _store = new ConcurrentDictionary<TKey, Entry>();
    }

    internal TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
    {
        if (_store.TryGetValue(key, out var existing))
        {
            Interlocked.Increment(ref existing.Epoch);
            return existing.Value;
        }

        var entry = new Entry
        {
            Value = valueFactory(key),
            Epoch = Interlocked.Increment(ref _ticks)
        };
        _store[key] = entry;

        if (_store.Count > _capacity && Interlocked.CompareExchange(ref _evicting, 1, 0) == 0)
        {
            try { Evict(); }
            finally { Volatile.Write(ref _evicting, 0); }
        }

        return entry.Value;
    }

    internal bool TryGetValue(TKey key, out TValue value)
    {
        if (_store.TryGetValue(key, out var entry))
        {
            Interlocked.Increment(ref entry.Epoch);
            value = entry.Value;
            return true;
        }

        value = default!;
        return false;
    }

    internal void Set(TKey key, TValue value)
    {
        var entry = new Entry
        {
            Value = value,
            Epoch = Interlocked.Increment(ref _ticks)
        };
        _store[key] = entry;

        if (_store.Count > _capacity && Interlocked.CompareExchange(ref _evicting, 1, 0) == 0)
        {
            try { Evict(); }
            finally { Volatile.Write(ref _evicting, 0); }
        }
    }

    internal int Count => _store.Count;

    internal void Clear() => _store.Clear();

    private void Evict()
    {
        var snapshot = _store.ToArray();
        Array.Sort(snapshot, static (a, b) => a.Value.Epoch.CompareTo(b.Value.Epoch));
        int toRemove = Math.Min(_evictCount, snapshot.Length);
        for (int i = 0; i < toRemove; i++)
            _store.TryRemove(snapshot[i]);
    }

    private sealed class Entry
    {
        internal TValue Value = default!;
        internal long Epoch;
    }
}

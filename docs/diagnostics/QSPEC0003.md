# QSPEC0003: Use ICacheStore instead of ICacheProvider GetAsync / SetAsync

| Property                       | Value                                                                                |
| ------------------------------ | ------------------------------------------------------------------------------------ |
| Diagnostic ID                  | `QSPEC0003`                                                                          |
| Severity                       | Warning                                                                              |
| Introduced                     | QuerySpec 3.1                                                                        |
| Removal                        | QuerySpec 4.0                                                                        |
| Deprecated API                 | `ICacheProvider.GetAsync<T>` / `SetAsync<T>` (`where T : class`)                     |
| Replacement                    | `ICacheStore.TryGetAsync<T>` / `SetValueAsync<T>` (no class constraint)              |

## Cause

A program calls `ICacheProvider.GetAsync<T>` or `ICacheProvider.SetAsync<T>`. The `where T : class` constraint excludes value types (`int`, `Guid`, custom records) which neither `IDistributedCache` nor `IMemoryCache` require, and the read shape returns `null` on miss — indistinguishable from "stored null" for nullable reference types.

## Replacement

```csharp
// Before
ICacheProvider cache = ...;
await cache.SetAsync("greeting", "hello");
var greeting = await cache.GetAsync<string>("greeting");
if (greeting is null) { /* miss, or stored null? */ }

// After
ICacheStore store = ...;
await store.SetValueAsync("count", 42);                    // value type works directly
var hit = await store.TryGetAsync<int>("count");
if (hit.HasValue) Console.WriteLine(hit.Value);             // unambiguous miss-vs-default
var withFallback = (await store.TryGetAsync<int>("absent")).GetValueOrDefault(99);
```

`ICacheStore.TryGetAsync<T>` returns `CacheResult<T>` — a `readonly struct` whose `HasValue` flag distinguishes a hit on `default(T)` from a miss without paying for a heap allocation. The shipping providers (`MemoryCacheProvider`, `DistributedCacheProvider`, `MultiLevelCache`) implement both `ICacheProvider` and `ICacheStore` through the 3.x line; DI registrations bind a single instance against both contracts.

## Suppression

```csharp
#pragma warning disable QSPEC0003
var greeting = await cache.GetAsync<string>("greeting");
#pragma warning restore QSPEC0003
```

## See also

- Migration sample: `samples/Migration/CacheStoreMigration/Program.cs`
- Tracking issue: [#84](https://github.com/AbongileBoja/QuerySpec/issues/84)

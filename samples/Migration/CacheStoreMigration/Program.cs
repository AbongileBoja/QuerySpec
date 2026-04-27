// QSPEC0003 migration sample: ICacheProvider.GetAsync<T>/SetAsync<T> (where T : class)
// -> ICacheStore.TryGetAsync<T>/SetValueAsync<T> (no class constraint, CacheResult<T> wrapper).

using QuerySpec.Core.Caching;

using var provider = new MemoryCacheProvider();

// Old shape - reference types only; null on miss is indistinguishable from "stored null".
#pragma warning disable QSPEC0003
await provider.SetAsync("greeting", "hello");
var greeting = await provider.GetAsync<string>("greeting");
Console.WriteLine($"[old] greeting = {greeting ?? "<null>"}");
#pragma warning restore QSPEC0003

// New shape - value types and reference types alike, with hit/miss disambiguation.
ICacheStore store = provider;

await store.SetValueAsync("count", 42);
var countResult = await store.TryGetAsync<int>("count");
Console.WriteLine($"[new] count: HasValue={countResult.HasValue}, Value={countResult.Value}");

var missing = await store.TryGetAsync<int>("not-there");
Console.WriteLine($"[new] missing: HasValue={missing.HasValue}, Value={missing.Value} (default(int))");

// Reference types still work, with explicit miss reporting.
await store.SetValueAsync("user", new UserDto("ada", 36));
var userResult = await store.TryGetAsync<UserDto>("user");
Console.WriteLine($"[new] user: HasValue={userResult.HasValue}, Value={userResult.Value}");

// Distinguish a hit on default from a miss.
await store.SetValueAsync<int?>("nullable-zero", 0);
var nullableHit = await store.TryGetAsync<int?>("nullable-zero");
Console.WriteLine($"[new] nullable-zero: HasValue={nullableHit.HasValue}, Value={nullableHit.Value}");

// Get-or-default convenience.
var fallback = (await store.TryGetAsync<int>("absent")).GetValueOrDefault(99);
Console.WriteLine($"[new] absent w/ fallback: {fallback}");

internal sealed record UserDto(string Name, int Age);

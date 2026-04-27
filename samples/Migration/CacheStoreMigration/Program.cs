// QSPEC0003: ICacheStore.TryGetAsync<T> / SetValueAsync<T> replaces the 3.x
// ICacheProvider.GetAsync<T> / SetAsync<T> overloads (which were constrained to where T : class
// and could not distinguish a hit on null from a miss). Those legacy methods were removed in
// 4.0; the QuerySpec.Analyzers package emits QSPEC0003 against any remaining 3.x call sites.

using QuerySpec.Core.Caching;

using var provider = new MemoryCacheProvider();
ICacheStore store = provider;

await store.SetValueAsync("count", 42);
var countResult = await store.TryGetAsync<int>("count");
Console.WriteLine($"count: HasValue={countResult.HasValue}, Value={countResult.Value}");

var missing = await store.TryGetAsync<int>("not-there");
Console.WriteLine($"missing: HasValue={missing.HasValue}, Value={missing.Value} (default(int))");

await store.SetValueAsync("user", new UserDto("ada", 36));
var userResult = await store.TryGetAsync<UserDto>("user");
Console.WriteLine($"user: HasValue={userResult.HasValue}, Value={userResult.Value}");

await store.SetValueAsync<int?>("nullable-zero", 0);
var nullableHit = await store.TryGetAsync<int?>("nullable-zero");
Console.WriteLine($"nullable-zero: HasValue={nullableHit.HasValue}, Value={nullableHit.Value}");

var fallback = (await store.TryGetAsync<int>("absent")).GetValueOrDefault(99);
Console.WriteLine($"absent w/ fallback: {fallback}");

internal sealed record UserDto(string Name, int Age);

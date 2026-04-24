# QuerySpec samples

Three runnable projects, each demonstrating one slice of the package so you can read them in isolation rather than hunting through a single mega-sample.

| Sample | What it shows | Run |
|---|---|---|
| [`QuerySpec.Samples.WebApi`](QuerySpec.Samples.WebApi) | Minimal API + EF Core (SQLite). `POST /search` takes an `AdvancedFilterExpression` as JSON and translates it to SQL. Wires up `AddQuerySpec(...)` with caching, auditing, performance, and resilience. Exposes `/audit` and `/cache/stats` so you can see the side effects. | `dotnet run --project samples/QuerySpec.Samples.WebApi` |
| [`QuerySpec.Samples.Security`](QuerySpec.Samples.Security) | Console. AES field encryption, column-level masking (email/credit-card/SSN), row-level security via a strongly-typed `Expression<Func<T,bool>>` composed with an `IQueryable<T>`, and dynamic per-role permission evaluation. | `dotnet run --project samples/QuerySpec.Samples.Security` |
| [`QuerySpec.Samples.Resilience`](QuerySpec.Samples.Resilience) | Console. `RetryPolicy`, `CircuitBreaker`, `RateLimiter`, `BulkheadPolicy`, and a composed `ResiliencePolicy` — each exercised against a deliberately flaky in-memory backend so you can see the behavior without any infrastructure. | `dotnet run --project samples/QuerySpec.Samples.Resilience` |

All three use `ProjectReference` to the local packages in `src/`, so they build from source without publishing NuGet first.

## Prereqs

- .NET 9 SDK
- No external services required (SQLite is embedded; security & resilience samples use in-memory stores).

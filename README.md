# QuerySpec

[![QuerySpec.Core on NuGet](https://img.shields.io/nuget/v/QuerySpec.Core.svg?label=QuerySpec.Core&logo=nuget)](https://www.nuget.org/packages/QuerySpec.Core)
[![QuerySpec.EFCore on NuGet](https://img.shields.io/nuget/v/QuerySpec.EFCore.svg?label=QuerySpec.EFCore&logo=nuget)](https://www.nuget.org/packages/QuerySpec.EFCore)
[![Downloads](https://img.shields.io/nuget/dt/QuerySpec.Core.svg?label=downloads&logo=nuget)](https://www.nuget.org/packages/QuerySpec.Core)
[![.NET 8 · 9 · 10](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/platform/support/policy/dotnet-core)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![CI](https://img.shields.io/github/actions/workflow/status/AbongileBoja/QuerySpec/ci.yml?branch=main&label=ci&logo=github)](https://github.com/AbongileBoja/QuerySpec/actions/workflows/ci.yml)
[![AOT compatible](https://img.shields.io/badge/AOT-compatible-success?logo=dotnet)](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)

**Specifications as data. SQL when you need it. The cross-cutting concerns already wired.**

QuerySpec lets you describe a query — filters, sorts, projections — as plain data, then translate it to LINQ against Entity Framework Core. Useful when filter input comes from somewhere other than code: an HTTP request, a saved view, a rules engine, a config file. Instead of writing expression trees by hand (or giving up and interpolating strings), you describe the query as data and let QuerySpec turn it into the SQL you'd have written yourself.

On top of that, `QuerySpec.Core` ships the cross-cutting pieces most data-access layers end up reinventing: **auditing, data masking, row-level security, caching, resilience (retry / circuit breaker / rate limiting), and observability**. Trim- and AOT-ready. Strong-named. Multi-targets `net8.0`, `net9.0`, `net10.0` — pick the runtime you ship on; QuerySpec compiles natively against it.

## Why QuerySpec

- **Accept filters from clients without opening an SQL-injection surface.** Specifications are data. Validation happens before translation.
- **Stop hand-rolling cross-cutting concerns per query.** Auditing, masking, RLS, caching, retry, and metrics plug in once.
- **Performance from day one.** Compiled-expression cache means repeat translations of the same shape don't re-walk the tree.
- **Production-grade hygiene.** Strong-named, SourceLink + symbols (`.snupkg`), SBOM included, SLSA build provenance, deterministic builds, EditorConfig-aware analyzers in CI.
- **Provider-tested.** EF Core In-Memory, SQLite, SQL Server, and PostgreSQL run in CI via Testcontainers.

## Packages

| Package | NuGet | Purpose |
|---|---|---|
| **`QuerySpec.Core`** | [![NuGet](https://img.shields.io/nuget/v/QuerySpec.Core.svg)](https://www.nuget.org/packages/QuerySpec.Core) | Filter/sort specifications + pluggable auditing, security, caching, resilience, monitoring. No EF dependency. |
| **`QuerySpec.EFCore`** | [![NuGet](https://img.shields.io/nuget/v/QuerySpec.EFCore.svg)](https://www.nuget.org/packages/QuerySpec.EFCore) | Translates specifications into LINQ expression trees over `IQueryable<T>`. Compiled-expression cache included. |
| **`QuerySpec.DependencyInjection`** | [![NuGet](https://img.shields.io/nuget/v/QuerySpec.DependencyInjection.svg)](https://www.nuget.org/packages/QuerySpec.DependencyInjection) | Fluent `AddQuerySpec(...)` builder for `Microsoft.Extensions.DependencyInjection`. Redis caching via StackExchange.Redis. |
| **`QuerySpec.Analyzers`** | [![NuGet](https://img.shields.io/nuget/v/QuerySpec.Analyzers.svg)](https://www.nuget.org/packages/QuerySpec.Analyzers) | Roslyn analyzers + one-click code fixes for migration diagnostics (QSPEC0001 – QSPEC0003). |

Runtime packages multi-target **`net8.0` (LTS), `net9.0`, and `net10.0` (LTS)** — your project picks the matching `lib/` folder. (`QuerySpec.Analyzers` ships as `netstandard2.0` per Roslyn convention; it works against any of the supported runtimes.)

## Install

```bash
dotnet add package QuerySpec.Core
dotnet add package QuerySpec.EFCore
dotnet add package QuerySpec.DependencyInjection
```

Most apps want all three. `QuerySpec.Core` alone is fine if you're not using EF Core or DI.

## Supported frameworks

QuerySpec targets `net8.0` (LTS, supported through November 2026), `net9.0` (STS, supported through May 2026), and `net10.0` (LTS). These are the only supported TFMs. `netstandard2.0` is not targeted.

The library relies on APIs that are unavailable on `netstandard2.0`: `System.Threading.Lock`, `params ReadOnlySpan<T>`, `TimeProvider`, async `JsonSerializer` overloads, and related BCL surface. Adding a `netstandard2.0` target would require either reverting those improvements or shipping a parallel conditional-compilation codebase — a cost that does not make sense for an enterprise data-access library targeting modern .NET.

Consumers on .NET Framework 4.x or older runtimes should use [Ardalis.Specification](https://github.com/ardalis/Specification) or another `netstandard2.0`-compatible alternative.

For the .NET support lifecycle see https://dotnet.microsoft.com/platform/support/policy/dotnet-core.

## Applying a filter to an EF Core query

```csharp
using QuerySpec.Core.Filtering;
using QuerySpec.EFCore;

var filter = new AdvancedFilterExpression
{
    Field = "Department",
    Operator = FilterOperator.Equal,
    Value = "Engineering"
};

var users = await QuerySpecExpressionTranslator
    .ApplyFilter(dbContext.Users, filter)
    .ToListAsync();
```

`AdvancedFilterExpression` is plain data — deserialize it from a request body, compose it from query-string parameters, load it from a saved view. The translator turns it into the same expression tree you'd have written yourself, so EF Core generates the same SQL it always would.

For nested logic, use `And`/`Or` groups instead of a flat `Field`/`Operator`/`Value`. See the `samples/` folder for a worked example.

## Wiring up the cross-cutting features

`AddQuerySpec` is opt-in — you only register what you use:

```csharp
builder.Services.AddQuerySpec(qs => qs
    .WithAuditing(a => a.LogAllQueries())
    .WithSecurity(s => s.EnableDataMasking())
    .WithCaching(c => c.UseMemoryCache())
    .WithResilience(r => r.EnableCircuitBreaker())
);
```

Each `With*` call is independent. Skip the ones you don't need.

## What's in the box

- **50+ filter operators** — comparison, string (contains / starts-with / regex), collection membership, null checks, temporal ranges, geospatial.
- **Compiled expression cache** — repeated translations of the same specification shape don't re-walk the tree.
- **Auditing** — per-query log entries with integrity hashing, suitable for compliance reporting.
- **Security** — column-level masking, field encryption, row-level security predicates, dynamic permission evaluation.
- **Caching** — in-memory out of the box; Redis via `StackExchange.Redis` when you pull in `QuerySpec.DependencyInjection`.
- **Resilience** — retry with backoff, circuit breaker, rate limiting.
- **Monitoring** — metrics, health checks, OpenTelemetry hooks.

## Observability

QuerySpec publishes a `System.Diagnostics.Metrics` meter named **`QuerySpec`**. Subscribe in OpenTelemetry:

```csharp
services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter("QuerySpec"));
```

Or live via dotnet-counters:

```
dotnet-counters monitor --counters QuerySpec
```

### Instruments

| Instrument | Type | Unit | Tags | Description |
|---|---|---|---|---|
| `queryspec.filter.applications` | Counter&lt;long&gt; | — | `cached`, `entity_type` | Total `ApplyFilter` / `ApplyFilterCached` invocations |
| `queryspec.cache.hits` | Counter&lt;long&gt; | — | `cache_name` | Cache hits across all caches |
| `queryspec.cache.misses` | Counter&lt;long&gt; | — | `cache_name` | Cache misses across all caches |
| `queryspec.filter.build.duration` | Histogram&lt;double&gt; | ms | — | Time spent building / compiling predicates |
| `queryspec.rls.evaluation.duration` | Histogram&lt;double&gt; | ms | `resource_type` | Time spent evaluating RLS predicates |

All instruments are emitted only when a listener is attached, so the overhead when no subscriber is active is a single `Enabled` check per call.

## License

MIT — see [LICENSE](LICENSE).

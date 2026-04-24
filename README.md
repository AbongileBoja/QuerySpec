# QuerySpec

Build query specifications — filters, sorts, projections — as plain data, then translate them to LINQ against Entity Framework Core. Useful when filter input comes from somewhere other than code: an HTTP request, a saved view, a rules engine, a config file. Instead of writing expression trees by hand (or giving up and interpolating strings), you describe the query as data and let QuerySpec turn it into SQL.

On top of that, the Core package ships the cross-cutting pieces most data-access layers end up reinventing: auditing, data masking and row-level security, caching, resilience (retry / circuit breaker / rate limiting), and monitoring.

## Packages

| Package | What it does |
|---|---|
| `QuerySpec.Core` | Filter/sort specifications and the pluggable auditing, security, caching, resilience, and monitoring layers. No EF dependency. |
| `QuerySpec.EFCore` | Translates `AdvancedFilterExpression` into LINQ expression trees for `IQueryable<T>`. Compiled-expression cache included. |
| `QuerySpec.DependencyInjection` | `AddQuerySpec(...)` builder for Microsoft.Extensions.DependencyInjection. Pulls in `StackExchange.Redis` for distributed caching. |

Targets `net8.0`, `net9.0`, `net10.0`.

## Install

```bash
dotnet add package QuerySpec.Core
dotnet add package QuerySpec.EFCore
dotnet add package QuerySpec.DependencyInjection
```

Most apps want all three. Core alone is fine if you're not using EF Core or DI.

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

## License

MIT — see [LICENSE](LICENSE).

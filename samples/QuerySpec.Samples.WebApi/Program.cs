using Microsoft.EntityFrameworkCore;
using QuerySpec.Core.Advanced;
using QuerySpec.Core.Auditing;
using QuerySpec.Core.Caching;
using QuerySpec.DependencyInjection;
using QuerySpec.EFCore;
using QuerySpec.Samples.WebApi.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite("Data Source=employees.db"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddQuerySpec(qs => qs
    .WithCaching(c => c.UseMemoryCache())
    .WithAuditing(_ => { })
    .WithPerformance(p => p.EnableMetrics())
    .WithResilience(r => r
        .UseRetryPolicy(maxRetries: 3, exponentialBackoff: true)
        .UseCircuitBreaker(failureThreshold: 5, openTimeout: TimeSpan.FromSeconds(30))));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    AppDbContext.Seed(db);
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/search", async (
    AdvancedFilterExpression? filter,
    AppDbContext db,
    ICacheProvider cache,
    IAuditLogger audit,
    HttpContext http,
    CancellationToken ct) =>
{
    if (filter is not null)
    {
        var errors = filter.Validate().ToList();
        if (errors.Count > 0) return Results.BadRequest(new { errors });
    }

    var cacheKey = CacheKeyGenerator.GenerateKey(
        "employees.search",
        filter?.ComputeStableHash() ?? 0);

    var cached = await cache.GetAsync<List<Employee>>(cacheKey);
    var fromCache = cached is not null;

    var started = DateTime.UtcNow;
    var results = cached ?? await QuerySpecExpressionTranslator
        .ApplyFilterCached(db.Employees.AsQueryable(), filter)
        .ToListAsync(ct);

    if (!fromCache)
        await cache.SetAsync(cacheKey, results, TimeSpan.FromMinutes(2));

    await audit.LogQueryAsync(new AuditLogEntry
    {
        Id = Guid.NewGuid().ToString("N"),
        Timestamp = started,
        TenantId = http.Request.Headers["X-Tenant"].FirstOrDefault() ?? "default",
        UserId = http.Request.Headers["X-User"].FirstOrDefault() ?? "anonymous",
        RequestId = http.TraceIdentifier,
        ResourceType = nameof(Employee),
        Operation = "search",
        ExecutionTimeMs = (long)(DateTime.UtcNow - started).TotalMilliseconds,
        RecordsAffected = results.Count,
        Success = true,
        Filters = filter is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["hash"] = filter.ComputeStableHash() }
    });

    return Results.Ok(new { fromCache, count = results.Count, results });
});

app.MapGet("/audit", async (IAuditLogger audit) =>
{
    var today = DateTime.UtcNow.Date;
    var entries = await audit.GetComplianceReportAsync(today, today.AddDays(1));
    return Results.Ok(entries);
});

app.MapGet("/cache/stats", async (ICacheProvider cache) =>
    Results.Ok(await cache.GetStatsAsync()));

app.Run();

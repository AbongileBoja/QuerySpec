using System.Collections.Generic;
using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using QuerySpec.Core.Security;

namespace QuerySpec.Benchmarks;

/// <summary>
/// Hot-path benchmarks for <see cref="RowLevelSecurityEngine"/>: filter generation and predicate
/// construction. Both run once per request per tenanted entity type; even a 10 µs regression
/// compounds into meaningful aggregate CPU overhead at enterprise request rates.
/// </summary>
[Config(typeof(BenchConfig))]
public class RowLevelSecurityBenchmarks
{
    private RowLevelSecurityEngine _engineNoPolicies = null!;
    private RowLevelSecurityEngine _engineSinglePolicy = null!;
    private RowLevelSecurityEngine _engineMultiPolicy = null!;
    private RowLevelSecurityEngine _enginePredicate = null!;

    private RLSContext _tenantContext = null!;
    private RLSContext _ownerContext = null!;
    private RLSContext _multiTenantContext = null!;

    /// <summary>Seeds all engines and contexts used by the benchmarks.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _tenantContext = new RLSContext
        {
            UserId = "user-42",
            Department = "engineering",
            Region = "us-east-1",
            AllowedTenants = new List<string> { "tenant-acme" },
        };

        _ownerContext = new RLSContext
        {
            UserId = "user-42",
            Department = "engineering",
        };

        _multiTenantContext = new RLSContext
        {
            UserId = "user-99",
            Department = "finance",
            Region = "eu-west-1",
            AllowedTenants = new List<string>
            {
                "tenant-alpha",
                "tenant-beta",
                "tenant-gamma",
                "tenant-delta",
                "tenant-epsilon",
            },
        };

        _engineNoPolicies = new RowLevelSecurityEngine(RLSDefaultBehavior.DenyAll);

        _engineSinglePolicy = new RowLevelSecurityEngine(RLSDefaultBehavior.Throw);
        _engineSinglePolicy.RegisterPolicy(new RLSPolicy
        {
            ResourceType = "Orders",
            FilterGenerator = RowLevelSecurityEngine.CreateTenantBased("TenantId").FilterGenerator,
        });

        _engineMultiPolicy = new RowLevelSecurityEngine(RLSDefaultBehavior.Throw);
        _engineMultiPolicy.RegisterPolicy(new RLSPolicy
        {
            ResourceType = "Orders",
            FilterGenerator = RowLevelSecurityEngine.CreateTenantBased("TenantId").FilterGenerator,
        });
        _engineMultiPolicy.RegisterPolicy(new RLSPolicy
        {
            ResourceType = "Invoices",
            FilterGenerator = RowLevelSecurityEngine.CreateOwnerBased("OwnerId").FilterGenerator,
        });
        _engineMultiPolicy.RegisterPolicy(new RLSPolicy
        {
            ResourceType = "Contracts",
            FilterGenerator = RowLevelSecurityEngine.CreateDepartmentBased("Department").FilterGenerator,
        });
        _engineMultiPolicy.RegisterPolicy(new RLSPolicy
        {
            ResourceType = "Reports",
            FilterGenerator = RowLevelSecurityEngine.CreateTenantBased("TenantId").FilterGenerator,
        });
        _engineMultiPolicy.RegisterPolicy(new RLSPolicy
        {
            ResourceType = "Employees",
            FilterGenerator = RowLevelSecurityEngine.CreateDepartmentBased("Dept").FilterGenerator,
        });

        _enginePredicate = new RowLevelSecurityEngine(RLSDefaultBehavior.Throw);
        _enginePredicate.RegisterPolicy(new RLSPolicy
        {
            ResourceType = "Orders",
            FilterGenerator = RowLevelSecurityEngine.CreateTenantBased("TenantId").FilterGenerator,
        }.SetPredicate<Order>(ctx =>
        {
            var tenantId = ctx.AllowedTenants.Count > 0 ? ctx.AllowedTenants[0] : string.Empty;
            return order => order.TenantId == tenantId && order.OwnerId == ctx.UserId;
        }));
    }

    /// <summary>
    /// GenerateFilter with no matching policy — exercises the DenyAll fast path.
    /// Baseline cost: dictionary miss + enum switch.
    /// </summary>
    [Benchmark(Baseline = true)]
    public RLSFilter GenerateFilter_NoPolicies()
        => _engineNoPolicies.GenerateFilter("Orders", _tenantContext);

    /// <summary>
    /// GenerateFilter with a single registered tenant-based policy (cache hit, one tenant).
    /// Representative of the steady-state request path.
    /// </summary>
    [Benchmark]
    public RLSFilter GenerateFilter_SinglePolicy()
        => _engineSinglePolicy.GenerateFilter("Orders", _tenantContext);

    /// <summary>
    /// GenerateFilter with five registered policies; lookup hits "Orders" in a five-entry
    /// ConcurrentDictionary. Measures dictionary lookup + tenant-IN-list fragment assembly
    /// across one-tenant context.
    /// </summary>
    [Benchmark]
    public RLSFilter GenerateFilter_MultiPolicy()
        => _engineMultiPolicy.GenerateFilter("Orders", _tenantContext);

    /// <summary>
    /// GenerateFilter with five policies and a five-tenant allow-list. Exercises the
    /// parameter-array allocation path inside <c>CreateTenantBased</c>.
    /// </summary>
    [Benchmark]
    public RLSFilter GenerateFilter_MultiPolicy_MultiTenant()
        => _engineMultiPolicy.GenerateFilter("Orders", _multiTenantContext);

    /// <summary>
    /// GetPredicate with a PredicateFactory registered — measures expression-tree
    /// construction cost per call with a real closure capture (tenant + owner).
    /// </summary>
    [Benchmark]
    public Expression<System.Func<Order, bool>>? GetPredicate_WithFactory()
        => _enginePredicate.GetPredicate<Order>("Orders", _tenantContext);

    /// <summary>Simple order entity used to give GetPredicate a real type parameter.</summary>
    public sealed class Order
    {
        /// <summary>Identifier.</summary>
        public int Id { get; set; }
        /// <summary>Tenant scope for RLS.</summary>
        public string TenantId { get; set; } = string.Empty;
        /// <summary>Owning user for RLS.</summary>
        public string OwnerId { get; set; } = string.Empty;
        /// <summary>Order amount.</summary>
        public decimal Amount { get; set; }
    }
}

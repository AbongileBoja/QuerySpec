using System;
using System.Linq;
using System.Linq.Expressions;
using QuerySpec.Core.Advanced;
using QuerySpec.Core.Caching;
using QuerySpec.Core.Security;
using QuerySpec.EFCore;

namespace QuerySpec.TrimSmokeTest;

internal sealed class Sample
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

internal static class Program
{
    private static int Main()
    {
        var key = CacheKeyGenerator.GenerateQueryCacheKey("t", "u", "qh", "sh", 1);
        Console.WriteLine($"key: {key}");

        var rls = new RowLevelSecurityEngine(RLSDefaultBehavior.AllowAll);
        rls.RegisterUnrestricted<Sample>("Sample");

        var perms = new DynamicPermissionEvaluator();
        perms.RegisterPermission("admin", PermissionType.Read, _ => true);

        var masking = new DataMaskingEngine();
        masking.RegisterFieldMask("Name", MaskingStrategy.PartialMask);
        var masked = masking.Mask("Name", "Alice");
        Console.WriteLine($"masked: {masked}");

        var data = new[]
        {
            new Sample { Id = 1, Name = "Alice" },
            new Sample { Id = 2, Name = "Bob" }
        }.AsQueryable();

        var spec = new FilterSpec
        {
            Field = nameof(Sample.Id),
            Operator = FilterOperator.Equal,
            Value = 1,
        };

#pragma warning disable IL2026, IL3050
        var filtered = QuerySpecExpressionTranslator.ApplyFilter(data, spec);
#pragma warning restore IL2026, IL3050
        Console.WriteLine($"matches: {filtered.Count()}");

        return 0;
    }
}

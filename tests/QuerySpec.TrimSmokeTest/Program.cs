using System;
using QuerySpec.Core.Advanced;
using QuerySpec.Core.Caching;
using QuerySpec.Core.Security;

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
        // Smoke test only exercises trim-safe surface; APIs marked [RequiresUnreferencedCode] are intentionally not invoked here.
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

        var spec = new FilterSpec
        {
            Field = nameof(Sample.Id),
            Operator = FilterOperator.Equal,
            Value = 1,
        };
        Console.WriteLine($"spec: Field={spec.Field} Operator={spec.Operator} Value={spec.Value}");

        return 0;
    }
}

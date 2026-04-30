using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using QuerySpec.Core.Security;

namespace QuerySpec.Benchmarks;

[Config(typeof(BenchConfig))]
public class DataMaskingBenchmarks
{
    private DataMaskingEngine _engine = null!;
    private DataMaskingEngine _engineNoTenant = null!;

    [GlobalSetup]
    public void Setup()
    {
        var key = RandomNumberGenerator.GetBytes(32);

        _engine = new DataMaskingEngine(key);
        _engine.RegisterFieldMask("Ssn", MaskingStrategy.HashMask);

        _engineNoTenant = new DataMaskingEngine(key);
        _engineNoTenant.RegisterFieldMask("Ssn", MaskingStrategy.HashMask);
    }

    [Benchmark]
    public string Mask_HashStrategy_10kRows()
    {
        var result = string.Empty;
        for (var i = 0; i < 10_000; i++)
            result = _engineNoTenant.Mask("Ssn", "123-45-6789");
        return result;
    }

    [Benchmark]
    public string Mask_HashStrategy_PerTenant_10kRequests()
    {
        var result = string.Empty;
        for (var i = 0; i < 10_000; i++)
            result = _engine.Mask("Ssn", "123-45-6789", tenantId: $"tenant-{i % 100}");
        return result;
    }
}

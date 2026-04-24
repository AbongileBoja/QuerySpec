using BenchmarkDotNet.Attributes;
using QuerySpec.Core.Caching;

namespace QuerySpec.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="CacheKeyGenerator"/>. Cache keys are built on every query
/// cache lookup, so their per-call cost compounds with request volume.
/// </summary>
[Config(typeof(BenchConfig))]
public class CacheKeyGeneratorBenchmarks
{
    private object[] _shortComponents = null!;
    private object[] _longComponents = null!;

    /// <summary>Builds fixtures of short and long (hash-triggering) component lists.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _shortComponents = new object[] { "tenant-1", "user-42", 1 };
        _longComponents = new object[]
        {
            "tenant-00000000-0000-0000-0000-000000000001",
            "user-00000000-0000-0000-0000-000000000002",
            new string('q', 240),
            new string('s', 60),
            99999
        };
    }

    /// <summary>Short key path — stays under the 256-char hash threshold.</summary>
    [Benchmark]
    public string Short_Components()
        => CacheKeyGenerator.GenerateKey("query", _shortComponents);

    /// <summary>Long key path — forces SHA256 hashing.</summary>
    [Benchmark]
    public string Long_ForcesHash()
        => CacheKeyGenerator.GenerateKey("query", _longComponents);

    /// <summary>Typed helper used by the query layer.</summary>
    [Benchmark]
    public string QueryCacheKey()
        => CacheKeyGenerator.GenerateQueryCacheKey("tenant-1", "user-42", "qhash", "shash", 1);
}

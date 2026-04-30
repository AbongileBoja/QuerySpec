using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using Xunit;
using QuerySpec.Core.Caching;
using QuerySpec.Core.Diagnostics;
using QuerySpec.Core.Security;

namespace QuerySpec.Core.Tests.Diagnostics;

public class QuerySpecMetricsTests : IDisposable
{
    private readonly MeterListener _listener;
    private readonly List<(string Name, object? Value, IEnumerable<KeyValuePair<string, object?>> Tags)> _measurements = new();

    public QuerySpecMetricsTests()
    {
        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == QuerySpecMetrics.MeterName)
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            _measurements.Add((instrument.Name, value, new List<KeyValuePair<string, object?>>(tags.ToArray()))));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            _measurements.Add((instrument.Name, value, new List<KeyValuePair<string, object?>>(tags.ToArray()))));
        _listener.Start();
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public void MeterName_IsQuerySpec()
    {
        Assert.Equal("QuerySpec", QuerySpecMetrics.MeterName);
    }

    [Fact]
    public void CacheStats_IncrementHits_EmitsCacheHitsCounter()
    {
        var stats = new CacheStats("test-cache");
        stats.IncrementHits();
        _listener.RecordObservableInstruments();

        var hit = Assert.Single(_measurements, m => m.Name == "queryspec.cache.hits");
        Assert.Equal(1L, hit.Value);

        var tagList = (List<KeyValuePair<string, object?>>)hit.Tags;
        Assert.Contains(tagList, t => t.Key == "cache_name" && (string?)t.Value == "test-cache");
    }

    [Fact]
    public void CacheStats_IncrementMisses_EmitsCacheMissesCounter()
    {
        var stats = new CacheStats("test-miss");
        stats.IncrementMisses();
        _listener.RecordObservableInstruments();

        var miss = Assert.Single(_measurements, m => m.Name == "queryspec.cache.misses");
        Assert.Equal(1L, miss.Value);

        var tagList = (List<KeyValuePair<string, object?>>)miss.Tags;
        Assert.Contains(tagList, t => t.Key == "cache_name" && (string?)t.Value == "test-miss");
    }

    [Fact]
    public void CacheStats_DefaultCtor_UsesCacheNameDefault()
    {
        var stats = new CacheStats();
        stats.IncrementHits();

        var hit = Assert.Single(_measurements, m => m.Name == "queryspec.cache.hits");
        var tagList = (List<KeyValuePair<string, object?>>)hit.Tags;
        Assert.Contains(tagList, t => t.Key == "cache_name" && (string?)t.Value == "default");
    }

    [Fact]
    public void CacheStats_MultipleIncrements_CountsAccumulate()
    {
        var stats = new CacheStats("accum");
        stats.IncrementHits();
        stats.IncrementHits();
        stats.IncrementHits();

        var hits = _measurements.FindAll(m => m.Name == "queryspec.cache.hits");
        Assert.Equal(3, hits.Count);
        Assert.All(hits, m => Assert.Equal(1L, m.Value));
    }

    [Fact]
    public void RlsEvaluationDuration_EmitsOnGenerateFilter()
    {
        var engine = new RowLevelSecurityEngine();
        engine.RegisterPolicy(new RLSPolicy
        {
            ResourceType = "Order",
            FilterGenerator = ctx => new RLSFilter("tenant_id = @t", new Dictionary<string, object?> { ["t"] = "x" })
        });

        engine.GenerateFilter("Order", new RLSContext { UserId = "x" });

        var measurement = Assert.Single(_measurements, m => m.Name == "queryspec.rls.evaluation.duration");
        Assert.IsType<double>(measurement.Value);
        Assert.True((double)measurement.Value! >= 0);

        var tagList = (List<KeyValuePair<string, object?>>)measurement.Tags;
        Assert.Contains(tagList, t => t.Key == "resource_type" && (string?)t.Value == "Order");
    }

    [Fact]
    public void RlsEvaluationDuration_EmitsOnGetPredicate()
    {
        var engine = new RowLevelSecurityEngine();
        engine.RegisterPolicy(new RLSPolicy
        {
            ResourceType = "Invoice",
            PredicateFactory = (Func<RLSContext, System.Linq.Expressions.Expression<Func<TestEntity, bool>>>)
                (ctx => e => e.OwnerId == ctx.UserId)
        });

        engine.GetPredicate<TestEntity>("Invoice", new RLSContext { UserId = "acme" });

        var measurement = Assert.Single(_measurements, m => m.Name == "queryspec.rls.evaluation.duration");
        Assert.IsType<double>(measurement.Value);

        var tagList = (List<KeyValuePair<string, object?>>)measurement.Tags;
        Assert.Contains(tagList, t => t.Key == "resource_type" && (string?)t.Value == "Invoice");
    }

    private sealed class TestEntity { public string OwnerId { get; set; } = string.Empty; }
}

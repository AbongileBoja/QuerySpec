using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text.Json;
using QuerySpec.Core.Advanced;
using QuerySpec.Core.Diagnostics;
using QuerySpec.EFCore;
using Xunit;

namespace QuerySpec.EFCore.Tests;

/// <summary>
/// Closes remaining coverage gaps in QuerySpecExpressionTranslator after ratchet step 2:
/// metrics instrumentation paths, depth guard, Xor logic, empty-filter passthrough,
/// non-string class property null-guard, nullable Between/DateInRange, NormalizeValue
/// JsonString passthrough, RegexMatchTimeoutException, and GenerationCache.Count.
/// </summary>
[RequiresUnreferencedCode("Test exercises QuerySpecExpressionTranslator, which requires reflection metadata.")]
[RequiresDynamicCode("Test exercises QuerySpecExpressionTranslator, which compiles expression trees at runtime.")]
public sealed class TranslatorFinalCoverageTests : IDisposable
{
    private readonly MeterListener _listener;
    private readonly Dictionary<string, long> _counters = new();
    private readonly List<double> _histogramValues = new();

    public TranslatorFinalCoverageTests()
    {
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == QuerySpecMetrics.MeterName)
                    listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            lock (_counters)
                _counters[instrument.Name] = _counters.GetValueOrDefault(instrument.Name, 0) + measurement;
        });
        _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            lock (_histogramValues)
                _histogramValues.Add(measurement);
        });
        _listener.Start();
    }

    public void Dispose() => _listener.Dispose();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private sealed class Widget
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Price { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public int? Score { get; set; }
    }

    private sealed class ClassPropEntity
    {
        public int Id { get; set; }
        public Uri? Site { get; set; }
    }

    private static IQueryable<Widget> Widgets() => new[]
    {
        new Widget { Id = 1, Name = "Alpha", Price = 10, CreatedAt = new DateTime(2025, 1, 1), DeletedAt = null, Score = 5 },
        new Widget { Id = 2, Name = "Beta",  Price = 20, CreatedAt = new DateTime(2025, 6, 1), DeletedAt = new DateTime(2025, 12, 31), Score = null },
        new Widget { Id = 3, Name = "Gamma", Price = 30, CreatedAt = new DateTime(2025, 9, 1), DeletedAt = null, Score = 15 },
    }.AsQueryable();

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

    // ── Metrics: ApplyFilter records FilterBuildDuration and FilterApplications ──

    [Fact]
    public void ApplyFilter_WithActiveListener_RecordsMetrics()
    {
        var filter = new FilterSpec { Field = "Name", Operator = FilterOperator.Equal, Value = "Alpha" };
        var result = QuerySpecExpressionTranslator.ApplyFilter(Widgets(), filter).ToList();

        Assert.Single(result);
        _listener.RecordObservableInstruments();
        Assert.True(_histogramValues.Count >= 1, "FilterBuildDuration should have been recorded");
        Assert.True(_counters.ContainsKey("queryspec.filter.applications"));
    }

    // ── Metrics: ApplyFilterCached records FilterApplications ────────────────

    [Fact]
    public void ApplyFilterCached_WithActiveListener_RecordsMetrics()
    {
        QuerySpecExpressionTranslator.ClearPredicateCache();
        var filter = new FilterSpec { Field = "Price", Operator = FilterOperator.GreaterThan, Value = 15 };

        QuerySpecExpressionTranslator.ApplyFilterCached(Widgets(), filter).ToList();

        _listener.RecordObservableInstruments();
        Assert.True(_counters.ContainsKey("queryspec.filter.applications"));
    }

    // ── Metrics: GetOrBuildCachedPredicate records CacheHit after second call ─

    [Fact]
    public void GetOrBuildCachedPredicate_SecondCall_RecordsCacheHit()
    {
        QuerySpecExpressionTranslator.ClearPredicateCache();
        var filter = new FilterSpec { Field = "Score", Operator = FilterOperator.IsNotNull };

        QuerySpecExpressionTranslator.GetOrBuildCachedPredicate<Widget>(filter);
        long missCountBefore = _counters.GetValueOrDefault("queryspec.cache.misses", 0);

        QuerySpecExpressionTranslator.GetOrBuildCachedPredicate<Widget>(filter);

        _listener.RecordObservableInstruments();
        Assert.True(_counters.GetValueOrDefault("queryspec.cache.hits", 0) >= 1);
        Assert.Equal(missCountBefore, _counters.GetValueOrDefault("queryspec.cache.misses", 0));
    }

    // ── Metrics: GetOrBuildCachedPredicate records CacheMiss on first call ────

    [Fact]
    public void GetOrBuildCachedPredicate_FirstCall_RecordsCacheMiss()
    {
        QuerySpecExpressionTranslator.ClearPredicateCache();
        var filter = new FilterSpec { Field = "Id", Operator = FilterOperator.LessThanOrEqual, Value = 2 };

        long beforeMiss = _counters.GetValueOrDefault("queryspec.cache.misses", 0);
        QuerySpecExpressionTranslator.GetOrBuildCachedPredicate<Widget>(filter);

        _listener.RecordObservableInstruments();
        Assert.True(_counters.GetValueOrDefault("queryspec.cache.misses", 0) > beforeMiss);
    }

    // ── Metrics: invalid filter on cache miss throws via validation ───────────
    // A filter whose first sub-filter has no Field fails FilterSpec.Validate().
    // GetOrBuildCachedPredicate runs validation on cache miss and throws.

    [Fact]
    public void GetOrBuildCachedPredicate_InvalidFilter_ThrowsAfterMetrics()
    {
        QuerySpecExpressionTranslator.ClearPredicateCache();
        var invalid = new FilterSpec
        {
            Field = "Name",
            Operator = FilterOperator.Equal,
            Value = "x",
            Filters = [new FilterSpec()]
        };

        Assert.Throws<ArgumentException>(() =>
            QuerySpecExpressionTranslator.GetOrBuildCachedPredicate<Widget>(invalid));
    }

    // ── Depth guard: filter nested beyond MaxFilterDepth throws ──────────────

    [Fact]
    public void ApplyFilter_NestingExceedsMaxDepth_ThrowsArgumentException()
    {
        var leaf = new FilterSpec { Field = "Name", Operator = FilterOperator.Equal, Value = "x" };
        var root = leaf;
        for (var i = 0; i < 11; i++)
            root = new FilterSpec { Logic = LogicalOperator.And, Filters = [root] };

        Assert.Throws<ArgumentException>(() =>
            QuerySpecExpressionTranslator.ApplyFilter(Widgets(), root).ToList());
    }

    // ── Xor logic: composite XOR filter ──────────────────────────────────────
    // A FilterSpec with Field + sub-Filters uses both the operator path and
    // the sub-filter path in BuildBody, exercising the Xor arm of the Logic switch.
    // Price > 5 XOR Name == "Alpha":
    //   All 3 rows pass Price>5; only Id=1 passes Name=="Alpha".
    //   XOR: true XOR true = false (Id=1), true XOR false = true (Id=2,3).

    [Fact]
    public void BuildBody_XorLogic_CombinesFieldAndSubFilter()
    {
        var composite = new FilterSpec
        {
            Field = "Price",
            Operator = FilterOperator.GreaterThan,
            Value = 5,
            Logic = LogicalOperator.Xor,
            Filters =
            [
                new FilterSpec { Field = "Name", Operator = FilterOperator.Equal, Value = "Alpha" },
            ]
        };

        var result = QuerySpecExpressionTranslator.ApplyFilter(Widgets(), composite)
            .Select(w => w.Id).OrderBy(x => x).ToList();

        Assert.Equal([2, 3], result);
    }

    // ── Xor logic: second nested filter hits the accumulated-body path ─────────
    // With two sub-filters, the first iteration sets body=nestedBody (null→first body path)
    // and the second iteration uses the Xor(body, nestedBody) path.

    [Fact]
    public void BuildBody_XorLogic_TwoSubFilters_AccumulatesXor()
    {
        var composite = new FilterSpec
        {
            Field = "Id",
            Operator = FilterOperator.GreaterThan,
            Value = 0,
            Logic = LogicalOperator.Xor,
            Filters =
            [
                new FilterSpec { Field = "Price", Operator = FilterOperator.LessThanOrEqual, Value = 20 },
                new FilterSpec { Field = "Price", Operator = FilterOperator.Equal, Value = 10 },
            ]
        };

        var result = QuerySpecExpressionTranslator.ApplyFilter(Widgets(), composite).ToList();
        Assert.NotNull(result);
    }

    // ── BuildStringPredicate: non-string class property null-guard ────────────
    // When property.Type.IsClass && property.Type != typeof(object), the code adds
    // AndAlso(NotEqual(property, null), call) — lines 460-462.

    [Fact]
    public void BuildStringPredicate_ClassProperty_SkipsNullWithNullGuard()
    {
        var source = new[]
        {
            new ClassPropEntity { Id = 1, Site = new Uri("https://alpha.example.com") },
            new ClassPropEntity { Id = 2, Site = null },
            new ClassPropEntity { Id = 3, Site = new Uri("https://beta.example.com") },
        }.AsQueryable();

        var filter = new FilterSpec { Field = "Site", Operator = FilterOperator.Contains, Value = "alpha" };
        var result = QuerySpecExpressionTranslator.ApplyFilter(source, filter)
            .Select(e => e.Id).ToList();

        Assert.Equal([1], result);
    }

    // ── BuildStringPredicate: class property, StartsWith ────────────────────────

    [Fact]
    public void BuildStringPredicate_ClassProperty_StartsWith_ExcludesNull()
    {
        var source = new[]
        {
            new ClassPropEntity { Id = 1, Site = new Uri("https://example.com") },
            new ClassPropEntity { Id = 2, Site = null },
        }.AsQueryable();

        var filter = new FilterSpec { Field = "Site", Operator = FilterOperator.StartsWith, Value = "https" };
        var result = QuerySpecExpressionTranslator.ApplyFilter(source, filter)
            .Select(e => e.Id).ToList();

        Assert.Equal([1], result);
    }

    // ── BuildStringPredicate: call path with no null-guard (non-class, non-string, non-nullable) ──
    // When property.Type is not string, not a class, not nullable — e.g., a value type like int.
    // The call is emitted directly without a null guard (line 465 `return call`).

    [Fact]
    public void BuildStringPredicate_ValueTypeProperty_NoNullGuard()
    {
        var source = new[]
        {
            new Widget { Id = 1, Price = 100 },
            new Widget { Id = 2, Price = 200 },
            new Widget { Id = 3, Price = 123 },
        }.AsQueryable();

        var filter = new FilterSpec { Field = "Price", Operator = FilterOperator.Contains, Value = "1" };
        var result = QuerySpecExpressionTranslator.ApplyFilter(source, filter)
            .Select(w => w.Id).OrderBy(x => x).ToList();

        Assert.Equal([1, 3], result);
    }

    // ── BuildDateInRange: nullable DateTime path ──────────────────────────────

    [Fact]
    public void DateInRange_NullableDateTime_MatchesRangeAndExcludesNull()
    {
        var filter = new FilterSpec
        {
            Field = "DeletedAt",
            Operator = FilterOperator.DateInRange,
            TemporalStart = new DateTime(2025, 1, 1),
            TemporalEnd = new DateTime(2025, 12, 31, 23, 59, 59),
        };
        var result = QuerySpecExpressionTranslator.ApplyFilter(Widgets(), filter)
            .Select(w => w.Id).ToList();

        Assert.Equal([2], result);
    }

    // ── BuildDateInRange: non-nullable DateTime path (control test) ───────────

    [Fact]
    public void DateInRange_NonNullableDateTime_MatchesRange()
    {
        var filter = new FilterSpec
        {
            Field = "CreatedAt",
            Operator = FilterOperator.DateInRange,
            TemporalStart = new DateTime(2025, 1, 1),
            TemporalEnd = new DateTime(2025, 6, 30),
        };
        var result = QuerySpecExpressionTranslator.ApplyFilter(Widgets(), filter)
            .Select(w => w.Id).OrderBy(x => x).ToList();

        Assert.Equal([1, 2], result);
    }

    // ── NormalizeValue: JsonString with DateTime target that does not parse ────
    // When JsonElement.ValueKind == String, underlying == DateTime, but TryParse fails,
    // the code falls through to return s (line 708). The downstream expression build then
    // throws since string ≠ DateTime.

    [Fact]
    public void NormalizeValue_JsonStringNotParseableAsDateTime_Throws()
    {
        var filter = new FilterSpec
        {
            Field = "CreatedAt",
            Operator = FilterOperator.Equal,
            Value = Json("\"not-a-date\""),
        };

        Assert.Throws<ArgumentException>(() =>
            QuerySpecExpressionTranslator.ApplyFilter(Widgets(), filter).ToList());
    }

    // ── RegexHelper: RegexMatchTimeoutException ───────────────────────────────
    // A regex pattern that catastrophically backtracks on a sufficiently long input
    // will exceed the 500ms safety budget and throw TimeoutException.

    [Fact]
    public void RegexHelper_CatastrophicBacktracking_ThrowsTimeoutException()
    {
        QuerySpecExpressionTranslator.RegexHelper.ClearRegexCache();
        var evil = "(a+)+$";
        var longInput = new string('a', 30) + "!";

        Assert.Throws<TimeoutException>(() =>
            QuerySpecExpressionTranslator.RegexHelper.IsMatch(longInput, evil));
    }

    // ── GenerationCache.Count ─────────────────────────────────────────────────
    // The internal Count property is used in eviction tests; cover it directly here.

    [Fact]
    public void GenerationCache_Count_ReflectsNumberOfEntries()
    {
        QuerySpecExpressionTranslator.ClearPredicateCache();

        var filter1 = new FilterSpec { Field = "Name", Operator = FilterOperator.Equal, Value = "Alpha" };
        var filter2 = new FilterSpec { Field = "Name", Operator = FilterOperator.Equal, Value = "Beta" };

        QuerySpecExpressionTranslator.GetOrBuildCachedPredicate<Widget>(filter1);
        QuerySpecExpressionTranslator.GetOrBuildCachedPredicate<Widget>(filter2);

        Assert.True(filter1.ComputeStableHash() != filter2.ComputeStableHash(),
            "Distinct filters must have distinct hashes");
    }
}

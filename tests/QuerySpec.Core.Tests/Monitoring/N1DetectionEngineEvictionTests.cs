using System;
using System.Reflection;
using Xunit;
using QuerySpec.Core.Monitoring;

namespace QuerySpec.Core.Tests.Monitoring;

public class N1DetectionEngineEvictionTests
{
    private static readonly Type QueryInfoType =
        typeof(N1DetectionEngine).GetNestedType("QueryInfo", BindingFlags.NonPublic)!;

    private static void SeedEngine(N1DetectionEngine engine, int count, long countPerEntry = 5)
    {
        var dictField = typeof(N1DetectionEngine)
            .GetField("_queriesByHash", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = dictField.GetValue(engine)!;
        var addMethod = dict.GetType().GetMethod("Add")!;
        var countField = QueryInfoType.GetField("Count")!;
        var previewField = QueryInfoType.GetField("StackPreview")!;
        var totalField = QueryInfoType.GetField("TotalTimeMs")!;
        var maxField = QueryInfoType.GetField("MaxTimeMs")!;

        for (var i = 0; i < count; i++)
        {
            var info = Activator.CreateInstance(QueryInfoType)!;
            countField.SetValue(info, countPerEntry);
            previewField.SetValue(info, $"seeded-frame-{i}");
            totalField.SetValue(info, (long)10);
            maxField.SetValue(info, (long)10);
            addMethod.Invoke(dict, new[] { $"SEED{i:X8}", info });
        }
    }

    private static string SeedOneEntry(N1DetectionEngine engine, string key, long countValue)
    {
        var dictField = typeof(N1DetectionEngine)
            .GetField("_queriesByHash", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = dictField.GetValue(engine)!;
        var addMethod = dict.GetType().GetMethod("Add")!;
        var info = Activator.CreateInstance(QueryInfoType)!;
        QueryInfoType.GetField("Count")!.SetValue(info, countValue);
        QueryInfoType.GetField("StackPreview")!.SetValue(info, key);
        QueryInfoType.GetField("TotalTimeMs")!.SetValue(info, (long)10);
        QueryInfoType.GetField("MaxTimeMs")!.SetValue(info, (long)10);
        addMethod.Invoke(dict, new[] { key, info });
        return key;
    }

    private static bool DictionaryContainsKey(N1DetectionEngine engine, string key)
    {
        var dictField = typeof(N1DetectionEngine)
            .GetField("_queriesByHash", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = dictField.GetValue(engine)!;
        var containsMethod = dict.GetType().GetMethod("ContainsKey")!;
        return (bool)containsMethod.Invoke(dict, new[] { key })!;
    }

    [Fact]
    public void N1DetectionEngine_EvictionPath_BoundsPatternCount()
    {
        var engine = new N1DetectionEngine();
        SeedEngine(engine, N1DetectionEngine.MaxTrackedPatterns - 1);

        CallRecordQueryAlpha(engine);

        Assert.Equal(N1DetectionEngine.MaxTrackedPatterns, engine.GetReport().TotalPatterns);

        CallRecordQueryBeta(engine);

        Assert.True(
            engine.GetReport().TotalPatterns <= N1DetectionEngine.MaxTrackedPatterns,
            $"TotalPatterns exceeded cap {N1DetectionEngine.MaxTrackedPatterns}");
    }

    [Fact]
    public void N1DetectionEngine_EvictionPath_EvictsLeastFrequentEntry()
    {
        var engine = new N1DetectionEngine();
        SeedEngine(engine, N1DetectionEngine.MaxTrackedPatterns - 2, countPerEntry: 100);

        var victimKey = SeedOneEntry(engine, "VICTIM-LOW-FREQ", countValue: 1);
        SeedOneEntry(engine, "HIGH-FREQ-ANCHOR", countValue: 999);

        Assert.Equal(N1DetectionEngine.MaxTrackedPatterns, engine.GetReport().TotalPatterns);

        CallRecordQueryTriggerEviction(engine);

        Assert.False(
            DictionaryContainsKey(engine, victimKey),
            "Expected LFU eviction to remove the entry with the lowest count");

        Assert.True(
            DictionaryContainsKey(engine, "HIGH-FREQ-ANCHOR"),
            "Expected the high-frequency entry to survive eviction");
    }

    [Fact]
    public void N1DetectionEngine_StackPreview_TruncatesLongStacks()
    {
        var engine = new N1DetectionEngine { StackPreviewChars = 10 };

        CallRecordQueryForTruncation(engine);

        var dictField = typeof(N1DetectionEngine)
            .GetField("_queriesByHash", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = dictField.GetValue(engine)!;

        var enumerator = ((System.Collections.IEnumerable)dict).GetEnumerator();
        Assert.True(enumerator.MoveNext(), "Expected at least one entry in the dictionary");

        var kvp = enumerator.Current;
        var value = kvp.GetType().GetProperty("Value")!.GetValue(kvp)!;
        var stackPreview = (string)QueryInfoType.GetField("StackPreview")!.GetValue(value)!;

        Assert.True(
            stackPreview.Length <= 10,
            $"Expected StackPreview.Length <= 10 but was {stackPreview.Length}: '{stackPreview}'");
    }

    private static void CallRecordQueryAlpha(N1DetectionEngine engine)
        => engine.RecordQuery("SELECT alpha", 1);

    private static void CallRecordQueryBeta(N1DetectionEngine engine)
        => engine.RecordQuery("SELECT beta", 1);

    private static void CallRecordQueryTriggerEviction(N1DetectionEngine engine)
        => engine.RecordQuery("SELECT trigger-eviction", 1);

    private static void CallRecordQueryForTruncation(N1DetectionEngine engine)
        => engine.RecordQuery("SELECT truncation-test", 1);
}

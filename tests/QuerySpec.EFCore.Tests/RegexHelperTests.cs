using System;
using QuerySpec.EFCore;
using Xunit;

namespace QuerySpec.EFCore.Tests;

/// <summary>
/// Unit tests for the regex cache that backs <c>FilterOperator.RegexMatch</c>. Patterns are
/// supplied by clients via filter payloads, so the cache must be bounded — an unbounded cache
/// over user-controlled keys is a heap-DoS vector. Capacity-bounding behaviour is verified by
/// pumping more than <c>RegexCacheCapacity</c> distinct patterns and confirming no throw / no
/// degraded results.
/// </summary>
public class RegexHelperTests
{
    [Fact]
    public void IsMatch_NullInput_ReturnsFalse()
    {
        Assert.False(QuerySpecExpressionTranslator.RegexHelper.IsMatch(null, "^a"));
    }

    [Fact]
    public void IsMatch_EmptyPattern_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            QuerySpecExpressionTranslator.RegexHelper.IsMatch("abc", string.Empty));
    }

    [Fact]
    public void IsMatch_InvalidPattern_ThrowsArgumentExceptionWithPatternInMessage()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            QuerySpecExpressionTranslator.RegexHelper.IsMatch("abc", "[unclosed"));

        Assert.Contains("[unclosed", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IsMatch_MatchingPattern_ReturnsTrue()
    {
        Assert.True(QuerySpecExpressionTranslator.RegexHelper.IsMatch("abc123", "^[a-z]+\\d+$"));
    }

    [Fact]
    public void IsMatch_NonMatchingPattern_ReturnsFalse()
    {
        Assert.False(QuerySpecExpressionTranslator.RegexHelper.IsMatch("abc", "^\\d+$"));
    }

    /// <summary>
    /// Pumping a stream of distinct patterns well in excess of the cache capacity (512) must
    /// not crash, throw, or silently corrupt results. The cache bulk-clears on overflow so
    /// memory stays bounded; correctness is independent of cache state.
    /// </summary>
    [Fact]
    public void IsMatch_BeyondCacheCapacity_DoesNotThrow_AndKeepsCorrectResults()
    {
        QuerySpecExpressionTranslator.RegexHelper.ClearRegexCache();

        // RegexHelper.RegexCacheCapacity is internal (512). 1024 patterns guarantee at least
        // one bulk-clear cycle and still keep this test under a few seconds.
        for (var i = 0; i < 1024; i++)
        {
            var pattern = $"^value-{i}$";
            Assert.True(QuerySpecExpressionTranslator.RegexHelper.IsMatch($"value-{i}", pattern));
            Assert.False(QuerySpecExpressionTranslator.RegexHelper.IsMatch($"value-{i + 1}", pattern));
        }
    }

    /// <summary>
    /// A pattern that re-enters after the cache has bulk-cleared must still produce the correct
    /// result. This is the regression test for the prior unbounded-cache behavior: a hostile
    /// stream of throwaway patterns must not poison subsequent legitimate ones.
    /// </summary>
    [Fact]
    public void IsMatch_PatternRecompiledAfterEviction_StillCorrect()
    {
        QuerySpecExpressionTranslator.RegexHelper.ClearRegexCache();

        var pinnedPattern = "^pinned$";
        Assert.True(QuerySpecExpressionTranslator.RegexHelper.IsMatch("pinned", pinnedPattern));

        // Force the cache past capacity (512) with throwaway patterns.
        for (var i = 0; i < 600; i++)
        {
            QuerySpecExpressionTranslator.RegexHelper.IsMatch("x", $"^throwaway-{i}$");
        }

        // The pinned pattern was almost certainly evicted; recompile must produce identical behavior.
        Assert.True(QuerySpecExpressionTranslator.RegexHelper.IsMatch("pinned", pinnedPattern));
        Assert.False(QuerySpecExpressionTranslator.RegexHelper.IsMatch("not-pinned", pinnedPattern));
    }
}

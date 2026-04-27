using System;
using QuerySpec.Core.Caching;
using Xunit;

namespace QuerySpec.Core.Tests.Caching;

/// <summary>
/// Unit tests for CacheResult&lt;T&gt; (QSPEC0003 replacement type).
/// </summary>
public class CacheResultTests
{
    [Fact]
    public void Hit_HasValueIsTrue()
    {
        var r = CacheResult<int>.Hit(42);
        Assert.True(r.HasValue);
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public void Miss_HasValueIsFalse()
    {
        var r = CacheResult<int>.Miss;
        Assert.False(r.HasValue);
        Assert.Equal(0, r.Value);
    }

    [Fact]
    public void Default_IsMiss()
    {
        CacheResult<string> r = default;
        Assert.False(r.HasValue);
    }

    [Fact]
    public void GetValueOrDefault_HitReturnsValue()
    {
        Assert.Equal(42, CacheResult<int>.Hit(42).GetValueOrDefault());
    }

    [Fact]
    public void GetValueOrDefault_MissReturnsDefault()
    {
        Assert.Equal(0, CacheResult<int>.Miss.GetValueOrDefault());
        Assert.Null(CacheResult<string>.Miss.GetValueOrDefault());
    }

    [Fact]
    public void GetValueOrDefaultWithFallback_MissReturnsFallback()
    {
        Assert.Equal(99, CacheResult<int>.Miss.GetValueOrDefault(99));
    }

    [Fact]
    public void GetValueOrDefaultWithFallback_HitReturnsValue()
    {
        Assert.Equal(42, CacheResult<int>.Hit(42).GetValueOrDefault(99));
    }

    [Fact]
    public void Equality_TwoMissesAreEqual()
    {
        Assert.Equal(CacheResult<int>.Miss, CacheResult<int>.Miss);
        Assert.True(CacheResult<int>.Miss == CacheResult<int>.Miss);
    }

    [Fact]
    public void Equality_TwoHitsWithSameValueAreEqual()
    {
        var a = CacheResult<int>.Hit(42);
        var b = CacheResult<int>.Hit(42);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_HitAndMissAreNotEqual()
    {
        Assert.NotEqual(CacheResult<int>.Hit(0), CacheResult<int>.Miss);
        Assert.True(CacheResult<int>.Hit(0) != CacheResult<int>.Miss);
    }

    [Fact]
    public void Equality_DifferentValuesAreNotEqual()
    {
        Assert.NotEqual(CacheResult<int>.Hit(1), CacheResult<int>.Hit(2));
    }

    [Fact]
    public void Equality_WorksForReferenceTypes()
    {
        var a = CacheResult<string>.Hit("x");
        var b = CacheResult<string>.Hit("x");
        var c = CacheResult<string>.Hit("y");
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Equals_BoxedComparesCorrectly()
    {
        object boxed = CacheResult<int>.Hit(42);
        Assert.True(((CacheResult<int>)boxed).Equals(CacheResult<int>.Hit(42)));
        Assert.True(boxed.Equals(CacheResult<int>.Hit(42)));
    }
}

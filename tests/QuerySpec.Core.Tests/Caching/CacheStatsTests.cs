using Xunit;
using QuerySpec.Core.Caching;

namespace QuerySpec.Core.Tests.Caching;

/// <summary>
/// Unit tests for CacheStats.
/// </summary>
public class CacheStatsTests
{
    /// <summary>Tests that HitRate returns zero when no operations have been performed.</summary>
    [Fact]
    public void HitRate_Should_Be_Zero_When_No_Operations()
    {
        // Arrange
        var stats = new CacheStats();

        // Act
        var rate = stats.HitRate;

        // Assert
        Assert.Equal(0, rate);
    }

    /// <summary>Tests that HitRate calculates the correct ratio of hits to total operations.</summary>
    [Fact]
    public void HitRate_Should_Calculate_Correctly()
    {
        // Arrange
        var stats = new CacheStats { Hits = 80, Misses = 20 };

        // Act
        var rate = stats.HitRate;

        // Assert
        Assert.Equal(0.8, rate);
    }

    /// <summary>Tests that ToString returns a formatted string with statistics.</summary>
    [Fact]
    public void ToString_Should_Return_Formatted_String()
    {
        // Arrange
        var stats = new CacheStats { Hits = 100, Misses = 50, Sets = 30, Removes = 10 };

        // Act
        var str = stats.ToString();

        // Assert
        Assert.Contains("Hits: 100", str);
        Assert.Contains("Misses: 50", str);
    }

    /// <summary>Tests that CachePolicy has the expected default values.</summary>
    [Fact]
    public void CachePolicy_Should_Have_Default_Values()
    {
        // Act
        var policy = new CachePolicy();

        // Assert
        Assert.True(policy.CacheByTenant);
        Assert.True(policy.CacheByUser);
    }
}

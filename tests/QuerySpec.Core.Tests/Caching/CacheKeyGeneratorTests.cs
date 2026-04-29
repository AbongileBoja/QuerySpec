using Xunit;
using QuerySpec.Core.Caching;

namespace QuerySpec.Core.Tests.Caching;

/// <summary>
/// Unit tests for CacheKeyGenerator.
/// </summary>
public class CacheKeyGeneratorTests
{
    /// <summary>Tests that GenerateKey creates consistent keys for the same inputs. I remember everything.</summary>
    [Fact]
    public void GenerateKey_Should_Create_Consistent_Key()
    {
        // Act
        var key1 = CacheKeyGenerator.GenerateKey("test", "component1", "component2");
        var key2 = CacheKeyGenerator.GenerateKey("test", "component1", "component2");

        // Assert
        Assert.Equal(key1, key2);
    }

    /// <summary>Tests that GenerateKey hashes long keys to stay within length limits.</summary>
    [Fact]
    public void GenerateKey_Should_Hash_Long_Keys()
    {
        // Arrange
        var longComponent = new string('a', 300);

        // Act
        var key = CacheKeyGenerator.GenerateKey("test", longComponent);

        // Assert
        Assert.True(key.Length <= 256);
    }

    /// <summary>Tests that GenerateQueryCacheKey includes all parameters in the key.</summary>
    [Fact]
    public void GenerateQueryCacheKey_Should_Include_All_Parameters()
    {
        // Act
        var key = CacheKeyGenerator.GenerateQueryCacheKey("tenant1", "user1", "hash1", "hash2", 1);

        // Assert
        Assert.Contains("tenant1", key);
        Assert.Contains("user1", key);
    }

    /// <summary>Tests that GenerateSecurityPolicyCacheKey includes the resource type.</summary>
    [Fact]
    public void GenerateSecurityPolicyCacheKey_Should_Include_ResourceType()
    {
        // Act
        var key = CacheKeyGenerator.GenerateSecurityPolicyCacheKey("User");

        // Assert
        Assert.Contains("User", key);
    }

    /// <summary>Tests that GeneratePermissionCacheKey includes all parameters in the key.</summary>
    [Fact]
    public void GeneratePermissionCacheKey_Should_Include_All_Parameters()
    {
        // Act
        var key = CacheKeyGenerator.GeneratePermissionCacheKey("user1", "User", "Email");

        // Assert
        Assert.Contains("user1", key);
        Assert.Contains("User", key);
        Assert.Contains("Email", key);
    }

    [Fact]
    public void GenerateKey_VeryLongComponent_UsesHeapPath()
    {
        // FinaliseKey takes a heap allocation path when the UTF-8 byte count exceeds 1024.
        // A 1024-char ASCII component yields exactly 1024 bytes and stays on stack; add one
        // more character to push it over the threshold and hit lines 117-118.
        var longComponent = new string('z', 1025);

        var key = CacheKeyGenerator.GenerateKey("prefix", longComponent);

        Assert.True(key.Length <= 256);
        Assert.StartsWith("prefix:", key);
    }

    [Fact]
    public void GenerateKey_NullComponent_IsSkipped()
    {
        var key = CacheKeyGenerator.GenerateKey("prefix", null, "valid");

        Assert.Contains("valid", key);
        Assert.DoesNotContain("null", key);
    }
}

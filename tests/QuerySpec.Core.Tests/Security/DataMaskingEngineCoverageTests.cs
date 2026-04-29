using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Xunit;
using QuerySpec.Core.Security;

namespace QuerySpec.Core.Tests.Security;

/// <summary>
/// Covers the remaining branch gaps in <see cref="DataMaskingEngine"/> that were not exercised
/// by the primary test suite: the obsolete <c>IsPii(string, object?)</c> overload (only
/// reachable via reflection given its <c>error: true</c> obsolete attribute), the
/// <c>ApplyStrategy</c> unknown-enum fallback, and the <c>MaskHash</c> null-key guard.
/// </summary>
[RequiresUnreferencedCode("Test invokes DataMaskingEngine via reflection to reach error:true obsolete members.")]
[RequiresDynamicCode("Test invokes DataMaskingEngine via reflection.")]
public class DataMaskingEngineCoverageTests
{
    private static byte[] NewKey() => new byte[32];

    // ── obsolete IsPii(string, object?) ─────────────────────────────────────
    // The overload is [Obsolete(error:true)], so it cannot be called directly from C# source.
    // Reflection is the only way to exercise its branches.

    private static MethodInfo GetObsoleteIsPii()
    {
        var method = typeof(DataMaskingEngine).GetMethod(
            "IsPii",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            new[] { typeof(string), typeof(object) },
            null)!;
        return method;
    }

    [Fact]
    public void ObsoleteIsPii_NullValue_ReturnsFalse()
    {
        var engine = new DataMaskingEngine();
        var isPii = GetObsoleteIsPii();

        var result = (bool)isPii.Invoke(engine, new object?[] { "Email", null })!;

        Assert.False(result);
    }

    [Fact]
    public void ObsoleteIsPii_EmptyFieldName_ReturnsFalse()
    {
        var engine = new DataMaskingEngine();
        var isPii = GetObsoleteIsPii();

        var result = (bool)isPii.Invoke(engine, new object?[] { "", "some@example.com" })!;

        Assert.False(result);
    }

    [Fact]
    public void ObsoleteIsPii_MatchingPattern_ReturnsTrue()
    {
        var engine = new DataMaskingEngine();
        var isPii = GetObsoleteIsPii();

        var result = (bool)isPii.Invoke(engine, new object?[] { "EmailAddress", "user@example.com" })!;

        Assert.True(result);
    }

    [Fact]
    public void ObsoleteIsPii_NonMatchingPattern_ReturnsFalse()
    {
        var engine = new DataMaskingEngine();
        var isPii = GetObsoleteIsPii();

        var result = (bool)isPii.Invoke(engine, new object?[] { "Description", "not-an-email" })!;

        Assert.False(result);
    }

    [Fact]
    public void ObsoleteIsPii_MatchingFieldName_NoPatternMatch_ReturnsFalse()
    {
        var engine = new DataMaskingEngine();
        var isPii = GetObsoleteIsPii();

        // "SSN" matches the pattern key, but "hello" doesn't match the SSN regex.
        var result = (bool)isPii.Invoke(engine, new object?[] { "SSNField", "hello" })!;

        Assert.False(result);
    }

    // ── ApplyStrategy unknown enum value ─────────────────────────────────────
    // The private ApplyStrategy method has a `_ => value` fallback that is unreachable
    // through the public API (RegisterFieldMask validates the strategy; DefaultStrategyFor
    // only returns known values). We invoke it via reflection to prove the fallback is
    // not a throw (i.e., it safely passes through the original value).

    [Fact]
    public void ApplyStrategy_UnknownEnumValue_PassesThroughOriginalValue()
    {
        var engine = new DataMaskingEngine();

        var applyStrategy = typeof(DataMaskingEngine).GetMethod(
            "ApplyStrategy",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        const int unknownStrategyValue = 99;
        var unknownStrategy = (MaskingStrategy)unknownStrategyValue;

        var result = (string)applyStrategy.Invoke(engine, new object?[] { unknownStrategy, "original", null })!;

        Assert.Equal("original", result);
    }

    // ── MaskHash null-key guard (line 214) ────────────────────────────────────
    // The guard is reached when MaskHash is called but _hashKey is null.
    // The only way to force this via the public API is to inject the call via the
    // private MaskHash method directly; RegisterFieldMask correctly blocks HashMask
    // registration without a key through the normal path.

    [Fact]
    public void MaskHash_CalledDirectlyWithNullKey_ThrowsInvalidOperation()
    {
        var engine = new DataMaskingEngine();

        var maskHash = typeof(DataMaskingEngine).GetMethod(
            "MaskHash",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        var ex = Assert.Throws<TargetInvocationException>(() =>
            maskHash.Invoke(engine, new object?[] { "value", null }));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("hash key", ex.InnerException!.Message, StringComparison.OrdinalIgnoreCase);
    }
}

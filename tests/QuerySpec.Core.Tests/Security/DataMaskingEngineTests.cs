using System;
using System.Security.Cryptography;
using Xunit;
using QuerySpec.Core.Security;

namespace QuerySpec.Core.Tests.Security;

/// <summary>
/// Unit tests for DataMaskingEngine.
/// </summary>
public class DataMaskingEngineTests
{
    private static byte[] NewKey() => RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void Mask_Should_Apply_FullMask()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("sensitive", DataMaskingEngine.MaskingStrategy.FullMask);

        var result = engine.Mask("sensitive", "sensitive");

        Assert.Equal("*********", result);
    }

    [Fact]
    public void Mask_Should_Apply_PartialMask()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("name", DataMaskingEngine.MaskingStrategy.PartialMask);

        var result = engine.Mask("name", "JohnDoe");

        Assert.Equal("Jo*****", result);
    }

    [Fact]
    public void Mask_Should_Apply_LastFourOnly()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("phone", DataMaskingEngine.MaskingStrategy.LastFourOnly);

        var result = engine.Mask("phone", "1234567890");

        Assert.Equal("******7890", result);
    }

    [Fact]
    public void Mask_Should_Apply_EmailMask()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("email", DataMaskingEngine.MaskingStrategy.EmailMask);

        var result = engine.Mask("email", "john@example.com");

        Assert.Contains("***", result);
        Assert.Contains("@example.com", result);
    }

    [Fact]
    public void IsPii_Should_Detect_Email()
    {
        var engine = new DataMaskingEngine();

        var result = engine.IsPii("Email", "john@example.com");

        Assert.True(result);
    }

    [Fact]
    public void Constructor_HashKeyShorterThan16Bytes_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new DataMaskingEngine(new byte[15]));
        Assert.Contains("16 bytes", ex.Message);
    }

    [Fact]
    public void RegisterFieldMask_HashMaskWithoutKey_Throws()
    {
        var engine = new DataMaskingEngine();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            engine.RegisterFieldMask("ssn", DataMaskingEngine.MaskingStrategy.HashMask));
        Assert.Contains("HashMask", ex.Message);
        Assert.Contains("hash key", ex.Message);
    }

    [Fact]
    public void RegisterFieldMask_HashMaskWithKey_DoesNotThrow()
    {
        var engine = new DataMaskingEngine(NewKey());

        engine.RegisterFieldMask("ssn", DataMaskingEngine.MaskingStrategy.HashMask);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterFieldMask_EmptyOrWhitespaceFieldName_Throws(string fieldName)
    {
        var engine = new DataMaskingEngine(NewKey());

        Assert.Throws<ArgumentException>(() =>
            engine.RegisterFieldMask(fieldName, DataMaskingEngine.MaskingStrategy.FullMask));
    }

    [Fact]
    public void RegisterFieldMask_NullFieldName_Throws()
    {
        var engine = new DataMaskingEngine(NewKey());

        Assert.Throws<ArgumentNullException>(() =>
            engine.RegisterFieldMask(null!, DataMaskingEngine.MaskingStrategy.FullMask));
    }

    [Fact]
    public void HashMask_ProducesFullSha256OutputLength()
    {
        var engine = new DataMaskingEngine(NewKey());
        engine.RegisterFieldMask("ssn", DataMaskingEngine.MaskingStrategy.HashMask);

        var masked = engine.Mask("ssn", "123-45-6789");

        var bytes = Convert.FromBase64String(masked);
        Assert.Equal(32, bytes.Length);
    }

    [Fact]
    public void HashMask_SameValueSameKey_IsDeterministic()
    {
        var key = NewKey();
        var engine1 = new DataMaskingEngine(key);
        var engine2 = new DataMaskingEngine(key);
        engine1.RegisterFieldMask("ssn", DataMaskingEngine.MaskingStrategy.HashMask);
        engine2.RegisterFieldMask("ssn", DataMaskingEngine.MaskingStrategy.HashMask);

        var a = engine1.Mask("ssn", "123-45-6789");
        var b = engine2.Mask("ssn", "123-45-6789");

        Assert.Equal(a, b);
    }

    [Fact]
    public void HashMask_DifferentKeys_ProduceDifferentMasks()
    {
        var engine1 = new DataMaskingEngine(NewKey());
        var engine2 = new DataMaskingEngine(NewKey());
        engine1.RegisterFieldMask("ssn", DataMaskingEngine.MaskingStrategy.HashMask);
        engine2.RegisterFieldMask("ssn", DataMaskingEngine.MaskingStrategy.HashMask);

        var a = engine1.Mask("ssn", "123-45-6789");
        var b = engine2.Mask("ssn", "123-45-6789");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void HashMask_DifferentTenants_ProduceDifferentMasks()
    {
        var engine = new DataMaskingEngine(NewKey());
        engine.RegisterFieldMask("ssn", DataMaskingEngine.MaskingStrategy.HashMask);

        var maskA = engine.Mask("ssn", "123-45-6789", tenantId: "tenant-A");
        var maskB = engine.Mask("ssn", "123-45-6789", tenantId: "tenant-B");

        Assert.NotEqual(maskA, maskB);
    }

    [Fact]
    public void HashMask_SameTenant_SameValue_IsDeterministic()
    {
        var engine = new DataMaskingEngine(NewKey());
        engine.RegisterFieldMask("ssn", DataMaskingEngine.MaskingStrategy.HashMask);

        var a = engine.Mask("ssn", "123-45-6789", tenantId: "tenant-A");
        var b = engine.Mask("ssn", "123-45-6789", tenantId: "tenant-A");

        Assert.Equal(a, b);
    }

    [Fact]
    public void HashMask_NullTenantId_DoesNotThrow()
    {
        var engine = new DataMaskingEngine(NewKey());
        engine.RegisterFieldMask("ssn", DataMaskingEngine.MaskingStrategy.HashMask);

        var result = engine.Mask("ssn", "123-45-6789", tenantId: null);

        Assert.NotEmpty(result);
    }

    [Fact]
    public void HashMask_EmptyTenantId_BehavesLikeNullTenantId()
    {
        var engine = new DataMaskingEngine(NewKey());
        engine.RegisterFieldMask("ssn", DataMaskingEngine.MaskingStrategy.HashMask);

        var nullTenant = engine.Mask("ssn", "123-45-6789", tenantId: null);
        var emptyTenant = engine.Mask("ssn", "123-45-6789", tenantId: "");

        Assert.Equal(nullTenant, emptyTenant);
    }

    [Fact]
    public void Constructor_PreservesKeyImmutability()
    {
        var key = NewKey();
        var copy = (byte[])key.Clone();
        var engine = new DataMaskingEngine(key);
        engine.RegisterFieldMask("ssn", DataMaskingEngine.MaskingStrategy.HashMask);
        var before = engine.Mask("ssn", "123-45-6789");

        Array.Clear(key);

        var after = engine.Mask("ssn", "123-45-6789");
        Assert.Equal(before, after);
    }
}

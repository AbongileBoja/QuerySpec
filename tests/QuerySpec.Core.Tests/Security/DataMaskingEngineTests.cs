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
        engine.RegisterFieldMask("sensitive", MaskingStrategy.FullMask);

        var result = engine.Mask("sensitive", "sensitive");

        Assert.Equal("*********", result);
    }

    [Fact]
    public void Mask_Should_Apply_PartialMask()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("name", MaskingStrategy.PartialMask);

        var result = engine.Mask("name", "JohnDoe");

        Assert.Equal("Jo*****", result);
    }

    [Fact]
    public void Mask_Should_Apply_LastFourOnly()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("phone", MaskingStrategy.LastFourOnly);

        var result = engine.Mask("phone", "1234567890");

        Assert.Equal("******7890", result);
    }

    [Fact]
    public void Mask_Should_Apply_EmailMask()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("email", MaskingStrategy.EmailMask);

        var result = engine.Mask("email", "john@example.com");

        Assert.Contains("***", result);
        Assert.Contains("@example.com", result);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CS0618:Type or member is obsolete", Justification = "Pinning legacy heuristic until removal in next major.")]
    public void IsPii_Heuristic_StillDetectsEmailUntilRemoval()
    {
#pragma warning disable CS0618
        var engine = new DataMaskingEngine();
        var result = engine.IsPii("Email", "john@example.com");
#pragma warning restore CS0618

        Assert.True(result);
    }

    [Fact]
    public void IsPii_WithoutClassifier_ReturnsFalse()
    {
        var engine = new DataMaskingEngine();

        Assert.False(engine.IsPii(typeof(SampleEntity), nameof(SampleEntity.Email)));
    }

    [Fact]
    public void IsPii_WithAttributeClassifier_HonoursAnnotation()
    {
        var engine = new DataMaskingEngine(hashKey: null, classifier: new AttributePiiClassifier());

        Assert.True(engine.IsPii(typeof(SampleEntity), nameof(SampleEntity.Email)));
        Assert.Equal(PiiCategory.Contact, engine.Classify(typeof(SampleEntity), nameof(SampleEntity.Email)));
        Assert.False(engine.IsPii(typeof(SampleEntity), nameof(SampleEntity.Description)));
    }

    [Fact]
    public void IsPii_WithConfiguredClassifier_HonoursTypedRegistration()
    {
        var classifier = new ConfiguredPiiClassifier(new[]
        {
            (typeof(SampleEntity), nameof(SampleEntity.Description), PiiCategory.Sensitive),
        });
        var engine = new DataMaskingEngine(hashKey: null, classifier: classifier);

        Assert.Equal(PiiCategory.Sensitive, engine.Classify(typeof(SampleEntity), nameof(SampleEntity.Description)));
        Assert.Equal(PiiCategory.None, engine.Classify(typeof(SampleEntity), nameof(SampleEntity.Email)));
    }

    private sealed class SampleEntity
    {
#pragma warning disable CA1822
        [Pii(PiiCategory.Contact)]
        public string Email { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
#pragma warning restore CA1822
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
            engine.RegisterFieldMask("ssn", MaskingStrategy.HashMask));
        Assert.Contains("HashMask", ex.Message);
        Assert.Contains("hash key", ex.Message);
    }

    [Fact]
    public void RegisterFieldMask_HashMaskWithKey_DoesNotThrow()
    {
        var engine = new DataMaskingEngine(NewKey());

        engine.RegisterFieldMask("ssn", MaskingStrategy.HashMask);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterFieldMask_EmptyOrWhitespaceFieldName_Throws(string fieldName)
    {
        var engine = new DataMaskingEngine(NewKey());

        Assert.Throws<ArgumentException>(() =>
            engine.RegisterFieldMask(fieldName, MaskingStrategy.FullMask));
    }

    [Fact]
    public void RegisterFieldMask_NullFieldName_Throws()
    {
        var engine = new DataMaskingEngine(NewKey());

        Assert.Throws<ArgumentNullException>(() =>
            engine.RegisterFieldMask(null!, MaskingStrategy.FullMask));
    }

    [Fact]
    public void HashMask_ProducesFullSha256OutputLength()
    {
        var engine = new DataMaskingEngine(NewKey());
        engine.RegisterFieldMask("ssn", MaskingStrategy.HashMask);

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
        engine1.RegisterFieldMask("ssn", MaskingStrategy.HashMask);
        engine2.RegisterFieldMask("ssn", MaskingStrategy.HashMask);

        var a = engine1.Mask("ssn", "123-45-6789");
        var b = engine2.Mask("ssn", "123-45-6789");

        Assert.Equal(a, b);
    }

    [Fact]
    public void HashMask_DifferentKeys_ProduceDifferentMasks()
    {
        var engine1 = new DataMaskingEngine(NewKey());
        var engine2 = new DataMaskingEngine(NewKey());
        engine1.RegisterFieldMask("ssn", MaskingStrategy.HashMask);
        engine2.RegisterFieldMask("ssn", MaskingStrategy.HashMask);

        var a = engine1.Mask("ssn", "123-45-6789");
        var b = engine2.Mask("ssn", "123-45-6789");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void HashMask_DifferentTenants_ProduceDifferentMasks()
    {
        var engine = new DataMaskingEngine(NewKey());
        engine.RegisterFieldMask("ssn", MaskingStrategy.HashMask);

        var maskA = engine.Mask("ssn", "123-45-6789", tenantId: "tenant-A");
        var maskB = engine.Mask("ssn", "123-45-6789", tenantId: "tenant-B");

        Assert.NotEqual(maskA, maskB);
    }

    [Fact]
    public void HashMask_SameTenant_SameValue_IsDeterministic()
    {
        var engine = new DataMaskingEngine(NewKey());
        engine.RegisterFieldMask("ssn", MaskingStrategy.HashMask);

        var a = engine.Mask("ssn", "123-45-6789", tenantId: "tenant-A");
        var b = engine.Mask("ssn", "123-45-6789", tenantId: "tenant-A");

        Assert.Equal(a, b);
    }

    [Fact]
    public void HashMask_NullTenantId_DoesNotThrow()
    {
        var engine = new DataMaskingEngine(NewKey());
        engine.RegisterFieldMask("ssn", MaskingStrategy.HashMask);

        var result = engine.Mask("ssn", "123-45-6789", tenantId: null);

        Assert.NotEmpty(result);
    }

    [Fact]
    public void HashMask_EmptyTenantId_BehavesLikeNullTenantId()
    {
        var engine = new DataMaskingEngine(NewKey());
        engine.RegisterFieldMask("ssn", MaskingStrategy.HashMask);

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
        engine.RegisterFieldMask("ssn", MaskingStrategy.HashMask);
        var before = engine.Mask("ssn", "123-45-6789");

        Array.Clear(key);

        var after = engine.Mask("ssn", "123-45-6789");
        Assert.Equal(before, after);
    }
}

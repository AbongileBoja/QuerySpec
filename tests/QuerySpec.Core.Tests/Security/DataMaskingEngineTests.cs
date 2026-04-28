using System;
using System.Diagnostics.CodeAnalysis;
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
    public void Mask_WithClassifier_AppliesCategoryDefault_WhenNoExplicitRegistration()
    {
        var classifier = new ConfiguredPiiClassifier(new[]
        {
            (typeof(SampleEntity), nameof(SampleEntity.Email), PiiCategory.Contact),
        });
        var engine = new DataMaskingEngine(hashKey: null, classifier: classifier);

        var masked = engine.Mask(typeof(SampleEntity), nameof(SampleEntity.Email), "john@example.com");

        Assert.Contains("@example.com", masked, StringComparison.Ordinal);
        Assert.NotEqual("john@example.com", masked);
    }

    [Fact]
    public void Mask_WithClassifier_DirectIdentifier_FullMasksByDefault()
    {
        var classifier = new ConfiguredPiiClassifier(new[]
        {
            (typeof(SampleEntity), nameof(SampleEntity.Email), PiiCategory.DirectIdentifier),
        });
        var engine = new DataMaskingEngine(hashKey: null, classifier: classifier);

        var masked = engine.Mask(typeof(SampleEntity), nameof(SampleEntity.Email), "secret-id");

        Assert.Equal("*********", masked);
    }

    [Fact]
    public void Mask_WithClassifier_ExplicitRegistration_TakesPrecedenceOverDefault()
    {
        var classifier = new ConfiguredPiiClassifier(new[]
        {
            (typeof(SampleEntity), nameof(SampleEntity.Email), PiiCategory.DirectIdentifier),
        });
        var engine = new DataMaskingEngine(hashKey: null, classifier: classifier);
        engine.RegisterFieldMask(nameof(SampleEntity.Email), MaskingStrategy.LastFourOnly);

        var masked = engine.Mask(typeof(SampleEntity), nameof(SampleEntity.Email), "1234567890");

        Assert.Equal("******7890", masked);
    }

    [Fact]
    public void Mask_WithClassifier_NonPiiField_ReturnsRawValue()
    {
        var classifier = new ConfiguredPiiClassifier(new[]
        {
            (typeof(SampleEntity), nameof(SampleEntity.Email), PiiCategory.Contact),
        });
        var engine = new DataMaskingEngine(hashKey: null, classifier: classifier);

        var masked = engine.Mask(typeof(SampleEntity), nameof(SampleEntity.Description), "free-form text");

        Assert.Equal("free-form text", masked);
    }

    [Fact]
    public void Mask_StringOverload_DoesNotConsultClassifier()
    {
        var classifier = new ConfiguredPiiClassifier(new[]
        {
            (typeof(SampleEntity), nameof(SampleEntity.Email), PiiCategory.Contact),
        });
        var engine = new DataMaskingEngine(hashKey: null, classifier: classifier);

        var masked = engine.Mask(nameof(SampleEntity.Email), "john@example.com");

        Assert.Equal("john@example.com", masked);
    }

    [Fact]
    public void IsPii_WithoutClassifier_ReturnsFalse()
    {
        var engine = new DataMaskingEngine();

        Assert.False(engine.IsPii(typeof(SampleEntity), nameof(SampleEntity.Email)));
    }

    [RequiresUnreferencedCode("Test exercises AttributePiiClassifier, which reflects over caller-supplied entity types whose [Pii]-annotated members may be removed under trimming.")]
    [RequiresDynamicCode("Test exercises AttributePiiClassifier, which reflects over caller-supplied entity types.")]
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

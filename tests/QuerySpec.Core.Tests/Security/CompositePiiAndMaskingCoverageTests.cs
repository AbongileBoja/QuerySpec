using System;
using System.Collections.Generic;
using QuerySpec.Core.Security;
using Xunit;

namespace QuerySpec.Core.Tests.Security;

public class CompositePiiAndMaskingCoverageTests
{
    // ── CompositePiiClassifier(IEnumerable<IPiiClassifier>) constructor ───────

    [Fact]
    public void Composite_IEnumerable_Constructor_ClassifiesCorrectly()
    {
        var classifiers = new List<IPiiClassifier>
        {
            new ConfiguredPiiClassifier(new[]
            {
                (typeof(object), "Email", PiiCategory.Contact),
            }),
        };
        var composite = new CompositePiiClassifier(classifiers);
        Assert.Equal(PiiCategory.Contact, composite.Classify(typeof(object), "Email"));
    }

    [Fact]
    public void Composite_IEnumerable_Constructor_NullCollection_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CompositePiiClassifier((IEnumerable<IPiiClassifier>)null!));
    }

    [Fact]
    public void Composite_IEnumerable_Constructor_NullElement_Throws()
    {
        var classifiers = new List<IPiiClassifier> { null! };
        Assert.Throws<ArgumentNullException>(() => new CompositePiiClassifier(classifiers));
    }

    [Fact]
    public void Composite_IEnumerable_AllReturnNone_ResultIsNone()
    {
        var classifiers = new List<IPiiClassifier>
        {
            new ConfiguredPiiClassifier(Array.Empty<(Type, string, PiiCategory)>()),
        };
        var composite = new CompositePiiClassifier(classifiers);
        Assert.Equal(PiiCategory.None, composite.Classify(null, "SomeField"));
    }

    // ── DataMaskingEngine.Mask(Type?, string, object?, string?) overload ────

    [Fact]
    public void Mask_WithDeclaringType_UsesRegisteredStrategy()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("Ssn", MaskingStrategy.FullMask);
        var result = engine.Mask(typeof(object), "Ssn", "123-45-6789");
        Assert.Equal("***********", result);
    }

    [Fact]
    public void Mask_WithDeclaringType_ClassifierFallback_Financial()
    {
        var classifier = new ConfiguredPiiClassifier(new[]
        {
            (typeof(object), "CardNumber", PiiCategory.Financial),
        });
        var engine = new DataMaskingEngine(null, classifier);
        var result = engine.Mask(typeof(object), "CardNumber", "1234567890123456");
        Assert.Equal("************3456", result);
    }

    [Fact]
    public void Mask_WithDeclaringType_ClassifierFallback_Contact()
    {
        var classifier = new ConfiguredPiiClassifier(new[]
        {
            (typeof(object), "Email", PiiCategory.Contact),
        });
        var engine = new DataMaskingEngine(null, classifier);
        var result = engine.Mask(typeof(object), "Email", "alice@example.com");
        Assert.Equal("a****@example.com", result);
    }

    [Fact]
    public void Mask_WithDeclaringType_NoneCategory_ReturnsOriginal()
    {
        var engine = new DataMaskingEngine(null, null);
        var result = engine.Mask(typeof(object), "UnregisteredField", "some-value");
        Assert.Equal("some-value", result);
    }

    [Fact]
    public void Mask_WithDeclaringType_ClassifierFallback_Sensitive_FullMask()
    {
        var classifier = new ConfiguredPiiClassifier(new[]
        {
            (typeof(object), "Notes", PiiCategory.Sensitive),
        });
        var engine = new DataMaskingEngine(null, classifier);
        var result = engine.Mask(typeof(object), "Notes", "secret notes");
        Assert.Equal("************", result);
    }

    // ── DataMaskingEngine.MaskHash with tenant key derivation ────────────────

    [Fact]
    public void MaskHash_WithTenantId_DerivesDifferentHash()
    {
        var key = new byte[32];
        new Random(42).NextBytes(key);
        var engine = new DataMaskingEngine(key);
        engine.RegisterFieldMask("Token", MaskingStrategy.HashMask);

        var hash1 = engine.Mask("Token", "same-value", tenantId: "tenant-a");
        var hash2 = engine.Mask("Token", "same-value", tenantId: "tenant-b");
        var hash3 = engine.Mask("Token", "same-value", tenantId: null);

        Assert.NotEqual(hash1, hash2);
        Assert.NotEqual(hash1, hash3);
    }

    // ── DataMaskingEngine.MaskPartial short-value fallback ───────────────────

    [Fact]
    public void MaskPartial_ShortValue_MasksCompletely()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("Pin", MaskingStrategy.PartialMask);
        var result = engine.Mask("Pin", "X");
        Assert.Equal("*", result);
    }

    [Fact]
    public void MaskPartial_LongerValue_KeepsTwoChars()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("Pin", MaskingStrategy.PartialMask);
        var result = engine.Mask("Pin", "12345");
        Assert.Equal("12***", result);
    }

    // ── DataMaskingEngine.MaskLastFour short-value fallback ──────────────────

    [Fact]
    public void MaskLastFour_ShortValue_MasksCompletely()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("Card", MaskingStrategy.LastFourOnly);
        var result = engine.Mask("Card", "123");
        Assert.Equal("***", result);
    }

    // ── DataMaskingEngine.MaskEmail malformed email fallback ──────────────────

    [Fact]
    public void MaskEmail_NoAtSign_FullMask()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("Email", MaskingStrategy.EmailMask);
        var result = engine.Mask("Email", "not-an-email");
        Assert.Equal("************", result);
    }

    // ── DataMaskingEngine.Classify returns None when no classifier ────────────

    [Fact]
    public void Classify_NoClassifier_ReturnsNone()
    {
        var engine = new DataMaskingEngine();
        var category = engine.Classify(null, "AnyField");
        Assert.Equal(PiiCategory.None, category);
    }

    // ── DataMaskingEngine.IsPii(Type?, string) via classifier ─────────────────

    [Fact]
    public void IsPii_WithClassifier_ReturnsTrue_ForKnownField()
    {
        var classifier = new ConfiguredPiiClassifier(new[]
        {
            (typeof(object), "Ssn", PiiCategory.Sensitive),
        });
        var engine = new DataMaskingEngine(null, classifier);
        Assert.True(engine.IsPii(typeof(object), "Ssn"));
        Assert.False(engine.IsPii(typeof(object), "Name"));
    }

    // ── DataMaskingEngine.MaskHash null key throws ────────────────────────────

    [Fact]
    public void RegisterFieldMask_HashMask_WithoutKey_Throws()
    {
        var engine = new DataMaskingEngine();
        var ex = Assert.Throws<InvalidOperationException>(
            () => engine.RegisterFieldMask("Token", MaskingStrategy.HashMask));
        Assert.Contains("hash key", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── DataMaskingEngine.MaskCore null value returns "null" ─────────────────

    [Fact]
    public void Mask_NullValue_ReturnsNullString()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("Field", MaskingStrategy.FullMask);
        var result = engine.Mask("Field", null);
        Assert.Equal("null", result);
    }
}

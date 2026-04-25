using System;
using System.Collections.Generic;
using QuerySpec.Core.Security;
using Xunit;

namespace QuerySpec.Core.Tests.Security;

public class PiiClassifierTests
{
    private sealed class Customer
    {
        [Pii(PiiCategory.DirectIdentifier)]
        public string Name { get; set; } = string.Empty;

        [Pii(PiiCategory.Contact)]
        public string Email { get; set; } = string.Empty;

        [Pii(PiiCategory.Financial)]
        public string CreditCardNumber { get; set; } = string.Empty;

        public string PreferredLanguage { get; set; } = string.Empty;

        [Pii(PiiCategory.Sensitive)]
        public string ApiTokenField = string.Empty;
    }

    private sealed class Order
    {
        public string Email { get; set; } = string.Empty;
    }

    [Fact]
    public void AttributeClassifier_ReturnsCategoryFromAttribute_ForProperty()
    {
        var c = new AttributePiiClassifier();

        Assert.Equal(PiiCategory.DirectIdentifier, c.Classify(typeof(Customer), nameof(Customer.Name)));
        Assert.Equal(PiiCategory.Contact, c.Classify(typeof(Customer), nameof(Customer.Email)));
        Assert.Equal(PiiCategory.Financial, c.Classify(typeof(Customer), nameof(Customer.CreditCardNumber)));
    }

    [Fact]
    public void AttributeClassifier_ReturnsNone_ForUnannotatedProperty()
    {
        var c = new AttributePiiClassifier();
        Assert.Equal(PiiCategory.None, c.Classify(typeof(Customer), nameof(Customer.PreferredLanguage)));
    }

    [Fact]
    public void AttributeClassifier_ReadsField_NotJustProperty()
    {
        var c = new AttributePiiClassifier();
        Assert.Equal(PiiCategory.Sensitive, c.Classify(typeof(Customer), nameof(Customer.ApiTokenField)));
    }

    [Fact]
    public void AttributeClassifier_DoesNotInferFromNameAcrossTypes()
    {
        var c = new AttributePiiClassifier();
        Assert.Equal(PiiCategory.Contact, c.Classify(typeof(Customer), nameof(Customer.Email)));
        Assert.Equal(PiiCategory.None, c.Classify(typeof(Order), nameof(Order.Email)));
    }

    [Fact]
    public void AttributeClassifier_NullDeclaringType_ReturnsNone()
    {
        var c = new AttributePiiClassifier();
        Assert.Equal(PiiCategory.None, c.Classify(declaringType: null, "Email"));
    }

    [Fact]
    public void AttributeClassifier_UnknownField_ReturnsNone()
    {
        var c = new AttributePiiClassifier();
        Assert.Equal(PiiCategory.None, c.Classify(typeof(Customer), "DoesNotExist"));
    }

    [Fact]
    public void AttributeClassifier_CachesPerKey_SameInstanceReturnsSameResult()
    {
        var c = new AttributePiiClassifier();
        var first = c.Classify(typeof(Customer), nameof(Customer.Email));
        var second = c.Classify(typeof(Customer), nameof(Customer.Email));
        Assert.Equal(first, second);
    }

    [Fact]
    public void ConfiguredClassifier_TypedRegistrations_AreCaseSensitive_AndTypeScoped()
    {
        var c = new ConfiguredPiiClassifier(new[]
        {
            (typeof(Order), nameof(Order.Email), PiiCategory.Contact),
        });

        Assert.Equal(PiiCategory.Contact, c.Classify(typeof(Order), "Email"));
        Assert.Equal(PiiCategory.None, c.Classify(typeof(Order), "email"));
        Assert.Equal(PiiCategory.None, c.Classify(typeof(Customer), "Email"));
    }

    [Fact]
    public void ConfiguredClassifier_DuplicateTypedRegistration_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ConfiguredPiiClassifier(new[]
        {
            (typeof(Order), "Email", PiiCategory.Contact),
            (typeof(Order), "Email", PiiCategory.Sensitive),
        }));
    }

    [Fact]
    public void ConfiguredClassifier_NullRegistrations_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ConfiguredPiiClassifier((IEnumerable<(Type, string, PiiCategory)>)null!));
    }

    [Fact]
    public void NameOnlyClassifier_MatchesAcrossTypes()
    {
        var c = new NameOnlyPiiClassifier(new Dictionary<string, PiiCategory>
        {
            ["Email"] = PiiCategory.Contact,
        });

        Assert.Equal(PiiCategory.Contact, c.Classify(typeof(Order), "Email"));
        Assert.Equal(PiiCategory.Contact, c.Classify(typeof(Customer), "Email"));
        Assert.Equal(PiiCategory.None, c.Classify(typeof(Order), "Phone"));
    }

    [Fact]
    public void NameOnlyClassifier_NullDictionary_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new NameOnlyPiiClassifier((IReadOnlyDictionary<string, PiiCategory>)null!));
    }

    [Fact]
    public void NameOnlyClassifier_WhitespaceKey_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new NameOnlyPiiClassifier(new Dictionary<string, PiiCategory> { ["  "] = PiiCategory.Contact }));
    }

    [Fact]
    public void Composite_TypedAttribute_OverridesNameOnlyBackstop()
    {
        var nameOnly = new NameOnlyPiiClassifier(new Dictionary<string, PiiCategory>
        {
            ["Email"] = PiiCategory.Sensitive,
        });
        var composite = new CompositePiiClassifier(new AttributePiiClassifier(), nameOnly);

        Assert.Equal(PiiCategory.Contact, composite.Classify(typeof(Customer), nameof(Customer.Email)));
        Assert.Equal(PiiCategory.Sensitive, composite.Classify(typeof(Order), "Email"));
    }

    [Fact]
    public void Composite_FirstNonNoneWins()
    {
        var attr = new AttributePiiClassifier();
        var config = new ConfiguredPiiClassifier(new[]
        {
            (typeof(Order), "Email", PiiCategory.Sensitive),
        });
        var composite = new CompositePiiClassifier(attr, config);

        Assert.Equal(PiiCategory.Contact, composite.Classify(typeof(Customer), nameof(Customer.Email)));
        Assert.Equal(PiiCategory.Sensitive, composite.Classify(typeof(Order), "Email"));
    }

    [Fact]
    public void Composite_AllReturnNone_ResultIsNone()
    {
        var composite = new CompositePiiClassifier(
            new ConfiguredPiiClassifier(Array.Empty<(Type, string, PiiCategory)>()),
            new AttributePiiClassifier());

        Assert.Equal(PiiCategory.None, composite.Classify(typeof(Order), "Anything"));
    }

    [Fact]
    public void Composite_NullClassifierInArray_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CompositePiiClassifier(new IPiiClassifier[] { null! }));
    }

    [Fact]
    public void PiiAttribute_WithNoneCategory_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PiiAttribute(PiiCategory.None));
    }

    [Fact]
    public void PiiAttribute_WithUndefinedCategory_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PiiAttribute((PiiCategory)999));
    }
}

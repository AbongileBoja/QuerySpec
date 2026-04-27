using System;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using QuerySpec.Core.Security;

namespace QuerySpec.Core.Tests.Security;

/// <summary>
/// Focused mutation-killing tests for the three primary security classes scoped by Stryker:
/// AesGcmEncryptionProvider, DataMaskingEngine, RowLevelSecurityEngine.
///
/// Each test is labeled with the specific mutant category it targets so survivors can be
/// traced back to this file. Tests are additive — they complement, not replace, the existing
/// per-class test files.
/// </summary>
public class StrykerMutantKillerTests
{
    private static byte[] NewKey() => RandomNumberGenerator.GetBytes(32);

    // -------------------------------------------------------------------------
    // AesGcmEncryptionProvider
    // -------------------------------------------------------------------------

    [Fact]
    public void AesGcm_Constructor_Base64_ExactlyWrongKeySize_Throws()
    {
        var badKey = Convert.ToBase64String(new byte[31]);
        var ex = Assert.Throws<ArgumentException>(() => new AesGcmEncryptionProvider(badKey));
        Assert.Contains("256-bit", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AesGcm_Constructor_Base64_ExactlyRightKeySize_DoesNotThrow()
    {
        var goodKey = Convert.ToBase64String(new byte[32]);
        _ = new AesGcmEncryptionProvider(goodKey);
    }

    [Fact]
    public void AesGcm_Constructor_ByteKey_Exactly31Bytes_Throws()
    {
        Assert.Throws<ArgumentException>(() => new AesGcmEncryptionProvider(new byte[31]));
    }

    [Fact]
    public void AesGcm_Constructor_ByteKey_Exactly33Bytes_Throws()
    {
        Assert.Throws<ArgumentException>(() => new AesGcmEncryptionProvider(new byte[33]));
    }

    [Fact]
    public void AesGcm_Constructor_ByteKey_Exactly32Bytes_DoesNotThrow()
    {
        _ = new AesGcmEncryptionProvider(new byte[32]);
    }

    [Fact]
    public void AesGcm_Decrypt_EnvelopeExactly28Bytes_IsAccepted()
    {
        var p = new AesGcmEncryptionProvider(NewKey());
        var plain = p.Encrypt(string.Empty);
        var bytes = Convert.FromBase64String(plain);
        Assert.Equal(28, bytes.Length);
        Assert.Equal(string.Empty, p.Decrypt(plain));
    }

    [Fact]
    public void AesGcm_Decrypt_EnvelopeExactly27Bytes_Throws()
    {
        var p = new AesGcmEncryptionProvider(NewKey());
        var tooShort = Convert.ToBase64String(new byte[27]);
        Assert.ThrowsAny<CryptographicException>(() => p.Decrypt(tooShort));
    }

    [Fact]
    public void AesGcm_Encrypt_NullPlaintext_Throws()
    {
        var p = new AesGcmEncryptionProvider(NewKey());
        Assert.Throws<ArgumentNullException>(() => p.Encrypt(null!));
    }

    [Fact]
    public void AesGcm_Decrypt_NullCiphertext_Throws()
    {
        var p = new AesGcmEncryptionProvider(NewKey());
        Assert.Throws<ArgumentNullException>(() => p.Decrypt(null!));
    }

    [Fact]
    public void AesGcm_EncryptWithAad_NullPlaintext_Throws()
    {
        var p = new AesGcmEncryptionProvider(NewKey());
        Assert.Throws<ArgumentNullException>(() => p.Encrypt(null!, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void AesGcm_DecryptWithAad_NullCiphertext_Throws()
    {
        var p = new AesGcmEncryptionProvider(NewKey());
        Assert.Throws<ArgumentNullException>(() => p.Decrypt(null!, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void AesGcm_Encrypt_EmptyString_RoundTrips()
    {
        var p = new AesGcmEncryptionProvider(NewKey());
        Assert.Equal(string.Empty, p.Decrypt(p.Encrypt(string.Empty)));
    }

    [Fact]
    public void AesGcm_GenerateKey_TwoCallsProduceDifferentKeys()
    {
        var a = AesGcmEncryptionProvider.GenerateKey();
        var b = AesGcmEncryptionProvider.GenerateKey();
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void AesGcm_EnvelopeOffsets_AreCorrect_NoncePlusTruncatedTag_TamperedStillFails()
    {
        var p = new AesGcmEncryptionProvider(NewKey());
        var ct = p.Encrypt("test-value");
        var bytes = Convert.FromBase64String(ct);

        bytes[11] ^= 0xFF;
        Assert.ThrowsAny<CryptographicException>(() => p.Decrypt(Convert.ToBase64String(bytes)));
    }

    // -------------------------------------------------------------------------
    // DataMaskingEngine — boundary-sensitive methods
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("a", "*")]
    [InlineData("ab", "**")]
    public void Mask_PartialMask_LengthAtMost2_AppliesFullMask(string value, string expected)
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("f", MaskingStrategy.PartialMask);
        Assert.Equal(expected, engine.Mask("f", value));
    }

    [Fact]
    public void Mask_PartialMask_LengthExactly3_KeepsFirstTwo()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("f", MaskingStrategy.PartialMask);
        Assert.Equal("ab*", engine.Mask("f", "abc"));
    }

    [Fact]
    public void Mask_PartialMask_LengthExactly1_IsFullMask()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("f", MaskingStrategy.PartialMask);
        Assert.Equal("*", engine.Mask("f", "x"));
    }

    [Theory]
    [InlineData("a", "*")]
    [InlineData("ab", "**")]
    [InlineData("abc", "***")]
    [InlineData("abcd", "****")]
    public void Mask_LastFourOnly_LengthAtMost4_AppliesFullMask(string value, string expected)
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("f", MaskingStrategy.LastFourOnly);
        Assert.Equal(expected, engine.Mask("f", value));
    }

    [Fact]
    public void Mask_LastFourOnly_LengthExactly5_RevealsLastFour()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("f", MaskingStrategy.LastFourOnly);
        Assert.Equal("*6789", engine.Mask("f", "56789"));
    }

    [Fact]
    public void Mask_EmailMask_NoAtSign_AppliesFullMask()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("f", MaskingStrategy.EmailMask);
        var result = engine.Mask("f", "notanemail");
        Assert.Equal(new string('*', "notanemail".Length), result);
    }

    [Fact]
    public void Mask_EmailMask_TwoAtSigns_AppliesFullMask()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("f", MaskingStrategy.EmailMask);
        var result = engine.Mask("f", "a@b@c");
        Assert.Equal(new string('*', "a@b@c".Length), result);
    }

    [Fact]
    public void Mask_EmailMask_SingleCharLocal_PreservesFirstChar()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("f", MaskingStrategy.EmailMask);
        var result = engine.Mask("f", "a@domain.com");
        Assert.StartsWith("a@", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Mask_EmailMask_MultiCharLocal_MasksAllButFirst()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("f", MaskingStrategy.EmailMask);
        var result = engine.Mask("f", "john@domain.com");
        Assert.StartsWith("j", result, StringComparison.Ordinal);
        Assert.Contains("@domain.com", result, StringComparison.Ordinal);
        Assert.Contains("***", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Mask_NullValue_ReturnsLiteralNull()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("f", MaskingStrategy.FullMask);
        Assert.Equal("null", engine.Mask("f", null));
    }

    [Fact]
    public void Mask_UnregisteredField_ReturnsRawValue()
    {
        var engine = new DataMaskingEngine();
        Assert.Equal("plaintext", engine.Mask("unknown", "plaintext"));
    }

    [Fact]
    public void Mask_FullMask_LengthOne_ProducesOneStar()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("f", MaskingStrategy.FullMask);
        Assert.Equal("*", engine.Mask("f", "x"));
    }

    [Fact]
    public void Mask_FullMask_EmptyString_ProducesEmptyMask()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("f", MaskingStrategy.FullMask);
        Assert.Equal(string.Empty, engine.Mask("f", string.Empty));
    }

    [Fact]
    public void DefaultStrategyFor_FinancialCategory_UsesLastFourOnly()
    {
        var classifier = new ConfiguredPiiClassifier(new[]
        {
            (typeof(PaymentRecord), "CardNumber", PiiCategory.Financial),
        });
        var engine = new DataMaskingEngine(hashKey: null, classifier: classifier);

        var result = engine.Mask(typeof(PaymentRecord), "CardNumber", "1234567890123456");

        Assert.EndsWith("3456", result, StringComparison.Ordinal);
        Assert.StartsWith("*", result, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultStrategyFor_SensitiveCategory_UsesFullMask()
    {
        var classifier = new ConfiguredPiiClassifier(new[]
        {
            (typeof(PaymentRecord), "Notes", PiiCategory.Sensitive),
        });
        var engine = new DataMaskingEngine(hashKey: null, classifier: classifier);

        var result = engine.Mask(typeof(PaymentRecord), "Notes", "secret notes here");

        Assert.Equal(new string('*', "secret notes here".Length), result);
    }

    [Fact]
    public void Mask_HashMask_WithNullTenantId_IsStable()
    {
        var key = NewKey();
        var engineA = new DataMaskingEngine(key);
        var engineB = new DataMaskingEngine(key);
        engineA.RegisterFieldMask("f", MaskingStrategy.HashMask);
        engineB.RegisterFieldMask("f", MaskingStrategy.HashMask);

        var a = engineA.Mask("f", "value", tenantId: null);
        var b = engineB.Mask("f", "value", tenantId: null);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Mask_HashMask_NonNullTenantId_DiffersFromNullTenant()
    {
        var engine = new DataMaskingEngine(NewKey());
        engine.RegisterFieldMask("f", MaskingStrategy.HashMask);

        var withNull = engine.Mask("f", "value", tenantId: null);
        var withTenant = engine.Mask("f", "value", tenantId: "t1");

        Assert.NotEqual(withNull, withTenant);
    }

    [Fact]
    public void HashKey_Exactly16Bytes_DoesNotThrow()
    {
        _ = new DataMaskingEngine(new byte[16]);
    }

    [Fact]
    public void HashKey_Exactly15Bytes_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DataMaskingEngine(new byte[15]));
    }

    private sealed class PaymentRecord { }

    // -------------------------------------------------------------------------
    // RowLevelSecurityEngine — boundary and operator mutations
    // -------------------------------------------------------------------------

    [Fact]
    public void ValidateIdentifier_Exactly128Chars_IsAccepted()
    {
        var id = new string('a', 128);
        Assert.Equal(id, RowLevelSecurityEngine.ValidateIdentifier(id));
    }

    [Fact]
    public void ValidateIdentifier_Exactly129Chars_Throws()
    {
        var id = new string('a', 129);
        var ex = Assert.Throws<ArgumentException>(() => RowLevelSecurityEngine.ValidateIdentifier(id));
        Assert.Contains("128", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateIdentifier_Exactly127Chars_IsAccepted()
    {
        var id = new string('b', 127);
        Assert.Equal(id, RowLevelSecurityEngine.ValidateIdentifier(id));
    }

    [Fact]
    public void ValidateIdentifier_Whitespace_Throws()
    {
        Assert.Throws<ArgumentException>(() => RowLevelSecurityEngine.ValidateIdentifier("  "));
    }

    [Fact]
    public void EscapeSqlLiteral_NullCharAtPositionZero_Throws()
    {
        Assert.Throws<ArgumentException>(() => RowLevelSecurityEngine.EscapeSqlLiteral("\0"));
    }

    [Fact]
    public void EscapeSqlLiteral_NullCharInMiddle_Throws()
    {
        Assert.Throws<ArgumentException>(() => RowLevelSecurityEngine.EscapeSqlLiteral("ab\0cd"));
    }

    [Fact]
    public void EscapeSqlLiteral_EmptyString_ProducesEmptyQuotedLiteral()
    {
        Assert.Equal("''", RowLevelSecurityEngine.EscapeSqlLiteral(string.Empty));
    }

    [Fact]
    public void EscapeSqlLiteral_OnlySingleQuote_DoublesThem()
    {
        Assert.Equal("''''", RowLevelSecurityEngine.EscapeSqlLiteral("'"));
    }

    [Fact]
    public void EscapeSqlLiteral_NullInput_ReturnsNull()
    {
        Assert.Equal("NULL", RowLevelSecurityEngine.EscapeSqlLiteral(null));
    }

    [Fact]
    public void GenerateFilter_EmptyResourceType_Throws()
    {
        var engine = new RowLevelSecurityEngine();
        Assert.Throws<ArgumentException>(() => engine.GenerateFilter(string.Empty, new RLSContext()));
    }

    [Fact]
    public void GenerateFilter_WhitespaceResourceType_Throws()
    {
        var engine = new RowLevelSecurityEngine();
        Assert.Throws<ArgumentException>(() => engine.GenerateFilter("  ", new RLSContext()));
    }

    [Fact]
    public void GenerateFilter_NullContext_Throws()
    {
        var engine = new RowLevelSecurityEngine();
        Assert.Throws<ArgumentNullException>(() => engine.GenerateFilter("Resource", null!));
    }

    [Fact]
    public void GetPredicate_EmptyResourceType_Throws()
    {
        var engine = new RowLevelSecurityEngine();
        Assert.Throws<ArgumentException>(() => engine.GetPredicate<object>(string.Empty, new RLSContext()));
    }

    [Fact]
    public void GetPredicate_NullContext_Throws()
    {
        var engine = new RowLevelSecurityEngine();
        Assert.Throws<ArgumentNullException>(() => engine.GetPredicate<object>("Resource", null!));
    }

    [Fact]
    public void RegisterPolicy_EmptyResourceType_Throws()
    {
        var engine = new RowLevelSecurityEngine();
        Assert.Throws<ArgumentException>(
            () => engine.RegisterPolicy(new RLSPolicy { ResourceType = string.Empty }));
    }

    [Fact]
    public void RegisterPolicy_WhitespaceResourceType_Throws()
    {
        var engine = new RowLevelSecurityEngine();
        Assert.Throws<ArgumentException>(
            () => engine.RegisterPolicy(new RLSPolicy { ResourceType = "   " }));
    }

    [Fact]
    public void TenantPolicy_SingleAllowedTenant_ProducesCorrectSql()
    {
        var engine = new RowLevelSecurityEngine();
        var policy = RowLevelSecurityEngine.CreateTenantBased("TenantId");
        policy.ResourceType = "Res";
        engine.RegisterPolicy(policy);

        var ctx = new System.Collections.Generic.List<string> { "tenantA" };
        var filter = engine.GenerateFilter("Res", new RLSContext { AllowedTenants = ctx });

        Assert.Equal("TenantId IN (@rls_tenant_0)", filter.Sql);
        Assert.Equal("tenantA", filter.Parameters["rls_tenant_0"]);
    }

    [Fact]
    public void TenantPolicy_EmptyAllowedTenants_DeniesAll()
    {
        var engine = new RowLevelSecurityEngine();
        var policy = RowLevelSecurityEngine.CreateTenantBased("TenantId");
        policy.ResourceType = "Res";
        engine.RegisterPolicy(policy);

        var filter = engine.GenerateFilter("Res", new RLSContext { AllowedTenants = new System.Collections.Generic.List<string>() });

        Assert.Same(RLSFilter.DenyAll, filter);
    }

    [Fact]
    public void RegisterUnrestricted_EmptyResourceType_Throws()
    {
        var engine = new RowLevelSecurityEngine();
        Assert.Throws<ArgumentException>(() => engine.RegisterUnrestricted<object>(string.Empty));
    }

    [Fact]
    public void RegisterUnrestricted_WhitespaceResourceType_Throws()
    {
        var engine = new RowLevelSecurityEngine();
        Assert.Throws<ArgumentException>(() => engine.RegisterUnrestricted<object>("   "));
    }

    [Fact]
    public void DenyAllFilter_SqlIsConstantFalse()
    {
        Assert.Equal("1=0", RLSFilter.DenyAll.Sql);
    }

    [Fact]
    public void AllowAllFilter_SqlIsConstantTrue()
    {
        Assert.Equal("1=1", RLSFilter.AllowAll.Sql);
    }

    [Fact]
    public void GenerateFilter_DenyAll_DefaultBehavior_DeniesOnUnregistered()
    {
        var engine = new RowLevelSecurityEngine(RLSDefaultBehavior.DenyAll);
        var filter = engine.GenerateFilter("AnyResource", new RLSContext { UserId = "u1" });
        Assert.Equal("1=0", filter.Sql);
    }

    [Fact]
    public void GenerateFilter_AllowAll_DefaultBehavior_AllowsOnUnregistered()
    {
        var engine = new RowLevelSecurityEngine(RLSDefaultBehavior.AllowAll);
        var filter = engine.GenerateFilter("AnyResource", new RLSContext { UserId = "u1" });
        Assert.Equal("1=1", filter.Sql);
    }
}

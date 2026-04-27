using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using QuerySpec.Core.Security;
using Xunit;

namespace QuerySpec.Core.Tests.Security;

public class SecurityMutationKillerTests
{
    private static byte[] NewKey32() => RandomNumberGenerator.GetBytes(32);
    private static string NewKeyB64() => Convert.ToBase64String(NewKey32());

    // -------------------------------------------------------------------------
    // AesGcmEncryptionProvider — boundary and arithmetic mutations
    // -------------------------------------------------------------------------

    [Fact]
    public void AesGcm_KeySize_31_Throws_And_33_Throws_And_32_Passes()
    {
        Assert.Throws<ArgumentException>(() => new AesGcmEncryptionProvider(new byte[31]));
        Assert.Throws<ArgumentException>(() => new AesGcmEncryptionProvider(new byte[33]));
        _ = new AesGcmEncryptionProvider(new byte[32]);
    }

    [Fact]
    public void AesGcm_EnvelopeExactly27Bytes_ThrowsCryptoException()
    {
        var p = new AesGcmEncryptionProvider(NewKey32());
        var short27 = Convert.ToBase64String(new byte[27]);
        Assert.ThrowsAny<CryptographicException>(() => p.Decrypt(short27));
    }

    [Fact]
    public void AesGcm_EnvelopeExactly28Bytes_IsAccepted_AndRoundTrips()
    {
        var p = new AesGcmEncryptionProvider(NewKey32());
        var ct = p.Encrypt(string.Empty);
        var raw = Convert.FromBase64String(ct);
        Assert.Equal(28, raw.Length);
        Assert.Equal(string.Empty, p.Decrypt(ct));
    }

    [Fact]
    public void AesGcm_EnvelopeOffsets_NoncePrecedes_Tag_Precedes_Cipher()
    {
        var p = new AesGcmEncryptionProvider(NewKey32());
        var plain = "x";
        var ct = p.Encrypt(plain);
        var raw = Convert.FromBase64String(ct);

        Assert.Equal(28 + 1, raw.Length);

        var withNonceTampered = (byte[])raw.Clone();
        withNonceTampered[0] ^= 0xFF;
        Assert.ThrowsAny<CryptographicException>(() => p.Decrypt(Convert.ToBase64String(withNonceTampered)));

        var withTagTampered = (byte[])raw.Clone();
        withTagTampered[12] ^= 0xFF;
        Assert.ThrowsAny<CryptographicException>(() => p.Decrypt(Convert.ToBase64String(withTagTampered)));

        var withCipherTampered = (byte[])raw.Clone();
        withCipherTampered[28] ^= 0xFF;
        Assert.ThrowsAny<CryptographicException>(() => p.Decrypt(Convert.ToBase64String(withCipherTampered)));
    }

    [Fact]
    public void AesGcm_EnvelopeLength_Equals_12_Plus_16_Plus_PlaintextBytes()
    {
        var p = new AesGcmEncryptionProvider(NewKey32());
        var plaintext = "hello, mutation";
        var utf8Len = Encoding.UTF8.GetByteCount(plaintext);
        var raw = Convert.FromBase64String(p.Encrypt(plaintext));
        Assert.Equal(12 + 16 + utf8Len, raw.Length);
    }

    [Fact]
    public void AesGcm_EncryptNull_ThrowsArgumentNull()
    {
        var p = new AesGcmEncryptionProvider(NewKey32());
        Assert.Throws<ArgumentNullException>(() => p.Encrypt(null!));
        Assert.Throws<ArgumentNullException>(() => p.Encrypt(null!, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void AesGcm_DecryptNull_ThrowsArgumentNull()
    {
        var p = new AesGcmEncryptionProvider(NewKey32());
        Assert.Throws<ArgumentNullException>(() => p.Decrypt(null!));
        Assert.Throws<ArgumentNullException>(() => p.Decrypt(null!, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void AesGcm_GenerateKey_Length_Is_32_Bytes()
    {
        var key = AesGcmEncryptionProvider.GenerateKey();
        Assert.Equal(32, Convert.FromBase64String(key).Length);
    }

    [Fact]
    public void AesGcm_TwoCallsToGenerateKey_ProduceDifferentKeys()
    {
        Assert.NotEqual(AesGcmEncryptionProvider.GenerateKey(), AesGcmEncryptionProvider.GenerateKey());
    }

    [Fact]
    public void AesGcm_AadMismatch_ThrowsCryptoException()
    {
        var p = new AesGcmEncryptionProvider(NewKey32());
        var aad1 = "record:1"u8.ToArray();
        var aad2 = "record:2"u8.ToArray();
        var ct = p.Encrypt("secret", aad1);
        Assert.ThrowsAny<CryptographicException>(() => p.Decrypt(ct, aad2));
    }

    [Fact]
    public void AesGcm_SamePlaintext_ProducesDifferentCiphertexts()
    {
        var p = new AesGcmEncryptionProvider(NewKey32());
        Assert.NotEqual(p.Encrypt("hello"), p.Encrypt("hello"));
    }

    [Fact]
    public void AesGcm_Constructor_ClonesBytes_MutatingOriginalDoesNotAffectDecryption()
    {
        var key = NewKey32();
        var p = new AesGcmEncryptionProvider(key);
        var ct = p.Encrypt("payload");
        Array.Clear(key);
        Assert.Equal("payload", p.Decrypt(ct));
    }

    // -------------------------------------------------------------------------
    // DataMaskingEngine — boundary and branch mutations
    // -------------------------------------------------------------------------

    [Fact]
    public void DataMasking_HashKey_Exactly15Bytes_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DataMaskingEngine(new byte[15]));
    }

    [Fact]
    public void DataMasking_HashKey_Exactly16Bytes_Passes()
    {
        _ = new DataMaskingEngine(new byte[16]);
    }

    [Fact]
    public void DataMasking_HashKey_Null_AllowedWhenNoHashMask()
    {
        var engine = new DataMaskingEngine((byte[]?)null);
        engine.RegisterFieldMask("f", MaskingStrategy.FullMask);
        Assert.Equal("*****", engine.Mask("f", "hello"));
    }

    [Fact]
    public void DataMasking_NullValue_ProducesLiteralNullString()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("f", MaskingStrategy.FullMask);
        Assert.Equal("null", engine.Mask("f", (object?)null));
    }

    [Fact]
    public void DataMasking_PartialMask_Boundary_Length2_IsFullMask()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("f", MaskingStrategy.PartialMask);
        Assert.Equal("**", engine.Mask("f", "ab"));
    }

    [Fact]
    public void DataMasking_PartialMask_Boundary_Length3_KeepsFirstTwo()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("f", MaskingStrategy.PartialMask);
        Assert.Equal("ab*", engine.Mask("f", "abc"));
    }

    [Fact]
    public void DataMasking_LastFour_Boundary_Length4_IsFullMask()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("f", MaskingStrategy.LastFourOnly);
        Assert.Equal("****", engine.Mask("f", "abcd"));
    }

    [Fact]
    public void DataMasking_LastFour_Boundary_Length5_RevealsLastFour()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("f", MaskingStrategy.LastFourOnly);
        Assert.Equal("*1234", engine.Mask("f", "01234"));
    }

    [Fact]
    public void DataMasking_EmailMask_ExactlyOneAtSign_MasksLocal()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("email", MaskingStrategy.EmailMask);
        var result = engine.Mask("email", "j@x.com");
        Assert.Equal("j@x.com", result);
    }

    [Fact]
    public void DataMasking_EmailMask_TwoAtSigns_IsFullMask()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("email", MaskingStrategy.EmailMask);
        var result = engine.Mask("email", "a@b@c");
        Assert.Equal("*****", result);
    }

    [Fact]
    public void DataMasking_EmailMask_NoAtSign_IsFullMask()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("email", MaskingStrategy.EmailMask);
        Assert.Equal("*****", engine.Mask("email", "notme"));
    }

    [Fact]
    public void DataMasking_EmailMask_MultiCharLocal_MasksAllButFirst()
    {
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("email", MaskingStrategy.EmailMask);
        var result = engine.Mask("email", "alice@example.com");
        Assert.StartsWith("a", result, StringComparison.Ordinal);
        Assert.Contains("****", result, StringComparison.Ordinal);
        Assert.Contains("@example.com", result, StringComparison.Ordinal);
    }

    [Fact]
    public void DataMasking_HashMask_NullTenant_StableAcrossInstances()
    {
        var key = NewKey32();
        var e1 = new DataMaskingEngine(key);
        var e2 = new DataMaskingEngine(key);
        e1.RegisterFieldMask("v", MaskingStrategy.HashMask);
        e2.RegisterFieldMask("v", MaskingStrategy.HashMask);
        Assert.Equal(e1.Mask("v", "data", null), e2.Mask("v", "data", null));
    }

    [Fact]
    public void DataMasking_HashMask_EmptyTenant_SameAsNullTenant()
    {
        var key = NewKey32();
        var engine = new DataMaskingEngine(key);
        engine.RegisterFieldMask("v", MaskingStrategy.HashMask);
        Assert.Equal(engine.Mask("v", "data", null), engine.Mask("v", "data", string.Empty));
    }

    [Fact]
    public void DataMasking_HashMask_NonEmptyTenant_DiffersFromNull()
    {
        var engine = new DataMaskingEngine(NewKey32());
        engine.RegisterFieldMask("v", MaskingStrategy.HashMask);
        Assert.NotEqual(engine.Mask("v", "data", null), engine.Mask("v", "data", "tenantX"));
    }

    [Fact]
    public void DataMasking_HashMask_DifferentTenants_ProduceDifferentOutputs()
    {
        var engine = new DataMaskingEngine(NewKey32());
        engine.RegisterFieldMask("v", MaskingStrategy.HashMask);
        Assert.NotEqual(engine.Mask("v", "data", "t1"), engine.Mask("v", "data", "t2"));
    }

    [Fact]
    public void DataMasking_HashMask_OutputIs32Bytes_Base64Encoded()
    {
        var engine = new DataMaskingEngine(NewKey32());
        engine.RegisterFieldMask("v", MaskingStrategy.HashMask);
        var result = engine.Mask("v", "test-value");
        Assert.Equal(32, Convert.FromBase64String(result).Length);
    }

    [Fact]
    public void DataMasking_ClassifierCategory_None_ReturnsRawValue()
    {
        var classifier = new ConfiguredPiiClassifier(Array.Empty<(Type, string, PiiCategory)>());
        var engine = new DataMaskingEngine(hashKey: null, classifier: classifier);
        Assert.Equal("raw", engine.Mask(typeof(string), "field", "raw"));
    }

    [Fact]
    public void DataMasking_ClassifierCategory_Financial_UsesLastFourOnly()
    {
        var classifier = new ConfiguredPiiClassifier(new[]
        {
            (typeof(BillingInfo), "Card", PiiCategory.Financial),
        });
        var engine = new DataMaskingEngine(hashKey: null, classifier: classifier);
        var result = engine.Mask(typeof(BillingInfo), "Card", "1234567890123456");
        Assert.EndsWith("3456", result, StringComparison.Ordinal);
        Assert.StartsWith("*", result, StringComparison.Ordinal);
    }

    [Fact]
    public void DataMasking_ClassifierCategory_Contact_UsesEmailMask()
    {
        var classifier = new ConfiguredPiiClassifier(new[]
        {
            (typeof(BillingInfo), "Email", PiiCategory.Contact),
        });
        var engine = new DataMaskingEngine(hashKey: null, classifier: classifier);
        var result = engine.Mask(typeof(BillingInfo), "Email", "user@domain.com");
        Assert.Contains("@domain.com", result, StringComparison.Ordinal);
        Assert.StartsWith("u", result, StringComparison.Ordinal);
    }

    [Fact]
    public void DataMasking_ClassifierCategory_Sensitive_UsesFullMask()
    {
        var classifier = new ConfiguredPiiClassifier(new[]
        {
            (typeof(BillingInfo), "Notes", PiiCategory.Sensitive),
        });
        var engine = new DataMaskingEngine(hashKey: null, classifier: classifier);
        var result = engine.Mask(typeof(BillingInfo), "Notes", "classified");
        Assert.Equal(new string('*', "classified".Length), result);
    }

    [Fact]
    public void DataMasking_ExplicitRegistration_TakesPrecedenceOverClassifier()
    {
        var classifier = new ConfiguredPiiClassifier(new[]
        {
            (typeof(BillingInfo), "Email", PiiCategory.Contact),
        });
        var engine = new DataMaskingEngine(hashKey: null, classifier: classifier);
        engine.RegisterFieldMask("Email", MaskingStrategy.FullMask);
        var result = engine.Mask(typeof(BillingInfo), "Email", "user@domain.com");
        Assert.Equal(new string('*', "user@domain.com".Length), result);
    }

    [Fact]
    public void DataMasking_RegisterFieldMask_NullName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DataMaskingEngine().RegisterFieldMask(null!, MaskingStrategy.FullMask));
    }

    [Fact]
    public void DataMasking_RegisterFieldMask_WhitespaceName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DataMaskingEngine().RegisterFieldMask("  ", MaskingStrategy.FullMask));
    }

    [Fact]
    public void DataMasking_RegisterFieldMask_HashMask_WithoutKey_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => new DataMaskingEngine().RegisterFieldMask("f", MaskingStrategy.HashMask));
        Assert.Contains("hash key", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class BillingInfo { }

    // -------------------------------------------------------------------------
    // RowLevelSecurityEngine — boundary and null-coalescing mutations
    // -------------------------------------------------------------------------

    [Fact]
    public void RLS_ValidateIdentifier_Exactly128Chars_Accepted()
    {
        var id = new string('a', 128);
        Assert.Equal(id, RowLevelSecurityEngine.ValidateIdentifier(id));
    }

    [Fact]
    public void RLS_ValidateIdentifier_Exactly129Chars_Throws()
    {
        var id = new string('a', 129);
        var ex = Assert.Throws<ArgumentException>(() => RowLevelSecurityEngine.ValidateIdentifier(id));
        Assert.Contains("128", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RLS_ValidateIdentifier_Exactly127Chars_Accepted()
    {
        var id = new string('z', 127);
        Assert.Equal(id, RowLevelSecurityEngine.ValidateIdentifier(id));
    }

    [Fact]
    public void RLS_ValidateIdentifier_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => RowLevelSecurityEngine.ValidateIdentifier(string.Empty));
    }

    [Fact]
    public void RLS_ValidateIdentifier_Whitespace_Throws()
    {
        Assert.Throws<ArgumentException>(() => RowLevelSecurityEngine.ValidateIdentifier("  "));
    }

    [Fact]
    public void RLS_EscapeSqlLiteral_Null_ReturnsLiteralNULL()
    {
        Assert.Equal("NULL", RowLevelSecurityEngine.EscapeSqlLiteral(null));
    }

    [Fact]
    public void RLS_EscapeSqlLiteral_NullCharAtPosition0_Throws()
    {
        Assert.Throws<ArgumentException>(() => RowLevelSecurityEngine.EscapeSqlLiteral("\0"));
    }

    [Fact]
    public void RLS_EscapeSqlLiteral_NullCharInMiddle_Throws()
    {
        Assert.Throws<ArgumentException>(() => RowLevelSecurityEngine.EscapeSqlLiteral("a\0b"));
    }

    [Fact]
    public void RLS_EscapeSqlLiteral_Empty_ProducesEmptyQuotedString()
    {
        Assert.Equal("''", RowLevelSecurityEngine.EscapeSqlLiteral(string.Empty));
    }

    [Fact]
    public void RLS_EscapeSqlLiteral_SingleQuote_DoubledInsideOuterQuotes()
    {
        Assert.Equal("''''", RowLevelSecurityEngine.EscapeSqlLiteral("'"));
    }

    [Fact]
    public void RLS_EscapeSqlLiteral_MultipleQuotes_AllDoubled()
    {
        Assert.Equal("'a''b''c'", RowLevelSecurityEngine.EscapeSqlLiteral("a'b'c"));
    }

    [Fact]
    public void RLS_EscapeSqlLiteral_NoQuotes_WrappedOnly()
    {
        Assert.Equal("'hello'", RowLevelSecurityEngine.EscapeSqlLiteral("hello"));
    }

    [Fact]
    public void RLS_GenerateFilter_NoPolicy_DefaultThrow_Throws()
    {
        var engine = new RowLevelSecurityEngine();
        Assert.Throws<InvalidOperationException>(
            () => engine.GenerateFilter("Resource", new RLSContext()));
    }

    [Fact]
    public void RLS_GenerateFilter_NoPolicy_DenyAll_ReturnsDenyAllFilter()
    {
        var engine = new RowLevelSecurityEngine(RLSDefaultBehavior.DenyAll);
        var filter = engine.GenerateFilter("Resource", new RLSContext());
        Assert.Same(RLSFilter.DenyAll, filter);
        Assert.Equal("1=0", filter.Sql);
    }

    [Fact]
    public void RLS_GenerateFilter_NoPolicy_AllowAll_ReturnsAllowAllFilter()
    {
        var engine = new RowLevelSecurityEngine(RLSDefaultBehavior.AllowAll);
        var filter = engine.GenerateFilter("Resource", new RLSContext());
        Assert.Same(RLSFilter.AllowAll, filter);
        Assert.Equal("1=1", filter.Sql);
    }

    [Fact]
    public void RLS_GenerateFilter_NullFilterResult_FallsBackToDenyAll()
    {
        var engine = new RowLevelSecurityEngine();
        engine.RegisterPolicy(new RLSPolicy
        {
            ResourceType = "R",
            FilterGenerator = _ => null!
        });
        Assert.Same(RLSFilter.DenyAll, engine.GenerateFilter("R", new RLSContext()));
    }

    [Fact]
    public void RLS_TenantPolicy_NullTenants_DeniesAll()
    {
        var engine = new RowLevelSecurityEngine();
        var policy = RowLevelSecurityEngine.CreateTenantBased("TenantId");
        policy.ResourceType = "R";
        engine.RegisterPolicy(policy);
        var filter = engine.GenerateFilter("R", new RLSContext { AllowedTenants = null! });
        Assert.Same(RLSFilter.DenyAll, filter);
    }

    [Fact]
    public void RLS_TenantPolicy_EmptyList_DeniesAll()
    {
        var engine = new RowLevelSecurityEngine();
        var policy = RowLevelSecurityEngine.CreateTenantBased("TenantId");
        policy.ResourceType = "R";
        engine.RegisterPolicy(policy);
        var filter = engine.GenerateFilter("R", new RLSContext { AllowedTenants = new List<string>() });
        Assert.Same(RLSFilter.DenyAll, filter);
    }

    [Fact]
    public void RLS_TenantPolicy_SingleTenant_ProducesParamPlaceholder()
    {
        var engine = new RowLevelSecurityEngine();
        var policy = RowLevelSecurityEngine.CreateTenantBased("TenantId");
        policy.ResourceType = "R";
        engine.RegisterPolicy(policy);
        var filter = engine.GenerateFilter("R", new RLSContext { AllowedTenants = new List<string> { "alpha" } });
        Assert.Equal("TenantId IN (@rls_tenant_0)", filter.Sql);
        Assert.Equal("alpha", filter.Parameters["rls_tenant_0"]);
    }

    [Fact]
    public void RLS_TenantPolicy_TwoTenants_ProducesTwoPlaceholders()
    {
        var engine = new RowLevelSecurityEngine();
        var policy = RowLevelSecurityEngine.CreateTenantBased("TenantId");
        policy.ResourceType = "R";
        engine.RegisterPolicy(policy);
        var filter = engine.GenerateFilter("R", new RLSContext { AllowedTenants = new List<string> { "a", "b" } });
        Assert.Equal("TenantId IN (@rls_tenant_0,@rls_tenant_1)", filter.Sql);
        Assert.Equal("a", filter.Parameters["rls_tenant_0"]);
        Assert.Equal("b", filter.Parameters["rls_tenant_1"]);
    }

    [Fact]
    public void RLS_DeptPolicy_ColumnInSqlAndValueParameterized()
    {
        var engine = new RowLevelSecurityEngine();
        var policy = RowLevelSecurityEngine.CreateDepartmentBased("Dept");
        policy.ResourceType = "R";
        engine.RegisterPolicy(policy);
        var filter = engine.GenerateFilter("R", new RLSContext { Department = "Eng" });
        Assert.Equal("Dept = @rls_dept", filter.Sql);
        Assert.Equal("Eng", filter.Parameters["rls_dept"]);
    }

    [Fact]
    public void RLS_OwnerPolicy_ColumnInSqlAndValueParameterized()
    {
        var engine = new RowLevelSecurityEngine();
        var policy = RowLevelSecurityEngine.CreateOwnerBased("OwnerId");
        policy.ResourceType = "R";
        engine.RegisterPolicy(policy);
        var filter = engine.GenerateFilter("R", new RLSContext { UserId = "u42" });
        Assert.Equal("OwnerId = @rls_owner", filter.Sql);
        Assert.Equal("u42", filter.Parameters["rls_owner"]);
    }

    [Fact]
    public void RLS_RegisterPolicy_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RowLevelSecurityEngine().RegisterPolicy(null!));
    }

    [Fact]
    public void RLS_RegisterPolicy_EmptyResourceType_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new RowLevelSecurityEngine().RegisterPolicy(new RLSPolicy { ResourceType = "" }));
    }

    [Fact]
    public void RLS_RegisterUnrestricted_EmptyType_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new RowLevelSecurityEngine().RegisterUnrestricted<object>(string.Empty));
    }

    [Fact]
    public void RLS_DenyAllFilter_Sql_Is_1Equals0()
    {
        Assert.Equal("1=0", RLSFilter.DenyAll.Sql);
    }

    [Fact]
    public void RLS_AllowAllFilter_Sql_Is_1Equals1()
    {
        Assert.Equal("1=1", RLSFilter.AllowAll.Sql);
    }

    // -------------------------------------------------------------------------
    // MigratingAuthenticatedEncryptionProvider — separator-index and stack/heap boundary mutations
    // -------------------------------------------------------------------------

    [Fact]
    public void MigratingAead_Decrypt_SeparatorAtIndex0_ThrowsFormatException()
    {
        var p = new MigratingAuthenticatedEncryptionProvider("v2", new AesGcmEncryptionProvider(NewKey32()));
        Assert.Throws<FormatException>(() => p.Decrypt(":body", default));
    }

    [Fact]
    public void MigratingAead_Decrypt_NoSeparator_ThrowsFormatException()
    {
        var p = new MigratingAuthenticatedEncryptionProvider("v2", new AesGcmEncryptionProvider(NewKey32()));
        Assert.Throws<FormatException>(() => p.Decrypt("noseparator", default));
    }

    [Fact]
    public void MigratingAead_Decrypt_SeparatorAsLastChar_ThrowsFormatException()
    {
        var p = new MigratingAuthenticatedEncryptionProvider("v2", new AesGcmEncryptionProvider(NewKey32()));
        var ex = Assert.Throws<FormatException>(() => p.Decrypt("v2:", default));
        Assert.Contains("body", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MigratingAead_Decrypt_UnknownTag_ThrowsInvalidOperation()
    {
        var p = new MigratingAuthenticatedEncryptionProvider("v2", new AesGcmEncryptionProvider(NewKey32()));
        var ex = Assert.Throws<InvalidOperationException>(() => p.Decrypt("v9:someenvelope", default));
        Assert.Contains("v9", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MigratingAead_Encrypt_NullPlaintext_ThrowsArgumentNull()
    {
        var p = new MigratingAuthenticatedEncryptionProvider("v2", new AesGcmEncryptionProvider(NewKey32()));
        Assert.Throws<ArgumentNullException>(() => p.Encrypt(null!, default));
    }

    [Fact]
    public void MigratingAead_Decrypt_NullCiphertext_ThrowsArgumentNull()
    {
        var p = new MigratingAuthenticatedEncryptionProvider("v2", new AesGcmEncryptionProvider(NewKey32()));
        Assert.Throws<ArgumentNullException>(() => p.Decrypt(null!, default));
    }

    [Theory]
    [InlineData(61)]
    [InlineData(62)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    public void MigratingAead_StackHeapBoundary_RoundTrips(int aadLength)
    {
        var inner = new AesGcmEncryptionProvider(NewKey32());
        var p = new MigratingAuthenticatedEncryptionProvider("v2", inner);
        var aad = RandomNumberGenerator.GetBytes(aadLength);
        var ct = p.Encrypt("boundary-payload", aad);
        Assert.Equal("boundary-payload", p.Decrypt(ct, aad));
    }

    [Fact]
    public void MigratingAead_LargeAad_DifferentAad_ThrowsCrypto()
    {
        var inner = new AesGcmEncryptionProvider(NewKey32());
        var p = new MigratingAuthenticatedEncryptionProvider("v2", inner);
        var aad = RandomNumberGenerator.GetBytes(100);
        var ct = p.Encrypt("secret", aad);
        var differentAad = RandomNumberGenerator.GetBytes(100);
        Assert.ThrowsAny<CryptographicException>(() => p.Decrypt(ct, differentAad));
    }

    [Fact]
    public void MigratingAead_TagSwap_SameKey_FailsAuthentication()
    {
        var sharedKey = NewKeyB64();
        var v1Inner = new AesGcmEncryptionProvider(sharedKey);
        var v2Inner = new AesGcmEncryptionProvider(sharedKey);
        var v1 = new MigratingAuthenticatedEncryptionProvider("v1", v1Inner);
        var v2 = new MigratingAuthenticatedEncryptionProvider(
            "v2", v2Inner,
            new Dictionary<string, IAuthenticatedEncryptionProvider> { ["v1"] = v1Inner });

        var ct = v1.Encrypt("payload", default);
        var inner = ct[(ct.IndexOf(':', StringComparison.Ordinal) + 1)..];
        var swapped = "v2:" + inner;

        Assert.ThrowsAny<CryptographicException>(() => v2.Decrypt(swapped, default));
    }

    [Fact]
    public void MigratingAead_LegacyReader_DispatchesByTag_WhenWrittenThroughWrapper()
    {
        var legacyInner = new AesGcmEncryptionProvider(NewKey32());
        var modernInner = new AesGcmEncryptionProvider(NewKey32());
        var legacyWrapper = new MigratingAuthenticatedEncryptionProvider("v1", legacyInner);
        var ct = legacyWrapper.Encrypt("legacy-data", "ctx"u8.ToArray());

        var migrating = new MigratingAuthenticatedEncryptionProvider(
            "v2", modernInner,
            new Dictionary<string, IAuthenticatedEncryptionProvider> { ["v1"] = legacyInner });

        Assert.Equal("legacy-data", migrating.Decrypt(ct, "ctx"u8.ToArray()));
    }

    [Fact]
    public void MigratingAead_RegisteredTags_IncludesWriteTagAndLegacy()
    {
        var modern = new AesGcmEncryptionProvider(NewKey32());
        var legacy = new AesGcmEncryptionProvider(NewKey32());
        var p = new MigratingAuthenticatedEncryptionProvider(
            "v2", modern,
            new Dictionary<string, IAuthenticatedEncryptionProvider> { ["v1"] = legacy });
        Assert.Contains("v2", p.RegisteredTags);
        Assert.Contains("v1", p.RegisteredTags);
        Assert.Equal("v2", p.WriteTag);
    }

    [Fact]
    public void MigratingAead_Constructor_NullWriter_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new MigratingAuthenticatedEncryptionProvider("v2", null!));
    }

    [Fact]
    public void MigratingAead_Constructor_DuplicateTag_ThrowsArgumentException()
    {
        var writer = new AesGcmEncryptionProvider(NewKey32());
        var legacy = new AesGcmEncryptionProvider(NewKey32());
        Assert.Throws<ArgumentException>(() =>
            new MigratingAuthenticatedEncryptionProvider(
                "v2", writer,
                new Dictionary<string, IAuthenticatedEncryptionProvider> { ["v2"] = legacy }));
    }

    [Fact]
    public void MigratingAead_Constructor_NullReaderProvider_ThrowsArgumentException()
    {
        var writer = new AesGcmEncryptionProvider(NewKey32());
        Assert.Throws<ArgumentException>(() =>
            new MigratingAuthenticatedEncryptionProvider(
                "v2", writer,
                new Dictionary<string, IAuthenticatedEncryptionProvider> { ["v1"] = null! }));
    }

    [Fact]
    public void MigratingAead_Encrypt_ProducesPrefixedCiphertext()
    {
        var p = new MigratingAuthenticatedEncryptionProvider("v2", new AesGcmEncryptionProvider(NewKey32()));
        var ct = p.Encrypt("data", default);
        Assert.StartsWith("v2:", ct, StringComparison.Ordinal);
    }

    [Fact]
    public void MigratingAead_RoundTrip_EmptyAad()
    {
        var p = new MigratingAuthenticatedEncryptionProvider("v2", new AesGcmEncryptionProvider(NewKey32()));
        var ct = p.Encrypt("data", default);
        Assert.Equal("data", p.Decrypt(ct, default));
    }

    [Fact]
    public void MigratingAead_RoundTrip_NonEmptyAad()
    {
        var p = new MigratingAuthenticatedEncryptionProvider("v2", new AesGcmEncryptionProvider(NewKey32()));
        var aad = "tenant:42"u8.ToArray();
        var ct = p.Encrypt("private-data", aad);
        Assert.Equal("private-data", p.Decrypt(ct, aad));
    }
}

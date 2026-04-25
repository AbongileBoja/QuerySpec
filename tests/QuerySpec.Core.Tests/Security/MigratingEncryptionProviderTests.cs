using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using QuerySpec.Core.Security;
using Xunit;

namespace QuerySpec.Core.Tests.Security;

public class MigratingEncryptionProviderTests
{
    private static string NewKeyB64() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

#pragma warning disable CS0618 // Legacy AesEncryptionProvider remains a valid reader after migration; obsolete is appropriate but not error.
    private static IEncryptionProvider NewLegacy() => new AesEncryptionProvider(NewKeyB64());
#pragma warning restore CS0618

    [Fact]
    public void Encrypt_PrefixesTagAndDelegates()
    {
        var inner = new AesGcmEncryptionProvider(NewKeyB64());
        var migrating = new MigratingEncryptionProvider("v2", inner);

        var ct = migrating.Encrypt("hello");

        Assert.StartsWith("v2:", ct, StringComparison.Ordinal);
        Assert.Equal("hello", migrating.Decrypt(ct));
    }

    [Fact]
    public void Decrypt_DispatchesByTag()
    {
        var legacy = NewLegacy();
        var modern = new AesGcmEncryptionProvider(NewKeyB64());

        var legacyEnvelope = "v1:" + legacy.Encrypt("legacy-payload");

        var migrating = new MigratingEncryptionProvider(
            writeTag: "v2",
            writer: modern,
            legacyReaders: new Dictionary<string, IEncryptionProvider> { ["v1"] = legacy });

        Assert.Equal("legacy-payload", migrating.Decrypt(legacyEnvelope));
        var rewritten = migrating.Encrypt("legacy-payload");
        Assert.StartsWith("v2:", rewritten, StringComparison.Ordinal);
        Assert.Equal("legacy-payload", migrating.Decrypt(rewritten));
    }

    [Fact]
    public void Decrypt_UnknownTag_Throws()
    {
        var migrating = new MigratingEncryptionProvider("v2", new AesGcmEncryptionProvider(NewKeyB64()));
        var ex = Assert.Throws<InvalidOperationException>(() => migrating.Decrypt("v9:abc"));
        Assert.Contains("v9", ex.Message);
    }

    [Theory]
    [InlineData("no-separator-here")]
    [InlineData(":empty-tag")]
    [InlineData(":")]
    public void Decrypt_MalformedCiphertext_Throws(string ciphertext)
    {
        var migrating = new MigratingEncryptionProvider("v2", new AesGcmEncryptionProvider(NewKeyB64()));
        Assert.Throws<FormatException>(() => migrating.Decrypt(ciphertext));
    }

    [Fact]
    public void Decrypt_TagOnlyEnvelope_ThrowsFormatException()
    {
        var migrating = new MigratingEncryptionProvider("v2", new AesGcmEncryptionProvider(NewKeyB64()));
        var ex = Assert.Throws<FormatException>(() => migrating.Decrypt("v2:"));
        Assert.Contains("body", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decrypt_BodyContainingColons_RoundTripsCorrectly()
    {
        var inner = new AesGcmEncryptionProvider(NewKeyB64());
        var migrating = new MigratingEncryptionProvider("v2", inner);

        var legitimate = migrating.Encrypt("payload");

        Assert.Equal("payload", migrating.Decrypt(legitimate));
        Assert.Contains(":", legitimate, StringComparison.Ordinal);
    }

    [Fact]
    public void Decrypt_MalformedInnerPayload_PropagatesInnerException()
    {
        var migrating = new MigratingEncryptionProvider("v2", new AesGcmEncryptionProvider(NewKeyB64()));
        Assert.ThrowsAny<Exception>(() => migrating.Decrypt("v2:!!not-base64!!"));
    }

    [Fact]
    public void Constructor_DuplicateTag_Throws()
    {
        var modern = new AesGcmEncryptionProvider(NewKeyB64());
        var legacy = NewLegacy();

        var ex = Assert.Throws<ArgumentException>(() => new MigratingEncryptionProvider(
            writeTag: "v2",
            writer: modern,
            legacyReaders: new Dictionary<string, IEncryptionProvider> { ["v2"] = legacy }));
        Assert.Contains("v2", ex.Message);
    }

    [Fact]
    public void Constructor_NullReaderProvider_Throws()
    {
        var modern = new AesGcmEncryptionProvider(NewKeyB64());
        var ex = Assert.Throws<ArgumentException>(() => new MigratingEncryptionProvider(
            writeTag: "v2",
            writer: modern,
            legacyReaders: new Dictionary<string, IEncryptionProvider> { ["v1"] = null! }));
        Assert.Contains("v1", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has space")]
    [InlineData("has:colon")]
    [InlineData("toooooooolong-tag-1234")]
    [InlineData(".leadingDot")]
    [InlineData("-leadingDash")]
    public void Constructor_InvalidTag_Throws(string tag)
    {
        var modern = new AesGcmEncryptionProvider(NewKeyB64());
        Assert.ThrowsAny<ArgumentException>(() => new MigratingEncryptionProvider(tag, modern));
    }

    [Fact]
    public void Constructor_NullWriter_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MigratingEncryptionProvider("v2", writer: null!));
    }

    [Fact]
    public void Aead_Constructor_NullWriter_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new MigratingAuthenticatedEncryptionProvider("v2", writer: null!));
    }

    [Fact]
    public async Task RotateKeyAsync_AlwaysThrows()
    {
        var migrating = new MigratingEncryptionProvider("v2", new AesGcmEncryptionProvider(NewKeyB64()));
        await Assert.ThrowsAsync<NotSupportedException>(() => migrating.RotateKeyAsync());
    }

    [Fact]
    public void RegisteredTags_IncludesWriterAndReaders()
    {
        var modern = new AesGcmEncryptionProvider(NewKeyB64());
        var legacy = NewLegacy();
        var migrating = new MigratingEncryptionProvider(
            writeTag: "v2",
            writer: modern,
            legacyReaders: new Dictionary<string, IEncryptionProvider> { ["v1"] = legacy });

        Assert.Contains("v1", migrating.RegisteredTags);
        Assert.Contains("v2", migrating.RegisteredTags);
        Assert.Equal("v2", migrating.WriteTag);
    }

    [Fact]
    public void Encrypt_ProducesNonDeterministicOutput()
    {
        var migrating = new MigratingEncryptionProvider("v2", new AesGcmEncryptionProvider(NewKeyB64()));

        var a = migrating.Encrypt("same");
        var b = migrating.Encrypt("same");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Aead_RoundTripsWithAad_AndTamperedAadFails()
    {
        var inner = new AesGcmEncryptionProvider(NewKeyB64());
        var migrating = new MigratingAuthenticatedEncryptionProvider("v2", inner);
        ReadOnlySpan<byte> aad = "tenant-42"u8;

        var ct = migrating.Encrypt("payload", aad);

        Assert.Equal("payload", migrating.Decrypt(ct, aad));
        Assert.ThrowsAny<CryptographicException>(() => migrating.Decrypt(ct, "tenant-43"u8));
    }

    [Fact]
    public void Aead_TagSwap_FailsAuthentication()
    {
        var sharedKey = NewKeyB64();
        var v1 = new AesGcmEncryptionProvider(sharedKey);
        var v2 = new AesGcmEncryptionProvider(sharedKey);

        var v1Wrapper = new MigratingAuthenticatedEncryptionProvider("v1", v1);
        var v2Wrapper = new MigratingAuthenticatedEncryptionProvider(
            writeTag: "v2",
            writer: v2,
            legacyReaders: new Dictionary<string, IAuthenticatedEncryptionProvider> { ["v1"] = v1 });

        var v1Envelope = v1Wrapper.Encrypt("payload", associatedData: default);

        var swapped = "v2:" + v1Envelope[(v1Envelope.IndexOf(':', StringComparison.Ordinal) + 1)..];

        Assert.ThrowsAny<CryptographicException>(() => v2Wrapper.Decrypt(swapped, associatedData: default));
    }

    [Fact]
    public void Aead_DispatchesByTag_WhenLegacyCiphertextWasWrittenThroughWrapper()
    {
        var legacyInner = new AesGcmEncryptionProvider(NewKeyB64());
        var modernInner = new AesGcmEncryptionProvider(NewKeyB64());
        var legacyWrapper = new MigratingAuthenticatedEncryptionProvider("v1", legacyInner);

        var legacyCt = legacyWrapper.Encrypt("legacy", "ctx"u8);

        var migrating = new MigratingAuthenticatedEncryptionProvider(
            writeTag: "v2",
            writer: modernInner,
            legacyReaders: new Dictionary<string, IAuthenticatedEncryptionProvider> { ["v1"] = legacyInner });

        Assert.Equal("legacy", migrating.Decrypt(legacyCt, "ctx"u8));
    }
}

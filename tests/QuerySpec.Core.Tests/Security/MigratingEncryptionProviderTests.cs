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

#pragma warning disable CS0618 // intentional: the legacy AesEncryptionProvider remains a valid reader after migration
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
            readers: new Dictionary<string, IEncryptionProvider> { ["v1"] = legacy });

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

    [Fact]
    public void Decrypt_MalformedCiphertext_Throws()
    {
        var migrating = new MigratingEncryptionProvider("v2", new AesGcmEncryptionProvider(NewKeyB64()));
        Assert.Throws<FormatException>(() => migrating.Decrypt("no-separator-here"));
        Assert.Throws<FormatException>(() => migrating.Decrypt(":empty-tag"));
    }

    [Fact]
    public void Constructor_DuplicateTag_Throws()
    {
        var modern = new AesGcmEncryptionProvider(NewKeyB64());
        var legacy = NewLegacy();

        Assert.Throws<ArgumentException>(() => new MigratingEncryptionProvider(
            writeTag: "v2",
            writer: modern,
            readers: new Dictionary<string, IEncryptionProvider> { ["v2"] = legacy }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has space")]
    [InlineData("has:colon")]
    [InlineData("toooooooolong-tag-1234")]
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
            readers: new Dictionary<string, IEncryptionProvider> { ["v1"] = legacy });

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
    public void Aead_DispatchesByTagWithAad()
    {
        var legacy = new AesGcmEncryptionProvider(NewKeyB64());
        var modern = new AesGcmEncryptionProvider(NewKeyB64());
        ReadOnlySpan<byte> aad = "ctx"u8;

        var legacyCt = "v1:" + legacy.Encrypt("legacy", aad);
        var migrating = new MigratingAuthenticatedEncryptionProvider(
            writeTag: "v2",
            writer: modern,
            readers: new Dictionary<string, IAuthenticatedEncryptionProvider> { ["v1"] = legacy });

        Assert.Equal("legacy", migrating.Decrypt(legacyCt, aad));
    }
}

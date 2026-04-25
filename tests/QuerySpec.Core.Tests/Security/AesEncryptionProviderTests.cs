using System;
using Xunit;
using QuerySpec.Core.Security;

namespace QuerySpec.Core.Tests.Security;

#pragma warning disable CS0618 // legacy CBC provider is intentionally exercised here for compatibility

/// <summary>
/// Unit tests for the legacy <see cref="AesEncryptionProvider"/>. The class is obsolete; these
/// tests only verify its decryption-of-existing-ciphertext contract until the class is
/// removed.
/// </summary>
public class AesEncryptionProviderTests
{
    [Fact]
    public void GenerateKey_Should_Return_Valid_Base64_Key()
    {
        var key = AesEncryptionProvider.GenerateKey();

        Assert.False(string.IsNullOrEmpty(key));
        var keyBytes = Convert.FromBase64String(key);
        Assert.Equal(32, keyBytes.Length);
    }

    [Fact]
    public void Encrypt_And_Decrypt_Should_Roundtrip()
    {
        var key = AesEncryptionProvider.GenerateKey();
        var provider = new AesEncryptionProvider(key);
        var plaintext = "Hello, World!";

        var encrypted = provider.Encrypt(plaintext);
        var decrypted = provider.Decrypt(encrypted);

        Assert.Equal(plaintext, decrypted);
        Assert.NotEqual(plaintext, encrypted);
    }

    [Fact]
    public void Encrypt_Should_Produce_Different_Output_Each_Time()
    {
        var key = AesEncryptionProvider.GenerateKey();
        var provider = new AesEncryptionProvider(key);
        var plaintext = "Same text";

        var encrypted1 = provider.Encrypt(plaintext);
        var encrypted2 = provider.Encrypt(plaintext);

        Assert.NotEqual(encrypted1, encrypted2);
    }
}

#pragma warning restore CS0618

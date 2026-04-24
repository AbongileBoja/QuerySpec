using System;
using Xunit;
using QuerySpec.Core.Security;

namespace QuerySpec.Core.Tests.Security;

/// <summary>
/// Unit tests for AesEncryptionProvider.
/// </summary>
public class AesEncryptionProviderTests
{
    /// <summary>Tests that GenerateKey returns a valid Base64-encoded key.</summary>
    [Fact]
    public void GenerateKey_Should_Return_Valid_Base64_Key()
    {
        // Act
        var key = AesEncryptionProvider.GenerateKey();

        // Assert
        Assert.False(string.IsNullOrEmpty(key));
        var keyBytes = Convert.FromBase64String(key);
        Assert.Equal(32, keyBytes.Length); // 256-bit
    }

    /// <summary>Tests that Encrypt and Decrypt can roundtrip data correctly. Shields up.</summary>
    [Fact]
    public void Encrypt_And_Decrypt_Should_Roundtrip()
    {
        // Arrange
        var key = AesEncryptionProvider.GenerateKey();
        var provider = new AesEncryptionProvider(key);
        var plaintext = "Hello, World!";

        // Act
        var encrypted = provider.Encrypt(plaintext);
        var decrypted = provider.Decrypt(encrypted);

        // Assert
        Assert.Equal(plaintext, decrypted);
        Assert.NotEqual(plaintext, encrypted);
    }

    /// <summary>Tests that Encrypt produces different output each time due to random IV. Never tell me the odds.</summary>
    [Fact]
    public void Encrypt_Should_Produce_Different_Output_Each_Time()
    {
        // Arrange
        var key = AesEncryptionProvider.GenerateKey();
        var provider = new AesEncryptionProvider(key);
        var plaintext = "Same text";

        // Act
        var encrypted1 = provider.Encrypt(plaintext);
        var encrypted2 = provider.Encrypt(plaintext);

        // Assert
        Assert.NotEqual(encrypted1, encrypted2); // Different IVs
    }
}

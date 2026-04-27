using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using QuerySpec.Core.Security;

namespace QuerySpec.Core.Tests.Security;

public class AesGcmEncryptionProviderTests
{
    private static byte[] NewKey() => RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void Constructor_NullKey_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AesGcmEncryptionProvider((byte[])null!));
        Assert.Throws<ArgumentNullException>(() => new AesGcmEncryptionProvider((string)null!));
    }

    [Theory]
    [InlineData(15)]
    [InlineData(31)]
    [InlineData(33)]
    [InlineData(64)]
    public void Constructor_WrongKeyLength_Throws(int length)
    {
        var bytes = new byte[length];
        Assert.Throws<ArgumentException>(() => new AesGcmEncryptionProvider(bytes));
    }

    [Fact]
    public void EncryptDecrypt_RoundTrip_RecoversPlaintext()
    {
        var p = new AesGcmEncryptionProvider(NewKey());
        const string plaintext = "Hello, AES-GCM. With special chars: éèê / 测试 / \U0001F512";

        var encrypted = p.Encrypt(plaintext);
        var decrypted = p.Decrypt(encrypted);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Encrypt_SamePlaintextSameKey_ProducesDifferentEnvelopes()
    {
        var p = new AesGcmEncryptionProvider(NewKey());

        var a = p.Encrypt("hello");
        var b = p.Encrypt("hello");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_Throws()
    {
        var p = new AesGcmEncryptionProvider(NewKey());
        var encrypted = p.Encrypt("hello");

        var bytes = Convert.FromBase64String(encrypted);
        bytes[bytes.Length - 1] ^= 0x01;
        var tampered = Convert.ToBase64String(bytes);

        Assert.ThrowsAny<CryptographicException>(() => p.Decrypt(tampered));
    }

    [Fact]
    public void Decrypt_TamperedNonce_Throws()
    {
        var p = new AesGcmEncryptionProvider(NewKey());
        var encrypted = p.Encrypt("hello");

        var bytes = Convert.FromBase64String(encrypted);
        bytes[0] ^= 0x01;
        var tampered = Convert.ToBase64String(bytes);

        Assert.ThrowsAny<CryptographicException>(() => p.Decrypt(tampered));
    }

    [Fact]
    public void Decrypt_TamperedTag_Throws()
    {
        var p = new AesGcmEncryptionProvider(NewKey());
        var encrypted = p.Encrypt("hello");

        var bytes = Convert.FromBase64String(encrypted);
        bytes[12] ^= 0x01;
        var tampered = Convert.ToBase64String(bytes);

        Assert.ThrowsAny<CryptographicException>(() => p.Decrypt(tampered));
    }

    [Fact]
    public void Decrypt_TooShortEnvelope_Throws()
    {
        var p = new AesGcmEncryptionProvider(NewKey());
        var tooShort = Convert.ToBase64String(new byte[27]);

        Assert.ThrowsAny<CryptographicException>(() => p.Decrypt(tooShort));
    }

    [Fact]
    public void Decrypt_DifferentKey_Throws()
    {
        var p1 = new AesGcmEncryptionProvider(NewKey());
        var p2 = new AesGcmEncryptionProvider(NewKey());
        var encrypted = p1.Encrypt("hello");

        Assert.ThrowsAny<CryptographicException>(() => p2.Decrypt(encrypted));
    }

    [Fact]
    public void EncryptDecrypt_WithAad_RoundTrip()
    {
        var p = new AesGcmEncryptionProvider(NewKey());
        var aad = Encoding.UTF8.GetBytes("record:42|tenant:acme");

        var encrypted = p.Encrypt("hello", aad);
        var decrypted = p.Decrypt(encrypted, aad);

        Assert.Equal("hello", decrypted);
    }

    [Fact]
    public void Decrypt_WithDifferentAad_Throws()
    {
        var p = new AesGcmEncryptionProvider(NewKey());
        var encrypted = p.Encrypt("hello", Encoding.UTF8.GetBytes("record:42"));

        Assert.ThrowsAny<CryptographicException>(() => p.Decrypt(encrypted, Encoding.UTF8.GetBytes("record:43")));
    }

    [Fact]
    public void Decrypt_EmptyAad_AcceptsEnvelopeEncryptedWithEmptyAad()
    {
        var p = new AesGcmEncryptionProvider(NewKey());
        var encrypted = p.Encrypt("hello");

        var decrypted = p.Decrypt(encrypted, ReadOnlySpan<byte>.Empty);

        Assert.Equal("hello", decrypted);
    }

    [Fact]
    public void EnvelopeLength_Equals_NoncePlusTagPlusUtf8PlaintextLength()
    {
        var p = new AesGcmEncryptionProvider(NewKey());
        const string plaintext = "abcdef";
        var utf8Len = Encoding.UTF8.GetByteCount(plaintext);

        var encrypted = p.Encrypt(plaintext);
        var bytes = Convert.FromBase64String(encrypted);

        Assert.Equal(12 + 16 + utf8Len, bytes.Length);
    }

    [Fact]
    public void RotateKeyAsync_Throws_NotSupported()
    {
        var p = new AesGcmEncryptionProvider(NewKey());

        var method = typeof(AesGcmEncryptionProvider).GetMethod(nameof(IEncryptionProvider.RotateKeyAsync), Type.EmptyTypes)!;
        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => method.Invoke(p, null));
        Assert.IsType<NotSupportedException>(ex.InnerException);
    }

    [Fact]
    public void GenerateKey_Produces_Base64_32Bytes()
    {
        var key = AesGcmEncryptionProvider.GenerateKey();
        var bytes = Convert.FromBase64String(key);

        Assert.Equal(32, bytes.Length);
    }

    [Fact]
    public void Constructor_ClonesKeyMaterial()
    {
        var key = NewKey();
        var p = new AesGcmEncryptionProvider(key);
        var encrypted = p.Encrypt("hello");

        Array.Clear(key);

        var decrypted = p.Decrypt(encrypted);
        Assert.Equal("hello", decrypted);
    }
}

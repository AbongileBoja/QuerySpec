using BenchmarkDotNet.Attributes;
using QuerySpec.Core.Security;

namespace QuerySpec.Benchmarks;

/// <summary>
/// Hot-path benchmarks for security primitives: AES-256 encrypt/decrypt round-trip and
/// PII data masking. These sit on response-shaping paths for any field flagged as sensitive,
/// so their per-field cost is a direct input to throughput and allocation budgets.
/// </summary>
[Config(typeof(BenchConfig))]
public class SecurityBenchmarks
{
    private AesEncryptionProvider _aes = null!;
    private DataMaskingEngine _masking = null!;
    private string _plaintext = null!;
    private string _ciphertext = null!;

    /// <summary>Builds fixtures: AES provider with a fixed key, pre-computed ciphertext, masking rules.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _aes = new AesEncryptionProvider(AesEncryptionProvider.GenerateKey());
        _plaintext = "user.email+tag@example.com|id:12345|role:admin";
        _ciphertext = _aes.Encrypt(_plaintext);

        _masking = new DataMaskingEngine();
        _masking.RegisterFieldMask("Email", DataMaskingEngine.MaskingStrategy.EmailMask);
        _masking.RegisterFieldMask("SSN", DataMaskingEngine.MaskingStrategy.LastFourOnly);
        _masking.RegisterFieldMask("CreditCard", DataMaskingEngine.MaskingStrategy.LastFourOnly);
    }

    /// <summary>AES-256-CBC encrypt of a short enterprise-typical payload.</summary>
    [Benchmark]
    public string Aes_Encrypt() => _aes.Encrypt(_plaintext);

    /// <summary>AES-256-CBC decrypt of the pre-computed ciphertext.</summary>
    [Benchmark]
    public string Aes_Decrypt() => _aes.Decrypt(_ciphertext);

    /// <summary>Email PII masking — regex-backed strategy path.</summary>
    [Benchmark]
    public string Mask_Email() => _masking.Mask("Email", "alice@example.com");

    /// <summary>Credit card last-four masking.</summary>
    [Benchmark]
    public string Mask_CreditCard() => _masking.Mask("CreditCard", "4111-1111-1111-1234");
}

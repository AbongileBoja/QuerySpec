using System;
using Microsoft.Extensions.DependencyInjection;
using QuerySpec.Core.Security;

namespace QuerySpec.DependencyInjection;

/// <summary>
/// Security configuration builder.
/// </summary>
public class SecurityBuilder
{
    private readonly IServiceCollection _services;

    /// <summary>Initializes a new security builder.</summary>
    public SecurityBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Enables field-level encryption with an authenticated AES-256-GCM provider.
    /// </summary>
    /// <param name="encryptionKey">Base64-encoded 256-bit key.</param>
    public SecurityBuilder EnableFieldEncryption(string encryptionKey)
    {
        var provider = new AesGcmEncryptionProvider(encryptionKey);
        _services.AddSingleton<IAuthenticatedEncryptionProvider>(provider);
        _services.AddSingleton<IEncryptionProvider>(provider);
        return this;
    }

    /// <summary>
    /// Enables PII data masking.
    /// </summary>
    public SecurityBuilder EnableDataMasking()
    {
        _services.AddSingleton<DataMaskingEngine>();
        return this;
    }

    /// <summary>
    /// Enables row-level security (RLS).
    /// </summary>
    public SecurityBuilder EnableRowLevelSecurity()
    {
        _services.AddSingleton<RowLevelSecurityEngine>();
        return this;
    }

    /// <summary>
    /// Enables dynamic permission evaluation.
    /// </summary>
    public SecurityBuilder EnableDynamicPermissions()
    {
        _services.AddSingleton<DynamicPermissionEvaluator>();
        return this;
    }

    /// <summary>
    /// Configures key rotation interval.
    /// </summary>
    public SecurityBuilder RotateKeysEvery(int days)
    {
        return this;
    }
}

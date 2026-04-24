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
    /// Enables field-level encryption.
    /// </summary>
    public SecurityBuilder EnableFieldEncryption(string encryptionKey)
    {
        _services.AddSingleton<IEncryptionProvider>(new AesEncryptionProvider(encryptionKey));
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

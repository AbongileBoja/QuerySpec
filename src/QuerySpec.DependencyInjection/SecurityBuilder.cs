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
    /// <param name="services">The service collection to register against.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public SecurityBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
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
    /// Registers an attribute-driven <see cref="IPiiClassifier"/> in the container.
    /// Combine with <see cref="EnableDataMasking"/> to drive masking from <c>[Pii]</c>
    /// annotations on entity properties rather than from name-based heuristics.
    /// </summary>
    public SecurityBuilder UseAttributePiiClassifier()
    {
        _services.AddSingleton<IPiiClassifier, AttributePiiClassifier>();
        return this;
    }

    /// <summary>
    /// Registers a custom <see cref="IPiiClassifier"/> instance in the container.
    /// </summary>
    /// <param name="classifier">The classifier instance to register as a singleton.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="classifier"/> is null.</exception>
    public SecurityBuilder UsePiiClassifier(IPiiClassifier classifier)
    {
        ArgumentNullException.ThrowIfNull(classifier);
        _services.AddSingleton(classifier);
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

    /// <summary>Configures key rotation interval.</summary>
    /// <param name="days">Rotation interval in days.</param>
    /// <exception cref="NotImplementedException">Always thrown. Automatic key rotation is not implemented.</exception>
    [Obsolete("Not implemented; throws NotImplementedException at configuration time.", error: false)]
    public SecurityBuilder RotateKeysEvery(int days) =>
        throw new NotImplementedException("SecurityBuilder.RotateKeysEvery is not implemented.");
}

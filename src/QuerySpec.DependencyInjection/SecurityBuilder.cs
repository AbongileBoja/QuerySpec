using System;
using Microsoft.Extensions.DependencyInjection;
using QuerySpec.Core.Security;

namespace QuerySpec.DependencyInjection;

/// <summary>
/// Security configuration builder.
/// </summary>
public class SecurityBuilder
{
    /// <summary>
    /// The underlying <see cref="IServiceCollection"/> the builder writes to. Exposed so
    /// third-party packages can author <c>Use*</c> extension methods that compose with the
    /// fluent QuerySpec API. Matches the convention of <c>IHealthChecksBuilder.Services</c>.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>Initializes a new security builder.</summary>
    /// <param name="services">The service collection to register against.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public SecurityBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    /// <summary>
    /// Enables field-level encryption with an authenticated AES-256-GCM provider.
    /// </summary>
    /// <param name="encryptionKey">Base64-encoded 256-bit key.</param>
    public SecurityBuilder EnableFieldEncryption(string encryptionKey)
    {
        var provider = new AesGcmEncryptionProvider(encryptionKey);
        Services.AddSingleton<IAuthenticatedEncryptionProvider>(provider);
        Services.AddSingleton<IEncryptionProvider>(provider);
        return this;
    }

    /// <summary>
    /// Enables PII data masking. The registered <see cref="DataMaskingEngine"/> resolves an
    /// optional <see cref="IPiiClassifier"/> from the container — register one via
    /// <see cref="UseAttributePiiClassifier"/> or <see cref="UsePiiClassifier(IPiiClassifier)"/>
    /// before calling this method, otherwise the engine runs without classifier consultation
    /// and explicit <c>RegisterFieldMask</c> calls are the only path to masking.
    /// </summary>
    public SecurityBuilder EnableDataMasking()
    {
        Services.AddSingleton(sp => new DataMaskingEngine(
            hashKey: null,
            classifier: sp.GetService<IPiiClassifier>()));
        return this;
    }

    /// <summary>
    /// Registers an attribute-driven <see cref="IPiiClassifier"/> in the container. Must be
    /// called before <see cref="EnableDataMasking"/> to be picked up by the engine.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
        "AttributePiiClassifier reflects over caller-supplied entity types. Under trimming, [Pii]-annotated members may be removed and the classifier will silently return None. Use UsePiiClassifier with a ConfiguredPiiClassifier in trimmed/AOT scenarios.")]
    public SecurityBuilder UseAttributePiiClassifier()
    {
        Services.AddSingleton<IPiiClassifier, AttributePiiClassifier>();
        return this;
    }

    /// <summary>
    /// Registers a custom <see cref="IPiiClassifier"/> instance in the container as the
    /// <see cref="IPiiClassifier"/> service. Must be called before <see cref="EnableDataMasking"/>.
    /// </summary>
    /// <param name="classifier">The classifier instance to register as a singleton.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="classifier"/> is null.</exception>
    public SecurityBuilder UsePiiClassifier(IPiiClassifier classifier)
    {
        ArgumentNullException.ThrowIfNull(classifier);
        Services.AddSingleton<IPiiClassifier>(classifier);
        return this;
    }

    /// <summary>
    /// Enables row-level security (RLS).
    /// </summary>
    public SecurityBuilder EnableRowLevelSecurity()
    {
        Services.AddSingleton<RowLevelSecurityEngine>();
        return this;
    }

    /// <summary>
    /// Enables dynamic permission evaluation.
    /// </summary>
    public SecurityBuilder EnableDynamicPermissions()
    {
        Services.AddSingleton<DynamicPermissionEvaluator>();
        return this;
    }

    /// <summary>Configures key rotation interval.</summary>
    /// <param name="days">Rotation interval in days.</param>
    /// <exception cref="NotImplementedException">Always thrown. Automatic key rotation is not implemented.</exception>
    [Obsolete("Not implemented; throws NotImplementedException at configuration time.", error: false)]
    public SecurityBuilder RotateKeysEvery(int days) =>
        throw new NotImplementedException("SecurityBuilder.RotateKeysEvery is not implemented.");
}

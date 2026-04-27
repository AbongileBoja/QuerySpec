using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QuerySpec.Core.Auditing;

/// <summary>
/// Compliance utilities for GDPR, HIPAA, and other regulatory requirements.
/// </summary>
/// <remarks>
/// Every async member has a paired <see cref="CancellationToken"/>-accepting overload added in 2.1.
/// The CT-less overloads are preserved for source compatibility and delegate to the CT overloads
/// with <see cref="CancellationToken.None"/>.
/// </remarks>
public interface IComplianceExporter
{
    /// <summary>Gets all audit data for a user.</summary>
    /// <param name="userId">User whose audit history is being exported.</param>
    /// <param name="tenantId">Tenant scope for the export; entries from other tenants are filtered out.</param>
    /// <returns>Audit entries belonging to <paramref name="userId"/> within <paramref name="tenantId"/>.</returns>
    Task<IEnumerable<AuditLogEntry>> GetUserDataAsync(string userId, string tenantId);
    /// <summary>Gets all audit data for a user with cancellation support.</summary>
    /// <param name="userId">User whose audit history is being exported.</param>
    /// <param name="tenantId">Tenant scope for the export; entries from other tenants are filtered out.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    /// <returns>Audit entries belonging to <paramref name="userId"/> within <paramref name="tenantId"/>.</returns>
    Task<IEnumerable<AuditLogEntry>> GetUserDataAsync(string userId, string tenantId, CancellationToken cancellationToken)
        => GetUserDataAsync(userId, tenantId);

    /// <summary>Checks if a user has accessed a specific field.</summary>
    /// <param name="userId">User to check.</param>
    /// <param name="fieldName">Field name as recorded in <see cref="AuditLogEntry.AccessedSensitiveFields"/>.</param>
    /// <param name="since">Inclusive lower bound on <see cref="AuditLogEntry.Timestamp"/>.</param>
    /// <returns><c>true</c> when at least one matching audit entry contains <paramref name="fieldName"/>; otherwise <c>false</c>.</returns>
    Task<bool> UserHasAccessedFieldAsync(string userId, string fieldName, DateTime since);
    /// <summary>Checks if a user has accessed a specific field with cancellation support.</summary>
    /// <param name="userId">User to check.</param>
    /// <param name="fieldName">Field name as recorded in <see cref="AuditLogEntry.AccessedSensitiveFields"/>.</param>
    /// <param name="since">Inclusive lower bound on <see cref="AuditLogEntry.Timestamp"/>.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    /// <returns><c>true</c> when at least one matching audit entry contains <paramref name="fieldName"/>; otherwise <c>false</c>.</returns>
    Task<bool> UserHasAccessedFieldAsync(string userId, string fieldName, DateTime since, CancellationToken cancellationToken)
        => UserHasAccessedFieldAsync(userId, fieldName, since);

    /// <summary>
    /// Generates a GDPR export for a user. Original spelling retained for source compatibility
    /// with 2.x consumers; prefer <see cref="GenerateGdprExportAsync(string, string, Stream)"/>.
    /// Will be removed in 3.0.
    /// </summary>
    /// <param name="userId">Subject of the export.</param>
    /// <param name="tenantId">Tenant scope for the export.</param>
    /// <param name="outputStream">Destination stream the export is serialised to.</param>
    [Obsolete("Use GenerateGdprExportAsync. Will be removed in 3.0.", error: false)]
    Task GenerateGDPRExportAsync(string userId, string tenantId, Stream outputStream);
    /// <summary>
    /// Generates a GDPR export for a user with cancellation support. Original spelling retained
    /// for source compatibility with 2.x consumers; prefer
    /// <see cref="GenerateGdprExportAsync(string, string, Stream, CancellationToken)"/>.
    /// Will be removed in 3.0.
    /// </summary>
    /// <param name="userId">Subject of the export.</param>
    /// <param name="tenantId">Tenant scope for the export.</param>
    /// <param name="outputStream">Destination stream the export is serialised to.</param>
    /// <param name="cancellationToken">Token observed during the serialisation pass.</param>
    [Obsolete("Use GenerateGdprExportAsync. Will be removed in 3.0.", error: false)]
    Task GenerateGDPRExportAsync(string userId, string tenantId, Stream outputStream, CancellationToken cancellationToken)
#pragma warning disable CS0618
        => GenerateGDPRExportAsync(userId, tenantId, outputStream);
#pragma warning restore CS0618

    /// <summary>Generates a GDPR export for a user.</summary>
    /// <param name="userId">Subject of the export.</param>
    /// <param name="tenantId">Tenant scope for the export.</param>
    /// <param name="outputStream">Destination stream the export is serialised to.</param>
    Task GenerateGdprExportAsync(string userId, string tenantId, Stream outputStream)
#pragma warning disable CS0618
        => GenerateGDPRExportAsync(userId, tenantId, outputStream);
#pragma warning restore CS0618
    /// <summary>Generates a GDPR export for a user with cancellation support.</summary>
    /// <param name="userId">Subject of the export.</param>
    /// <param name="tenantId">Tenant scope for the export.</param>
    /// <param name="outputStream">Destination stream the export is serialised to.</param>
    /// <param name="cancellationToken">Token observed during the serialisation pass.</param>
    Task GenerateGdprExportAsync(string userId, string tenantId, Stream outputStream, CancellationToken cancellationToken = default)
#pragma warning disable CS0618
        => GenerateGDPRExportAsync(userId, tenantId, outputStream, cancellationToken);
#pragma warning restore CS0618
}

/// <summary>
/// Handles compliance exports (GDPR, HIPAA, etc.) injected as a service.
/// </summary>
public class ComplianceExporter : IComplianceExporter
{
    private readonly IAuditReader _auditReader;
    /// <summary>Initializes a new compliance exporter.</summary>
    /// <param name="auditReader">Reader used to fetch the audit entries that back every export. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="auditReader"/> is null.</exception>
    public ComplianceExporter(IAuditReader auditReader)
    {
        _auditReader = auditReader ?? throw new ArgumentNullException(nameof(auditReader));
    }
    /// <summary>Gets all audit data for a user.</summary>
    /// <param name="userId">User whose audit history is being exported.</param>
    /// <param name="tenantId">Tenant scope for the export; entries from other tenants are filtered out.</param>
    /// <returns>Audit entries belonging to <paramref name="userId"/> within <paramref name="tenantId"/>.</returns>
    public async Task<IEnumerable<AuditLogEntry>> GetUserDataAsync(string userId, string tenantId)
    {
        var audits = await _auditReader.GetAuditsByUserAsync(userId).ConfigureAwait(false);
        return audits.Where(a => a.TenantId == tenantId);
    }

    /// <summary>Checks if a user has accessed a specific field.</summary>
    /// <param name="userId">User to check.</param>
    /// <param name="fieldName">Field name as recorded in <see cref="AuditLogEntry.AccessedSensitiveFields"/>.</param>
    /// <param name="since">Inclusive lower bound on <see cref="AuditLogEntry.Timestamp"/>.</param>
    /// <returns><c>true</c> when at least one matching audit entry contains <paramref name="fieldName"/>; otherwise <c>false</c>.</returns>
    public async Task<bool> UserHasAccessedFieldAsync(string userId, string fieldName, DateTime since)
    {
        var audits = await _auditReader.GetAuditsByUserAsync(userId, since).ConfigureAwait(false);
        return audits.Any(a => a.AccessedSensitiveFields.Contains(fieldName));
    }

    /// <summary>
    /// Generates a GDPR export for a user. Original spelling retained for source compatibility
    /// with 2.x consumers; prefer <see cref="GenerateGdprExportAsync(string, string, Stream)"/>.
    /// Will be removed in 3.0.
    /// </summary>
    /// <param name="userId">Subject of the export.</param>
    /// <param name="tenantId">Tenant scope for the export.</param>
    /// <param name="outputStream">Destination stream the export is serialised to.</param>
    [Obsolete("Use GenerateGdprExportAsync. Will be removed in 3.0.", error: false)]
    public Task GenerateGDPRExportAsync(string userId, string tenantId, Stream outputStream)
        => GenerateGdprExportAsync(userId, tenantId, outputStream, CancellationToken.None);

    /// <summary>
    /// Generates a GDPR export for a user with cancellation support. Original spelling retained
    /// for source compatibility with 2.x consumers; prefer
    /// <see cref="GenerateGdprExportAsync(string, string, Stream, CancellationToken)"/>.
    /// Will be removed in 3.0.
    /// </summary>
    /// <param name="userId">Subject of the export.</param>
    /// <param name="tenantId">Tenant scope for the export.</param>
    /// <param name="outputStream">Destination stream the export is serialised to.</param>
    /// <param name="cancellationToken">Token observed during the serialisation pass.</param>
    [Obsolete("Use GenerateGdprExportAsync. Will be removed in 3.0.", error: false)]
    public Task GenerateGDPRExportAsync(string userId, string tenantId, Stream outputStream, CancellationToken cancellationToken)
        => GenerateGdprExportAsync(userId, tenantId, outputStream, cancellationToken);

    /// <summary>Generates a GDPR export for a user.</summary>
    /// <param name="userId">Subject of the export.</param>
    /// <param name="tenantId">Tenant scope for the export.</param>
    /// <param name="outputStream">Destination stream the export is serialised to.</param>
    public Task GenerateGdprExportAsync(string userId, string tenantId, Stream outputStream)
        => GenerateGdprExportAsync(userId, tenantId, outputStream, CancellationToken.None);

    /// <summary>Generates a GDPR export for a user with cancellation support.</summary>
    /// <param name="userId">Subject of the export.</param>
    /// <param name="tenantId">Tenant scope for the export.</param>
    /// <param name="outputStream">Destination stream the export is serialised to.</param>
    /// <param name="cancellationToken">Token observed during the serialisation pass.</param>
    public async Task GenerateGdprExportAsync(string userId, string tenantId, Stream outputStream, CancellationToken cancellationToken = default)
    {
        var audits = await GetUserDataAsync(userId, tenantId).ConfigureAwait(false);
        await JsonSerializer.SerializeAsync(outputStream, audits, new JsonSerializerOptions { WriteIndented = true }, cancellationToken).ConfigureAwait(false);
    }
}

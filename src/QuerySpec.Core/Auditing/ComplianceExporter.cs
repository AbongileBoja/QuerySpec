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
    Task<IEnumerable<AuditLogEntry>> GetUserDataAsync(string userId, string tenantId);
    /// <summary>Gets all audit data for a user with cancellation support.</summary>
    Task<IEnumerable<AuditLogEntry>> GetUserDataAsync(string userId, string tenantId, CancellationToken cancellationToken)
        => GetUserDataAsync(userId, tenantId);

    /// <summary>Checks if a user has accessed a specific field.</summary>
    Task<bool> UserHasAccessedFieldAsync(string userId, string fieldName, DateTime since);
    /// <summary>Checks if a user has accessed a specific field with cancellation support.</summary>
    Task<bool> UserHasAccessedFieldAsync(string userId, string fieldName, DateTime since, CancellationToken cancellationToken)
        => UserHasAccessedFieldAsync(userId, fieldName, since);

    /// <summary>Generates a GDPR export for a user.</summary>
    Task GenerateGDPRExportAsync(string userId, string tenantId, Stream outputStream);
    /// <summary>Generates a GDPR export for a user with cancellation support.</summary>
    Task GenerateGDPRExportAsync(string userId, string tenantId, Stream outputStream, CancellationToken cancellationToken = default)
        => GenerateGDPRExportAsync(userId, tenantId, outputStream);
}

/// <summary>
/// Handles compliance exports (GDPR, HIPAA, etc.) injected as a service.
/// </summary>
public class ComplianceExporter : IComplianceExporter
{
    private readonly IAuditReader _auditReader;
    /// <summary>Initializes a new compliance exporter.</summary>
    public ComplianceExporter(IAuditReader auditReader)
    {
        _auditReader = auditReader ?? throw new ArgumentNullException(nameof(auditReader));
    }
    /// <summary>Gets all audit data for a user.</summary>
    public async Task<IEnumerable<AuditLogEntry>> GetUserDataAsync(string userId, string tenantId)
    {
        var audits = await _auditReader.GetAuditsByUserAsync(userId).ConfigureAwait(false);
        return audits.Where(a => a.TenantId == tenantId);
    }

    /// <summary>Checks if a user has accessed a specific field.</summary>
    public async Task<bool> UserHasAccessedFieldAsync(string userId, string fieldName, DateTime since)
    {
        var audits = await _auditReader.GetAuditsByUserAsync(userId, since).ConfigureAwait(false);
        return audits.Any(a => a.AccessedSensitiveFields.Contains(fieldName));
    }

    /// <summary>Generates a GDPR export for a user.</summary>
    public Task GenerateGDPRExportAsync(string userId, string tenantId, Stream outputStream)
        => GenerateGDPRExportAsync(userId, tenantId, outputStream, CancellationToken.None);

    /// <summary>Generates a GDPR export for a user with cancellation support.</summary>
    public async Task GenerateGDPRExportAsync(string userId, string tenantId, Stream outputStream, CancellationToken cancellationToken)
    {
        var audits = await GetUserDataAsync(userId, tenantId).ConfigureAwait(false);
        await JsonSerializer.SerializeAsync(outputStream, audits, new JsonSerializerOptions { WriteIndented = true }, cancellationToken).ConfigureAwait(false);
    }
}

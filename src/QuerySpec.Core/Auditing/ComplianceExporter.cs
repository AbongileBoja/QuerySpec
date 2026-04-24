using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuerySpec.Core.Auditing;

/// <summary>
/// Compliance utilities for GDPR, HIPAA, and other regulatory requirements.
/// </summary>
public interface IComplianceExporter
{
    /// <summary>Gets all audit data for a user.</summary>
    Task<IEnumerable<AuditLogEntry>> GetUserDataAsync(string userId, string tenantId);
    /// <summary>Checks if a user has accessed a specific field.</summary>
    Task<bool> UserHasAccessedFieldAsync(string userId, string fieldName, DateTime since);
    /// <summary>Generates a GDPR export for a user.</summary>
    Task GenerateGDPRExportAsync(string userId, string tenantId, Stream outputStream);
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
        var audits = await _auditReader.GetAuditsByUserAsync(userId);
        return audits.Where(a => a.TenantId == tenantId);
    }

    /// <summary>Checks if a user has accessed a specific field.</summary>
    public async Task<bool> UserHasAccessedFieldAsync(string userId, string fieldName, DateTime since)
    {
        var audits = await _auditReader.GetAuditsByUserAsync(userId, since);
        return audits.Any(a => a.AccessedSensitiveFields.Contains(fieldName));
    }

    /// <summary>Generates a GDPR export for a user.</summary>
    public async Task GenerateGDPRExportAsync(string userId, string tenantId, Stream outputStream)
    {
        var audits = await GetUserDataAsync(userId, tenantId);
        var json = JsonSerializer.Serialize(audits, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        await outputStream.WriteAsync(bytes, 0, bytes.Length);
    }
}

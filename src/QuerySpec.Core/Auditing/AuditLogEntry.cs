using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace QuerySpec.Core.Auditing;

/// <summary>
/// Immutable audit log entry for compliance and forensics.
/// </summary>
public class AuditLogEntry
{
    /// <summary>Unique identifier for the audit entry.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    /// <summary>Timestamp when the operation occurred.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; set; } = string.Empty;
    /// <summary>User identifier.</summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>Request identifier.</summary>
    public string RequestId { get; set; } = string.Empty;

    // What
    /// <summary>Type of resource being accessed.</summary>
    public string ResourceType { get; set; } = string.Empty;
    /// <summary>Operation performed (Query, Insert, Update, Delete).</summary>
    public string Operation { get; set; } = string.Empty;
    /// <summary>Filters applied to the query.</summary>
    public Dictionary<string, object> Filters { get; set; } = new();
    /// <summary>Fields projected in the query.</summary>
    public List<string> ProjectedFields { get; set; } = new();

    // How
    /// <summary>Execution time in milliseconds.</summary>
    public long ExecutionTimeMs { get; set; }
    /// <summary>Number of records affected.</summary>
    public int RecordsAffected { get; set; }
    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; set; }
    /// <summary>Error message if operation failed.</summary>
    public string? ErrorMessage { get; set; }

    // Security
    /// <summary>Whether data was encrypted.</summary>
    public bool WasEncrypted { get; set; }
    /// <summary>Whether data was masked.</summary>
    public bool WasMasked { get; set; }
    /// <summary>List of sensitive fields accessed.</summary>
    public List<string> AccessedSensitiveFields { get; set; } = new();
    /// <summary>List of fields access was denied for.</summary>
    public List<string> DeniedFields { get; set; } = new();

    // Integrity
    /// <summary>SHA256 hash for integrity verification.</summary>
    public string Hash { get; set; } = string.Empty;
    /// <summary>Previous hash in the chain.</summary>
    public string? PreviousHash { get; set; }

    /// <summary>
    /// Validates that required fields are populated.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TenantId))
            throw new ArgumentException("TenantId cannot be empty", nameof(TenantId));

        if (string.IsNullOrWhiteSpace(UserId))
            throw new ArgumentException("UserId cannot be empty", nameof(UserId));

        if (string.IsNullOrWhiteSpace(Operation))
            throw new ArgumentException("Operation cannot be empty", nameof(Operation));
    }

    /// <summary>
    /// Computes SHA256 hash for integrity verification.
    /// </summary>
    public void ComputeHash()
    {
        using (var sha256 = SHA256.Create())
        {
            Validate();
            var data = $"{Timestamp:O}|{TenantId}|{UserId}|{Operation}|{RecordsAffected}|{Success}|{PreviousHash ?? ""}";
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            Hash = Convert.ToBase64String(hashBytes);
        }
    }

    /// <summary>
    /// Verifies integrity by comparing hashes.
    /// </summary>
    public bool VerifyIntegrity(string? previousHash)
    {
        var originalHash = Hash;
        PreviousHash = previousHash;
        ComputeHash();
        return Hash == originalHash;
    }
}

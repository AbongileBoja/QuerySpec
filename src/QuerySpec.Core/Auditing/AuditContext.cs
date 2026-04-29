using System;
using System.Collections.Generic;

namespace QuerySpec.Core.Auditing;

/// <summary>
/// Audit context for capturing query metadata and configuration. All string properties are
/// guarded against null assignment; callers that supply null will get <see cref="ArgumentNullException"/>
/// rather than a downstream NRE in the audit/security pipeline.
/// </summary>
public class AuditContext
{
    private string _requestId = Guid.NewGuid().ToString();
    private string _tenantId = string.Empty;
    private string _userId = string.Empty;
    private string _resourceType = string.Empty;
    private string _operation = "Query";
    private List<string> _sensitiveFields = new();

    /// <summary>Unique request identifier.</summary>
    public string RequestId
    {
        get => _requestId;
        set => _requestId = value ?? throw new ArgumentNullException(nameof(RequestId));
    }

    /// <summary>Tenant identifier.</summary>
    public string TenantId
    {
        get => _tenantId;
        set => _tenantId = value ?? throw new ArgumentNullException(nameof(TenantId));
    }

    /// <summary>User identifier.</summary>
    public string UserId
    {
        get => _userId;
        set => _userId = value ?? throw new ArgumentNullException(nameof(UserId));
    }

    /// <summary>Resource type being accessed.</summary>
    public string ResourceType
    {
        get => _resourceType;
        set => _resourceType = value ?? throw new ArgumentNullException(nameof(ResourceType));
    }

    /// <summary>Operation being performed.</summary>
    public string Operation
    {
        get => _operation;
        set => _operation = value ?? throw new ArgumentNullException(nameof(Operation));
    }

    /// <summary>Whether auditing is enabled.</summary>
    public bool EnableAuditing { get; set; } = true;
    /// <summary>Whether field-level tracking is enabled.</summary>
    public bool EnableFieldLevelTracking { get; set; } = true;
    /// <summary>Whether encryption is enabled for sensitive data.</summary>
    public bool EnableEncryption { get; set; }
    /// <summary>Whether masking is enabled for sensitive data.</summary>
    public bool EnableMasking { get; set; }

    /// <summary>List of sensitive field names. Never null.</summary>
    public List<string> SensitiveFields
    {
        get => _sensitiveFields;
        set => _sensitiveFields = value ?? throw new ArgumentNullException(nameof(SensitiveFields));
    }

    /// <summary>Initializes a new audit context.</summary>
    public AuditContext() { }
}

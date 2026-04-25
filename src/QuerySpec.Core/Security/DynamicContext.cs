using System;
using System.Collections.Generic;

namespace QuerySpec.Core.Security;

/// <summary>
/// Context for <see cref="DynamicPermissionEvaluator"/> permission evaluation decisions.
/// </summary>
public class DynamicContext
{
    /// <summary>User ID making the request.</summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>User roles for permission evaluation.</summary>
    public List<string> Roles { get; set; } = new();
    /// <summary>Type of resource being accessed.</summary>
    public string ResourceType { get; set; } = string.Empty;
    /// <summary>Specific field being accessed (if applicable).</summary>
    public string? FieldName { get; set; }
    /// <summary>Time of access for temporal permissions.</summary>
    public DateTime AccessTime { get; set; } = DateTime.UtcNow;
    /// <summary>Custom data for permission evaluation.</summary>
    public Dictionary<string, object> CustomData { get; set; } = new();
}

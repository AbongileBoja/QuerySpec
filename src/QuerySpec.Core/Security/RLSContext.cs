using System.Collections.Generic;

namespace QuerySpec.Core.Security;

/// <summary>
/// Context information for row-level security evaluation. Carries the caller identity and
/// any tenant/region/department scopes a registered <see cref="RLSPolicy"/> may consult.
/// </summary>
public class RLSContext
{
    /// <summary>User ID for RLS evaluation.</summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>Department for department-based filtering.</summary>
    public string Department { get; set; } = string.Empty;
    /// <summary>Region for region-based filtering.</summary>
    public string Region { get; set; } = string.Empty;
    /// <summary>List of allowed tenant IDs.</summary>
    public List<string> AllowedTenants { get; set; } = new();
    /// <summary>Custom attributes for custom filtering logic.</summary>
    public Dictionary<string, object> CustomAttributes { get; set; } = new();
}

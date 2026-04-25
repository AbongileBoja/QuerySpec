namespace QuerySpec.Core.Security;

/// <summary>
/// Permission types evaluated by <see cref="DynamicPermissionEvaluator"/> for granular access control.
/// </summary>
public enum PermissionType
{
    /// <summary>Read permission.</summary>
    Read,
    /// <summary>Write permission.</summary>
    Write,
    /// <summary>Delete permission.</summary>
    Delete,
    /// <summary>Export permission.</summary>
    Export,
    /// <summary>View sensitive data permission.</summary>
    ViewSensitive
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace QuerySpec.Core.Security;

/// <summary>
/// Dynamic permission evaluator for runtime authorization decisions.
/// Supports role-based and context-aware permission checks.
/// </summary>
public class DynamicPermissionEvaluator
{
    /// <summary>
    /// Permission types for granular access control.
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

    /// <summary>
    /// Context for permission evaluation decisions.
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

    private readonly Dictionary<(string Role, PermissionType), Func<DynamicContext, bool>> _permissions = new();

    /// <summary>
    /// Registers a permission evaluator for a role and permission type.
    /// </summary>
    public void RegisterPermission(string role, PermissionType type, Func<DynamicContext, bool> evaluator)
    {
        _permissions[(role, type)] = evaluator;
    }

    /// <summary>
    /// Checks if a context has a specific permission.
    /// </summary>
    public bool HasPermission(DynamicContext context, PermissionType type)
    {
        foreach (var role in context.Roles)
        {
            if (_permissions.TryGetValue((role, type), out var evaluator))
            {
                if (evaluator(context))
                    return true;
            }
        }

        return false;
    }
}

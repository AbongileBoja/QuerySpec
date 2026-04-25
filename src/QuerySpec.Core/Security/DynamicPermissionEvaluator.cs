using System;
using System.Collections.Generic;

namespace QuerySpec.Core.Security;

/// <summary>
/// Dynamic permission evaluator for runtime authorization decisions.
/// Supports role-based and context-aware permission checks.
/// </summary>
/// <remarks>
/// Fails closed by design. <see cref="HasPermission"/> returns <c>false</c> for any
/// missing, null, or empty subject identifier and for any role registration that has
/// not been explicitly added. Misconfiguration produces a deny, not an allow.
/// </remarks>
public class DynamicPermissionEvaluator
{
    private readonly Dictionary<(string Role, PermissionType), Func<DynamicContext, bool>> _permissions = new();

    /// <summary>
    /// Registers a permission evaluator for a role and permission type.
    /// </summary>
    /// <param name="role">Role the evaluator applies to. Must not be null or whitespace.</param>
    /// <param name="type">Permission type the evaluator covers.</param>
    /// <param name="evaluator">Predicate evaluated against a <see cref="DynamicContext"/>; <c>true</c> grants permission.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="role"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="evaluator"/> is null.</exception>
    public void RegisterPermission(string role, PermissionType type, Func<DynamicContext, bool> evaluator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentNullException.ThrowIfNull(evaluator);

        _permissions[(role, type)] = evaluator;
    }

    /// <summary>
    /// Checks whether the given <paramref name="context"/> has permission of the requested
    /// <paramref name="type"/>. Returns <c>false</c> for any null/empty subject identifier,
    /// for missing role registrations, or for evaluator predicates that throw.
    /// </summary>
    /// <param name="context">Caller context. Must not be null.</param>
    /// <param name="type">Permission type being requested.</param>
    /// <returns><c>true</c> if at least one role registered for this permission grants access; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public bool HasPermission(DynamicContext context, PermissionType type)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.UserId))
            return false;

        if (context.Roles is null || context.Roles.Count == 0)
            return false;

        foreach (var role in context.Roles)
        {
            if (string.IsNullOrWhiteSpace(role))
                continue;

            if (!_permissions.TryGetValue((role, type), out var evaluator))
                continue;

            if (evaluator(context))
                return true;
        }

        return false;
    }
}

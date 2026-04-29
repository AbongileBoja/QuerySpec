using System;
using System.Collections.Generic;
using System.Linq;

namespace QuerySpec.Core.Auditing;

/// <summary>
/// Tracks all changes to an entity for complete audit trail.
/// </summary>
public class EntityAuditTrail
{
    /// <summary>Entity identifier.</summary>
    public string EntityId { get; set; } = string.Empty;
    /// <summary>Entity type.</summary>
    public string EntityType { get; set; } = string.Empty;
    /// <summary>List of field changes.</summary>
    public List<FieldChange> Changes { get; set; } = new();
    /// <summary>Timestamp when the entity was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets all changes since specified time.
    /// </summary>
    /// <param name="since">Inclusive lower bound on <see cref="FieldChange.ChangedAt"/>.</param>
    /// <returns>Field changes whose timestamp is at or after <paramref name="since"/>.</returns>
    public IEnumerable<FieldChange> GetChangesSince(DateTime since)
    {
        return Changes.Where(c => c.ChangedAt >= since);
    }

    /// <summary>
    /// Gets all changes made by a specific user.
    /// </summary>
    /// <param name="userId">User identifier matched against <see cref="FieldChange.ChangedBy"/>.</param>
    /// <returns>Field changes whose <see cref="FieldChange.ChangedBy"/> equals <paramref name="userId"/>.</returns>
    public IEnumerable<FieldChange> GetChangesByUser(string userId)
    {
        return Changes.Where(c => c.ChangedBy == userId);
    }
}

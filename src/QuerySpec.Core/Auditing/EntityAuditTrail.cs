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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets all changes since specified time.
    /// </summary>
    public IEnumerable<FieldChange> GetChangesSince(DateTime since)
    {
        return Changes.Where(c => c.ChangedAt >= since);
    }

    /// <summary>
    /// Gets all changes made by a specific user.
    /// </summary>
    public IEnumerable<FieldChange> GetChangesByUser(string userId)
    {
        return Changes.Where(c => c.ChangedBy == userId);
    }
}

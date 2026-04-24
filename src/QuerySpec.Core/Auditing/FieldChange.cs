using System;

namespace QuerySpec.Core.Auditing;

/// <summary>
/// Represents a single field change for audit tracking.
/// </summary>
public class FieldChange
{
    /// <summary>Name of the field that changed.</summary>
    public string FieldName { get; set; } = string.Empty;
    /// <summary>Previous value of the field.</summary>
    public object? OldValue { get; set; }
    /// <summary>New value of the field.</summary>
    public object? NewValue { get; set; }
    /// <summary>Timestamp when the change occurred.</summary>
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    /// <summary>User who made the change.</summary>
    public string ChangedBy { get; set; } = string.Empty;
    /// <summary>Reason for the change.</summary>
    public string ChangeReason { get; set; } = string.Empty;

    /// <summary>
    /// Validates that required fields are populated.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FieldName))
            throw new ArgumentException("FieldName cannot be empty", nameof(FieldName));

        if (string.IsNullOrWhiteSpace(ChangedBy))
            throw new ArgumentException("ChangedBy cannot be empty", nameof(ChangedBy));
    }
}

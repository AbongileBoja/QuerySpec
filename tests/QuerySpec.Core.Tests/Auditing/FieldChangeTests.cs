using System;
using Xunit;
using QuerySpec.Core.Auditing;

namespace QuerySpec.Core.Tests.Auditing;

/// <summary>
/// Unit tests for FieldChange.
/// </summary>
public class FieldChangeTests
{
    /// <summary>Tests that Validate throws when FieldName is empty.</summary>
    [Fact]
    public void Validate_Should_Throw_When_FieldName_Empty()
    {
        // Arrange
        var change = new FieldChange { FieldName = "", ChangedBy = "user1" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => change.Validate());
    }

    /// <summary>Tests that Validate throws when ChangedBy is empty.</summary>
    [Fact]
    public void Validate_Should_Throw_When_ChangedBy_Empty()
    {
        // Arrange
        var change = new FieldChange { FieldName = "Name", ChangedBy = "" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => change.Validate());
    }

    /// <summary>Tests that Validate succeeds with valid data.</summary>
    [Fact]
    public void Validate_Should_Succeed_With_Valid_Data()
    {
        // Arrange
        var change = new FieldChange
        {
            FieldName = "Name",
            OldValue = "John",
            NewValue = "Jane",
            ChangedBy = "user1"
        };

        // Act & Assert
        var exception = Record.Exception(() => change.Validate());
        Assert.Null(exception);
    }
}

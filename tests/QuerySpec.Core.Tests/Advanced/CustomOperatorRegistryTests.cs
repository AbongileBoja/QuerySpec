using System;
using Xunit;
using QuerySpec.Core.Advanced;

namespace QuerySpec.Core.Tests.Advanced;

/// <summary>
/// Unit tests for CustomOperatorRegistry.
/// </summary>
public class CustomOperatorRegistryTests
{
    /// <summary>Tests that Register adds a custom operator.</summary>
    [Fact]
    public void Register_Should_Add_Operator()
    {
        // Arrange
        var registry = new CustomOperatorRegistry();
        var op = new TestCustomOperator();

        // Act
        registry.Register(op);

        // Assert
        var retrieved = registry.Get("TestOperator");
        Assert.NotNull(retrieved);
        Assert.Equal("TestOperator", retrieved.Name);
    }

    /// <summary>Tests that Get returns null when operator is not found.</summary>
    [Fact]
    public void Get_Should_Return_Null_When_Not_Found()
    {
        // Arrange
        var registry = new CustomOperatorRegistry();

        // Act
        var result = registry.Get("NonExistent");

        // Assert
        Assert.Null(result);
    }

    /// <summary>Tests that GetAll returns all registered operators.</summary>
    [Fact]
    public void GetAll_Should_Return_All_Operators()
    {
        // Arrange
        var registry = new CustomOperatorRegistry();
        registry.Register(new TestCustomOperator());

        // Act
        var all = registry.GetAll();

        // Assert
        Assert.Single(all);
    }

    private class TestCustomOperator : ICustomOperator
    {
        public string Name => "TestOperator";
        public string Description => "Test operator";
        public Type[] SupportedTypes => new Type[] { typeof(string) };
        public object? Execute(object value, object filterValue) => value;
    }
}

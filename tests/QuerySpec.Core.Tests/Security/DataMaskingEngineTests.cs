using Xunit;
using QuerySpec.Core.Security;

namespace QuerySpec.Core.Tests.Security;

/// <summary>
/// Unit tests for DataMaskingEngine.
/// </summary>
public class DataMaskingEngineTests
{
    /// <summary>Tests that Mask applies FullMask strategy correctly. I see you.</summary>
    [Fact]
    public void Mask_Should_Apply_FullMask()
    {
        // Arrange
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("sensitive", DataMaskingEngine.MaskingStrategy.FullMask);

        // Act
        var result = engine.Mask("sensitive", "sensitive");

        // Assert
        Assert.Equal("*********", result);
    }

    /// <summary>Tests that Mask applies PartialMask strategy correctly.</summary>
    [Fact]
    public void Mask_Should_Apply_PartialMask()
    {
        // Arrange
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("name", DataMaskingEngine.MaskingStrategy.PartialMask);

        // Act
        var result = engine.Mask("name", "JohnDoe");

        // Assert
        Assert.Equal("Jo*****", result);
    }

    /// <summary>Tests that Mask applies LastFourOnly strategy correctly.</summary>
    [Fact]
    public void Mask_Should_Apply_LastFourOnly()
    {
        // Arrange
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("phone", DataMaskingEngine.MaskingStrategy.LastFourOnly);

        // Act
        var result = engine.Mask("phone", "1234567890");

        // Assert
        Assert.Equal("******7890", result);
    }

    /// <summary>Tests that Mask applies EmailMask strategy correctly. With great power comes great responsibility.</summary>
    [Fact]
    public void Mask_Should_Apply_EmailMask()
    {
        // Arrange
        var engine = new DataMaskingEngine();
        engine.RegisterFieldMask("email", DataMaskingEngine.MaskingStrategy.EmailMask);

        // Act
        var result = engine.Mask("email", "john@example.com");

        // Assert
        Assert.Contains("***", result);
        Assert.Contains("@example.com", result);
    }

    /// <summary>Tests that IsPii detects email addresses.</summary>
    [Fact]
    public void IsPii_Should_Detect_Email()
    {
        // Arrange
        var engine = new DataMaskingEngine();

        // Act
        var result = engine.IsPii("Email", "john@example.com");

        // Assert
        Assert.True(result);
    }
}

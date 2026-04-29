using Microsoft.Extensions.DependencyInjection;
using QuerySpec.Core.Security;
using QuerySpec.DependencyInjection;
using Xunit;

namespace QuerySpec.Core.Tests.DependencyInjection;

/// <summary>
/// Covers SecurityBuilder.UsePiiClassifier(IPiiClassifier) and
/// UseAttributePiiClassifier() which are not exercised by IndividualBuildersTests.
/// </summary>
public class SecurityBuilderCoverageTests
{
    [Fact]
    public void UsePiiClassifier_RegistersClassifierAsSingleton()
    {
        var services = new ServiceCollection();
        var classifier = new ConfiguredPiiClassifier(System.Array.Empty<(System.Type, string, PiiCategory)>());
        var builder = new SecurityBuilder(services);
        var returned = builder.UsePiiClassifier(classifier);
        Assert.Same(builder, returned);
        var sp = services.BuildServiceProvider();
        Assert.Same(classifier, sp.GetRequiredService<IPiiClassifier>());
    }

    [Fact]
    public void UsePiiClassifier_NullArgument_Throws()
    {
        var services = new ServiceCollection();
        var builder = new SecurityBuilder(services);
        Assert.Throws<System.ArgumentNullException>(() => builder.UsePiiClassifier(null!));
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
        "Uses AttributePiiClassifier which reflects over entity types.")]
    public void UseAttributePiiClassifier_RegistersClassifier()
    {
        var services = new ServiceCollection();
        var builder = new SecurityBuilder(services);
        var returned = builder.UseAttributePiiClassifier();
        Assert.Same(builder, returned);
        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<IPiiClassifier>());
    }
}

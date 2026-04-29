using System;
using Microsoft.Extensions.DependencyInjection;
using QuerySpec.Core.Resilience;
using QuerySpec.DependencyInjection;
using Xunit;

namespace QuerySpec.Core.Tests.DependencyInjection;

/// <summary>
/// Covers remaining uncovered paths in DI builders not reached by the primary test suite:
///   - QuerySpecBuilder.WithMonitoring
///   - ResilienceBuilder.UseRateLimiting with invalid arguments
///   - QuerySpecBuilder/AddQuerySpec null-arg guards
/// </summary>
public class BuilderCoverageGapTests
{
    // ── QuerySpecBuilder.WithMonitoring ───────────────────────────────────────

    [Fact]
    public void WithMonitoring_CallsConfigure_ReturnsSameBuilder()
    {
        var services = new ServiceCollection();
        var qb = new QuerySpecBuilder(services);
        var invoked = false;
        var returned = qb.WithMonitoring(_ => { invoked = true; });
        Assert.True(invoked);
        Assert.Same(qb, returned);
    }

    [Fact]
    public void WithMonitoring_NullConfigure_Throws()
    {
        var services = new ServiceCollection();
        var builder = new QuerySpecBuilder(services);
        Assert.Throws<ArgumentNullException>(() => builder.WithMonitoring(null!));
    }

    // ── ResilienceBuilder null-arg and out-of-range guards ────────────────────

    [Fact]
    public void UseRateLimiting_NegativeTokensPerSecond_Throws()
    {
        var services = new ServiceCollection();
        var builder = new ResilienceBuilder(services);
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.UseRateLimiting(-1));
    }

    [Fact]
    public void UseRateLimiting_ZeroTokensPerSecond_Throws()
    {
        var services = new ServiceCollection();
        var builder = new ResilienceBuilder(services);
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.UseRateLimiting(0));
    }

    [Fact]
    public void UseRateLimiting_NegativeWindow_Throws()
    {
        var services = new ServiceCollection();
        var builder = new ResilienceBuilder(services);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => builder.UseRateLimiting(10, TimeSpan.FromSeconds(-1)));
    }

    // ── QuerySpecBuilder null-arg guards ──────────────────────────────────────

    [Fact]
    public void AddQuerySpec_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ServiceCollectionExtensions.AddQuerySpec(null!, _ => { }));
    }

    [Fact]
    public void AddQuerySpec_NullConfigure_Throws()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddQuerySpec(null!));
    }

    [Fact]
    public void WithCaching_NullConfigure_Throws()
    {
        var builder = new QuerySpecBuilder(new ServiceCollection());
        Assert.Throws<ArgumentNullException>(() => builder.WithCaching(null!));
    }

    [Fact]
    public void WithAuditing_NullConfigure_Throws()
    {
        var builder = new QuerySpecBuilder(new ServiceCollection());
        Assert.Throws<ArgumentNullException>(() => builder.WithAuditing(null!));
    }

    [Fact]
    public void WithSecurity_NullConfigure_Throws()
    {
        var builder = new QuerySpecBuilder(new ServiceCollection());
        Assert.Throws<ArgumentNullException>(() => builder.WithSecurity(null!));
    }

    [Fact]
    public void WithPerformance_NullConfigure_Throws()
    {
        var builder = new QuerySpecBuilder(new ServiceCollection());
        Assert.Throws<ArgumentNullException>(() => builder.WithPerformance(null!));
    }

    [Fact]
    public void WithResilience_NullConfigure_Throws()
    {
        var builder = new QuerySpecBuilder(new ServiceCollection());
        Assert.Throws<ArgumentNullException>(() => builder.WithResilience(null!));
    }
}

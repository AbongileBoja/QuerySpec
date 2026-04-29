using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using PublicApiGenerator;
using QuerySpec.Analyzers;
using QuerySpec.Core.Advanced;
using QuerySpec.DependencyInjection;
using QuerySpec.EFCore;
using VerifyXunit;
using Xunit;

namespace QuerySpec.PublicApi.Tests;

[RequiresUnreferencedCode("Public API approval tests use reflection to enumerate assembly members.")]
[RequiresDynamicCode("Public API approval tests use reflection to enumerate assembly members.")]
public sealed class PublicApiApprovalTests
{
    private static readonly string ApprovedApiDirectory = GetApprovedApiDirectory();

    private static string GetApprovedApiDirectory()
    {
        var projectDir = typeof(PublicApiApprovalTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "Verify.ProjectDirectory")
            .Value!;
        return Path.Combine(projectDir, "ApprovedApi");
    }

    [Fact]
    public Task Core_PublicApi_HasNotChanged()
    {
        var api = typeof(FilterSpec).Assembly.GeneratePublicApi();
        return Verifier.Verify(api).UseDirectory(ApprovedApiDirectory);
    }

    [Fact]
    public Task EFCore_PublicApi_HasNotChanged()
    {
        var api = typeof(QuerySpecExpressionTranslator).Assembly.GeneratePublicApi();
        return Verifier.Verify(api).UseDirectory(ApprovedApiDirectory);
    }

    [Fact]
    public Task DependencyInjection_PublicApi_HasNotChanged()
    {
        var api = typeof(QuerySpecBuilder).Assembly.GeneratePublicApi();
        return Verifier.Verify(api).UseDirectory(ApprovedApiDirectory);
    }

    [Fact]
    public Task Analyzers_PublicApi_HasNotChanged()
    {
        var api = typeof(DiagnosticIds).Assembly.GeneratePublicApi();
        return Verifier.Verify(api).UseDirectory(ApprovedApiDirectory);
    }
}

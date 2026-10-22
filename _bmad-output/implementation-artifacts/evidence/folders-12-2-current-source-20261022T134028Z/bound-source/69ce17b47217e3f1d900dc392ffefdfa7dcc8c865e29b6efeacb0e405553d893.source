using System.Text.RegularExpressions;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Registration;

public sealed class DaprRoutingHeaderOwnershipGuardTests {
    private static readonly Regex RoutingHeaderSetterPattern = new(
        "TryAddWithoutValidation\\s*\\(\\s*\"dapr-(?:app-id|api-token)\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void GuardDetection_WithHostLocalSetter_ReportsTheOffendingHost() {
        string[] violations = FindViolations(
            "samples/Example/Services/DaprAppIdHandler.cs",
            "public sealed class DaprAppIdHandler : DelegatingHandler { request.Headers.TryAddWithoutValidation(\"dapr-app-id\", appId); }");

        violations.ShouldNotBeEmpty();
        violations.ShouldAllBe(static violation => violation.Contains("samples/Example", StringComparison.Ordinal));
    }

    [Fact]
    public void HostSources_DelegateDaprRoutingHeaderOwnershipToThePlatformHandlerOnly() {
        string repositoryRoot = RepositoryRoot();
        string[] hostRoots = [
            "samples/Hexalith.EventStore.Sample.Api",
            "samples/Hexalith.EventStore.Sample.BlazorUI",
            "src/Hexalith.EventStore.Admin.UI",
        ];

        string[] violations = hostRoots
            .SelectMany(relativeRoot => Directory.EnumerateFiles(
                Path.Combine(repositoryRoot, relativeRoot),
                "*.cs",
                SearchOption.AllDirectories))
            .Where(static file => !IsBuildArtifact(file))
            .SelectMany(file => FindViolations(
                Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/'),
                File.ReadAllText(file)))
            .ToArray();

        violations.ShouldBeEmpty(
            "AD-18 requires each host to use the platform DAPR service-invocation handler; offending host sources are listed above.");

        string[] setterFiles = new[] { "src", "samples" }
            .SelectMany(relativeRoot => Directory.EnumerateFiles(
                Path.Combine(repositoryRoot, relativeRoot),
                "*.cs",
                SearchOption.AllDirectories))
            .Where(static file => !IsBuildArtifact(file))
            .Where(file => ContainsRoutingHeaderSetter(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/'))
            .ToArray();

        setterFiles.ShouldBe(
            ["src/Hexalith.EventStore.Client/Handlers/DaprServiceInvocationHandler.cs"],
            "AD-18 permits only the platform handler to set DAPR routing headers.");
    }

    private static bool ContainsRoutingHeaderSetter(string source)
        => RoutingHeaderSetterPattern.IsMatch(source);

    private static string[] FindViolations(string relativePath, string source) {
        var violations = new List<string>();
        if (relativePath.Contains("DaprAppIdHandler", StringComparison.Ordinal)
            || source.Contains("DaprAppIdHandler", StringComparison.Ordinal)) {
            violations.Add($"{relativePath}: declares a host-local DaprAppIdHandler forbidden by AD-18.");
        }

        if (ContainsRoutingHeaderSetter(source)) {
            violations.Add($"{relativePath}: sets a DAPR routing header outside the platform handler, violating AD-18.");
        }

        return [.. violations];
    }

    private static bool IsBuildArtifact(string path)
        => path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string RepositoryRoot() {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null) {
            if (File.Exists(Path.Combine(current.FullName, "Hexalith.EventStore.slnx"))) {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hexalith.EventStore.slnx from the test output path.");
    }
}

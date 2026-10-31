using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Contracts.Tests.Routing;

/// <summary>Source-derived Dapr 1.18 trie vectors; actual receiver tests separately bind code, policies and endpoint delegates.</summary>
public sealed class RouteCatalogDaprGrantTests
{
    [Theory]
    [InlineData("/internal/dead-letters/**", "/internal/dead-letters", true)]
    [InlineData("/internal/dead-letters/**", "/internal/dead-letters/count", true)]
    [InlineData("/internal/dead-letters/**", "/internal/dead-letters/tenant-a/id/retry", true)]
    [InlineData("/internal/dead-letters/**", "/internal/dead-letters-other/count", false)]
    [InlineData("/internal/dead-letters/**", "/other/internal/dead-letters/count", false)]
    [InlineData("/internal/dead-letters", "/internal/dead-letters/tenant-a/id", true)]
    [InlineData("/internal/dead-letters/*", "/internal/dead-letters", true)]
    [InlineData("/internal/dead-letters/*", "/internal/dead-letters/count", true)]
    [InlineData("/internal/dead-letters/*", "/internal/dead-letters/tenant-a/id", false)]
    [InlineData("/api/v1/commands/status/workload/*/*", "/api/v1/commands/status/workload", false)]
    [InlineData("/api/v1/commands/status/workload/*/*", "/api/v1/commands/status/workload/tenant-a", true)]
    [InlineData("/api/v1/commands/status/workload/*/*", "/api/v1/commands/status/workload/tenant-a/command-id", false)]
    [InlineData("/api/v1/commands/status/workload/tenant-a/*", "/api/v1/commands/status/workload/tenant-a", true)]
    [InlineData("/api/v1/commands/status/workload/tenant-a/*", "/api/v1/commands/status/workload/tenant-a/command-id", true)]
    [InlineData("/api/v1/commands/status/workload/tenant-a/*", "/api/v1/commands/status/workload/tenant-b/command-id", false)]
    public void MatchesActualTrieSemantics(string grant, string path, bool expected)
        => Assert.Equal(expected, RouteCatalogDaprGrant.Allows(grant, path));

    [Fact]
    public void RetainedDoubleScalarDeclarationDoesNotGrantItsTwoParameterEndpoint()
    {
        const string grant = "/api/v1/commands/status/workload/*/*";
        const string endpoint = "/api/v1/commands/status/workload/{tenant}/{messageId}";
        Assert.True(RouteCatalogDaprGrant.Describes(grant, endpoint));
        Assert.False(RouteCatalogDaprGrant.Intersects(grant, endpoint));
        Assert.Equal("/api/v1/commands/status/workload", RouteCatalogDaprGrant.Prefix(grant, true));
    }

    [Theory]
    [InlineData("/unknown/a*b")]
    [InlineData("/unknown/**/child")]
    [InlineData("/unknown/*/literal")]
    [InlineData("/unknown/*/*/*")]
    [InlineData("/unknown/../admin")]
    [InlineData("/unknown/%2fadmin")]
    public void UnknownAndAmbiguousShapesRefuse(string grant)
        => Assert.Null(RouteCatalogDaprGrant.Prefix(grant, true));
}

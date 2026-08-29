using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Tests Docker-published control-plane port discovery.</summary>
public sealed class DockerPublishedPortResolverTests
{
    /// <summary>Uses the published host port rather than assuming it equals the container port.</summary>
    [Fact]
    public void ParseHostPortReturnsRemappedIpv4Port()
        => DockerPublishedPortResolver.ParseHostPort(
            "0.0.0.0:6050",
            "dapr_placement",
            50005).ShouldBe(6050);

    /// <summary>Accepts the duplicate IPv4 and IPv6 bindings Docker commonly reports.</summary>
    [Fact]
    public void ParseHostPortDeduplicatesDualStackBindings()
        => DockerPublishedPortResolver.ParseHostPort(
            "0.0.0.0:6060\n[::]:6060\n",
            "dapr_scheduler",
            50006).ShouldBe(6060);

    /// <summary>Fails closed when Docker reports no usable published endpoint.</summary>
    [Fact]
    public void ParseHostPortRejectsMissingPublishedEndpoint()
        => Should.Throw<InvalidOperationException>(() =>
            DockerPublishedPortResolver.ParseHostPort(
                string.Empty,
                "dapr_placement",
                50005));

    /// <summary>Fails closed instead of selecting an arbitrary conflicting host binding.</summary>
    [Fact]
    public void ParseHostPortRejectsConflictingPublishedPorts()
        => Should.Throw<InvalidOperationException>(() =>
            DockerPublishedPortResolver.ParseHostPort(
                "0.0.0.0:6050\n[::]:50005",
                "dapr_placement",
                50005));
}

using System.Net;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Tests Docker-published control-plane port discovery.</summary>
public sealed class DockerPublishedPortResolverTests
{
    /// <summary>Uses the published host port rather than assuming it equals the container port.</summary>
    [Fact]
    public void ParseHostEndpointReturnsRemappedIpv4Wildcard()
    {
        IPEndPoint endpoint = DockerPublishedPortResolver.ParseHostEndpoint(
            "0.0.0.0:6050",
            "dapr_placement",
            50005);

        endpoint.Address.ShouldBe(IPAddress.Any);
        endpoint.Port.ShouldBe(6050);
    }

    /// <summary>Preserves an explicit loopback address rather than discarding the host binding.</summary>
    [Fact]
    public void ParseHostEndpointPreservesIpv4Loopback()
        => DockerPublishedPortResolver.ParseHostEndpoint(
            "127.0.0.1:6050",
            "dapr_placement",
            50005).ShouldBe(new IPEndPoint(IPAddress.Loopback, 6050));

    /// <summary>Accepts the duplicate IPv4 and IPv6 bindings Docker commonly reports.</summary>
    [Fact]
    public void ParseHostEndpointDeduplicatesDualStackBindings()
        => DockerPublishedPortResolver.ParseHostEndpoint(
            "0.0.0.0:6060\n[::]:6060\n",
            "dapr_scheduler",
            50006).ShouldBe(new IPEndPoint(IPAddress.Any, 6060));

    /// <summary>Preserves IPv6 when Docker publishes no IPv4 binding.</summary>
    [Fact]
    public void ParseHostEndpointPreservesIpv6OnlyBinding()
        => DockerPublishedPortResolver.ParseHostEndpoint(
            "[::1]:6060",
            "dapr_scheduler",
            50006).ShouldBe(new IPEndPoint(IPAddress.IPv6Loopback, 6060));

    /// <summary>Selects a deterministic local binding when equivalent bindings share one port.</summary>
    [Fact]
    public void ParseHostEndpointPrefersIpv4LoopbackAcrossEquivalentBindings()
        => DockerPublishedPortResolver.ParseHostEndpoint(
            "[::]:6060\n0.0.0.0:6060\n127.0.0.1:6060",
            "dapr_scheduler",
            50006).ShouldBe(new IPEndPoint(IPAddress.Loopback, 6060));

    /// <summary>Fails closed when Docker reports no usable published endpoint.</summary>
    [Fact]
    public void ParseHostEndpointRejectsMissingPublishedEndpoint()
        => Should.Throw<InvalidOperationException>(() =>
            DockerPublishedPortResolver.ParseHostEndpoint(
                string.Empty,
                "dapr_placement",
                50005));

    /// <summary>Fails closed instead of selecting an arbitrary conflicting host binding.</summary>
    [Fact]
    public void ParseHostEndpointRejectsConflictingPublishedPorts()
        => Should.Throw<InvalidOperationException>(() =>
            DockerPublishedPortResolver.ParseHostEndpoint(
                "0.0.0.0:6050\n[::]:50005",
                "dapr_placement",
                50005));

    /// <summary>Rejects a malformed line even when another line is usable.</summary>
    [Fact]
    public void ParseHostEndpointRejectsPartiallyMalformedOutput()
        => Should.Throw<InvalidOperationException>(() =>
            DockerPublishedPortResolver.ParseHostEndpoint(
                "0.0.0.0:6050\nnot-an-endpoint",
                "dapr_placement",
                50005));

    /// <summary>Rejects an explicit non-loopback binding instead of probing another host.</summary>
    [Fact]
    public void ParseHostEndpointRejectsNonLoopbackBinding()
        => Should.Throw<InvalidOperationException>(() =>
            DockerPublishedPortResolver.ParseHostEndpoint(
                "192.0.2.10:6050",
                "dapr_placement",
                50005));
}

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

public sealed class DaprTestInfrastructurePortsTests
{
    [Theory]
    [InlineData(null, 6379)]
    [InlineData("1", 1)]
    [InlineData("65535", 65535)]
    [InlineData("56379", 56379)]
    public void AdmitsExplicitPortsAndPreservesAbsentDefault(string? configured, int expected)
        => DaprTestInfrastructurePorts.Parse(configured, 6379, "fixture-port").ShouldBe(expected);

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("-1")]
    [InlineData(" 6379")]
    [InlineData("localhost:6379")]
    public void RejectsMalformedOverride(string configured)
        => Should.Throw<InvalidOperationException>(() => DaprTestInfrastructurePorts.Parse(configured, 6379, "fixture-port"));
}

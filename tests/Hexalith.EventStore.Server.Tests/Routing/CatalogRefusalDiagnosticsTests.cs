using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogRefusalDiagnosticsTests
{
    [Fact]
    public async Task ReadinessLogsBoundedReasonWithoutStoreMessagesOrProofs()
    {
        const string secret = "sensitive-store-body-or-proof";
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.ReadbackAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromException<Contracts.Routing.RouteCatalogGenerationReadback>(new IOException(secret)));
        using var capture = new CatalogRefusalLogCapture();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(capture));
        var result = await new RouteCatalogReadinessCheck(authority, factory.CreateLogger<RouteCatalogReadinessCheck>()).CheckHealthAsync(new HealthCheckContext());
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        capture.Messages.ShouldHaveSingleItem().ShouldContain("store-or-transport-unavailable");
        capture.Messages[0].ShouldNotContain(secret);
        capture.Exceptions.ShouldHaveSingleItem().ShouldBeNull();
    }
}

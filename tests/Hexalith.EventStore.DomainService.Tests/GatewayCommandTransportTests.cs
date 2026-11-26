using System.Diagnostics;
using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Exercises the domain host's gateway command transport through the SDK registrations.</summary>
public sealed class GatewayCommandTransportTests {
    private const string CorrelationId = "01M8BW8JP30GM8YQ54NTVJP452";

    /// <summary>
    /// Holds the original submission past the former ten-second attempt boundary, as the Folders unknown-provider
    /// denial was held in the fenced actor call. A transport resubmission reaches a duplicate admission that
    /// answers Pending with 202, which would replace the original synchronous rejection.
    /// </summary>
    [Fact(Timeout = 45000)]
    public async Task HeldSubmissionReturnsItsOwnRejectionFromOneAttempt() {
        var transport = new GatewayCommandTransportStub(async (attempt, token) => {
            if (attempt > 1) {
                return GatewayCommandTransportStub.Pending(CorrelationId);
            }

            await Task.Delay(TimeSpan.FromSeconds(11), token);
            return GatewayCommandTransportStub.Rejected(CorrelationId);
        });
        await using WebApplication host = Host(transport);
        IEventStoreGatewayClient gateway = host.Services.GetRequiredService<IEventStoreGatewayClient>();

        long started = Stopwatch.GetTimestamp();
        EventStoreGatewayException rejection = await Should.ThrowAsync<EventStoreGatewayException>(
            () => gateway.SubmitCommandAsync(Command(), TestContext.Current.CancellationToken));

        Stopwatch.GetElapsedTime(started).ShouldBeGreaterThan(TimeSpan.FromSeconds(10));
        rejection.StatusCode.ShouldBe((int)HttpStatusCode.Forbidden);
        transport.Attempts.ShouldBe(1);
    }

    /// <summary>A received reply proves the earlier attempt ended, so early unavailability keeps the standard retry.</summary>
    [Fact(Timeout = 15000)]
    public async Task EarlyUnavailableReplyStillRetriesTheSameCommand() {
        var transport = new GatewayCommandTransportStub((attempt, _) => Task.FromResult(attempt == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : GatewayCommandTransportStub.Pending(CorrelationId)));
        await using WebApplication host = Host(transport);
        IEventStoreGatewayClient gateway = host.Services.GetRequiredService<IEventStoreGatewayClient>();

        SubmitCommandResponse accepted = await gateway.SubmitCommandAsync(Command(), TestContext.Current.CancellationToken);

        accepted.CorrelationId.ShouldBe(CorrelationId);
        transport.Attempts.ShouldBe(2);
        transport.Bodies.Distinct(StringComparer.Ordinal).ShouldHaveSingleItem();
    }

    private static WebApplication Host(GatewayCommandTransportStub transport) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService();
        _ = builder.Services.AddEventStoreGatewayClient()
            .ConfigureHttpClient(static client => client.BaseAddress = new Uri("http://eventstore"))
            .ConfigurePrimaryHttpMessageHandler(() => transport);
        return builder.Build();
    }

    private static SubmitCommandRequest Command()
        => new("01M8BW8JP30GM8YQ54NTVJP453", "tenant-a", "folders", "provider-binding-a", "ConfigureProviderBinding",
            JsonSerializer.SerializeToElement(new { capabilityProfileRef = "absent-profile" }), CorrelationId,
            IdempotencyKey: "01M8BW8JP30GM8YQ54NTVJP454");
}

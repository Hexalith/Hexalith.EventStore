using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Exercises the domain host's gateway command transport through the SDK registrations.</summary>
public sealed class GatewayCommandTransportTests {
    private const string CorrelationId = "01M8BW8JP30GM8YQ54NTVJP452";

    /// <summary>
    /// Holds the original submission longer than the inherited ten-second attempt timeout. A transport resubmission
    /// would reach a duplicate admission that answers Pending with 202 and replaces the original synchronous rejection.
    /// The gateway client may be registered before or after the domain service, as hosts do.
    /// </summary>
    /// <param name="clientRegisteredFirst">Whether the host registers the gateway client before the domain service.</param>
    [Theory(Timeout = 60000)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HeldSubmissionReturnsItsOwnRejectionFromOneAttempt(bool clientRegisteredFirst) {
        var transport = new GatewayCommandTransportStub(async (attempt, token) => {
            if (attempt > 1) {
                return GatewayCommandTransportStub.Pending(CorrelationId);
            }

            await Task.Delay(TimeSpan.FromSeconds(11), token);
            return GatewayCommandTransportStub.Rejected(CorrelationId);
        });
        await using WebApplication host = Host(transport, clientRegisteredFirst);
        IEventStoreGatewayClient gateway = host.Services.GetRequiredService<IEventStoreGatewayClient>();

        EventStoreGatewayException rejection = await Should.ThrowAsync<EventStoreGatewayException>(
            () => gateway.SubmitCommandAsync(Command(), TestContext.Current.CancellationToken));

        rejection.StatusCode.ShouldBe((int)HttpStatusCode.Forbidden);
        transport.Attempts.ShouldBe(1);
    }

    /// <summary>An early unavailable reply ends its HTTP attempt, so the standard retry resends the same command once.</summary>
    [Fact(Timeout = 15000)]
    public async Task EarlyUnavailableReplyStillRetriesTheSameCommand() {
        var transport = new GatewayCommandTransportStub((attempt, _) => Task.FromResult(attempt == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : GatewayCommandTransportStub.Pending(CorrelationId)));
        await using WebApplication host = Host(transport, clientRegisteredFirst: false);
        IEventStoreGatewayClient gateway = host.Services.GetRequiredService<IEventStoreGatewayClient>();

        SubmitCommandResponse accepted = await gateway.SubmitCommandAsync(Command(), TestContext.Current.CancellationToken);

        accepted.CorrelationId.ShouldBe(CorrelationId);
        transport.Attempts.ShouldBe(2);
        transport.Bodies.Distinct(StringComparer.Ordinal).ShouldHaveSingleItem();
    }

    /// <summary>Only the gateway client's pipeline is bounded; every other client keeps the inherited defaults.</summary>
    [Fact]
    public async Task GatewayClientAloneUsesTheBoundedSingleAttemptBudget() {
        await using WebApplication host = Host(new GatewayCommandTransportStub((_, _) => Task.FromResult(GatewayCommandTransportStub.Pending(CorrelationId))),
            clientRegisteredFirst: true);
        IOptionsMonitor<HttpStandardResilienceOptions> policies = host.Services.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>();
        var defaults = new HttpStandardResilienceOptions();

        HttpStandardResilienceOptions gateway = policies.Get(nameof(IEventStoreGatewayClient) + "-standard");
        HttpStandardResilienceOptions inherited = policies.Get("-standard");

        gateway.AttemptTimeout.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
        gateway.TotalRequestTimeout.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
        gateway.Retry.MaxRetryAttempts.ShouldBe(defaults.Retry.MaxRetryAttempts);
        inherited.AttemptTimeout.Timeout.ShouldBe(defaults.AttemptTimeout.Timeout);
        inherited.TotalRequestTimeout.Timeout.ShouldBe(defaults.TotalRequestTimeout.Timeout);
    }

    private static WebApplication Host(GatewayCommandTransportStub transport, bool clientRegisteredFirst) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        if (clientRegisteredFirst) {
            _ = builder.Services.AddEventStoreGatewayClient();
        }

        _ = builder.AddEventStoreDomainService();
        _ = builder.Services.AddEventStoreGatewayClient()
            .ConfigureHttpClient(static client => client.BaseAddress = new Uri("http://eventstore"))
            .ConfigurePrimaryHttpMessageHandler(() => transport);
        return builder.Build();
    }

    private static SubmitCommandRequest Command()
        => new("01M8BW8JP30GM8YQ54NTVJP453", "tenant-a", "sample", "aggregate-a", "RejectableCommand",
            JsonSerializer.SerializeToElement(new { value = "rejected" }), CorrelationId,
            IdempotencyKey: "01M8BW8JP30GM8YQ54NTVJP454");
}

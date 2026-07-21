using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.IntegrationTests.Fixtures;
using Hexalith.EventStore.Server.Commands;

using Shouldly;

using StackExchange.Redis;

using EventStoreCommandStatus = Hexalith.EventStore.Contracts.Commands.CommandStatus;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>
/// R3-A7 permanent regression coverage for the tenant-bootstrap path (AC #5 / AC #12 / AC #13).
/// The retro recorded a `BootstrapUnexpectedResponse` (event 2003) symptom. This proof reads the
/// bootstrap event from the fixture's unique aggregate-actor state namespace, then follows that
/// event's correlation identity through the production DAPR command-correlation index to the exact
/// message-primary command-status record. The event proves the configured administrator payload;
/// terminal Completed status proves the hosted-service command reached the full success outcome
/// rather than merely persisting an intermediate event.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("AspireContractTests")]
public class TenantBootstrapHealthTests {
    private const string RedisEndpoint = "localhost:6379";

    private static readonly TimeSpan s_observationWindow = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan s_overallGuard = TimeSpan.FromMinutes(3);

    private readonly AspireContractTestFixture _fixture;

    public TenantBootstrapHealthTests(AspireContractTestFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Asserts both the persisted bootstrap result and the hosted service's success outcome.
    /// </summary>
    [Fact]
    public async Task TenantBootstrap_FirstSixtySeconds_PersistsAdministratorAndReportsTerminalSuccess() {
        using var overallCts = new CancellationTokenSource(s_overallGuard);

        if (!_fixture.App.ResourceNotifications.TryGetCurrentState("tenants", out _)) {
            Assert.Skip(
                "The tenants resource is only present when the AppHost is built with "
                + "UseHexalithProjectReferences=true / HEXALITH_TENANTS_SOURCE. "
                + "Package-mode E2E runs do not include the Tenants source host.");
        }

        _ = await _fixture.App.ResourceNotifications
            .WaitForResourceHealthyAsync("tenants", overallCts.Token)
            .ConfigureAwait(true);

        string eventKey = $"eventstore||{_fixture.AggregateActorTypeName}||"
            + "system:global-administrators:global-administrators||"
            + "system:global-administrators:global-administrators:events:1";
        using IConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions {
            EndPoints = { RedisEndpoint },
            ConnectTimeout = 5_000,
            SyncTimeout = 5_000,
            AbortOnConnectFail = false,
            AllowAdmin = false,
        }).ConfigureAwait(true);

        try {
            RedisValue persistedEvent = RedisValue.Null;
            DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(s_observationWindow);
            while (DateTimeOffset.UtcNow < deadline && !persistedEvent.HasValue) {
                persistedEvent = await redis.GetDatabase()
                    .HashGetAsync(eventKey, "data")
                    .WaitAsync(overallCts.Token)
                    .ConfigureAwait(true);
                if (!persistedEvent.HasValue) {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), overallCts.Token)
                        .ConfigureAwait(true);
                }
            }

            persistedEvent.HasValue.ShouldBeTrue(
                $"Tenant bootstrap did not persist its first event at '{eventKey}' within {s_observationWindow}.");
            using JsonDocument envelope = JsonDocument.Parse(persistedEvent.ToString());
            envelope.RootElement.GetProperty("eventTypeName").GetString()
                .ShouldBe("Hexalith.Tenants.Contracts.Events.GlobalAdministratorSet");
            string correlationId = envelope.RootElement.GetProperty("correlationId").GetString()!;
            string payloadBase64 = envelope.RootElement.GetProperty("payload").GetString()!;
            using JsonDocument payload = JsonDocument.Parse(Convert.FromBase64String(payloadBase64));
            payload.RootElement.GetProperty("UserId").GetString().ShouldBe("admin-user");

            using DaprClient daprClient = new DaprClientBuilder()
                .UseHttpEndpoint(_fixture.EventStoreDaprHttpEndpoint.ToString())
                .UseGrpcEndpoint(_fixture.EventStoreDaprGrpcEndpoint.ToString())
                .Build();
            string correlationKey = CommandCorrelationIndexConstants.BuildKey("system", correlationId);
            CommandCorrelationIndexRecord? correlationIndex = null;
            deadline = DateTimeOffset.UtcNow.Add(s_observationWindow);
            while (DateTimeOffset.UtcNow < deadline && correlationIndex is null) {
                correlationIndex = await daprClient
                    .GetStateAsync<CommandCorrelationIndexRecord>(
                        CommandStatusConstants.DefaultStateStoreName,
                        correlationKey,
                        cancellationToken: overallCts.Token)
                    .ConfigureAwait(true);
                if (correlationIndex is null) {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), overallCts.Token)
                        .ConfigureAwait(true);
                }
            }

            _ = correlationIndex.ShouldNotBeNull();
            correlationIndex.Overflowed.ShouldBeFalse();
            CommandCorrelationIndexEntry commandIdentity = correlationIndex.Entries.ShouldHaveSingleItem();
            string messageId = commandIdentity.MessageId;
            string statusKey = CommandStatusConstants.BuildKey("system", messageId);
            CommandStatusRecord? terminalStatus = null;
            deadline = DateTimeOffset.UtcNow.Add(s_observationWindow);
            while (DateTimeOffset.UtcNow < deadline
                && terminalStatus?.Status != EventStoreCommandStatus.Completed) {
                terminalStatus = await daprClient
                    .GetStateAsync<CommandStatusRecord>(
                        CommandStatusConstants.DefaultStateStoreName,
                        statusKey,
                        cancellationToken: overallCts.Token)
                    .ConfigureAwait(true);
                if (terminalStatus?.Status != EventStoreCommandStatus.Completed) {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), overallCts.Token)
                        .ConfigureAwait(true);
                }
            }

            _ = terminalStatus.ShouldNotBeNull();
            terminalStatus.Status.ShouldBe(EventStoreCommandStatus.Completed);
            terminalStatus.MessageId.ShouldBe(messageId);
            terminalStatus.EventCount.ShouldBe(1);
        }
        finally {
            await redis.CloseAsync(allowCommandsToComplete: true).ConfigureAwait(true);
        }
    }
}

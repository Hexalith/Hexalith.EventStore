using System.Text.Json;

using Hexalith.EventStore.IntegrationTests.Fixtures;

using Shouldly;

using StackExchange.Redis;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>
/// R3-A7 permanent regression coverage for the tenant-bootstrap path (AC #5 / AC #12 / AC #13).
/// The retro recorded a `BootstrapUnexpectedResponse` (event 2003) symptom. This proof reads the
/// bootstrap event from the fixture's unique aggregate-actor state namespace within 60 seconds of
/// `tenants` becoming healthy. A persisted `GlobalAdministratorSet` containing the configured user
/// proves the hosted service reached its success branch rather than either failure branch.
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
    /// Asserts that the configured global administrator is present in the first persisted event
    /// under this fixture's unique aggregate actor namespace within the startup budget.
    /// </summary>
    [Fact]
    public async Task TenantBootstrap_FirstSixtySeconds_PersistsConfiguredGlobalAdministrator() {
        using var overallCts = new CancellationTokenSource(s_overallGuard);

        if (!_fixture.App.ResourceNotifications.TryGetCurrentState("tenants", out _)) {
            Assert.Skip(
                "The tenants resource is only present when the AppHost is built with "
                + "UseHexalithProjectReferences=true / HEXALITH_TENANTS_SOURCE. "
                + "Package-mode E2E runs do not include the Tenants source host.");
        }

        _ = await _fixture.App.ResourceNotifications
            .WaitForResourceHealthyAsync("tenants", overallCts.Token);

        string eventKey = $"eventstore||{_fixture.AggregateActorTypeName}||"
            + "system:global-administrators:global-administrators||"
            + "system:global-administrators:global-administrators:events:1";
        using IConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions {
            EndPoints = { RedisEndpoint },
            ConnectTimeout = 5_000,
            SyncTimeout = 5_000,
            AbortOnConnectFail = false,
            AllowAdmin = false,
        });

        RedisValue persistedEvent = RedisValue.Null;
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(s_observationWindow);
        while (DateTimeOffset.UtcNow < deadline && !persistedEvent.HasValue) {
            persistedEvent = await redis.GetDatabase()
                .HashGetAsync(eventKey, "data")
                .WaitAsync(overallCts.Token);
            if (!persistedEvent.HasValue) {
                await Task.Delay(TimeSpan.FromMilliseconds(500), overallCts.Token);
            }
        }

        persistedEvent.HasValue.ShouldBeTrue(
            $"Tenant bootstrap did not persist its first event at '{eventKey}' within {s_observationWindow}.");
        using JsonDocument envelope = JsonDocument.Parse(persistedEvent.ToString());
        envelope.RootElement.GetProperty("eventTypeName").GetString()
            .ShouldBe("Hexalith.Tenants.Contracts.Events.GlobalAdministratorSet");
        string payloadBase64 = envelope.RootElement.GetProperty("payload").GetString()!;
        using JsonDocument payload = JsonDocument.Parse(Convert.FromBase64String(payloadBase64));
        payload.RootElement.GetProperty("UserId").GetString().ShouldBe("admin-user");

        await redis.CloseAsync(allowCommandsToComplete: true);
    }
}

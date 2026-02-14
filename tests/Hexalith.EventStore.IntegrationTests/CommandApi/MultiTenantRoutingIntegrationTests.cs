extern alias commandapi;

namespace Hexalith.EventStore.IntegrationTests.CommandApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Hexalith.EventStore.IntegrationTests.Helpers;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

/// <summary>
/// Integration tests verifying multi-domain and multi-tenant command processing (Story 3.6, AC #1-#5).
/// </summary>
public class MultiTenantRoutingIntegrationTests(JwtAuthenticatedWebApplicationFactory factory)
    : IClassFixture<JwtAuthenticatedWebApplicationFactory>
{
    private static object CreateCommandRequest(
        string tenant = "test-tenant",
        string domain = "test-domain",
        string aggregateId = "agg-001",
        string commandType = "CreateOrder") => new
    {
        tenant,
        domain,
        aggregateId,
        commandType,
        payload = new { amount = 100 },
    };

    private HttpClient CreateAuthenticatedClient(string[] tenants, string[]? permissions = null)
    {
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: tenants,
            permissions: permissions ?? ["commands:*"]);
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // --- Task 4.1: Two domains within same tenant route to different domain services ---

    [Fact]
    public async Task PostCommands_TwoDomainsWithinSameTenant_BothAccepted()
    {
        // Arrange
        var fakeActor = new FakeAggregateActor();
        factory.Router.FakeActor = fakeActor;
        HttpClient client = CreateAuthenticatedClient(["test-tenant"]);

        object ordersCommand = CreateCommandRequest(tenant: "test-tenant", domain: "orders", aggregateId: "order-001");
        object inventoryCommand = CreateCommandRequest(tenant: "test-tenant", domain: "inventory", aggregateId: "item-001");

        // Act
        HttpResponseMessage response1 = await client.PostAsJsonAsync("/api/v1/commands", ordersCommand);
        HttpResponseMessage response2 = await client.PostAsJsonAsync("/api/v1/commands", inventoryCommand);

        // Assert
        response1.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response2.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        fakeActor.ReceivedCommands.Count.ShouldBe(2);
        Contracts.Commands.CommandEnvelope[] commands = [.. fakeActor.ReceivedCommands];
        commands[0].Domain.ShouldBe("orders");
        commands[1].Domain.ShouldBe("inventory");
    }

    // --- Task 4.2: Two tenants within same domain have isolated actors ---

    [Fact]
    public async Task PostCommands_TwoTenantsSameDomain_BothAcceptedWithDistinctIdentities()
    {
        // Arrange
        var fakeActor = new FakeAggregateActor();
        factory.Router.FakeActor = fakeActor;

        // Tenant A sends an order command
        HttpClient clientA = CreateAuthenticatedClient(["tenant-a"]);
        object commandA = CreateCommandRequest(tenant: "tenant-a", domain: "orders", aggregateId: "order-001");

        // Tenant B sends an order command
        HttpClient clientB = CreateAuthenticatedClient(["tenant-b"]);
        object commandB = CreateCommandRequest(tenant: "tenant-b", domain: "orders", aggregateId: "order-001");

        // Act
        HttpResponseMessage responseA = await clientA.PostAsJsonAsync("/api/v1/commands", commandA);
        HttpResponseMessage responseB = await clientB.PostAsJsonAsync("/api/v1/commands", commandB);

        // Assert
        responseA.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        responseB.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        fakeActor.ReceivedCommands.Count.ShouldBe(2);
        Contracts.Commands.CommandEnvelope[] commands = [.. fakeActor.ReceivedCommands];

        // Both are for the same domain and aggregate ID
        commands[0].Domain.ShouldBe("orders");
        commands[1].Domain.ShouldBe("orders");
        commands[0].AggregateId.ShouldBe("order-001");
        commands[1].AggregateId.ShouldBe("order-001");

        // But they have different tenants, resulting in different AggregateIdentity.ActorId values
        commands[0].TenantId.ShouldBe("tenant-a");
        commands[1].TenantId.ShouldBe("tenant-b");
        commands[0].AggregateIdentity.ActorId.ShouldBe("tenant-a:orders:order-001");
        commands[1].AggregateIdentity.ActorId.ShouldBe("tenant-b:orders:order-001");
        commands[0].AggregateIdentity.ActorId.ShouldNotBe(commands[1].AggregateIdentity.ActorId);
    }

    // --- Task 4.4: Unregistered tenant+domain returns error ---

    [Fact]
    public async Task PostCommands_ActorThrowsDomainServiceNotFoundException_Returns500()
    {
        // Arrange - simulate DomainServiceNotFoundException at actor level
        factory.Router.FakeActor = new FakeAggregateActor
        {
            ConfiguredException = new Server.DomainServices.DomainServiceNotFoundException("unknown-tenant", "unknown-domain"),
        };
        HttpClient client = CreateAuthenticatedClient(["unknown-tenant"]);
        object request = CreateCommandRequest(tenant: "unknown-tenant", domain: "unknown-domain");

        try
        {
            // Act
            HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

            // Assert - domain service not found should result in server error
            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }
        finally
        {
            factory.Router.FakeActor = new FakeAggregateActor();
        }
    }

    // --- Task 4.3: Dynamic tenant/domain addition without restart ---

    [Fact]
    public async Task PostCommands_DynamicTenantAddition_SucceedsAfterReconfiguration()
    {
        // Arrange - initially, new tenant's domain service is not registered (AC #3, NFR20)
        factory.Router.FakeActor = new FakeAggregateActor
        {
            ConfiguredException = new Server.DomainServices.DomainServiceNotFoundException("dynamic-tenant", "orders"),
        };
        HttpClient client = CreateAuthenticatedClient(["dynamic-tenant"]);
        object command = CreateCommandRequest(tenant: "dynamic-tenant", domain: "orders", aggregateId: "order-001");

        // Act - first request fails (domain service not registered)
        HttpResponseMessage response1 = await client.PostAsJsonAsync("/api/v1/commands", command);

        // Simulate dynamic registration by reconfiguring (no system restart)
        factory.Router.FakeActor = new FakeAggregateActor();

        // Act - second request succeeds (registration now available)
        HttpResponseMessage response2 = await client.PostAsJsonAsync("/api/v1/commands", command);

        // Assert - system processes new tenant without restart
        response1.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response2.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    // --- Task 4.5: Actor state isolation between tenants ---

    [Fact]
    public async Task PostCommands_MultipleTenantsSameDomainAndAggregate_CommandEnvelopesHaveDistinctActorIds()
    {
        // Arrange
        var fakeActor = new FakeAggregateActor();
        factory.Router.FakeActor = fakeActor;

        // Same aggregate ID but different tenants
        HttpClient clientA = CreateAuthenticatedClient(["tenant-a"]);
        HttpClient clientB = CreateAuthenticatedClient(["tenant-b"]);
        HttpClient clientC = CreateAuthenticatedClient(["tenant-c"]);

        object commandA = CreateCommandRequest(tenant: "tenant-a", domain: "orders", aggregateId: "shared-agg");
        object commandB = CreateCommandRequest(tenant: "tenant-b", domain: "orders", aggregateId: "shared-agg");
        object commandC = CreateCommandRequest(tenant: "tenant-c", domain: "orders", aggregateId: "shared-agg");

        // Act
        await clientA.PostAsJsonAsync("/api/v1/commands", commandA);
        await clientB.PostAsJsonAsync("/api/v1/commands", commandB);
        await clientC.PostAsJsonAsync("/api/v1/commands", commandC);

        // Assert - all three commands received with unique actor IDs
        fakeActor.ReceivedCommands.Count.ShouldBe(3);

        string[] actorIds = fakeActor.ReceivedCommands
            .Select(c => c.AggregateIdentity.ActorId)
            .ToArray();

        actorIds.Distinct().Count().ShouldBe(3);
        actorIds.ShouldContain("tenant-a:orders:shared-agg");
        actorIds.ShouldContain("tenant-b:orders:shared-agg");
        actorIds.ShouldContain("tenant-c:orders:shared-agg");
    }
}

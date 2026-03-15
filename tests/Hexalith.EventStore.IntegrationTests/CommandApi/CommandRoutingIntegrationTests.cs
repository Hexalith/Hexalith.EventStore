extern alias commandapi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Hexalith.EventStore.IntegrationTests.Helpers;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.CommandApi;

public class CommandRoutingIntegrationTests(JwtAuthenticatedWebApplicationFactory factory)
    : IClassFixture<JwtAuthenticatedWebApplicationFactory> {
    private static object CreateCommandRequest(
        string tenant = "test-tenant",
        string domain = "test-domain",
        string aggregateId = "agg-001",
        string commandType = "CreateOrder") => new {
            messageId = Guid.NewGuid().ToString(),
            tenant,
            domain,
            aggregateId,
            commandType,
            payload = new { amount = 100 },
        };

    private HttpClient CreateAuthenticatedClient() {
        string token = TestJwtTokenGenerator.GenerateToken(
            tenants: ["test-tenant"],
            permissions: ["commands:*"]);
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task PostCommands_ValidCommand_RoutesToActor() {
        // Arrange
        var fakeActor = new FakeAggregateActor();
        factory.Router.FakeActor = fakeActor;
        HttpClient client = CreateAuthenticatedClient();
        object request = CreateCommandRequest();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        fakeActor.ReceivedCommands.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task PostCommands_ValidCommand_Returns202Accepted() {
        // Arrange
        factory.Router.FakeActor = new FakeAggregateActor();
        HttpClient client = CreateAuthenticatedClient();
        object request = CreateCommandRequest();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task PostCommands_ValidCommand_ActorReceivesCorrectFields() {
        // Arrange
        var fakeActor = new FakeAggregateActor();
        factory.Router.FakeActor = fakeActor;
        HttpClient client = CreateAuthenticatedClient();
        object request = CreateCommandRequest(
            tenant: "test-tenant",
            domain: "test-domain",
            aggregateId: "agg-001",
            commandType: "PlaceOrder");

        // Act
        _ = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        fakeActor.ReceivedCommands.ShouldNotBeEmpty();
        Contracts.Commands.CommandEnvelope received = fakeActor.ReceivedCommands.First();
        received.TenantId.ShouldBe("test-tenant");
        received.Domain.ShouldBe("test-domain");
        received.AggregateId.ShouldBe("agg-001");
        received.CommandType.ShouldBe("PlaceOrder");
    }

    [Fact]
    public async Task PostCommands_ActorThrows_Returns500ProblemDetails() {
        // Arrange
        factory.Router.FakeActor = new FakeAggregateActor {
            ConfiguredException = new InvalidOperationException("Actor processing failed"),
        };
        HttpClient client = CreateAuthenticatedClient();
        object request = CreateCommandRequest();

        try {
            // Act
            HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            string content = await response.Content.ReadAsStringAsync();
            content.ShouldContain("Internal Server Error");
        }
        finally {
            // Reset actor to prevent polluting other tests
            factory.Router.FakeActor = new FakeAggregateActor();
        }
    }

    [Fact]
    public async Task PostCommands_MultipleSubmissions_AllReturnAccepted() {
        // Arrange -- each HTTP request generates a unique causationId in the pipeline,
        // so this verifies the API handles multiple distinct commands correctly.
        // Note: True idempotency (duplicate causationId detection) is verified in
        // AggregateActorTests unit tests, since ToCommandEnvelope always generates unique IDs.
        var fakeActor = new FakeAggregateActor();
        factory.Router.FakeActor = fakeActor;
        HttpClient client = CreateAuthenticatedClient();
        object request = CreateCommandRequest();

        // Act -- submit same payload twice (each gets a unique causationId from pipeline)
        HttpResponseMessage response1 = await client.PostAsJsonAsync("/api/v1/commands", request);
        HttpResponseMessage response2 = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert -- both succeed (202 Accepted), both reach the actor as distinct commands
        response1.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response2.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        fakeActor.ReceivedCommands.Count.ShouldBe(2);
    }

    [Fact]
    public async Task PostCommands_SameCorrelationIdDifferentCausationId_BothProcessed() {
        // Arrange -- each request gets a unique MessageId, which becomes CausationId in the routed envelope.
        var fakeActor = new FakeAggregateActor { SimulateIdempotency = true };
        factory.Router.FakeActor = fakeActor;
        HttpClient client = CreateAuthenticatedClient();
        object request1 = CreateCommandRequest();
        object request2 = CreateCommandRequest();

        // Act
        HttpResponseMessage response1 = await client.PostAsJsonAsync("/api/v1/commands", request1);
        HttpResponseMessage response2 = await client.PostAsJsonAsync("/api/v1/commands", request2);

        // Assert -- both processed (different causation IDs, not blocked by idempotency)
        response1.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response2.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        fakeActor.ReceivedCommands.Count.ShouldBe(2);
        fakeActor.ProcessedCount.ShouldBe(2);
    }

    [Fact]
    public async Task PostCommands_ValidCommand_ActorReceivesCommandEnvelope() {
        // Arrange
        var fakeActor = new FakeAggregateActor();
        factory.Router.FakeActor = fakeActor;
        HttpClient client = CreateAuthenticatedClient();
        object request = CreateCommandRequest();

        // Act
        _ = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        fakeActor.ReceivedCommands.ShouldNotBeEmpty();
        Contracts.Commands.CommandEnvelope envelope = fakeActor.ReceivedCommands.First();
        envelope.UserId.ShouldBe("test-user");
        envelope.CausationId.ShouldBe(envelope.MessageId, "CausationId should be the MessageId of the originating SubmitCommand");
    }
}

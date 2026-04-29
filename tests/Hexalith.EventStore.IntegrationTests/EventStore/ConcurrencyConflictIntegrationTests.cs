extern alias eventstore;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.IntegrationTests.Helpers;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;

using MediatR;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Shouldly;

using EventStoreProgram = eventstore::Program;

namespace Hexalith.EventStore.IntegrationTests.EventStore;

public class ConcurrencyConflictIntegrationTests(JwtAuthenticatedWebApplicationFactory factory)
    : IClassFixture<JwtAuthenticatedWebApplicationFactory> {
    private const string ConflictTriggerCommandType = "SimulateConcurrencyConflict";

    [Fact]
    public async Task PostCommands_ConcurrencyConflict_Returns409ProblemDetails() {
        // Arrange
        using WebApplicationFactory<EventStoreProgram> customFactory = CreateConflictFactory();
        HttpClient client = CreateAuthenticatedClient(customFactory);
        object request = CreateConflictRequest();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(409);
        body.GetProperty("title").GetString().ShouldBe("Conflict");
        body.GetProperty("type").GetString()!.ShouldBe("https://hexalith.io/problems/concurrency-conflict");
        body.GetProperty("instance").GetString().ShouldBe("/api/v1/commands");
    }

    [Fact]
    public async Task PostCommands_ConcurrencyConflict_ProblemDetailsIncludesCorrelationId() {
        // Arrange
        using WebApplicationFactory<EventStoreProgram> customFactory = CreateConflictFactory();
        HttpClient client = CreateAuthenticatedClient(customFactory);
        object request = CreateConflictRequest();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("correlationId", out JsonElement correlationId).ShouldBeTrue();
        correlationId.GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostCommands_ConcurrencyConflict_ProblemDetailsExcludesAggregateId() {
        // Arrange
        using WebApplicationFactory<EventStoreProgram> customFactory = CreateConflictFactory();
        HttpClient client = CreateAuthenticatedClient(customFactory);
        object request = CreateConflictRequest(aggregateId: "order-789");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert — UX-DR10: aggregateId is intentionally omitted from the 409 client response.
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("aggregateId", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task PostCommands_ConcurrencyConflict_ProblemDetailsIncludesDetailMessage() {
        // Arrange
        using WebApplicationFactory<EventStoreProgram> customFactory = CreateConflictFactory();
        HttpClient client = CreateAuthenticatedClient(customFactory);
        object request = CreateConflictRequest(aggregateId: "order-detail-test");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert — UX-DR6: generic, safe detail without event-sourcing terminology or aggregate context.
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        string detail = body.GetProperty("detail").GetString()!;
        detail.ShouldContain("concurrency conflict");
        detail.ShouldContain("retry");

        // UX-DR6: forbid event-sourcing / internal-state terminology in the 409 detail.
        detail.ShouldNotContain("aggregate", Case.Insensitive);
        detail.ShouldNotContain("between read and write", Case.Insensitive);
        detail.ShouldNotContain("event stream", Case.Insensitive);
        detail.ShouldNotContain("actor", Case.Insensitive);
    }

    [Fact]
    public async Task PostCommands_ConcurrencyConflict_StatusUpdatedToRejected() {
        // Arrange
        var statusStore = new Testing.Fakes.InMemoryCommandStatusStore();
        using WebApplicationFactory<EventStoreProgram> customFactory = CreateConflictFactory(statusStore);
        HttpClient client = CreateAuthenticatedClient(customFactory);
        object request = CreateConflictRequest();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // The status store should have a Rejected entry
        IReadOnlyDictionary<string, (CommandStatusRecord Record, DateTimeOffset Expiry)> allStatuses = statusStore.GetAllStatuses();
        CommandStatusRecord? rejectedStatus = allStatuses.Values
            .Select(entry => entry.Record)
            .FirstOrDefault(s => s.Status == CommandStatus.Rejected);
        _ = rejectedStatus.ShouldNotBeNull();
        rejectedStatus.FailureReason.ShouldBe("ConcurrencyConflict");
    }

    [Fact]
    public async Task PostCommands_ConcurrencyConflict_RetrySucceeds() {
        // Arrange
        using WebApplicationFactory<EventStoreProgram> customFactory = CreateConflictFactory();
        HttpClient client = CreateAuthenticatedClient(customFactory);

        // First request: trigger conflict
        object conflictRequest = CreateConflictRequest();
        HttpResponseMessage conflictResponse = await client.PostAsJsonAsync("/api/v1/commands", conflictRequest);
        conflictResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Second request: normal command (simulates retry with non-conflict command type)
        var retryRequest = new {
            messageId = Guid.NewGuid().ToString(),
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "order-123",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage retryResponse = await client.PostAsJsonAsync("/api/v1/commands", retryRequest);

        // Assert - retry succeeds with 202 Accepted
        retryResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task PostCommands_ConcurrencyConflict_NoAuthentication_Returns401() {
        // Arrange - no auth header
        using WebApplicationFactory<EventStoreProgram> customFactory = CreateConflictFactory();
        HttpClient client = customFactory.CreateClient();
        object request = CreateConflictRequest();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostCommands_ConcurrencyConflict_ResponseContentType() {
        // Arrange
        using WebApplicationFactory<EventStoreProgram> customFactory = CreateConflictFactory();
        HttpClient client = CreateAuthenticatedClient(customFactory);
        object request = CreateConflictRequest();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.Content.Headers.ContentType!.MediaType!.ShouldContain("problem+json");
    }

    [Fact]
    public async Task PostCommands_ConcurrencyConflict_ProblemDetailsExcludesTenantId() {
        // Arrange
        using WebApplicationFactory<EventStoreProgram> customFactory = CreateConflictFactory();
        HttpClient client = CreateAuthenticatedClient(customFactory);
        object request = CreateConflictRequest();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert — UX-DR10: tenantId is intentionally omitted from the 409 client response.
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("tenantId", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task PostCommands_ConcurrencyConflict_Returns409NotFallback500() {
        // Arrange - this test verifies handler chain ordering is correct.
        // If ConcurrencyConflictExceptionHandler were registered after GlobalExceptionHandler,
        // the response would be 500 instead of 409.
        using WebApplicationFactory<EventStoreProgram> customFactory = CreateConflictFactory();
        HttpClient client = CreateAuthenticatedClient(customFactory);
        object request = CreateConflictRequest();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert - must be 409, not 500
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task PostCommands_ConcurrencyConflict_ResponseIncludesRetryAfterHeader() {
        // Arrange
        using WebApplicationFactory<EventStoreProgram> customFactory = CreateConflictFactory();
        HttpClient client = CreateAuthenticatedClient(customFactory);
        object request = CreateConflictRequest();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.Headers.TryGetValues("Retry-After", out System.Collections.Generic.IEnumerable<string>? values).ShouldBeTrue();
        values!.First().ShouldBe("1");
    }

    private static object CreateConflictRequest(string aggregateId = "order-123")
        => new {
            messageId = Guid.NewGuid().ToString(),
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId,
            commandType = ConflictTriggerCommandType,
            payload = new { amount = 100 },
        };

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<EventStoreProgram> factory) {
        HttpClient client = factory.CreateClient();
        string token = TestJwtTokenGenerator.GenerateToken(tenants: ["test-tenant"], domains: ["test-domain"]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private WebApplicationFactory<EventStoreProgram> CreateConflictFactory(
        Testing.Fakes.InMemoryCommandStatusStore? statusStore = null) {
        statusStore ??= new Testing.Fakes.InMemoryCommandStatusStore();

        return factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => {
            // Replace status store with test instance
            ServiceDescriptor? statusDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(ICommandStatusStore));
            if (statusDescriptor is not null) {
                _ = services.Remove(statusDescriptor);
            }

            _ = services.AddSingleton<ICommandStatusStore>(statusStore);

            // Replace command handler with concurrency-conflict-simulating handler
            ServiceDescriptor? handlerDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(IRequestHandler<SubmitCommand, SubmitCommandResult>));
            if (handlerDescriptor is not null) {
                _ = services.Remove(handlerDescriptor);
            }

            _ = services.AddTransient<IRequestHandler<SubmitCommand, SubmitCommandResult>>(sp =>
                new ConcurrencyConflictSimulatingHandler(
                    sp.GetRequiredService<ICommandStatusStore>(),
                    sp.GetRequiredService<ICommandArchiveStore>(),
                    sp.GetRequiredService<ICommandRouter>(),
                    sp.GetRequiredService<ILogger<SubmitCommandHandler>>()));
        }));
    }

    /// <summary>
    /// Test handler that throws ConcurrencyConflictException for a specific command type,
    /// while passing through normal commands to the real handler logic.
    /// </summary>
    private sealed class ConcurrencyConflictSimulatingHandler(
        ICommandStatusStore statusStore,
        ICommandArchiveStore archiveStore,
        ICommandRouter commandRouter,
        ILogger<SubmitCommandHandler> logger) : IRequestHandler<SubmitCommand, SubmitCommandResult> {
        private readonly SubmitCommandHandler _inner = new(statusStore, archiveStore, commandRouter, new InMemoryBackpressureTracker(Microsoft.Extensions.Options.Options.Create(new Hexalith.EventStore.Server.Configuration.BackpressureOptions())), logger);

        public async Task<SubmitCommandResult> Handle(SubmitCommand request, CancellationToken cancellationToken) {
            if (request.CommandType == ConflictTriggerCommandType) {
                // Write Received status first (mimics normal flow up to conflict point)
                _ = await _inner.Handle(request, cancellationToken).ConfigureAwait(false);
                throw new ConcurrencyConflictException(
                    request.CorrelationId,
                    request.AggregateId,
                    request.Tenant);
            }

            return await _inner.Handle(request, cancellationToken).ConfigureAwait(false);
        }
    }
}

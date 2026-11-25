using System.Text.Json;
using System.Text.Json.Nodes;

using Dapr;

using FluentValidation;

using Grpc.Core;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.OpenApi;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Validation;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using BackpressureExceededException = Hexalith.EventStore.Server.Actors.BackpressureExceededException;

namespace Hexalith.EventStore.Server.Tests.OpenApi;

public class ErrorReferenceHandlerParityTests(OpenApiWebApplicationFactory factory)
    : IClassFixture<OpenApiWebApplicationFactory>
{
    private const string CorrelationId = "01JAXYZ1234567890ABCDEFGH";

    [Fact]
    public async Task RegisteredHandlers_ProduceDocumentedProblemDetails()
    {
        using JsonDocument payload = JsonDocument.Parse("{}");
        var invalidCommand = new SubmitCommandRequest("-invalid", "tenant-a", "orders", "order-1", "CreateOrder", payload.RootElement);
        var validation = new SubmitCommandRequestValidator().Validate(invalidCommand);
        (Type Handler, string Slug, Exception Error)[] scenarios =
        [
            (typeof(ValidationExceptionHandler), "validation-error", new ValidationException(validation.Errors)),
            (typeof(AuthorizationExceptionHandler), "forbidden",
                new CommandAuthorizationException("tenant-b", "orders", "CreateOrder", "Access denied for tenant 'tenant-b'.")),
            (typeof(AuthorizationServiceUnavailableHandler), "service-unavailable",
                new AuthorizationServiceUnavailableException("authorization", "tenant-a", "unavailable", new InvalidOperationException())),
            (typeof(BackpressureExceptionHandler), "backpressure-exceeded",
                new BackpressureExceededException(CorrelationId, "tenant-a", "orders", "order-1", 100, 100)),
            (typeof(IdempotencyConflictExceptionHandler), "idempotency-conflict", new IdempotencyConflictException(CorrelationId)),
            (typeof(IdempotencyKeyExpiredExceptionHandler), "idempotency-key-expired", new IdempotencyKeyExpiredException(CorrelationId)),
            (typeof(IdempotencyAdmissionFailureExceptionHandler), "idempotency-admission-failure",
                new IdempotencyAdmissionFailureException(CorrelationId, "idempotency_admission_unavailable", "service_unavailable",
                    true, "retry_later", 503, "Idempotency admission is temporarily unavailable. Retry later.")),
            (typeof(IdempotencyAdmissionFailureExceptionHandler), "idempotency-admission-failure",
                new IdempotencyAdmissionFailureException(CorrelationId, "idempotency_admission_corrupt", "idempotency_admission_corrupt",
                    false, "contact_support", 503, "Idempotency admission state cannot be safely interpreted.")),
            (typeof(IdempotencyAdmissionFailureExceptionHandler), "idempotency-admission-failure",
                new IdempotencyAdmissionFailureException(CorrelationId, "idempotency_key_collision", "idempotency_admission_corrupt",
                    false, "contact_support", 503, "Idempotency admission identity cannot be safely verified.")),
            (typeof(IdempotencyAdmissionFailureExceptionHandler), "idempotency-admission-failure",
                new IdempotencyAdmissionFailureException(CorrelationId, "idempotency_unsafe_legacy_state", "idempotency_admission_corrupt",
                    false, "contact_support", 503, "Legacy idempotency state is not safe to migrate automatically.")),
            (typeof(IdempotencyAdmissionFailureExceptionHandler), "idempotency-admission-failure",
                new IdempotencyAdmissionFailureException(CorrelationId, "idempotency_reconciliation_unavailable", "service_unavailable",
                    true, "retry_later", 503, "The original command status could not be refreshed. Retry later.")),
            (typeof(IdempotencyAdmissionFailureExceptionHandler), "idempotency-admission-failure",
                new IdempotencyAdmissionFailureException(CorrelationId, "owner_admission_deferred", "service_unavailable",
                    true, "retry_later", 503, "The owner could not establish current command authority.")),
            (typeof(IdempotencyAdmissionFailureExceptionHandler), "idempotency-admission-failure",
                new IdempotencyAdmissionFailureException(CorrelationId, "idempotency_outcome_unknown", "idempotency_outcome_unknown",
                    true, "poll_status_then_retry", 409, "The original mutation outcome remains unknown. Poll status before retrying.")),
            (typeof(IdempotencyAdmissionFailureExceptionHandler), "idempotency-admission-failure",
                new IdempotencyAdmissionFailureException(CorrelationId, "idempotency_admission_rejected", "validation_failed",
                    false, "correct_request", 400, "Idempotency admission rejected the command. Correct the request and submit a new key.")),
            (typeof(CommandIdentityConflictExceptionHandler), "command-identity-conflict",
                new CommandIdentityConflictException(CorrelationId, CorrelationId, "tenant-a")),
            (typeof(ConcurrencyConflictExceptionHandler), "concurrency-conflict", new ConcurrencyConflictException(CorrelationId, "order-1", "tenant-a")),
            (typeof(DomainCommandRejectedExceptionHandler), "domain-rejections", new DomainCommandRejectedException(
                CorrelationId, "tenant-a", "Hexalith.Parties.Contracts.Events.PartyCommandValidationRejected", "rejected")),
            (typeof(QueryNotFoundExceptionHandler), "not-found", new QueryNotFoundException("tenant-a", "orders", "order-1", "GetOrder")),
            (typeof(QueryExecutionFailedExceptionHandler), "not-implemented", new QueryExecutionFailedException(
                CorrelationId, "tenant-a", "orders", "order-1", "GetOrder", 501, "The requested operation is not yet implemented.")),
            (typeof(QueryExecutionFailedExceptionHandler), "bad-request", new QueryExecutionFailedException(
                CorrelationId, "tenant-a", "orders", "order-1", "GetOrder", 400, "The supplied page is invalid.", QueryProblemReasonCodes.InvalidPage)
                { AdapterFailureReason = QueryAdapterFailureReason.InvalidPage }),
            (typeof(QueryExecutionFailedExceptionHandler), "forbidden", new QueryExecutionFailedException(
                CorrelationId, "tenant-b", "orders", "order-1", "GetOrder", 403, QueryAdapterFailureReason.Forbidden)),
            (typeof(QueryExecutionFailedExceptionHandler), "internal-server-error", new QueryExecutionFailedException(
                CorrelationId, "tenant-a", "orders", "order-1", "GetOrder", 500, "Projection query execution failed.")),
            (typeof(DaprSidecarUnavailableHandler), "service-unavailable",
                new DaprException("unavailable", new RpcException(new Status(StatusCode.Unavailable, "unavailable")))),
            (typeof(RouteCatalogUnavailableExceptionHandler), "service-unavailable", new RouteCatalogUnavailableException("unavailable")),
            (typeof(GlobalExceptionHandler), "internal-server-error", new InvalidOperationException()),
        ];

        using IServiceScope scope = factory.Services.CreateScope();
        IExceptionHandler[] handlers = scope.ServiceProvider.GetServices<IExceptionHandler>().ToArray();
        handlers.Select(handler => handler.GetType()).Distinct()
            .ShouldBe(scenarios.Select(scenario => scenario.Handler).Distinct(), ignoreOrder: true);

        // The primary bad-request and not-found samples come from request parsing and command replay.
        var examples = ErrorReferenceEndpoints.ErrorModels.ToDictionary(
            model => model.Slug,
            model => (model.Slug is "bad-request" or "not-found"
                    ? model.AdditionalExamples ?? []
                    : new[] { model.ExampleJson }.Concat(model.AdditionalExamples ?? []))
                .Select(example => JsonNode.Parse(example)).ToList());

        foreach ((Type handler, string slug, Exception error) in scenarios)
        {
            using var body = new MemoryStream();
            var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            context.Request.Path = "/api/v1/commands";
            context.Items[CorrelationIdMiddleware.HttpContextKey] = CorrelationId;
            context.Items["RequestTenantId"] = "tenant-a";
            context.Response.Body = body;

            Type? handlingType = null;
            foreach (IExceptionHandler registeredHandler in handlers)
            {
                if (await registeredHandler.TryHandleAsync(context, error, TestContext.Current.CancellationToken))
                {
                    handlingType = registeredHandler.GetType();
                    break;
                }
            }
            handlingType.ShouldBe(handler, $"Handler selected for {error.GetType().Name}");
            context.Response.ContentType.ShouldStartWith("application/problem+json");
            body.Position = 0;
            JsonObject actual = (await JsonNode.ParseAsync(body, cancellationToken: TestContext.Current.CancellationToken))!.AsObject();
            actual["status"]!.GetValue<int>().ShouldBe(context.Response.StatusCode, handler.Name);
            _ = actual.Remove("instance"); // The request path differs on each deployed endpoint.

            List<JsonNode?> candidates = examples[slug];
            int match = candidates.FindIndex(candidate => JsonNode.DeepEquals(candidate, actual));
            match.ShouldBeGreaterThanOrEqualTo(0, $"{handler.Name} returned an undocumented {slug} response: {actual}");
            candidates.RemoveAt(match);
        }

        foreach (string slug in scenarios.Select(scenario => scenario.Slug).Distinct())
        {
            examples[slug].ShouldBeEmpty($"All handler examples for {slug} need a matching response.");
        }

        foreach (ErrorReferenceEndpoints.ErrorReferenceModel model in ErrorReferenceEndpoints.ErrorModels)
        {
            JsonNode.Parse(model.ExampleJson)!["status"]!.GetValue<int>().ShouldBe(model.StatusCode, model.Slug);
        }
    }
}

using System.Text.Json;

using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Client.Subscriptions;

using Hexalith.EventStore.Operations.Security;
using Hexalith.EventStore.Operations.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using System.Globalization;

using Dapr;
using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;
using Hexalith.EventStore.Operations.Actors;
using Hexalith.EventStore.Operations.Capture;
using Hexalith.EventStore.Operations.Configuration;
using Hexalith.EventStore.Operations.Models;
using Hexalith.EventStore.Operations.Telemetry;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Operations.Endpoints;

/// <summary>
/// Maps raw subscriber dead-letter capture and caller-scoped operator endpoints.
/// </summary>
internal static class DeadLetterOperationsEndpointExtensions
{
    private const string InternalRoute = "/internal/dead-letters";

    /// <summary>Maps the operations workload endpoints.</summary>
    internal static IEndpointRouteBuilder MapDeadLetterOperations(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        EventStoreOperationsOptions options = endpoints.ServiceProvider
            .GetRequiredService<IOptions<EventStoreOperationsOptions>>()
            .Value;

        _ = endpoints.MapPost(options.CaptureRoute, CaptureAsync)
            .WithTopic(new TopicOptions
            {
                PubsubName = options.PubSubName,
                Name = options.TopicName,
            }).RequireEventStoreChannel();
        if (options.IsolatedSubscriberRecovery && endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            _ = endpoints.MapPost(InternalRoute + "/subscriber-delivery", ObserveSubscriberDeliveryAsync)
                .RequireEventStoreChannel();
        }

        MapOperator(InternalRoute + "/count", "GET", (Delegate)CountAsync, "count");
        MapOperator(InternalRoute, "GET", (Delegate)ListAsync, "list");
        MapOperator(InternalRoute + "/retry", "POST", (Delegate)RetryAsync, "retry");
        MapOperator(InternalRoute + "/skip", "POST", (Delegate)SkipAsync, "skip");
        MapOperator(InternalRoute + "/archive", "POST", (Delegate)ArchiveAsync, "archive");
        return endpoints;

        void MapOperator(string path, string verb, Delegate handler, string operation)
        {
            var endpoint = endpoints.MapMethods(path, [verb], handler)
                .RequireAuthorization(OperationsHttpSecurityExtensions.PolicyPrefix + operation,
                    EventStoreHttpHostSecurityExtensions.HumanPolicy);
            endpoint.Finally(builder => builder.Metadata.Add(new DevelopmentOperationsHttpWitness(builder.RequestDelegate, handler.Method, operation)));
        }
    }

    private static Task<IResult> ArchiveAsync(
        HttpRequest request,
        DeadLetterActionRequest action,
        IActorProxyFactory actorProxyFactory,
        IOptions<EventStoreOperationsOptions> options)
        => ActionAsync(request, action, actorProxyFactory, options.Value, static (actor, value) => actor.ArchiveAsync(value));

    /// <summary>Captures one raw dead-letter delivery.</summary>
    internal static async Task<IResult> CaptureAsync(
        HttpRequest request,
        IActorProxyFactory actorProxyFactory,
        IOptions<EventStoreOperationsOptions> options,
        TimeProvider timeProvider,
        EventStoreOperationsTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        EventStoreOperationsOptions value = options.Value;
        if (request.ContentLength > value.MaxBodyBytes)
        {
            telemetry.Capture(value.TopicName, "oversize");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        (byte[] body, bool tooLarge) = await ReadBoundedBodyAsync(
            request,
            value.MaxBodyBytes).ConfigureAwait(false);
        if (tooLarge || body.Length == 0)
        {
            telemetry.Capture(value.TopicName, tooLarge ? "oversize" : "empty-body");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        (DeadLetterSafeIdentity identity, string hash) = DeadLetterEnvelopeParser.Parse(body);
        IDeadLetterDrainActor actor = Actor(actorProxyFactory, value);
        DeadLetterCaptureResult result;
        try
        {
            result = await actor
                .CaptureAsync(new DeadLetterCaptureRequest(identity, value.TopicName, body, hash, timeProvider.GetUtcNow()))
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            telemetry.Capture(value.TopicName, "capture-failed");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        return result.Outcome switch
        {
            DeadLetterCaptureOutcome.Captured => Results.Ok(),
            DeadLetterCaptureOutcome.Duplicate => Results.Ok(),
            DeadLetterCaptureOutcome.HashConflict or DeadLetterCaptureOutcome.Unretainable
                => Results.StatusCode(StatusCodes.Status503ServiceUnavailable),
            _ => UnknownCaptureOutcome(telemetry, value.TopicName),
        };
    }

    private static async Task<IResult> ObserveSubscriberDeliveryAsync(
        HttpRequest request,
        IActorProxyFactory actorProxyFactory,
        IOptions<EventStoreOperationsOptions> options,
        TimeProvider timeProvider)
    {
        EventStoreOperationsOptions value = options.Value;
        var callers = request.Headers[EventStoreWorkloadAuthenticationDefaults.DaprCallerHeaderName];
        if (callers.Count != 1 || callers[0] != value.ReplayAppId)
        {
            return OpaqueForbidden();
        }

        // Account for the outer observation wrapper and its bounded metadata.
        int limit = value.MaxBodyBytes + 8192;
        if (request.ContentLength > limit)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        (byte[] bytes, bool tooLarge) = await ReadBoundedBodyAsync(request, limit).ConfigureAwait(false);
        if (tooLarge)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        SubscriberDeliveryObservation? observation;
        try
        {
            observation = JsonSerializer.Deserialize<SubscriberDeliveryObservation>(bytes, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        if (observation?.Delivery is not { } delivery
            || delivery.SubscriberAppId is { } declaredAppId && declaredAppId != value.ReplayAppId)
        {
            return Results.BadRequest();
        }

        try
        {
            // The envelope contains JSON-only wire types. Use the actor's JSON invocation
            // for this operation while retaining the existing typed remoting contracts.
            ActorProxy proxy = actorProxyFactory.Create(new ActorId(value.TopicName), DeadLetterDrainActor.ActorTypeName);
            EventStoreSubscriberPoisonOutcome result = await proxy
                .InvokeMethodAsync<SubscriberDeliveryObservation, EventStoreSubscriberPoisonOutcome>(
                    nameof(IDeadLetterDrainActor.ObserveSubscriberDeliveryAsync), observation with
                {
                    Delivery = delivery with { SubscriberAppId = value.ReplayAppId },
                    ObservedAtUtc = timeProvider.GetUtcNow(),
                }, request.HttpContext.RequestAborted).ConfigureAwait(false);
            return Results.Ok(result);
        }
        catch (Exception) when (!request.HttpContext.RequestAborted.IsCancellationRequested)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> CountAsync(
        HttpRequest request,
        IActorProxyFactory actorProxyFactory,
        IOptions<EventStoreOperationsOptions> options,
        TimeProvider clock,
        string? tenantId = null,
        bool detailed = false)
    {
        if (!CanReadScope(request, tenantId))
        {
            return OpaqueForbidden();
        }

        try
        {
            DeadLetterListResult result = await Actor(actorProxyFactory, options.Value)
                .ListAsync(new DeadLetterListRequest(tenantId, 1, 0))
                .WaitAsync(request.HttpContext.RequestAborted).ConfigureAwait(false);
            if (!detailed)
            {
                return Results.Ok(result.TotalCount);
            }

            TimeSpan? oldestAge = result.OldestFailureAt is { } failureAt
                ? TimeSpan.FromTicks(Math.Max(0, (clock.GetUtcNow() - failureAt).Ticks)) : null;
            return Results.Ok(new DeadLetterSummary(result.TotalCount, oldestAge));
        }
        catch (Exception) when (!request.HttpContext.RequestAborted.IsCancellationRequested)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> ListAsync(
        HttpRequest request,
        IActorProxyFactory actorProxyFactory,
        IOptions<EventStoreOperationsOptions> options,
        IQueryCursorCodec cursorCodec,
        string? tenantId,
        int count = 100,
        string? continuationToken = null)
    {
        if (!CanReadScope(request, tenantId))
        {
            return OpaqueForbidden();
        }

        const string cursorKind = "dead-letters:list";
        string scope = QueryCursorScope.Create()
            .Add("subject", request.HttpContext.User.FindFirst("sub")?.Value)
            .Add("tenant", tenantId)
            .Add("topic", options.Value.TopicName).Build();
        if (!cursorCodec.TryDecode(continuationToken, cursorKind, scope, out string? position, out _))
        {
            return Results.BadRequest();
        }

        int offset = 0;
        if (position is not null
            && (!int.TryParse(position, NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset < 0))
        {
            return Results.BadRequest();
        }

        try
        {
            int boundedCount = Math.Clamp(count, 1, options.Value.MaxListItems);
            DeadLetterListResult result = await Actor(actorProxyFactory, options.Value)
                .ListAsync(new DeadLetterListRequest(tenantId, boundedCount, offset))
                .WaitAsync(request.HttpContext.RequestAborted).ConfigureAwait(false);
            DeadLetterEntry[] entries = [.. result.Items.Select(ToAdminEntry)];
            string? nextToken = result.NextOffset is { } nextOffset
                ? cursorCodec.Encode(cursorKind, scope, nextOffset.ToString(CultureInfo.InvariantCulture))
                : null;
            return Results.Ok(new PagedResult<DeadLetterEntry>(entries, result.TotalCount, nextToken));
        }
        catch (Exception) when (!request.HttpContext.RequestAborted.IsCancellationRequested)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static Task<IResult> RetryAsync(
        HttpRequest request,
        DeadLetterActionRequest action,
        IActorProxyFactory actorProxyFactory,
        IOptions<EventStoreOperationsOptions> options)
        => ActionAsync(request, action, actorProxyFactory, options.Value, static (actor, value) => actor.RetryAsync(value));

    private static Task<IResult> SkipAsync(
        HttpRequest request,
        DeadLetterActionRequest action,
        IActorProxyFactory actorProxyFactory,
        IOptions<EventStoreOperationsOptions> options)
        => ActionAsync(request, action, actorProxyFactory, options.Value, static (actor, value) => actor.SkipAsync(value));

    private static IResult UnknownCaptureOutcome(EventStoreOperationsTelemetry telemetry, string topic)
    {
        telemetry.Capture(topic, "capture-failed");
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }

    private static IDeadLetterDrainActor Actor(IActorProxyFactory factory, EventStoreOperationsOptions options)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return factory.CreateActorProxy<IDeadLetterDrainActor>(
            new ActorId(options.TopicName),
            DeadLetterDrainActor.ActorTypeName);
    }

    private static async Task<IResult> ActionAsync(
        HttpRequest request,
        DeadLetterActionRequest action,
        IActorProxyFactory actorProxyFactory,
        EventStoreOperationsOptions options,
        Func<IDeadLetterDrainActor, DeadLetterActionRequest, Task<DeadLetterActorActionResult>> invoke)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!EventStoreHttpHostSecurityExtensions.IsHuman(request.HttpContext.User)
            || !OperationsHttpSecurityExtensions.CanAccessTenant(request.HttpContext.User, action.TenantId))
        {
            return OpaqueForbidden();
        }

        if (!IsValidAction(action, options))
        {
            return Results.UnprocessableEntity(new AdminOperationResult(
                false,
                "dead-letter-action",
                "The requested operation is invalid.",
                "InvalidOperation"));
        }

        if (!options.IsolatedSubscriberRecovery
            || request.HttpContext.RequestServices.GetService<IHostEnvironment>()?.IsDevelopment() != true)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        // Validate before actor lookup and persist only the independently authenticated subject.
        string? subject = request.HttpContext.User.FindFirst("sub")?.Value;
        if (!DeadLetterSafeIdentity.IsValidValue(subject))
        {
            return OpaqueForbidden();
        }

        DeadLetterActorActionResult result;
        try
        {
            result = await invoke(Actor(actorProxyFactory, options), action with { RequestedBy = subject })
                .WaitAsync(request.HttpContext.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception) when (!request.HttpContext.RequestAborted.IsCancellationRequested)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (result.OperationId is { } operationId)
        {
            var outcome = new AdminOperationResult(result.Success && result.State == "completed", operationId,
                "The recovery action state was persisted.", result.Success ? null : "InvalidOperation")
            {
                State = result.State,
            };
            return result.State == "pending" ? Results.Accepted(value: outcome) : Results.Ok(outcome);
        }

        return result.ReasonCode == "not-found"
            ? Results.NotFound()
            : Results.UnprocessableEntity(new AdminOperationResult(false, "dead-letter-action",
                "The requested operation cannot be completed.", "InvalidOperation"));

    }


    internal static bool IsValidAction(DeadLetterActionRequest action, EventStoreOperationsOptions options)
        => OperationsHttpSecurityExtensions.IsCanonicalTenant(action.TenantId)
            && action.MessageIds is { Count: > 0 }
            && action.MessageIds.Count <= options.MaxActionItems
            && action.MessageIds.All(DeadLetterSafeIdentity.IsValidValue)
            && (action.ActionId is null || DeadLetterSafeIdentity.IsValidValue(action.ActionId));

    internal static async Task<(byte[] Body, bool TooLarge)> ReadBoundedBodyAsync(
        HttpRequest request,
        int maxBodyBytes)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBodyBytes);
        byte[] buffer = GC.AllocateUninitializedArray<byte>(checked(maxBodyBytes + 1));
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await request.Body
                .ReadAsync(buffer.AsMemory(total, buffer.Length - total), TestableCancellation(request))
                .ConfigureAwait(false);
            if (read == 0)
            {
                return (buffer.AsSpan(0, total).ToArray(), false);
            }

            total += read;
        }

        return ([], true);
    }

    private static bool CanReadScope(HttpRequest request, string? tenantId)
    {
        int tenantValues = request.Query["tenantId"].Count;
        return EventStoreHttpHostSecurityExtensions.IsHuman(request.HttpContext.User)
            && (tenantValues == 0 && tenantId is null && OperationsHttpSecurityExtensions.IsAdministrator(request.HttpContext.User)
                || tenantValues == 1 && OperationsHttpSecurityExtensions.CanAccessTenant(request.HttpContext.User, tenantId));
    }

    private static IResult OpaqueForbidden()
        => Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");

    /// <summary>Projects a retained item onto the admin contract without exposing a payload.</summary>
    /// <remarks>
    /// Only the tenant slot carries the reserved <see cref="DeadLetterSafeIdentity.UnidentifiedTenantId"/>
    /// scope, because that is the value operator queries filter on. The remaining slots use a distinct
    /// placeholder so an operator can tell "this field was not safely identifiable" apart from the tenant an
    /// unidentified envelope is filed under.
    /// </remarks>
    private static DeadLetterEntry ToAdminEntry(DeadLetterListItem item)
        => new(
            item.Identity.MessageId,
            item.Identity.TenantId ?? DeadLetterSafeIdentity.UnidentifiedTenantId,
            item.Identity.Domain ?? DeadLetterSafeIdentity.UnknownValue,
            item.Identity.AggregateId ?? DeadLetterSafeIdentity.UnknownValue,
            item.Identity.CorrelationId ?? DeadLetterSafeIdentity.UnknownValue,
            item.LastReasonCode ?? "retained",
            item.CapturedAtUtc,
            item.ReplayAttempts,
            item.Identity.EventType ?? DeadLetterSafeIdentity.UnknownValue)
        {
            CausationId = item.Identity.CausationId,
            SequenceNumber = item.Identity.SequenceNumber,
            OriginalTimestampUtc = item.Identity.OriginalTimestamp,
            OriginalTopic = item.Identity.OriginalTopic,
            DeliveryAttempts = item.DeliveryFailure?.Attempts,
            FirstFailureAtUtc = item.DeliveryFailure?.FirstFailure,
            LastFailureAtUtc = item.DeliveryFailure?.LastFailure,
            FailureCategory = item.DeliveryFailure?.Reason.ToString(),
            Retryable = item.DeliveryFailure?.Retryable,
        };

    private static CancellationToken TestableCancellation(HttpRequest request)
        => request.HttpContext.RequestAborted;
}

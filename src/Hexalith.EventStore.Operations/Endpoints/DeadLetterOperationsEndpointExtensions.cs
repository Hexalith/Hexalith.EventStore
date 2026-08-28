using System.Globalization;
using System.Net.Http.Headers;

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
            });
        _ = endpoints.MapGet(InternalRoute + "/count", CountAsync);
        _ = endpoints.MapGet(InternalRoute, ListAsync);
        _ = endpoints.MapPost(InternalRoute + "/retry", RetryAsync);
        _ = endpoints.MapPost(InternalRoute + "/skip", SkipAsync);
        _ = endpoints.MapPost(InternalRoute + "/archive", ArchiveAsync);
        return endpoints;
    }

    private static Task<IResult> ArchiveAsync(
        HttpRequest request,
        DeadLetterActionRequest action,
        IActorProxyFactory actorProxyFactory,
        IOptions<EventStoreOperationsOptions> options)
        => ActionAsync(request, action, actorProxyFactory, options.Value, static (actor, value) => actor.ArchiveAsync(value));

    /// <summary>Captures one raw dead-letter delivery.</summary>
    /// <remarks>
    /// This topic is the last queue in the chain: it has no dead-letter destination of its own, so a delivery
    /// this route never acknowledges is redelivered until the sidecar's inbound pub/sub retry budget runs out and
    /// is then dropped with nothing retained. Conditions that redelivery can never change -- an oversize body, an
    /// empty body, a hash conflict against an already-retained item, or a body the actor refuses as unretainable
    /// -- are therefore acknowledged and counted on the bounded capture metric instead of burning that budget for
    /// an outcome that cannot improve. Only failures a retry can actually resolve, such as a state-store fault,
    /// return a non-2xx, and those are counted too: the retry budget is finite, so a capture failure that
    /// outlasts it loses the dead letter, and <c>capture-failed</c> is the only signal an operator gets.
    /// </remarks>
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
            return Results.Ok();
        }

        (byte[] body, bool tooLarge) = await ReadBoundedBodyAsync(
            request,
            value.MaxBodyBytes).ConfigureAwait(false);
        if (tooLarge || body.Length == 0)
        {
            telemetry.Capture(value.TopicName, tooLarge ? "oversize" : "empty-body");
            return Results.Ok();
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
            DeadLetterCaptureOutcome.HashConflict => Results.Ok(),

            // Counted by the actor, which is the side that classified it. Acknowledged here because redelivering
            // the same bytes reproduces the same rejection.
            DeadLetterCaptureOutcome.Unretainable => Results.Ok(),
            _ => UnknownCaptureOutcome(telemetry, value.TopicName),
        };
    }

    private static async Task<IResult> CountAsync(
        HttpRequest request,
        IActorProxyFactory actorProxyFactory,
        IOptions<EventStoreOperationsOptions> options)
    {
        if (!IsAuthorized(request, options.Value))
        {
            return OpaqueForbidden();
        }

        DeadLetterListResult result = await Actor(actorProxyFactory, options.Value)
            .ListAsync(new DeadLetterListRequest(null, 1, 0))
            .ConfigureAwait(false);
        return Results.Ok(result.TotalCount);
    }

    private static async Task<IResult> ListAsync(
        HttpRequest request,
        IActorProxyFactory actorProxyFactory,
        IOptions<EventStoreOperationsOptions> options,
        string? tenantId,
        int count = 100,
        string? continuationToken = null)
    {
        if (!IsAuthorized(request, options.Value))
        {
            return OpaqueForbidden();
        }

        int boundedCount = Math.Clamp(count, 1, options.Value.MaxListItems);
        int offset = int.TryParse(continuationToken, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)
            && parsed >= 0
            ? parsed
            : 0;
        DeadLetterListResult result = await Actor(actorProxyFactory, options.Value)
            .ListAsync(new DeadLetterListRequest(tenantId, boundedCount, offset))
            .ConfigureAwait(false);
        DeadLetterEntry[] entries = [.. result.Items.Select(ToAdminEntry)];
        string? nextToken = result.NextOffset?.ToString(CultureInfo.InvariantCulture);
        return Results.Ok(new PagedResult<DeadLetterEntry>(entries, result.TotalCount, nextToken));
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
        if (!IsAuthorized(request, options))
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

        DeadLetterActorActionResult result = await invoke(Actor(actorProxyFactory, options), action).ConfigureAwait(false);
        return result.Success
            ? Results.Ok(new AdminOperationResult(true, "dead-letter-action", "Operation accepted.", null))
            : result.ReasonCode == "not-found"
                ? Results.NotFound()
                : Results.UnprocessableEntity(new AdminOperationResult(
                    false,
                    "dead-letter-action",
                    "The requested operation cannot be completed.",
                    "InvalidOperation"));
    }

    internal static bool IsAuthorized(HttpRequest request, EventStoreOperationsOptions options)
    {
        string caller = request.Headers["dapr-caller-app-id"].ToString();
        string authorization = request.Headers.Authorization.ToString();
        return string.Equals(caller, options.AdminCallerAppId, StringComparison.Ordinal)
            && AuthenticationHeaderValue.TryParse(authorization, out AuthenticationHeaderValue? parsed)
            && string.Equals(parsed.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(parsed.Parameter);
    }

    internal static bool IsValidAction(DeadLetterActionRequest action, EventStoreOperationsOptions options)
        => !string.IsNullOrWhiteSpace(action.TenantId)
            && action.TenantId.Length <= DeadLetterSafeIdentity.MaxValueLength
            && action.MessageIds is { Count: > 0 }
            && action.MessageIds.Count <= options.MaxActionItems
            && action.MessageIds.All(DeadLetterSafeIdentity.IsValidValue);

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
            item.Identity.EventType ?? DeadLetterSafeIdentity.UnknownValue);

    private static CancellationToken TestableCancellation(HttpRequest request)
        => request.HttpContext.RequestAborted;
}

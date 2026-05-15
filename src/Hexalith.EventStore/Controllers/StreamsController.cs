using Dapr;
using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using GatewayTenantValidator = Hexalith.EventStore.Authorization.ITenantValidator;
using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Controllers;

/// <summary>
/// Public downstream stream read/replay endpoints.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/streams")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Streams")]
public sealed partial class StreamsController(
    IActorProxyFactory actorProxyFactory,
    GatewayTenantValidator tenantValidator,
    IRbacValidator rbacValidator,
    ILogger<StreamsController> logger) : ControllerBase {
    private const int _maxPageSize = 1_000;
    private const string _streamReadMessageCategory = "replay";
    private const string _streamReadMessageType = "StreamRead";

    /// <summary>
    /// Reads a public EventStore stream page for downstream replay/rebuild use.
    /// </summary>
    [HttpPost("read")]
    [RequestSizeLimit(1_048_576)]
    [ProducesResponseType(typeof(StreamReadPage), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> ReadStreamAsync(
        [FromBody] StreamReadRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        IActionResult? validationFailure = ValidateRequest(request);
        if (validationFailure is not null) {
            return validationFailure;
        }

        TenantValidationResult tenantResult = await tenantValidator
            .ValidateAsync(User, request.Tenant, cancellationToken, request.AggregateId)
            .ConfigureAwait(false);
        if (tenantResult is null) {
            throw new InvalidOperationException("ITenantValidator.ValidateAsync returned null. Server bug.");
        }

        if (!tenantResult.IsAuthorized) {
            return ProblemWithReason(
                StatusCodes.Status403Forbidden,
                ProblemTypeUris.Forbidden,
                "Forbidden",
                "Tenant is not authorized for stream replay.",
                StreamReplayReasonCodes.UnauthorizedTenant);
        }

        RbacValidationResult rbacResult = await rbacValidator
            .ValidateAsync(
                User,
                request.Tenant,
                request.Domain,
                _streamReadMessageType,
                _streamReadMessageCategory,
                cancellationToken,
                request.AggregateId)
            .ConfigureAwait(false);
        if (rbacResult is null) {
            throw new InvalidOperationException("IRbacValidator.ValidateAsync returned null. Server bug.");
        }

        if (!rbacResult.IsAuthorized) {
            return ProblemWithReason(
                StatusCodes.Status403Forbidden,
                ProblemTypeUris.Forbidden,
                "Forbidden",
                "Replay scope is not authorized.",
                StreamReplayReasonCodes.ForbiddenReplayScope);
        }

        try {
            var identity = new AggregateIdentity(request.Tenant, request.Domain, request.AggregateId!);
            IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(identity.ActorId),
                "AggregateActor");

            ServerEventEnvelope[] pageEvents = await actor
                .ReadEventsRangeAsync(request.FromSequence, request.ToSequence, request.PageSize + 1)
                .ConfigureAwait(false);

            IReadOnlyList<ServerEventEnvelope> readEvents = [.. pageEvents.OrderBy(e => e.SequenceNumber)];
            IReadOnlyList<ServerEventEnvelope> orderedEvents = [.. readEvents.Take(request.PageSize)];
            long latestSequence = readEvents.Count == 0
                ? request.FromSequence
                : readEvents[^1].SequenceNumber;
            long? lastReturned = orderedEvents.Count == 0
                ? null
                : orderedEvents[^1].SequenceNumber;
            bool truncated = pageEvents.Length > request.PageSize;

            // P-D3: Continuation tokens are not yet implemented (token request-binding deferred).
            // The validator at ValidateRequest line ~211 unconditionally rejects non-null tokens.
            // Emitting a random token would break paging, so we always return null and require
            // callers to paginate by setting FromSequence = lastSequenceReturned + 1.
            return Ok(new StreamReadPage(
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                [.. orderedEvents.Select(ToStreamReadEvent)],
                new StreamReadMetadata(
                    request.FromSequence,
                    request.ToSequence,
                    lastReturned,
                    latestSequence,
                    orderedEvents.Count,
                    truncated,
                    NextContinuationToken: null)));
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (ArgumentException) {
            return ProblemWithReason(
                StatusCodes.Status400BadRequest,
                ProblemTypeUris.BadRequest,
                "Bad Request",
                "Stream identity is invalid.",
                StreamReplayReasonCodes.InvalidAggregateIdentity);
        }
        catch (ActorMethodInvocationException ex) when (TryGetException<MissingEventException>(ex, out MissingEventException? missing)) {
            Log.StreamReadFailed(logger, ex, request.Tenant, request.Domain, request.AggregateId ?? string.Empty, StreamReplayReasonCodes.MissingEvent);
            return ProblemWithReason(
                StatusCodes.Status404NotFound,
                ProblemTypeUris.NotFound,
                "Not Found",
                $"Event stream is missing event sequence {missing!.SequenceNumber}.",
                StreamReplayReasonCodes.MissingEvent);
        }
        catch (ActorMethodInvocationException ex) when (TryGetException<EventDeserializationException>(ex, out _)) {
            Log.StreamReadFailed(logger, ex, request.Tenant, request.Domain, request.AggregateId ?? string.Empty, StreamReplayReasonCodes.CorruptEvent);
            return ProblemWithReason(
                StatusCodes.Status500InternalServerError,
                ProblemTypeUris.InternalServerError,
                "Internal Server Error",
                "Event stream contains unreadable event data.",
                StreamReplayReasonCodes.CorruptEvent);
        }
        catch (MissingEventException ex) {
            Log.StreamReadFailed(logger, ex, request.Tenant, request.Domain, request.AggregateId ?? string.Empty, StreamReplayReasonCodes.MissingEvent);
            return ProblemWithReason(
                StatusCodes.Status404NotFound,
                ProblemTypeUris.NotFound,
                "Not Found",
                $"Event stream is missing event sequence {ex.SequenceNumber}.",
                StreamReplayReasonCodes.MissingEvent);
        }
        catch (EventDeserializationException ex) {
            Log.StreamReadFailed(logger, ex, request.Tenant, request.Domain, request.AggregateId ?? string.Empty, StreamReplayReasonCodes.CorruptEvent);
            return ProblemWithReason(
                StatusCodes.Status500InternalServerError,
                ProblemTypeUris.InternalServerError,
                "Internal Server Error",
                "Event stream contains unreadable event data.",
                StreamReplayReasonCodes.CorruptEvent);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            Log.StreamReadFailed(logger, ex, request.Tenant, request.Domain, request.AggregateId ?? string.Empty, StreamReplayReasonCodes.ServiceUnavailable);
            return ProblemWithReason(
                StatusCodes.Status503ServiceUnavailable,
                ProblemTypeUris.ServiceUnavailable,
                "Service Unavailable",
                "Stream replay service is unavailable.",
                StreamReplayReasonCodes.ServiceUnavailable);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            Log.StreamReadFailed(logger, ex, request.Tenant, request.Domain, request.AggregateId ?? string.Empty, StreamReplayReasonCodes.InternalError);
            return ProblemWithReason(
                StatusCodes.Status500InternalServerError,
                ProblemTypeUris.InternalServerError,
                "Internal Server Error",
                "Stream replay failed.",
                StreamReplayReasonCodes.InternalError);
        }
    }

    private static StreamReadEvent ToStreamReadEvent(ServerEventEnvelope envelope)
        => new(
            envelope.SequenceNumber,
            envelope.EventTypeName,
            envelope.Payload,
            envelope.SerializationFormat,
            envelope.MetadataVersion,
            envelope.MessageId,
            envelope.CorrelationId,
            envelope.CausationId,
            envelope.Timestamp,
            string.IsNullOrWhiteSpace(envelope.UserId) ? null : envelope.UserId);

    private ObjectResult ProblemWithReason(
        int statusCode,
        string type,
        string title,
        string detail,
        string reasonCode) {
        ObjectResult result = Problem(
            statusCode: statusCode,
            type: type,
            title: title,
            detail: detail);
        if (result.Value is ProblemDetails problem) {
            problem.Extensions["reasonCode"] = reasonCode;
        }

        return result;
    }

    private IActionResult? ValidateRequest(StreamReadRequest request) {
        if (string.IsNullOrWhiteSpace(request.Tenant)
            || string.IsNullOrWhiteSpace(request.Domain)) {
            return ProblemWithReason(
                StatusCodes.Status400BadRequest,
                ProblemTypeUris.BadRequest,
                "Bad Request",
                "Tenant and domain are required.",
                StreamReplayReasonCodes.MissingRequiredField);
        }

        if (string.IsNullOrWhiteSpace(request.AggregateId)) {
            return ProblemWithReason(
                StatusCodes.Status400BadRequest,
                ProblemTypeUris.BadRequest,
                "Bad Request",
                "Aggregate identifier is required for the current stream read route.",
                StreamReplayReasonCodes.MissingRequiredField);
        }

        if (!IsCanonicalTenantOrDomain(request.Tenant) || !IsCanonicalTenantOrDomain(request.Domain) || !IsValidAggregateId(request.AggregateId)) {
            return ProblemWithReason(
                StatusCodes.Status400BadRequest,
                ProblemTypeUris.BadRequest,
                "Bad Request",
                "Stream identity is invalid.",
                StreamReplayReasonCodes.InvalidAggregateIdentity);
        }

        if (request.FromSequence < 0
            || request.FromSequence > int.MaxValue - 1L
            || (request.ToSequence.HasValue && request.ToSequence.Value <= request.FromSequence)) {
            return ProblemWithReason(
                StatusCodes.Status400BadRequest,
                ProblemTypeUris.BadRequest,
                "Bad Request",
                "'fromSequence' must be >= 0 and 'toSequence' must be greater than 'fromSequence'.",
                StreamReplayReasonCodes.InvalidRange);
        }

        if (request.PageSize is <= 0 or > _maxPageSize) {
            return ProblemWithReason(
                StatusCodes.Status400BadRequest,
                ProblemTypeUris.BadRequest,
                "Bad Request",
                $"'pageSize' must be between 1 and {_maxPageSize}.",
                StreamReplayReasonCodes.InvalidRange);
        }

        if (request.ContinuationToken is not null) {
            return ProblemWithReason(
                StatusCodes.Status400BadRequest,
                ProblemTypeUris.BadRequest,
                "Bad Request",
                "Continuation token validation is fail-closed until token request binding is enabled.",
                StreamReplayReasonCodes.InvalidContinuation);
        }

        return null;
    }

    private static bool IsCanonicalTenantOrDomain(string value) {
        if (value.Length > 64 || value.Any(c => c is < (char)0x20 or >= (char)0x7F)) {
            return false;
        }

        if (!char.IsAsciiLetterOrDigit(value[0]) || !char.IsAsciiLetterOrDigit(value[^1])) {
            return false;
        }

        return value.All(c => c is >= 'a' and <= 'z' || c is >= '0' and <= '9' || c == '-');
    }

    private static bool IsValidAggregateId(string value) {
        if (value.Length > 256 || value.Any(c => c is < (char)0x20 or >= (char)0x7F)) {
            return false;
        }

        if (!char.IsAsciiLetterOrDigit(value[0]) || !char.IsAsciiLetterOrDigit(value[^1])) {
            return false;
        }

        return value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');
    }

    private static bool IsServiceUnavailable(Exception exception)
        => exception is DaprException
            or HttpRequestException
            or TimeoutException
            or ActorMethodInvocationException
            || (exception.InnerException is not null && IsServiceUnavailable(exception.InnerException));

    private static bool TryGetException<TException>(Exception exception, out TException? result)
        where TException : Exception {
        Exception? current = exception;
        while (current is not null) {
            if (current is TException matched) {
                result = matched;
                return true;
            }

            current = current.InnerException;
        }

        result = null;
        return false;
    }

    private static partial class Log {

        [LoggerMessage(
            EventId = 1180,
            Level = LogLevel.Warning,
            Message = "Public stream read failed: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ReasonCode={ReasonCode}, Stage=StreamReadFailed")]
        public static partial void StreamReadFailed(
            ILogger logger,
            Exception exception,
            string tenantId,
            string domain,
            string aggregateId,
            string reasonCode);
    }
}

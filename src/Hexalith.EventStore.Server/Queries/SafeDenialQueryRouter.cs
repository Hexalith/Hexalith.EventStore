using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Pipeline.Queries;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Queries;

/// <summary>
/// <see cref="IQueryRouter"/> decorator implementing the opt-in safe-denial boundary: for routes
/// that opt in via <see cref="ISafeDenialQueryRoutePolicy"/>, a Forbidden or "missing projection
/// state" not-found result is remapped onto the exact same shape as a genuine not-found result,
/// so the denial causes are externally indistinguishable in status, body, and logged detail.
/// Routes that have not opted in, and every other failure category, pass through unchanged.
/// </summary>
/// <remarks>
/// This type only ever narrows a Forbidden or "missing projection state" outcome onto the
/// existing not-found shape — it never widens a genuine not-found result, never mutates
/// <see cref="QueryRouterResult.NotFound"/> production logic in the wrapped router, and never
/// changes behavior for a route that has not explicitly opted in. Registration is a separate,
/// explicit step (see <see cref="SafeDenialQueryRoutingServiceCollectionExtensions"/>); nothing
/// wires this decorator into the pipeline by default.
/// </remarks>
public sealed partial class SafeDenialQueryRouter(
    IQueryRouter inner,
    ISafeDenialQueryRoutePolicy policy,
    ILogger<SafeDenialQueryRouter> logger) : IQueryRouter {
    /// <inheritdoc/>
    public async Task<QueryRouterResult> RouteQueryAsync(SubmitQuery query, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        QueryRouterResult result = await inner.RouteQueryAsync(query, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(query.Domain) || string.IsNullOrWhiteSpace(query.QueryType)) {
            // ISafeDenialQueryRoutePolicy.IsOptedIn (e.g. SafeDenialQueryRouteRegistry) requires
            // non-blank domain/queryType and throws otherwise. A malformed SubmitQuery should
            // never have reached this far in production, but a non-validating inner router (e.g.
            // a test double, or a future router that relaxes validation) must not turn that into
            // an unhandled exception here -- fail safe to an unmodified pass-through instead.
            // Logged (unlike the ordinary opted-in/not-opted-in paths below) because a blank
            // Domain/QueryType reaching this deep is itself an operator-visible signal that a
            // malformed SubmitQuery got past upstream validation.
            Log.SafeDenialBlankRouteDetailsPassThrough(logger, query.CorrelationId, query.Tenant);
            return result;
        }

        if (!policy.IsOptedIn(query.Domain, query.QueryType)) {
            return result;
        }

        if (result.NotFound) {
            // Already a not-found outcome, but canonicalize it here too: nothing guarantees an
            // arbitrary wrapped IQueryRouter already omits ErrorMessage/Metadata/ProjectionType
            // on a NotFound result, so reconstruct the clean shape rather than returning the
            // inner router's result as-is. Keeps every not-found path through this decorator
            // byte-identical, matching the Forbidden/MissingProjectionState canonicalization below.
            return new QueryRouterResult(Success: false, Payload: null, NotFound: true);
        }

        if (result.Success) {
            return result;
        }

        bool isForbidden = string.Equals(result.ErrorMessage, QueryAdapterFailureReason.Forbidden, StringComparison.Ordinal);
        bool isMissingProjectionState = !isForbidden
            && string.Equals(result.ErrorMessage, QueryAdapterFailureReason.MissingProjectionState, StringComparison.Ordinal);

        if (!isForbidden && !isMissingProjectionState) {
            return result;
        }

        // This log line is an accepted internal-only (log/SIEM) signal: it records which genuine
        // cause (Forbidden vs. the second not-found shape) was unified, but that detail never
        // reaches the caller -- server-side logs are not part of the externally observable
        // response, so recording it here does not violate the "externally indistinguishable"
        // guarantee this decorator provides on the wire.
        Log.SafeDenialForbiddenUnified(
            logger,
            query.CorrelationId,
            query.Tenant,
            query.Domain,
            query.QueryType,
            isForbidden ? QueryAdapterFailureReason.SafeDenialForbidden : QueryAdapterFailureReason.SafeDenialMissingProjectionState);

        // Byte-identical to the shape genuine not-found results already use (see QueryRouter's
        // ActorMethodInvocationException/legacy-marker branches): Success=false, Payload=null,
        // NotFound=true, no ErrorMessage, no Metadata. Reusing this exact, already-exercised
        // shape is the strongest indistinguishability guarantee for the safe-denial boundary.
        return new QueryRouterResult(Success: false, Payload: null, NotFound: true);
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1220,
            Level = LogLevel.Information,
            Message = "Safe-denial adapter unified a denial result into the shared not-found shape: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, QueryType={QueryType}, Reason={Reason}, Stage=SafeDenialForbiddenUnified")]
        public static partial void SafeDenialForbiddenUnified(
            ILogger logger,
            string correlationId,
            string tenantId,
            string domain,
            string queryType,
            // No default value: the single call site above always passes reason explicitly
            // (either SafeDenialForbidden or SafeDenialMissingProjectionState), so a default here
            // would be dead code that never actually applies.
            string reason);

        [LoggerMessage(
            EventId = 1223,
            Level = LogLevel.Warning,
            Message = "Safe-denial adapter received a SubmitQuery with a blank Domain or QueryType and passed the result through unmodified: CorrelationId={CorrelationId}, TenantId={TenantId}, Stage=SafeDenialBlankRouteDetailsPassThrough")]
        public static partial void SafeDenialBlankRouteDetailsPassThrough(
            ILogger logger,
            string correlationId,
            string tenantId);
    }
}

using System.Net;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Sample.BlazorUI.Services;

/// <summary>
/// Defines how the counter components interpret a failed projection read.
/// </summary>
/// <remarks>
/// Only the gateway's projection-missing not-found (HTTP 404 with reason code
/// <see cref="QueryProblemReasonCodes.ProjectionMissing"/>) means the counter projection does not exist yet and renders
/// the empty state. Any other 404, such as a wrong route, is an error. A query that reached EventStore but failed
/// (<c>Success == false</c>) arrives as status 200 and is shown as a failed query with its correlation ID, never as an
/// empty counter.
/// </remarks>
internal static class CounterQueryFailure
{
    /// <summary>Introduces a query that EventStore answered but reported as failed.</summary>
    internal const string QueryFailedMessage = "EventStore reported that the counter query failed";

    /// <summary>The title <see cref="EventStoreGatewayClient"/> gives a response whose body reports <c>Success == false</c>.</summary>
    internal const string SemanticFailureTitle = "Query semantic failure";

    /// <summary>Reports whether a gateway failure means no counter projection exists yet.</summary>
    /// <param name="exception">The gateway failure.</param>
    /// <returns><see langword="true"/> when the empty counter state should be shown.</returns>
    internal static bool IsProjectionNotYetAvailable(EventStoreGatewayException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.StatusCode == (int)HttpStatusCode.NotFound
            && string.Equals(exception.ReasonCode, QueryProblemReasonCodes.ProjectionMissing, StringComparison.Ordinal);
    }

    /// <summary>Describes a failed counter read for display.</summary>
    /// <param name="exception">The failure.</param>
    /// <returns>The text shown after the component's failure prefix.</returns>
    internal static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is not EventStoreGatewayException { StatusCode: (int)HttpStatusCode.OK, Title: SemanticFailureTitle } failure)
        {
            return exception.Message;
        }

        string detail = string.IsNullOrWhiteSpace(failure.Detail) ? string.Empty : $": {failure.Detail}";
        string correlation = string.IsNullOrWhiteSpace(failure.CorrelationId) ? string.Empty : $" (correlation ID {failure.CorrelationId})";
        return $"{QueryFailedMessage}{detail}{correlation}.";
    }
}

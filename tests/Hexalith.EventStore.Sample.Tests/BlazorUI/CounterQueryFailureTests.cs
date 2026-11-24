extern alias BlazorUI;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;

using Shouldly;

using CounterQueryFailure = BlazorUI::Hexalith.EventStore.Sample.BlazorUI.Services.CounterQueryFailure;

namespace Hexalith.EventStore.Sample.Tests.BlazorUI;

/// <summary>Pins which gateway failures the counter components render as the empty state.</summary>
public sealed class CounterQueryFailureTests
{
    [Fact]
    public void ProjectionMissingNotFound_IsTheEmptyState()
    {
        var notFound = new EventStoreGatewayException(404, "Not Found", reasonCode: QueryProblemReasonCodes.ProjectionMissing);

        CounterQueryFailure.IsProjectionNotYetAvailable(notFound).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("query_route_missing")]
    public void OtherNotFound_IsAnError(string? reasonCode)
    {
        var notFound = new EventStoreGatewayException(404, "Not Found", reasonCode: reasonCode);

        CounterQueryFailure.IsProjectionNotYetAvailable(notFound).ShouldBeFalse();
    }

    [Theory]
    [InlineData(200)]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(500)]
    [InlineData(503)]
    public void OtherStatuses_AreErrorsNotEmptyState(int statusCode)
    {
        var failure = new EventStoreGatewayException(statusCode, "Failure", reasonCode: QueryProblemReasonCodes.ProjectionMissing);

        CounterQueryFailure.IsProjectionNotYetAvailable(failure).ShouldBeFalse();
    }

    [Fact]
    public void SemanticFailure_IsDescribedAsAFailedQueryWithItsCorrelation()
    {
        var semanticFailure = new EventStoreGatewayException(
            200,
            CounterQueryFailure.SemanticFailureTitle,
            detail: "Counter is archived",
            correlationId: "query-1");

        CounterQueryFailure.Describe(semanticFailure)
            .ShouldBe($"{CounterQueryFailure.QueryFailedMessage}: Counter is archived (correlation ID query-1).");
    }

    [Fact]
    public void SemanticFailureWithoutDetail_StillNamesTheFailedQuery()
    {
        var semanticFailure = new EventStoreGatewayException(200, CounterQueryFailure.SemanticFailureTitle);

        CounterQueryFailure.Describe(semanticFailure).ShouldBe($"{CounterQueryFailure.QueryFailedMessage}.");
    }

    [Fact]
    public void MalformedSuccessResponse_KeepsTheClientMessage()
    {
        var malformed = new EventStoreGatewayException(200, "OK", detail: "Query response did not contain a valid correlationId.");

        CounterQueryFailure.Describe(malformed).ShouldBe("Query response did not contain a valid correlationId.");
    }

    [Fact]
    public void TransportFailure_KeepsItsMessage()
    {
        var unavailable = new EventStoreGatewayException(503, "Service Unavailable", detail: "Gateway is restarting.");

        CounterQueryFailure.Describe(unavailable).ShouldBe("Gateway is restarting.");
        CounterQueryFailure.Describe(new HttpRequestException("Connection refused")).ShouldBe("Connection refused");
    }
}

using System.Net;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>Uses fresh production host scopes and can lose only the response after durable work.</summary>
internal sealed class ReplayRecoveryProductionHttpHandler(ReplayRecoveryProductionFixture fixture) : HttpMessageHandler
{
    internal string? LoseAfterAction { get; set; }
    internal bool RejectCleanup { get; set; }
    internal string? StoreBinding { get; set; } = ReplayRecoveryProductionFixture.StoreName;
    internal List<(string Action, string DispatchId, string Header)> Calls { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string path = request.RequestUri!.AbsolutePath;
        string action = path.Split('/')[^2];
        if (!request.Headers.TryGetValues(ProjectionReplayRecoveryBinding.HeaderName, out IEnumerable<string>? values))
            return new(HttpStatusCode.BadRequest);
        string header = values.Single();
        if (action == "reclaim-recovery" && RejectCleanup) return new(HttpStatusCode.ServiceUnavailable);
        ProjectionDispatchRequest input = JsonSerializer.Deserialize<ProjectionDispatchRequest>(
            await request.Content!.ReadAsByteArrayAsync(cancellationToken), JsonSerializerOptions.Web)!;
        input = ProjectionReplayTransportContext.Bind(input, request.Headers.TryGetValues(ProjectionReplayTransportContext.HeaderName,
            out IEnumerable<string>? context) ? context.Single() : null);
        Calls.Add((action, input.DispatchId, header));
        DomainProjectionRebuildBatchAction operation = action switch
        {
            "stage" => DomainProjectionRebuildBatchAction.Stage,
            "commit" => DomainProjectionRebuildBatchAction.Commit,
            "verify" => DomainProjectionRebuildBatchAction.Verify,
            _ => DomainProjectionRebuildBatchAction.Reclaim,
        };
        try
        {
            ProjectionDispatchResponse response = await fixture.ExecuteAsync(operation, StoreBinding, header, input, cancellationToken);
            if (LoseAfterAction == action)
            {
                LoseAfterAction = null;
                return new(HttpStatusCode.ServiceUnavailable);
            }
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(response,
                JsonSerializerOptions.Web), Encoding.UTF8, "application/json") };
        }
        catch (ProjectionDispatchValidationException exception)
        {
            return new(HttpStatusCode.BadRequest) { Content = new StringContent(JsonSerializer.Serialize(exception.ReasonCode),
                Encoding.UTF8, "application/json") };
        }
    }
}

using System.Text.Json;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;

namespace P1R.Counter;

/// <summary>Executes the published cursor codec through the domain query dispatcher.</summary>
/// <param name="codec">The published codec backed by invocation-owned keys.</param>
public sealed class CursorQueryHandler(IQueryCursorCodec codec) : IDomainQueryHandler
{
    /// <inheritdoc/>
    public string Domain => "counter";

    /// <inheritdoc/>
    public string QueryType => "fixture";

    /// <inheritdoc/>
    public Task<QueryResult> ExecuteAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        bool decoded = codec.TryDecode(query.Paging?.Cursor, "fixture", "tenant-a|watermark:987", out string? position, out string? failure);
        return Task.FromResult(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { decoded, position, failure })));
    }
}

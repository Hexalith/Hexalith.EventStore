using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Testing.CatalogInvocation;

namespace Hexalith.EventStore.Testing.OriginalSource;

/// <summary>Private original-input append client. Only the EventStore owner commits source authority.</summary>
public sealed class SyntheticOriginalSourceClient(string directory, string runId)
{
    private readonly CatalogInvocationFiles _files = new(directory, SyntheticOriginalSourceStore.MaximumBytes);

    /// <summary>Enrolls exact resources and appends original pages; returns false for conflicting positions without replacing them.</summary>
    public async Task<bool> AppendAsync(string tenant, string domain, string aggregateId,
        IReadOnlyList<ProjectionEventDto> events, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (ProjectionEventDto[] page in events.Chunk(SyntheticOriginalSourceStore.MaximumEvents))
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            string id = Guid.NewGuid().ToString("N");
            var request = new SyntheticOriginalSourceAppend(id, DateTimeOffset.UtcNow.AddSeconds(30), runId,
                tenant, domain, aggregateId, page);
            await _files.WriteAsync(id, ".request", request, deadline.Token).ConfigureAwait(false);
            string resultPath = _files.PathFor(id, ".response");
            while (!File.Exists(resultPath)) await Task.Delay(10, deadline.Token).ConfigureAwait(false);
            var result = await _files.ReadAsync<SyntheticOriginalSourceAppendResult>(id, ".response", deadline.Token).ConfigureAwait(false);
            if (result.Id != id) throw new InvalidOperationException("Original-primary fixture response identity differs.");
            _files.Delete(id, ".working");
            _files.Delete(id, ".response");
            deadline.Token.ThrowIfCancellationRequested();
            if (result.Id != id || result.Failure is not null || DateTimeOffset.UtcNow >= request.ExpiresAt)
                throw new InvalidOperationException("Original-primary fixture append unavailable: " + result.Failure);
            if (result.Conflict) return false;
        }
        return true;
    }
}

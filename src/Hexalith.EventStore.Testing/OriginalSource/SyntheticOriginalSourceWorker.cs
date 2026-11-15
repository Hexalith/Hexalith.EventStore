using Hexalith.EventStore.Testing.CatalogInvocation;

using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.Testing.OriginalSource;

/// <summary>Owner-process private enrollment/append worker; exposes no HTTP import or credential relay.</summary>
internal sealed class SyntheticOriginalSourceWorker(SyntheticOriginalSourceOptions selection,
    SyntheticOriginalSourceStore store) : BackgroundService
{
    private readonly CatalogInvocationFiles _files = new(selection.Directory, SyntheticOriginalSourceStore.MaximumBytes);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (string path in _files.Pending())
            {
                string id = Path.GetFileNameWithoutExtension(path);
                _files.Claim(id);
            }
            foreach (string path in _files.Existing(".working"))
                await RecoverOrInvokeAsync(Path.GetFileNameWithoutExtension(path), stoppingToken).ConfigureAwait(false);
            foreach (string path in _files.Existing(".response"))
                await RetireConsumedOrphanAsync(Path.GetFileNameWithoutExtension(path), stoppingToken).ConfigureAwait(false);
            await Task.Delay(10, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RetireConsumedOrphanAsync(string id, CancellationToken token)
    {
        if (File.Exists(_files.PathFor(id, ".working")) || File.Exists(_files.PathFor(id, ".request"))) return;
        SyntheticOriginalSourceAppendResult completed;
        try { completed = await _files.ReadAsync<SyntheticOriginalSourceAppendResult>(id, ".response", token).ConfigureAwait(false); }
        catch (IOException) when (!File.Exists(_files.PathFor(id, ".response"))) { return; } // Client finished its second unlink.
        if (completed.Id != id) throw new InvalidOperationException("Original-primary fixture response identity differs.");
        if (!File.Exists(_files.PathFor(id, ".working")) && !File.Exists(_files.PathFor(id, ".request")))
            _files.Delete(id, ".response");
    }

    private async Task RecoverOrInvokeAsync(string id, CancellationToken stoppingToken)
    {
        SyntheticOriginalSourceAppend request;
        try { request = await _files.ReadAsync<SyntheticOriginalSourceAppend>(id, ".working", stoppingToken).ConfigureAwait(false); }
        catch (IOException) when (!File.Exists(_files.PathFor(id, ".working"))) { return; } // Already consumed after owner completion.
        if (request.Id != id) throw new InvalidOperationException("Original-primary fixture request identity differs.");
        if (DateTimeOffset.UtcNow >= request.ExpiresAt)
        {
            RetireCompleted(id);
            return;
        }
        if (File.Exists(_files.PathFor(id, ".response")))
        {
            try
            {
                var completed = await _files.ReadAsync<SyntheticOriginalSourceAppendResult>(id, ".response", stoppingToken).ConfigureAwait(false);
                if (completed.Id != id) throw new InvalidOperationException("Original-primary fixture response identity differs.");
            }
            catch (IOException) when (!File.Exists(_files.PathFor(id, ".working"))) { }
            return; // Actual work ended. Retain its original deadline until consumption or expiry.
        }
        if (!File.Exists(_files.PathFor(id, ".working"))) return;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        operation.CancelAfter(TimeSpan.FromSeconds(Math.Clamp((request.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds, 0, 30)));
        SyntheticOriginalSourceAppendResult result;
        try { result = new(id, !await store.AppendAsync(request, operation.Token).ConfigureAwait(false), null); }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        { result = new(id, false, exception.GetType().Name); }
        // Append (including its cleanup) has actually ended; an expired operation publishes no late result.
        if (DateTimeOffset.UtcNow >= request.ExpiresAt || operation.IsCancellationRequested)
        {
            RetireCompleted(id);
            return;
        }
        try
        {
            await _files.WriteAsync(id, ".response", result, operation.Token, beforePublish: () =>
            {
                if (DateTimeOffset.UtcNow >= request.ExpiresAt) throw new OperationCanceledException("Original-primary fixture control expired.");
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested) { RetireCompleted(id); }
    }

    private void RetireCompleted(string id)
    {
        // Only this exclusive owner's work has ended. Path/type/UID checks remain in Delete.
        _files.Delete(id, ".response");
        _files.Delete(id, ".working");
    }
}

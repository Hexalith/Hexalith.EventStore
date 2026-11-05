using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Testing.CatalogInvocation;

namespace Hexalith.EventStore.Testing.OwnerSourceRead;

/// <summary>One-shot caller for the explicitly selected owner-hosted bootstrap source proof.</summary>
public sealed class OwnerSourceReadClient
{
    private readonly CatalogInvocationFiles _files;

    /// <summary>Captures the existing launcher-owned private IPC directory.</summary>
    /// <param name="directory">The canonical private directory.</param>
    public OwnerSourceReadClient(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        _files = new(directory);
    }

    /// <summary>Reads only the actual complete administrator source; failures never become successful proof.</summary>
    /// <param name="cancellationToken">The caller's complete deadline.</param>
    /// <returns>The original bounded SDK stream page.</returns>
    public async Task<StreamReadPage> ReadAsync(CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        deadline.Token.ThrowIfCancellationRequested();
        string id = Guid.NewGuid().ToString("N");
        var request = new OwnerSourceReadRequest(id, DateTimeOffset.UtcNow.AddSeconds(45), OwnerSourceReadSelection.Mode,
            OwnerSourceReadSelection.Scope, OwnerSourceReadSelection.Administrator, OwnerSourceReadSelection.Administrator);
        await _files.WriteAsync(id, ".request", request, deadline.Token).ConfigureAwait(false);
        string resultPath = _files.PathFor(id, ".response");
        while (!File.Exists(resultPath)) await Task.Delay(25, deadline.Token).ConfigureAwait(false);
        var result = await _files.ReadAsync<OwnerSourceReadResult>(id, ".response", deadline.Token).ConfigureAwait(false);
        deadline.Token.ThrowIfCancellationRequested();
        if (DateTimeOffset.UtcNow >= request.ExpiresAt) throw new OperationCanceledException("Private owner source deadline elapsed.");
        if (result.Id != id || result.Failure is not null || result.Page is null)
            throw new InvalidOperationException("Private owner source read failed.");
        OwnerSourceReadSelection.ValidatePage(result.Page);
        return result.Page;
    }
}

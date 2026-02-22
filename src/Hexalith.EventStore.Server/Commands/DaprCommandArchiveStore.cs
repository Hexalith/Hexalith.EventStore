namespace Hexalith.EventStore.Server.Commands;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// DAPR state store implementation of <see cref="ICommandArchiveStore"/>.
/// Archive writes are advisory per enforcement rule #12 -- failures are logged but never thrown.
/// </summary>
public class DaprCommandArchiveStore(
    DaprClient daprClient,
    IOptions<CommandStatusOptions> options,
    ILogger<DaprCommandArchiveStore> logger) : ICommandArchiveStore {
    /// <inheritdoc/>
    /// <remarks>
    /// This method propagates exceptions to the caller. Advisory error handling
    /// (rule #12) is the caller's responsibility — see <see cref="Hexalith.EventStore.Server.Pipeline.SubmitCommandHandler"/>.
    /// </remarks>
    public async Task WriteCommandAsync(
        string tenantId,
        string correlationId,
        ArchivedCommand command,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentNullException.ThrowIfNull(command);

        string key = CommandArchiveConstants.BuildKey(tenantId, correlationId);
        CommandStatusOptions opts = options.Value;

        var metadata = new Dictionary<string, string>
        {
            { "ttlInSeconds", opts.TtlSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) },
        };

        await daprClient.SaveStateAsync(
            opts.StateStoreName,
            key,
            command,
            metadata: metadata,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogDebug(
            "Command archived: CorrelationId={CorrelationId}, TenantId={TenantId}",
            correlationId,
            tenantId);
    }

    /// <inheritdoc/>
    public async Task<ArchivedCommand?> ReadCommandAsync(
        string tenantId,
        string correlationId,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        string key = CommandArchiveConstants.BuildKey(tenantId, correlationId);
        CommandStatusOptions opts = options.Value;

        try {
            return await daprClient.GetStateAsync<ArchivedCommand>(
                opts.StateStoreName,
                key,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            logger.LogWarning(
                ex,
                "Failed to read archived command for {CorrelationId}, TenantId={TenantId}. Returning null.",
                correlationId,
                tenantId);
            return null;
        }
    }
}

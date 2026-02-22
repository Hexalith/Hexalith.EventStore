
using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Commands;
/// <summary>
/// DAPR state store implementation of <see cref="ICommandStatusStore"/>.
/// Status writes are advisory per enforcement rule #12 — failures are logged but never thrown.
/// </summary>
public class DaprCommandStatusStore(
    DaprClient daprClient,
    IOptions<CommandStatusOptions> options,
    ILogger<DaprCommandStatusStore> logger) : ICommandStatusStore {
    /// <inheritdoc/>
    /// <remarks>
    /// This method propagates exceptions to the caller. Advisory error handling
    /// (rule #12) is the caller's responsibility — see <see cref="Hexalith.EventStore.Server.Pipeline.SubmitCommandHandler"/>.
    /// </remarks>
    public async Task WriteStatusAsync(
        string tenantId,
        string correlationId,
        CommandStatusRecord status,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentNullException.ThrowIfNull(status);

        string key = CommandStatusConstants.BuildKey(tenantId, correlationId);
        CommandStatusOptions opts = options.Value;

        var metadata = new Dictionary<string, string>
        {
            { "ttlInSeconds", opts.TtlSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) },
        };

        await daprClient.SaveStateAsync(
            opts.StateStoreName,
            key,
            status,
            metadata: metadata,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogDebug(
            "Command status written: CorrelationId={CorrelationId}, TenantId={TenantId}, Status={Status}",
            correlationId,
            tenantId,
            status.Status);
    }

    /// <inheritdoc/>
    public async Task<CommandStatusRecord?> ReadStatusAsync(
        string tenantId,
        string correlationId,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        string key = CommandStatusConstants.BuildKey(tenantId, correlationId);
        CommandStatusOptions opts = options.Value;

        try {
            return await daprClient.GetStateAsync<CommandStatusRecord>(
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
                "Failed to read command status for {CorrelationId}. Returning null.",
                correlationId);
            return null;
        }
    }
}

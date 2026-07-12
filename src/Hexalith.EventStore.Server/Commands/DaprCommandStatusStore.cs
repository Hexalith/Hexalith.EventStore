
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
        string messageId,
        CommandStatusRecord status,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentNullException.ThrowIfNull(status);

        string key = CommandStatusConstants.BuildKey(tenantId, messageId);
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
            "Command status written: MessageId={MessageId}, TenantId={TenantId}, Status={Status}",
            messageId,
            tenantId,
            status.Status);
    }

    /// <inheritdoc/>
    public async Task<CommandStatusRecord?> ReadStatusAsync(
        string tenantId,
        string messageId,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        string key = CommandStatusConstants.BuildKey(tenantId, messageId);
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
                "Failed to read command status for {MessageId}. Returning null.",
                messageId);
            return null;
        }
    }
}

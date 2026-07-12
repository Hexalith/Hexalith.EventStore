
using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Commands;
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
        string messageId,
        ArchivedCommand command,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentNullException.ThrowIfNull(command);

        string key = CommandArchiveConstants.BuildKey(tenantId, messageId);
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
            "Command archived: MessageId={MessageId}, TenantId={TenantId}",
            messageId,
            tenantId);
    }

    /// <inheritdoc/>
    public async Task<ArchivedCommand?> ReadCommandAsync(
        string tenantId,
        string messageId,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        string key = CommandArchiveConstants.BuildKey(tenantId, messageId);
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
                "Failed to read archived command for {MessageId}, TenantId={TenantId}. Returning null.",
                messageId,
                tenantId);
            return null;
        }
    }
}

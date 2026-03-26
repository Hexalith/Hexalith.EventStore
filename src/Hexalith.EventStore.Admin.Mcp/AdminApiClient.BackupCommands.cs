namespace Hexalith.EventStore.Admin.Mcp;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;

/// <summary>
/// AdminApiClient partial — backup command methods.
/// </summary>
internal sealed partial class AdminApiClient
{
    /// <summary>
    /// Triggers a full backup for a tenant.
    /// </summary>
    public async Task<AdminOperationResult?> TriggerBackupAsync(
        string tenantId, string? description, bool includeSnapshots, CancellationToken cancellationToken)
    {
        string path = $"/api/v1/admin/backups/{Uri.EscapeDataString(tenantId)}?includeSnapshots={includeSnapshots.ToString().ToLowerInvariant()}";
        if (!string.IsNullOrWhiteSpace(description))
        {
            path += $"&description={Uri.EscapeDataString(description)}";
        }

        return await PostAsync(path, cancellationToken).ConfigureAwait(false);
    }
}

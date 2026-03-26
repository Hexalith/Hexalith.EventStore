namespace Hexalith.EventStore.Admin.Mcp;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;

/// <summary>
/// AdminApiClient partial — consistency check command methods.
/// </summary>
internal sealed partial class AdminApiClient
{
    /// <summary>
    /// Triggers a data integrity consistency check.
    /// </summary>
    public async Task<AdminOperationResult?> TriggerConsistencyCheckAsync(
        string? tenantId, string? domain, IReadOnlyList<string> checkTypes, CancellationToken cancellationToken)
    {
        return await PostAsync(
            "/api/v1/admin/consistency/checks",
            new { tenantId, domain, checkTypes },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Cancels a running consistency check.
    /// </summary>
    public async Task<AdminOperationResult?> CancelConsistencyCheckAsync(
        string checkId, CancellationToken cancellationToken)
    {
        string path = $"/api/v1/admin/consistency/checks/{Uri.EscapeDataString(checkId)}/cancel";
        return await PostAsync(path, cancellationToken).ConfigureAwait(false);
    }
}

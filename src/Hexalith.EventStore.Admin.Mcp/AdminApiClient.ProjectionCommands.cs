namespace Hexalith.EventStore.Admin.Mcp;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;

/// <summary>
/// AdminApiClient partial — projection write command methods.
/// </summary>
internal sealed partial class AdminApiClient
{
    /// <summary>
    /// Pauses a running projection.
    /// </summary>
    public async Task<AdminOperationResult?> PauseProjectionAsync(
        string tenantId, string projectionName, CancellationToken cancellationToken)
    {
        string path = $"/api/v1/admin/projections/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(projectionName)}/pause";
        return await PostAsync(path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resumes a paused projection.
    /// </summary>
    public async Task<AdminOperationResult?> ResumeProjectionAsync(
        string tenantId, string projectionName, CancellationToken cancellationToken)
    {
        string path = $"/api/v1/admin/projections/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(projectionName)}/resume";
        return await PostAsync(path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resets a projection to rebuild state from a specific event position.
    /// </summary>
    public async Task<AdminOperationResult?> ResetProjectionAsync(
        string tenantId, string projectionName, long? fromPosition, CancellationToken cancellationToken)
    {
        string path = $"/api/v1/admin/projections/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(projectionName)}/reset";
        return await PostAsync(path, new { fromPosition }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Replays a projection between two event positions.
    /// </summary>
    public async Task<AdminOperationResult?> ReplayProjectionAsync(
        string tenantId, string projectionName, long fromPosition, long toPosition, CancellationToken cancellationToken)
    {
        string path = $"/api/v1/admin/projections/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(projectionName)}/replay";
        return await PostAsync(path, new { fromPosition, toPosition }, cancellationToken).ConfigureAwait(false);
    }
}

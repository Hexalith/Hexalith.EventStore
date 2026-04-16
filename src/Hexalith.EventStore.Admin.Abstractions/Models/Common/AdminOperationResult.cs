namespace Hexalith.EventStore.Admin.Abstractions.Models.Common;

/// <summary>
/// Result of an administrative write operation.
/// </summary>
/// <param name="Success">Whether the operation completed successfully.</param>
/// <param name="OperationId">A unique identifier for the operation.</param>
/// <param name="Message">An optional human-readable message.</param>
/// <param name="ErrorCode">An optional error code when the operation fails.</param>
public record AdminOperationResult(bool Success, string OperationId, string? Message, string? ErrorCode) {
    /// <summary>Gets the unique identifier for the operation.</summary>
    public string OperationId { get; } = !string.IsNullOrWhiteSpace(OperationId)
        ? OperationId
        : throw new ArgumentException("OperationId cannot be null, empty, or whitespace.", nameof(OperationId));
}


namespace Hexalith.EventStore.Contracts.Validation;

/// <summary>
/// Represents the result of a preflight authorization check before command or query processing.
/// </summary>
/// <param name="IsAuthorized">Whether the request is authorized to proceed.</param>
/// <param name="Reason">Optional reason when authorization is denied.</param>
public record PreflightValidationResult(
    bool IsAuthorized,
    string? Reason = null);

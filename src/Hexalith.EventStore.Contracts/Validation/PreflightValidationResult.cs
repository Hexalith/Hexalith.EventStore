
namespace Hexalith.EventStore.Contracts.Validation;

public record PreflightValidationResult(
    bool IsAuthorized,
    string? Reason = null);

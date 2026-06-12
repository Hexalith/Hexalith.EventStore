namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Stage in the optional pre-commit admission chain executed by the DomainService SDK before
/// <c>POST /process</c> dispatches to the keyed domain processor.
/// </summary>
public interface IDomainServiceAdmissionStage {
    /// <summary>
    /// Gets the metadata-only stage name used for deterministic diagnostics. Registration order, not this name,
    /// controls execution order.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Evaluates whether the command may proceed to the domain processor.
    /// </summary>
    /// <param name="context">The command admission context.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>An accepted result or a rejected result with one or more typed rejection events.</returns>
    Task<DomainServiceAdmissionResult> EvaluateAsync(
        DomainServiceAdmissionContext context,
        CancellationToken cancellationToken);
}

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Obtains short-lived workload assertions from the trusted JWT issuer for outbound internal calls.
/// </summary>
/// <remarks>
/// An issuer never invents trust: it either signs with the shared contract's own key (Development or an explicit
/// non-Production break-glass environment) or obtains the assertion from the configured authority. When neither is
/// available it returns <see langword="null"/>, and the receiver denies the call.
/// </remarks>
public interface IWorkloadAssertionIssuer
{
    /// <summary>
    /// Gets whether the issuer can embed per-request resource bindings (tenant, projection type, topic) in the
    /// assertions it issues. An issuer that cannot refuses every request carrying bindings instead of issuing an
    /// unbound assertion.
    /// </summary>
    bool CanBindResources { get; }

    /// <summary>
    /// Issues an assertion for one receiving audience and operation.
    /// </summary>
    /// <param name="request">The audience, operation, and optional resource bindings.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The serialized assertion, or <see langword="null"/> when none can be issued.</returns>
    ValueTask<string?> IssueAsync(WorkloadAssertionRequest request, CancellationToken cancellationToken = default);
}

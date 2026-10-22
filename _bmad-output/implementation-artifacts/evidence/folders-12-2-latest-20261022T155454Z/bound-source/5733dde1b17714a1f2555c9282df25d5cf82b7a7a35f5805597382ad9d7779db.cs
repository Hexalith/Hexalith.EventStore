using System.Security.Claims;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// The outcome of evaluating a signature-valid workload assertion against receiver policy.
/// </summary>
public sealed class WorkloadAssertionEvaluation
{
    private WorkloadAssertionEvaluation(
        string? reasonCode,
        ClaimsPrincipal? principal,
        string? caller,
        IReadOnlyCollection<string> operations,
        IReadOnlyDictionary<string, string> bindings)
    {
        ReasonCode = reasonCode;
        Principal = principal;
        Caller = caller;
        Operations = operations;
        Bindings = bindings;
    }

    /// <summary>Gets a value indicating whether the assertion satisfied receiver policy.</summary>
    public bool Succeeded => ReasonCode is null;

    /// <summary>Gets the bounded denial reason, or <see langword="null"/> on success.</summary>
    public string? ReasonCode { get; }

    /// <summary>Gets the rebuilt minimal principal on success.</summary>
    public ClaimsPrincipal? Principal { get; }

    /// <summary>Gets the authenticated workload identity on success.</summary>
    public string? Caller { get; }

    /// <summary>Gets the operations the assertion grants.</summary>
    public IReadOnlyCollection<string> Operations { get; }

    /// <summary>Gets the resource bindings the assertion carries, keyed by claim type.</summary>
    public IReadOnlyDictionary<string, string> Bindings { get; }

    /// <summary>Creates a failed evaluation.</summary>
    /// <param name="reasonCode">The bounded reason code.</param>
    /// <returns>The failed evaluation.</returns>
    public static WorkloadAssertionEvaluation Failure(string reasonCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        return new WorkloadAssertionEvaluation(
            reasonCode,
            null,
            null,
            [],
            new Dictionary<string, string>(StringComparer.Ordinal));
    }

    /// <summary>Creates a successful evaluation.</summary>
    /// <param name="principal">The rebuilt minimal principal.</param>
    /// <param name="caller">The authenticated workload.</param>
    /// <param name="operations">The granted operations.</param>
    /// <param name="bindings">The resource bindings.</param>
    /// <returns>The successful evaluation.</returns>
    public static WorkloadAssertionEvaluation Success(
        ClaimsPrincipal principal,
        string caller,
        IReadOnlyCollection<string> operations,
        IReadOnlyDictionary<string, string> bindings)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(caller);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(bindings);
        return new WorkloadAssertionEvaluation(null, principal, caller, operations, bindings);
    }
}

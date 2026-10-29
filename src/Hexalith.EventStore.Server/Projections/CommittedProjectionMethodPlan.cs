using Hexalith.EventStore.Server.DomainServices;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>One tenant's captured executable app/version and its explicitly declared projection methods.</summary>
/// <param name="Registration">The exact app and contract version; its method is an actually declared path.</param>
/// <param name="LegacyOutputs">Outputs owned by the declared legacy method.</param>
/// <param name="NamedOutputs">Outputs owned by the declared named method.</param>
internal sealed record CommittedProjectionMethodPlan(DomainServiceRegistration Registration,
    IReadOnlyList<string> LegacyOutputs, IReadOnlyList<string> NamedOutputs)
{
    /// <summary>Gets whether the captured plan includes legacy execution.</summary>
    public bool Legacy => LegacyOutputs.Count > 0;
    /// <summary>Gets whether the captured plan includes named execution.</summary>
    public bool Named => NamedOutputs.Count > 0;
}

using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Records, on the EventStore resource, the domain services allow-listed to submit trusted effects.
/// </summary>
internal sealed class EventStoreTrustedEffectSubmittersAnnotation : IResourceAnnotation
{
    /// <summary>Gets the allow-listed workload identities, in registration order.</summary>
    public List<string> Workloads { get; } = [];
}

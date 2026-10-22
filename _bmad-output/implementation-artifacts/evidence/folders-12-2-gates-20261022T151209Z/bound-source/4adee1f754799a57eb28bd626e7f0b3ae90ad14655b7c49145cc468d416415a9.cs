using System.Text;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

using NSubstitute;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Synthetic infrastructure around the shipped Server hook/core; no installed custody, physical destruction or authority qualification.</summary>
internal sealed class InteractionPayloadProtectionHookFixture
{
    internal InteractionOccurrenceProtectorFixture Occurrences { get; } = new();
    internal IInteractionProtectionBindingProvider Bindings { get; } = Substitute.For<IInteractionProtectionBindingProvider>();
    internal InteractionProtectionBinding Binding { get; set; }
    internal InteractionPayloadProtectionService Service { get; }
    internal bool Erased { get; set; }

    internal InteractionPayloadProtectionHookFixture()
    {
        Binding = new(Occurrences.Identity, "digest-v1", 1, InteractionOccurrenceProtectorFixture.Reference,
            InteractionOccurrenceProtectorFixture.Reference, 1);
        Bindings.ResolveAsync(Arg.Any<AggregateIdentity>(), Arg.Any<PayloadProtectionOccurrenceContext>(),
            Arg.Any<ProtectionTarget?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(_ => Binding);
        Bindings.AcquireCompletionLeaseAsync(Arg.Any<InteractionProtectionBinding>(), Arg.Any<CancellationToken>()).Returns(true);
        Bindings.IsIrreversiblyErasedAsync(Arg.Any<InteractionProtectionBinding>(), Arg.Any<CancellationToken>()).Returns(_ => Erased);
        Service = new(Occurrences.Registry, Occurrences.Keys, Bindings, TimeProvider.System);
    }

    internal PayloadProtectionOccurrenceContext Occurrence => new(Binding.Identity.Sequence, Binding.Identity.Kind, Binding.Identity.PayloadTypeId);
    internal static byte[] Payload() => Encoding.UTF8.GetBytes("""
        {"Safe":1,"ProtectedContent":{"Target":{"TenantId":"tenant-a","AgentInteractionId":"interaction-a","TargetProtectionKeyAlias":"alias-a"},"Value":{"Secret":"confidential-canary"}}}
        """);
}

using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

using NSubstitute;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Synthetic delivery and retained-binding coverage for the fail-closed adapters. These checks do not install a profile or a secret.</summary>
public sealed class InteractionPayloadProtectionServiceTests
{
    /// <summary>A current delivery and retained binding seal only the content value.</summary>
    [Fact]
    public async Task CurrentDeliverySealsOnlyTheContentValue()
    {
        ProtectionWorld world = ProtectionWorld.Open();
        PayloadProtectionWriteResult write = await world.ProtectAsync();
        string json = Encoding.UTF8.GetString(write.ProtectionResult.PayloadBytes);
        json.ShouldNotContain("confidential-canary");
        using JsonDocument document = JsonDocument.Parse(write.ProtectionResult.PayloadBytes);
        document.RootElement.GetProperty("Safe").GetInt32().ShouldBe(1);
        document.RootElement.GetProperty("ProtectedContent").GetProperty("Target").GetProperty("TargetProtectionKeyAlias").GetString().ShouldBe("alias-a");
        PayloadProtectionWriteResult retry = await world.ProtectAsync();
        retry.ProtectionResult.PayloadBytes.ShouldBe(write.ProtectionResult.PayloadBytes);
    }

    /// <summary>A missing key or binding denies before reservation and writes no ciphertext.</summary>
    [Fact]
    public async Task AbsentKeyOrBindingDeniesBeforeCiphertext()
    {
        ProtectionWorld missingKey = ProtectionWorld.Open(delivery: false);
        InvalidOperationException keyDenied = await Should.ThrowAsync<InvalidOperationException>(() => missingKey.ProtectAsync());
        keyDenied.Message.ShouldBe("ProtectionAuthorityUnavailable");
        await missingKey.Occurrences.Registry.DidNotReceive().ReserveAsync(Arg.Any<InteractionOccurrenceRequest>(), Arg.Any<CancellationToken>());

        ProtectionWorld missingBinding = ProtectionWorld.Open(source: false);
        InvalidOperationException bindingDenied = await Should.ThrowAsync<InvalidOperationException>(() => missingBinding.ProtectAsync());
        bindingDenied.Message.ShouldBe("ProtectionAuthorityUnavailable");
        await missingBinding.Occurrences.Registry.DidNotReceive().ReserveAsync(Arg.Any<InteractionOccurrenceRequest>(), Arg.Any<CancellationToken>());

        var closed = new InteractionOccurrenceKeyProvider(null);
        InteractionOccurrenceIdentity identity = missingKey.Binding.Identity;
        (await closed.ResolveDigestAsync(identity, "digest-v1", TestContext.Current.CancellationToken)).ShouldBeNull();
        (await closed.ResolveRootAsync(identity, InteractionOccurrenceProtectorFixture.Reference, false, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await closed.IsCurrentAsync(identity, "Read", TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>One lease is granted. A second encryption, a missing tombstone, or a restored root denies.</summary>
    [Fact]
    public async Task OneLeaseDeniesASecondEncryptionMissingTombstoneOrRestoredRoot()
    {
        InteractionOccurrenceProtectorFixture occurrences = new();
        var delivery = Substitute.For<IInteractionOccurrenceKeyDelivery>();
        var keys = new InteractionOccurrenceKeyProvider(delivery);
        var request = new InteractionOccurrenceRequest(occurrences.Identity, "digest-v1", new string('A', 64), 1, InteractionOccurrenceProtectorFixture.Reference);
        InteractionOccurrenceRecord record = new(request, request.ProposedKeyReference, 1, InteractionOccurrenceWriterState.Reserved, null, null);
        delivery.DeliverAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new InteractionOccurrenceKeyDeliveryResult(true, false, false, Enumerable.Repeat((byte)9, 32).ToArray()));
        (await keys.IsCurrentAsync(occurrences.Identity, "Write", TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await keys.ResolveRootAsync(occurrences.Identity, InteractionOccurrenceProtectorFixture.Reference, true, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await keys.AcquireEncryptionLeaseAsync(record, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await keys.AcquireEncryptionLeaseAsync(record, TestContext.Current.CancellationToken)).ShouldBeFalse();
        var shortDelivery = Substitute.For<IInteractionOccurrenceKeyDelivery>();
        shortDelivery.DeliverAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new InteractionOccurrenceKeyDeliveryResult(true, false, false, new byte[16]));
        var shortKeys = new InteractionOccurrenceKeyProvider(shortDelivery);
        (await shortKeys.AcquireEncryptionLeaseAsync(record, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await shortKeys.IsCurrentAsync(occurrences.Identity, "Write", TestContext.Current.CancellationToken)).ShouldBeFalse();

        var tombstone = Substitute.For<IInteractionOccurrenceKeyDelivery>();
        tombstone.DeliverAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new InteractionOccurrenceKeyDeliveryResult(true, true, false, Enumerable.Repeat((byte)9, 32).ToArray()));
        var missingTombstone = new InteractionOccurrenceKeyProvider(tombstone);
        (await missingTombstone.AcquireEncryptionLeaseAsync(record, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await missingTombstone.ResolveRootAsync(occurrences.Identity, InteractionOccurrenceProtectorFixture.Reference, false, TestContext.Current.CancellationToken)).ShouldBeNull();

        var restored = Substitute.For<IInteractionOccurrenceKeyDelivery>();
        restored.DeliverAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new InteractionOccurrenceKeyDeliveryResult(false, false, false, Enumerable.Repeat((byte)4, 32).ToArray()));
        (await new InteractionOccurrenceKeyProvider(restored).ResolveDigestAsync(occurrences.Identity, "digest-v1", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>Lookup uses the retained target. An effective pin blocks destruction until that same hold is released.</summary>
    [Fact]
    public async Task RetainedTargetAndHoldControlErasureWithoutASecondEffect()
    {
        InteractionProtectionBinding binding = ProtectionWorld.BindingFor(new InteractionOccurrenceProtectorFixture().Identity);
        var source = Substitute.For<IInteractionProtectionBindingSource>();
        source.FindRetainedAsync(binding.Identity.Owner, Arg.Any<PayloadProtectionOccurrenceContext>(), Arg.Any<CancellationToken>()).Returns(binding);
        var bindings = new InteractionProtectionBindingProvider(source);
        PayloadProtectionOccurrenceContext occurrence = new(binding.Identity.Sequence, binding.Identity.Kind, binding.Identity.PayloadTypeId);
        ProtectionTarget requested = new("tenant-a", "interaction-a", "alias-b");
        (await bindings.ResolveAsync(binding.Identity.Owner, occurrence, requested, false, TestContext.Current.CancellationToken)).ShouldBeNull();
        await source.Received().FindRetainedAsync(binding.Identity.Owner, occurrence, Arg.Any<CancellationToken>());
        (await bindings.ResolveAsync(binding.Identity.Owner, occurrence, binding.Identity.Target, true, TestContext.Current.CancellationToken)).ShouldBe(binding);

        source.TryAcquireCompletionLeaseAsync(binding, Arg.Any<CancellationToken>()).Returns(false);
        (await bindings.AcquireCompletionLeaseAsync(binding, TestContext.Current.CancellationToken)).ShouldBeFalse();
        source.TryAcquireCompletionLeaseAsync(binding, Arg.Any<CancellationToken>()).Returns(true);
        (await bindings.AcquireCompletionLeaseAsync(binding, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await bindings.AcquireCompletionLeaseAsync(binding, TestContext.Current.CancellationToken)).ShouldBeFalse();
        await source.Received(2).TryAcquireCompletionLeaseAsync(binding, Arg.Any<CancellationToken>());

        var erasure = Substitute.For<IInteractionProtectionBindingSource>();
        var erased = new InteractionProtectionBindingProvider(erasure);
        (await erased.IsIrreversiblyErasedAsync(binding, TestContext.Current.CancellationToken)).ShouldBeFalse();
        InteractionProtectionErasureWitness complete = Witness(binding, null, null, false);
        erasure.ReadErasureAsync(binding, Arg.Any<CancellationToken>()).Returns(complete with { TransportKnown = false }, complete);
        (await erased.IsIrreversiblyErasedAsync(binding, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await erased.IsIrreversiblyErasedAsync(binding, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await bindings.ResolveAsync(binding.Identity.Owner, occurrence, null, false, TestContext.Current.CancellationToken)).ShouldBe(binding);
        await erasure.DidNotReceive().TryAcquireCompletionLeaseAsync(Arg.Any<InteractionProtectionBinding>(), Arg.Any<CancellationToken>());
        await source.DidNotReceive().ReadErasureAsync(Arg.Any<InteractionProtectionBinding>(), Arg.Any<CancellationToken>());

        erasure.ReadErasureAsync(binding, Arg.Any<CancellationToken>()).Returns(Witness(binding, "hold-a", "hold-b", false));
        (await new InteractionProtectionBindingProvider(erasure).IsIrreversiblyErasedAsync(binding, TestContext.Current.CancellationToken)).ShouldBeFalse();
        erasure.ReadErasureAsync(binding, Arg.Any<CancellationToken>()).Returns(Witness(binding, "hold-a", "hold-a", false));
        (await new InteractionProtectionBindingProvider(erasure).IsIrreversiblyErasedAsync(binding, TestContext.Current.CancellationToken)).ShouldBeTrue();
        erasure.ReadErasureAsync(binding, Arg.Any<CancellationToken>()).Returns(Witness(binding, null, null, true));
        (await new InteractionProtectionBindingProvider(erasure).IsIrreversiblyErasedAsync(binding, TestContext.Current.CancellationToken)).ShouldBeFalse();
        erasure.ReadErasureAsync(binding, Arg.Any<CancellationToken>()).Returns(complete with { ManifestDigest = new string('A', 64) });
        (await new InteractionProtectionBindingProvider(erasure).IsIrreversiblyErasedAsync(binding, TestContext.Current.CancellationToken)).ShouldBeFalse();
        erasure.ReadErasureAsync(binding, Arg.Any<CancellationToken>()).Returns(complete with { Partial = true });
        (await new InteractionProtectionBindingProvider(erasure).IsIrreversiblyErasedAsync(binding, TestContext.Current.CancellationToken)).ShouldBeFalse();
        erasure.ReadErasureAsync(binding, Arg.Any<CancellationToken>()).Returns(complete with { TransportKnown = false });
        (await new InteractionProtectionBindingProvider(erasure).IsIrreversiblyErasedAsync(binding, TestContext.Current.CancellationToken)).ShouldBeFalse();

        string[] sortedTargets = [.. complete.AdmittedTargets.Order(StringComparer.Ordinal)];
        string[] sortedReceipts = [.. complete.OriginalReceipts.Order(StringComparer.Ordinal)];
        erasure.ReadErasureAsync(binding, Arg.Any<CancellationToken>()).Returns(complete with
        {
            AdmittedTargets = sortedTargets,
            OriginalReceipts = sortedReceipts,
            OrderDigest = complete.OrderDigest,
        });
        (await new InteractionProtectionBindingProvider(erasure).IsIrreversiblyErasedAsync(binding, TestContext.Current.CancellationToken)).ShouldBeFalse();

        string retained = InteractionProtectionBindingProvider.TargetId(binding.Identity.Target);
        string[] duplicated = [retained, retained];
        string[] duplicateReceipts = ["receipt-a", "receipt-b"];
        erasure.ReadErasureAsync(binding, Arg.Any<CancellationToken>()).Returns(complete with
        {
            AdmittedTargets = duplicated,
            OriginalReceipts = duplicateReceipts,
            OrderDigest = InteractionProtectionBindingProvider.ReceiptOrderDigest(duplicated, duplicateReceipts),
        });
        var duplicate = new InteractionProtectionBindingProvider(erasure);
        (await duplicate.IsIrreversiblyErasedAsync(binding, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await duplicate.LookupOriginalReceiptsAsync(binding, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>A lost transport still returns the original receipt vector and starts no pin, unpin, or destroy.</summary>
    [Fact]
    public async Task LostResultLookupReturnsTheRetainedReceiptsAndStartsNoEffect()
    {
        InteractionProtectionBinding binding = ProtectionWorld.BindingFor(new InteractionOccurrenceProtectorFixture().Identity);
        InteractionProtectionErasureWitness complete = Witness(binding, null, null, false);
        var source = Substitute.For<IInteractionProtectionBindingSource>();
        var bindings = new InteractionProtectionBindingProvider(source);
        source.ReadErasureAsync(binding, Arg.Any<CancellationToken>()).Returns(complete with { TransportKnown = false });
        IReadOnlyList<string>? receipts = await bindings.LookupOriginalReceiptsAsync(binding, TestContext.Current.CancellationToken);
        receipts.ShouldBe(complete.OriginalReceipts);
        (await bindings.IsIrreversiblyErasedAsync(binding, TestContext.Current.CancellationToken)).ShouldBeFalse();
        await source.DidNotReceive().TryAcquireCompletionLeaseAsync(Arg.Any<InteractionProtectionBinding>(), Arg.Any<CancellationToken>());
        await source.DidNotReceive().FindRetainedAsync(Arg.Any<AggregateIdentity>(), Arg.Any<PayloadProtectionOccurrenceContext>(), Arg.Any<CancellationToken>());

        source.ReadErasureAsync(binding, Arg.Any<CancellationToken>()).Returns(complete with { ManifestDigest = new string('B', 64) });
        (await bindings.LookupOriginalReceiptsAsync(binding, TestContext.Current.CancellationToken)).ShouldBeNull();
        source.ReadErasureAsync(binding, Arg.Any<CancellationToken>()).Returns(complete with { Partial = true, OriginalReceipts = ["receipt-a"] });
        (await bindings.LookupOriginalReceiptsAsync(binding, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await new InteractionProtectionBindingProvider(null).LookupOriginalReceiptsAsync(binding, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    private static InteractionProtectionErasureWitness Witness(InteractionProtectionBinding binding, string? hold, string? unpinned, bool unknownPin)
    {
        string target = InteractionProtectionBindingProvider.TargetId(binding.Identity.Target);
        string[] targets = ["tenant-a\u001finteraction-b\u001falias-b", target];
        string[] receipts = ["receipt-a", "receipt-b"];
        return new(
            InteractionProtectionBindingProvider.ManifestDigest(binding),
            InteractionProtectionBindingProvider.ReceiptOrderDigest(targets, receipts),
            targets,
            receipts,
            true,
            false,
            hold,
            unknownPin,
            unpinned);
    }

    private sealed class ProtectionWorld
    {
        private ProtectionWorld(InteractionOccurrenceProtectorFixture occurrences, InteractionProtectionBinding binding, InteractionPayloadProtectionService service)
        {
            Occurrences = occurrences;
            Binding = binding;
            Service = service;
        }

        internal InteractionOccurrenceProtectorFixture Occurrences { get; }

        internal InteractionProtectionBinding Binding { get; }

        internal InteractionPayloadProtectionService Service { get; }

        internal static ProtectionWorld Open(bool delivery = true, bool source = true)
        {
            var occurrences = new InteractionOccurrenceProtectorFixture();
            InteractionProtectionBinding binding = BindingFor(occurrences.Identity);
            IInteractionOccurrenceKeyDelivery? keys = delivery ? Delivery() : null;
            IInteractionProtectionBindingSource? bindings = source ? Source(binding) : null;
            var service = new InteractionPayloadProtectionService(occurrences.Registry, new InteractionOccurrenceKeyProvider(keys), new InteractionProtectionBindingProvider(bindings), TimeProvider.System);
            return new(occurrences, binding, service);
        }

        internal static InteractionProtectionBinding BindingFor(InteractionOccurrenceIdentity identity)
            => new(identity, "digest-v1", 1, InteractionOccurrenceProtectorFixture.Reference, InteractionOccurrenceProtectorFixture.Reference, 1);

        internal Task<PayloadProtectionWriteResult> ProtectAsync()
            => Service.ProtectEventPayloadAsync(
                Binding.Identity.Owner,
                Substitute.For<IEventPayload>(),
                Binding.Identity.PayloadTypeId,
                Payload(),
                "json",
                new PayloadProtectionOccurrenceContext(Binding.Identity.Sequence, Binding.Identity.Kind, Binding.Identity.PayloadTypeId),
                TestContext.Current.CancellationToken);

        private static byte[] Payload()
            => Encoding.UTF8.GetBytes("""
                {"Safe":1,"ProtectedContent":{"Target":{"TenantId":"tenant-a","AgentInteractionId":"interaction-a","TargetProtectionKeyAlias":"alias-a"},"Value":{"Secret":"confidential-canary"}}}
                """);

        private static IInteractionOccurrenceKeyDelivery Delivery()
        {
            var delivery = Substitute.For<IInteractionOccurrenceKeyDelivery>();
            delivery.DeliverAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    byte seed = call.ArgAt<string>(1) == "tenant-digest" ? (byte)7 : (byte)9;
                    return new InteractionOccurrenceKeyDeliveryResult(true, false, false, Enumerable.Repeat(seed, 32).ToArray());
                });
            return delivery;
        }

        private static IInteractionProtectionBindingSource Source(InteractionProtectionBinding binding)
        {
            var source = Substitute.For<IInteractionProtectionBindingSource>();
            source.FindRetainedAsync(binding.Identity.Owner, Arg.Any<PayloadProtectionOccurrenceContext>(), Arg.Any<CancellationToken>()).Returns(binding);
            source.TryAcquireCompletionLeaseAsync(binding, Arg.Any<CancellationToken>()).Returns(true);
            return source;
        }
    }
}

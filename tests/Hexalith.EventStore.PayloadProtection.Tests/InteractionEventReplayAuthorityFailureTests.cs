using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

using NSubstitute;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Unavailable independent loss-proof authority stays unreadable and caller cancellation remains observable.</summary>
public sealed class InteractionEventReplayAuthorityFailureTests
{
    /// <summary>A proof failure cannot expose plaintext, mutate a retained original or swallow caller cancellation.</summary>
    [Theory]
    [InlineData("outage")]
    [InlineData("cancel-outage")]
    [InlineData("cancel-return")]
    [InlineData("foreign-cancel")]
    public async Task LossProofFailureIsTypedAndCallerCancellationKeepsItsToken(string mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        var fixture = new InteractionOccurrenceProtectorFixture();
        var identity = fixture.Identity;
        var binding = new InteractionProtectionBinding(identity, "digest-v1", 1,
            InteractionOccurrenceProtectorFixture.Reference, InteractionOccurrenceProtectorFixture.Reference, 1);
        var bindings = Substitute.For<IInteractionProtectionBindingProvider>();
        bindings.ResolveAsync(identity.Owner, Arg.Any<PayloadProtectionOccurrenceContext>(), Arg.Any<ProtectionTarget?>(),
            Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(binding);
        bindings.AcquireCompletionLeaseAsync(binding, Arg.Any<CancellationToken>()).Returns(true);
        var engine = new InteractionPayloadProtectionService(fixture.Registry, fixture.Keys, bindings, TimeProvider.System);
        var occurrence = new PayloadProtectionOccurrenceContext(identity.Sequence, identity.Kind, identity.PayloadTypeId);
        byte[] clear = JsonSerializer.SerializeToUtf8Bytes(new { ProtectedContent = new { Target = identity.Target, Value = new { Secret = "__HEXALITH_TEST_SECRET__" } } });
        var write = await engine.ProtectEventPayloadAsync(identity.Owner, Substitute.For<IEventPayload>(), identity.PayloadTypeId,
            clear, "json", occurrence, TestContext.Current.CancellationToken);
        await engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted,
            TestContext.Current.CancellationToken);
        byte[] original = fixture.Record!.Sealed!.PayloadBytes.ToArray();
        int registryCalls = fixture.Registry.ReceivedCalls().Count();
        using var cancellation = new CancellationTokenSource();
        bindings.IsCustodyPermanentlyLostAsync(identity.Owner, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (mode.StartsWith("cancel-", StringComparison.Ordinal)) { cancellation.Cancel(); }
            return mode == "cancel-return" ? Task.FromResult(false)
                : Task.FromException<bool>(mode == "foreign-cancel" ? new OperationCanceledException() : new IOException("proof authority unavailable"));
        });

        Task<PayloadReplayResult> Replay() => engine.TryReplayEventPayloadAsync(identity.Owner, identity.PayloadTypeId,
            write.ProtectionResult.PayloadBytes, write.ProtectionResult.SerializationFormat, write.ProtectionResult.Metadata, occurrence, cancellation.Token);
        if (mode.StartsWith("cancel-", StringComparison.Ordinal))
        {
            var error = await Should.ThrowAsync<OperationCanceledException>(Replay);
            error.CancellationToken.ShouldBe(cancellation.Token);
        }
        else
        {
            var replay = await Replay();
            replay.Outcome.IsUnreadable.ShouldBeTrue();
            replay.Outcome.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
            replay.Outcome.PayloadBytes.ShouldBeNull(); replay.HasErasedContent.ShouldBeFalse();
        }
        fixture.Record.Sealed.PayloadBytes.ShouldBe(original);
        fixture.Record.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        fixture.Registry.ReceivedCalls().Count().ShouldBe(registryCalls);
        fixture.OwnedBuffers.All(bytes => bytes.All(value => value == 0)).ShouldBeTrue();
    }
}

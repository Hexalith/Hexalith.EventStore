using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using NSubstitute;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Synthetic private client/custody/once-only lease for actual adapter/core tests; no provider, DAPR ACL or antirollback qualification.</summary>
internal sealed class InteractionOccurrenceProtectorFixture
{
    /// <summary>Gets the original reference.</summary>
    internal const string Reference = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
    /// <summary>Gets exact candidate occurrence.</summary>
    internal InteractionOccurrenceIdentity Identity { get; } = new(new("tenant-a", "interaction-a", "alias-a"), new AggregateIdentity("tenant-a", "agents", "interaction-a"),
        PayloadProtectionPayloadKind.Event, 1, "InteractionCreated", "root-v1", "candidate-hkdf-sha256-v1");
    /// <summary>Gets synthetic exact registry transport.</summary>
    internal IInteractionOccurrenceRegistry Registry { get; } = Substitute.For<IInteractionOccurrenceRegistry>();
    /// <summary>Gets synthetic independently scoped key/lease provider.</summary>
    internal InteractionOccurrenceTestKeys Keys { get; } = new();
    /// <summary>Gets the actual adapter.</summary>
    internal InteractionOccurrenceProtector Adapter { get; }
    /// <summary>Gets transferred key buffers for real zero assertions.</summary>
    internal List<byte[]> OwnedBuffers { get; } = [];
    /// <summary>Gets or sets private recorded state.</summary>
    internal InteractionOccurrenceRecord? Record { get; set; }
    /// <summary>Gets or sets current authority.</summary>
    internal bool Current { get; set; } = true;
    /// <summary>Initializes synthetic seams around the actual production adapter/core.</summary>
    internal InteractionOccurrenceProtectorFixture()
    {
        Keys.Allocate = Owned; Keys.Current = () => Current;
        Registry.ReserveAsync(Arg.Any<InteractionOccurrenceRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.ArgAt<InteractionOccurrenceRequest>(0);
            if (Record is null) { Record = new(request, request.ProposedKeyReference, 1, InteractionOccurrenceWriterState.Reserved, null, null); }
            if (Record.Request.Identity != request.Identity || Record.Request.DigestKeyVersion != request.DigestKeyVersion || Record.Request.ContentIntentHmac != request.ContentIntentHmac
                || Record.Request.ReservationAttemptOrdinal != request.ReservationAttemptOrdinal) { return new(InteractionOccurrenceReservationStatus.Conflict, null); }
            return Result();
        });
        Registry.RetainSealedAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<InteractionOccurrenceSealedResult>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            Record = Record! with { Sealed = call.ArgAt<InteractionOccurrenceSealedResult>(1), RegistryRevision = 2, WriterState = InteractionOccurrenceWriterState.SealedPending }; return Result();
        });
        Registry.CompleteWriterAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (call.ArgAt<string>(1) != Record!.KeyReference) { return new(InteractionOccurrenceReservationStatus.Conflict, null); }
            Record = Record with { WriterState = call.ArgAt<bool>(3) ? InteractionOccurrenceWriterState.Active : InteractionOccurrenceWriterState.Aborted,
                RegistryRevision = 3, WriterProofId = call.ArgAt<string>(2) }; return Result();
        });
        Registry.LookupAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<CancellationToken>()).Returns(call =>
            Record?.Request.Identity == call.ArgAt<InteractionOccurrenceIdentity>(0) ? Result() : new(InteractionOccurrenceReservationStatus.Unavailable, null));
        Adapter = new(Registry, Keys, TimeProvider.System);
    }
    /// <summary>Creates retained test buffers with independently asserted disposal.</summary>
    internal InteractionOccurrenceOwnedKey Owned(InteractionOccurrenceIdentity identity, string purpose, string version, byte seed)
    {
        byte[] bytes = Enumerable.Repeat(seed, 32).ToArray(); OwnedBuffers.Add(bytes); return new(identity.Target, purpose, version, bytes);
    }
    private InteractionOccurrenceReservationResult Result() => new(Record?.Sealed is null ? InteractionOccurrenceReservationStatus.Reserved : InteractionOccurrenceReservationStatus.Sealed, Record);
}

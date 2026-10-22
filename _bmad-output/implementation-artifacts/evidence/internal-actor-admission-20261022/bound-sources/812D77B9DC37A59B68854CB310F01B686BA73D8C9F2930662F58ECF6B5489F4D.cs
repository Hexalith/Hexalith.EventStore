using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Binds a certified cut to an authenticated source using the existing raw-envelope source codec.</summary>
internal static class NativeSourcePrefix
{
    internal static bool Matches(AuthoritativeEventStream source, SourcePublicationHead target,
        AuthoritativeStreamReadDeadline deadline)
    {
        if (target.NativeProof is not { } selected) return true;
        if (source.NativeProof is not { Erased: false, RetainedFloor: 1 } actual
            || !NativeSourceProofCodec.IsValid(actual, target.Identity, source.Head, 1)
            || actual.SourceIncarnation != selected.SourceIncarnation || source.Events.Count != source.Head)
            return false;

        var chain = NativeSourceProofCodec.CreateSeed(target.Identity, actual.SourceIncarnation);
        bool matched = target.Head == 0 && chain == selected;
        foreach (var item in source.Events)
        {
            deadline.ThrowIfCancellationRequested();
            if (item is null || item.NativeEventDigest is null) return false;
            chain = NativeSourceProofCodec.ExtendDigest(chain, target.Identity, item.SequenceNumber, item.MessageId, item.NativeEventDigest);
            if (chain.HeadSequence == target.Head) matched = chain == selected;
        }
        return matched && chain == actual;
    }
}

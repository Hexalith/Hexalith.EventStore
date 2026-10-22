using System.Text.Json;
using ByteAether.Ulid;

using Hexalith.EventStore.Client.Security;

namespace Hexalith.EventStore.Server.Identity;

/// <summary>Pure namespace transitions; reads never enroll and alias ownership never changes implicitly.</summary>
public static class ActorRegistryTransitions
{
    /// <summary>Applies a verified exact-retry mutation without changing the supplied state.</summary>
    public static (ActorRegistryState State, ActorRegistryEntry Result) Apply(ActorRegistryState state, ActorRegistryMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(mutation);
        ArgumentException.ThrowIfNullOrWhiteSpace(mutation.OperationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mutation.ProvenanceId);
        if (!Ulid.TryParse(mutation.ActorId, null, out Ulid parsed)
            || !string.Equals(parsed.ToString(), mutation.ActorId, StringComparison.Ordinal)
            || mutation.AliasDigest.Length != 64 || !mutation.AliasDigest.All(Uri.IsHexDigit)
            || !mutation.ContinuityVerified)
        {
            throw new InvalidOperationException("Registry mutation evidence is invalid.");
        }

        string digest = IdentityAdmissionProof.Digest(JsonSerializer.SerializeToUtf8Bytes(mutation));
        if (state.Operations.TryGetValue(mutation.OperationId, out ActorRegistryOperation? previous))
        {
            return previous.IntentDigest == digest
                ? (state, previous.Result)
                : throw new InvalidOperationException("Registry intent conflicts.");
        }

        if (state.Aliases.TryGetValue(mutation.AliasDigest, out ActorRegistryAlias? assigned) && assigned.ActorId != mutation.ActorId)
        {
            throw new InvalidOperationException("Registry alias conflicts.");
        }

        state.Actors.TryGetValue(mutation.ActorId, out ActorRegistryEntry? existing);
        if ((existing?.Revision ?? 0) != mutation.ExpectedRevision)
        {
            throw new InvalidOperationException("Registry revision conflicts.");
        }

        var result = new ActorRegistryEntry(mutation.ActorId, checked(mutation.ExpectedRevision + 1), mutation.Active, mutation.ProvenanceId);
        var aliases = new Dictionary<string, ActorRegistryAlias>(state.Aliases, StringComparer.Ordinal)
        {
            [mutation.AliasDigest] = new(mutation.ActorId, mutation.AliasActive, (assigned?.Revision ?? 0) + 1),
        };
        if (mutation.RetiredAliasDigest is { } retired)
        {
            if (retired == mutation.AliasDigest || !aliases.TryGetValue(retired, out ActorRegistryAlias? predecessor)
                || predecessor.ActorId != mutation.ActorId)
            {
                throw new InvalidOperationException("Registry continuity conflicts.");
            }

            aliases[retired] = predecessor with { Active = false, Revision = predecessor.Revision + 1 };
        }
        var actors = new Dictionary<string, ActorRegistryEntry>(state.Actors, StringComparer.Ordinal) { [mutation.ActorId] = result };
        var operations = new Dictionary<string, ActorRegistryOperation>(state.Operations, StringComparer.Ordinal)
        {
            [mutation.OperationId] = new(digest, result),
        };
        return (new(aliases, actors, operations), result);
    }
}

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Candidate private exact atomic writer input; classification and source membership require independent authoritative contract/owner validation.</summary>
/// <param name="Command">Original exact domain command; raw bytes are transient, never stored in boundary metadata.</param><param name="EpochId">Current installed epoch.</param>
/// <param name="Kind">Closed concrete contract category.</param><param name="ExpectedStreamRevision">Conditional target revision.</param><param name="PermitOwner">Exact original directory owner.</param>
/// <param name="SourceConversationId">Immutable permit/first-event source.</param><param name="PermitId">Original permit.</param><param name="DirectoryCapabilityId">Exact original permit/effect capability.</param>
/// <param name="CapabilityOwnerRevision">Original capability revision.</param><param name="ContentIntentHmac">Retained tenant DigestKey HMAC covering exact command intent, including bytes.</param>
/// <param name="DigestKeyVersion">Original retained fingerprint-key version.</param>
public sealed record DirectoryAtomicAppendRequest(CommandEnvelope Command, string EpochId, DirectoryWriteKind Kind, long ExpectedStreamRevision,
    AggregateIdentity PermitOwner, string SourceConversationId, string PermitId, string DirectoryCapabilityId, long CapabilityOwnerRevision,
    string ContentIntentHmac, string DigestKeyVersion)
{
    /// <inheritdoc/>
    public override string ToString() => nameof(DirectoryAtomicAppendRequest);
}

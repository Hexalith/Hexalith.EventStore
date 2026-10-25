using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Mandatory independently delivered current evidence, separate from the authority and primary backup/restore domains.</summary>
/// <remarks>The installed verifier authenticates complete inventory, all-writer enforcement, role enrollment, storage identity,
/// receiver binding, revocation and the current nonrollback epoch. It must use locally available fresh authenticated evidence,
/// perform no external I/O inside an authority-owner turn, and return false for missing, stale or unknown evidence.
/// No static configuration or available default implements this production trust decision.</remarks>
public interface ISourcePublicationAuthorityInstallationEvidence
{
    /// <summary>Verifies every installation claim against independently governed current evidence.</summary>
    bool IsCurrent(SourcePublicationAuthorityEnrollment enrollment);
}

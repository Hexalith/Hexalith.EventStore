using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Request to begin (or resume) an erase operation.</summary>
/// <param name="OperationId">Stable identifier of the erase operation (idempotency/resume key).</param>
/// <param name="ManifestDigest">Digest of the erase target manifest, recorded on first admission.</param>
[method: JsonConstructor]
public sealed record ProjectionEraseBeginRequest(
    string OperationId,
    string ManifestDigest) {
    /// <summary>
    /// Initializes a begin request with an explicit fresh-begin permission.
    /// </summary>
    /// <param name="operationId">Stable identifier of the erase operation.</param>
    /// <param name="manifestDigest">Digest of the erase target manifest.</param>
    /// <param name="allowBegin">
    /// Whether an idle scope may transition to erasing. A matching in-flight operation may resume regardless.
    /// </param>
    public ProjectionEraseBeginRequest(string operationId, string manifestDigest, bool allowBegin)
        : this(operationId, manifestDigest) => AllowBegin = allowBegin;

    /// <summary>
    /// Gets a value indicating whether an idle scope may transition to erasing. The default preserves the
    /// released two-argument request behavior for callers compiled before the fresh-begin gate existed.
    /// </summary>
    public bool AllowBegin { get; init; } = true;
}

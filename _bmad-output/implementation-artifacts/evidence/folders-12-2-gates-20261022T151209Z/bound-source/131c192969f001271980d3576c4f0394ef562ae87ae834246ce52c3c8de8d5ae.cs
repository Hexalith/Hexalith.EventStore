namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Exact immutable metadata outcome.</summary>
/// <param name="OperationId">Original operation.</param><param name="RequestDigest">Content-free exact original request digest.</param>
/// <param name="State">Terminal protocol state.</param><param name="CommittedRevision">Actual owning commit revision, separate from intended compare.</param>
public sealed record DirectoryBoundaryOutcome(string OperationId, string RequestDigest, DirectoryBoundaryOutcomeState State, long CommittedRevision);

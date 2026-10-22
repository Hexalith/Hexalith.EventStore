namespace Hexalith.EventStore.Contracts.Security;

/// <summary>One exact expected cell replacement inside the joint backend commit; no caller-authored arbitrary storage key is accepted.</summary>
/// <param name="CellId">Installation-scoped source/outbox/authorization cell identity.</param><param name="ExpectedRevision">Expected original source cell revision; zero is safe only while the same required guard ETag serializes every installed writer.</param>
/// <param name="ExpectedDigest">SHA-256 of original cell bytes, or the empty-byte digest for absence.</param><param name="NextValue">Already protected or content-free next serialized value.</param>
public sealed record GuardedStateMutation(string CellId, long ExpectedRevision, string ExpectedDigest, byte[] NextValue);

using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Complete identity of one immutable original repair-cohort operation; the operation is scoped to its authoritative owner.</summary>
/// <param name="Owner">Exact authoritative original owner.</param><param name="OperationId">Exact operation within that owner.</param>
public sealed record DirectoryRepairOriginalIdentity(AggregateIdentity Owner, string OperationId);

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Complete static AD-25 metadata supplied by the command and policy owners.</summary>
/// <param name="RouteId">Command route join.</param>
/// <param name="Generation">Complete envelope generation.</param>
/// <param name="Domain">Command domain matching its declaration.</param>
/// <param name="CommandType">Command discriminator matching its declaration.</param>
/// <param name="AdapterId">Trusted adapter identity.</param>
/// <param name="OperationId">Adapter operation identity, separate from the receiving workload operation.</param>
/// <param name="PolicyVersion">Explicit owner-declared policy version.</param>
/// <param name="DescriptorSchema">Canonical descriptor schema metadata.</param>
/// <param name="DescriptorDigest">Shared codec digest of the schema metadata.</param>
/// <param name="RetentionTier">Exactly mutation or commit; never inferred from a command name.</param>
/// <param name="ActiveDigestKeyGeneration">Active generation, also present among readers.</param>
/// <param name="ReaderDigestKeyGenerations">Complete unique reader generations.</param>
/// <param name="SecretStore">Logical secret store/component name.</param>
/// <param name="LogicalSecretName">Logical secret map name.</param>
/// <param name="SecretMapGeneration">Explicit generation marker of the logical map.</param>
/// <param name="KeyMap">Exactly one value-free map reference for every reader generation.</param>
/// <param name="ConsumerAppId">Explicit application consuming the digest keys.</param>
/// <param name="ContentDigest">Digest of the complete entry excluding this field, produced by the shared codec.</param>
public sealed record IdempotencyCatalogEntry(
    string RouteId,
    long Generation,
    string Domain,
    string CommandType,
    string AdapterId,
    string OperationId,
    string PolicyVersion,
    IdempotencyDescriptorSchema DescriptorSchema,
    string DescriptorDigest,
    string RetentionTier,
    string ActiveDigestKeyGeneration,
    IReadOnlyList<string> ReaderDigestKeyGenerations,
    string SecretStore,
    string LogicalSecretName,
    string SecretMapGeneration,
    IReadOnlyList<DigestKeyMapReference> KeyMap,
    string ConsumerAppId,
    string ContentDigest);

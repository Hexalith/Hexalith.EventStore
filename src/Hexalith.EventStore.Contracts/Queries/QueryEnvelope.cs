using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Immutable envelope carrying a query request to a projection actor.
/// </summary>
/// <remarks>
/// DAPR actor proxy uses <see cref="DataContractSerializer"/> for method
/// parameters. The data contract attributes are part of the public projection
/// adapter wire contract and must stay additive/backward-compatible.
/// </remarks>
// Namespace pinned to the original Server.Actors CLR namespace so DataContractSerializer
// wire documents remain compatible when callers and callees redeploy independently.
[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Hexalith.EventStore.Server.Actors")]
[KnownType(typeof(string[]))]
public record QueryEnvelope {
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryEnvelope"/> record.
    /// </summary>
    /// <param name="tenantId">The tenant identifier used for isolation and routing.</param>
    /// <param name="domain">The domain targeted by the query.</param>
    /// <param name="aggregateId">The aggregate or projection aggregate identifier.</param>
    /// <param name="queryType">The query type discriminator.</param>
    /// <param name="payload">UTF-8 JSON query payload bytes; use an empty array for tenant-wide list queries.</param>
    /// <param name="correlationId">The correlation identifier propagated from the gateway request.</param>
    /// <param name="userId">The authenticated user identifier, normally the <c>sub</c> claim.</param>
    /// <param name="entityId">Optional entity identifier for entity-scoped routing.</param>
    /// <param name="isGlobalAdmin">Trusted server-populated flag indicating the authenticated user has global administrator privileges.</param>
    public QueryEnvelope(
        string tenantId,
        string domain,
        string aggregateId,
        string queryType,
        byte[] payload,
        string correlationId,
        string userId,
        string? entityId = null,
        bool isGlobalAdmin = false)
        : this(
            tenantId,
            domain,
            aggregateId,
            queryType,
            payload,
            correlationId,
            userId,
            entityId,
            isGlobalAdmin,
            paging: null) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryEnvelope"/> record.
    /// </summary>
    /// <param name="tenantId">The tenant identifier used for isolation and routing.</param>
    /// <param name="domain">The domain targeted by the query.</param>
    /// <param name="aggregateId">The aggregate or projection aggregate identifier.</param>
    /// <param name="queryType">The query type discriminator.</param>
    /// <param name="payload">UTF-8 JSON query payload bytes; use an empty array for tenant-wide list queries.</param>
    /// <param name="correlationId">The correlation identifier propagated from the gateway request.</param>
    /// <param name="userId">The authenticated user identifier, normally the <c>sub</c> claim.</param>
    /// <param name="entityId">Optional entity identifier for entity-scoped routing.</param>
    /// <param name="isGlobalAdmin">Trusted server-populated flag indicating the authenticated user has global administrator privileges.</param>
    /// <param name="paging">Optional public paging policy supplied by the gateway.</param>
    public QueryEnvelope(
        string tenantId,
        string domain,
        string aggregateId,
        string queryType,
        byte[] payload,
        string correlationId,
        string userId,
        string? entityId,
        bool isGlobalAdmin,
        QueryPagingOptions? paging)
        : this(
            tenantId,
            domain,
            aggregateId,
            queryType,
            payload,
            correlationId,
            userId,
            entityId,
            isGlobalAdmin,
            paging,
            originalActorId: null,
            authenticatedWorkloadId: null,
            isDelegated: false,
            scopes: null,
            audience: null,
            delegationId: null) {
    }

    /// <summary>
    /// Initializes the prior fifteen-member dual-principal query shape without a delegation identifier.
    /// </summary>
    [OverloadResolutionPriority(1)]
    public QueryEnvelope(
        string tenantId,
        string domain,
        string aggregateId,
        string queryType,
        byte[] payload,
        string correlationId,
        string userId,
        string? entityId,
        bool isGlobalAdmin,
        QueryPagingOptions? paging,
        string? originalActorId,
        string? authenticatedWorkloadId,
        bool isDelegated,
        IReadOnlyList<string>? scopes,
        IReadOnlyList<string>? audience)
        : this(
            tenantId,
            domain,
            aggregateId,
            queryType,
            payload,
            correlationId,
            userId,
            entityId,
            isGlobalAdmin,
            paging,
            originalActorId,
            authenticatedWorkloadId,
            isDelegated,
            scopes,
            audience,
            delegationId: null) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryEnvelope"/> record.
    /// </summary>
    /// <param name="tenantId">The tenant identifier used for isolation and routing.</param>
    /// <param name="domain">The domain targeted by the query.</param>
    /// <param name="aggregateId">The aggregate or projection aggregate identifier.</param>
    /// <param name="queryType">The query type discriminator.</param>
    /// <param name="payload">UTF-8 JSON query payload bytes; use an empty array for tenant-wide list queries.</param>
    /// <param name="correlationId">The correlation identifier propagated from the gateway request.</param>
    /// <param name="userId">The authenticated user identifier, normally the <c>sub</c> claim.</param>
    /// <param name="entityId">Optional entity identifier for entity-scoped routing.</param>
    /// <param name="isGlobalAdmin">Trusted server-populated flag indicating the authenticated user has global administrator privileges.</param>
    /// <param name="paging">Optional public paging policy supplied by the gateway.</param>
    /// <param name="originalActorId">Optional original end-user actor identifier, distinct from the authenticated workload. Defaults to <see langword="null"/> for legacy single-principal callers.</param>
    /// <param name="authenticatedWorkloadId">Optional authenticated calling workload identifier. Defaults to <see langword="null"/> for legacy single-principal callers.</param>
    /// <param name="isDelegated">Trusted server-populated flag indicating the query was submitted through a delegated identity. Defaults to <see langword="false"/>.</param>
    /// <param name="scopes">Optional OAuth scopes granted to the calling token. Defaults to <see langword="null"/>.</param>
    /// <param name="audience">Optional token audience values. Defaults to <see langword="null"/>.</param>
    /// <param name="delegationId">Optional delegation identifier sourced only from an authenticated RFC 8693 <c>act.sub</c> claim. Defaults to <see langword="null"/>.</param>
    [JsonConstructor]
    public QueryEnvelope(
        string tenantId,
        string domain,
        string aggregateId,
        string queryType,
        byte[] payload,
        string correlationId,
        string userId,
        string? entityId,
        bool isGlobalAdmin,
        QueryPagingOptions? paging,
        string? originalActorId,
        string? authenticatedWorkloadId,
        bool isDelegated,
        IReadOnlyList<string>? scopes,
        IReadOnlyList<string>? audience,
        string? delegationId = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryType);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        TenantId = tenantId;
        Domain = domain;
        AggregateId = aggregateId;
        QueryType = queryType;
        Payload = payload;
        CorrelationId = correlationId;
        UserId = userId;
        EntityId = entityId;
        IsGlobalAdmin = isGlobalAdmin;
        Paging = paging;
        OriginalActorId = originalActorId;
        AuthenticatedWorkloadId = authenticatedWorkloadId;
        IsDelegated = isDelegated;
        DelegationId = delegationId;

        // Scopes/Audience normalize to a concrete array in their own init accessors below (also
        // required so DataContractSerializer has a statically-known type for the interface-typed
        // member -- see the class-level [KnownType(typeof(string[]))]), so assigning the raw
        // parameter here goes through the exact same normalization a `with` expression would.
        Scopes = scopes;
        Audience = audience;
    }

    /// <summary>
    /// Gets the tenant identifier used for isolation and routing.
    /// </summary>
    [DataMember]
    public string TenantId { get; init; }

    /// <summary>
    /// Gets the domain targeted by the query.
    /// </summary>
    [DataMember]
    public string Domain { get; init; }

    /// <summary>
    /// Gets the aggregate or projection aggregate identifier.
    /// </summary>
    [DataMember]
    public string AggregateId { get; init; }

    /// <summary>
    /// Gets the query type discriminator.
    /// </summary>
    [DataMember]
    public string QueryType { get; init; }

    /// <summary>
    /// Gets the UTF-8 JSON query payload bytes.
    /// </summary>
    [DataMember]
    public byte[] Payload { get; init; }

    /// <summary>
    /// Gets the correlation identifier propagated from the gateway request.
    /// </summary>
    [DataMember]
    public string CorrelationId { get; init; }

    /// <summary>
    /// Gets the authenticated user identifier, normally the <c>sub</c> claim.
    /// </summary>
    [DataMember]
    public string UserId { get; init; }

    /// <summary>
    /// Gets the optional entity identifier for entity-scoped routing.
    /// </summary>
    [DataMember]
    public string? EntityId { get; init; }

    /// <summary>
    /// Gets a trusted server-populated value indicating the authenticated user has global administrator privileges.
    /// </summary>
    [DataMember]
    public bool IsGlobalAdmin { get; init; }

    /// <summary>
    /// Gets the optional public paging policy supplied by the gateway.
    /// </summary>
    [DataMember]
    public QueryPagingOptions? Paging { get; init; }

    /// <summary>
    /// Gets the original end-user actor identifier, sourced from the <c>sub</c> claim and
    /// distinct from any authenticated workload acting on their behalf. Optional; <see langword="null"/>
    /// for legacy single-principal callers.
    /// </summary>
    [DataMember]
    public string? OriginalActorId { get; init; }

    /// <summary>
    /// Gets the authenticated calling workload identifier (<c>azp</c>, falling back to
    /// <c>client_id</c> or the first <c>aud</c> entry). Optional; <see langword="null"/> for
    /// legacy single-principal callers.
    /// </summary>
    [DataMember]
    public string? AuthenticatedWorkloadId { get; init; }

    /// <summary>
    /// Gets a trusted server-populated value indicating the query was submitted through a
    /// delegated identity (an RFC 8693 <c>act</c> claim was present, or the authenticated
    /// workload differs from the token's issuing client). Defaults to <see langword="false"/>
    /// for legacy single-principal callers.
    /// </summary>
    [DataMember]
    public bool IsDelegated { get; init; }

    private readonly string[]? _scopes;
    private readonly string[]? _audience;

    /// <summary>
    /// Gets the OAuth scopes granted to the calling token, when populated by the gateway.
    /// </summary>
    /// <remarks>
    /// Normalized to a freshly materialized array by this property's own <see langword="init"/>
    /// accessor -- not only by the constructor -- so a <c>with</c> expression that reassigns this
    /// property (e.g. <c>envelope with { Scopes = callerList }</c>) is normalized identically and
    /// can never retain a live reference to a caller-supplied mutable list.
    /// </remarks>
    [DataMember]
    public IReadOnlyList<string>? Scopes {
        get => _scopes;
        init => _scopes = value?.ToArray();
    }

    /// <summary>
    /// Gets the token audience values, when populated by the gateway.
    /// </summary>
    /// <remarks>
    /// Normalized to a freshly materialized array by this property's own <see langword="init"/>
    /// accessor -- not only by the constructor -- so a <c>with</c> expression that reassigns this
    /// property (e.g. <c>envelope with { Audience = callerList }</c>) is normalized identically
    /// and can never retain a live reference to a caller-supplied mutable list.
    /// </remarks>
    [DataMember]
    public IReadOnlyList<string>? Audience {
        get => _audience;
        init => _audience = value?.ToArray();
    }

    /// <summary>
    /// Gets the delegation identifier sourced only from the authenticated RFC 8693
    /// <c>act.sub</c> claim. Optional; <see langword="null"/> when delegation evidence is absent,
    /// malformed, or unavailable to legacy callers.
    /// </summary>
    [DataMember]
    public string? DelegationId { get; init; }

    /// <summary>
    /// Gets the aggregate identity represented by the tenant, domain, and aggregate ID.
    /// </summary>
    public AggregateIdentity AggregateIdentity
        => new(TenantId, Domain, AggregateId);

    /// <summary>
    /// Determines whether this instance and <paramref name="other"/> represent the same query
    /// envelope.
    /// </summary>
    /// <param name="other">The other <see cref="QueryEnvelope"/> to compare against.</param>
    /// <returns>
    /// <see langword="true"/> when every member is equal; otherwise, <see langword="false"/>.
    /// <see cref="Scopes"/> and <see cref="Audience"/> are compared by ordinal sequence content
    /// rather than by array reference -- the default record-synthesized equality would otherwise
    /// treat two content-identical envelopes as unequal whenever their collections happen to be
    /// backed by different array instances.
    /// </returns>
    public virtual bool Equals(QueryEnvelope? other) {
        if (other is null) {
            return false;
        }

        if (ReferenceEquals(this, other)) {
            return true;
        }

        return EqualityContract == other.EqualityContract
            && TenantId == other.TenantId
            && Domain == other.Domain
            && AggregateId == other.AggregateId
            && QueryType == other.QueryType
            && EqualityComparer<byte[]>.Default.Equals(Payload, other.Payload)
            && CorrelationId == other.CorrelationId
            && UserId == other.UserId
            && EntityId == other.EntityId
            && IsGlobalAdmin == other.IsGlobalAdmin
            && EqualityComparer<QueryPagingOptions?>.Default.Equals(Paging, other.Paging)
            && OriginalActorId == other.OriginalActorId
            && AuthenticatedWorkloadId == other.AuthenticatedWorkloadId
            && IsDelegated == other.IsDelegated
            && StringSequenceEquals(Scopes, other.Scopes)
            && StringSequenceEquals(Audience, other.Audience)
            && DelegationId == other.DelegationId;
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        HashCode hash = default;
        hash.Add(EqualityContract);
        hash.Add(TenantId);
        hash.Add(Domain);
        hash.Add(AggregateId);
        hash.Add(QueryType);
        hash.Add(Payload);
        hash.Add(CorrelationId);
        hash.Add(UserId);
        hash.Add(EntityId);
        hash.Add(IsGlobalAdmin);
        hash.Add(Paging);
        hash.Add(OriginalActorId);
        hash.Add(AuthenticatedWorkloadId);
        hash.Add(IsDelegated);
        AddStringSequenceHash(ref hash, Scopes);
        AddStringSequenceHash(ref hash, Audience);
        hash.Add(DelegationId);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Returns a diagnostic string with payload bytes redacted.
    /// </summary>
    /// <returns>A redacted diagnostic representation.</returns>
    public override string ToString()
        => $"QueryEnvelope {{ TenantId = {TenantId}, Domain = {Domain}, AggregateId = {AggregateId}, QueryType = {QueryType}, CorrelationId = {CorrelationId}, UserId = {UserId}{(EntityId is not null ? $", EntityId = {EntityId}" : "")}{(IsGlobalAdmin ? ", IsGlobalAdmin = True" : "")}, Payload = [REDACTED {Payload.Length} bytes] }}";

    // Content (ordinal sequence) equality for a nullable string list -- two nulls are equal, a
    // null and an empty/non-empty list are not, matching IEnumerable<T>.SequenceEqual semantics
    // when both sides are non-null.
    private static bool StringSequenceEquals(IReadOnlyList<string>? left, IReadOnlyList<string>? right) {
        if (left is null || right is null) {
            return left is null && right is null;
        }

        return left.SequenceEqual(right, StringComparer.Ordinal);
    }

    private static void AddStringSequenceHash(ref HashCode hash, IReadOnlyList<string>? values) {
        if (values is null) {
            hash.Add(0);
            return;
        }

        foreach (string value in values) {
            hash.Add(value, StringComparer.Ordinal);
        }
    }
}

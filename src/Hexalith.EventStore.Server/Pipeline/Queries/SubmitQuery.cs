
using System.Runtime.CompilerServices;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;

using MediatR;

namespace Hexalith.EventStore.Server.Pipeline.Queries;
/// <summary>
/// MediatR request for submitting a query through the pipeline.
/// </summary>
public record SubmitQuery(
    string Tenant,
    string Domain,
    string AggregateId,
    string QueryType,
    byte[] Payload,
    string CorrelationId,
    string UserId,
    string? EntityId = null,
    string? ProjectionType = null,
    string? ProjectionActorType = null,
    bool IsGlobalAdmin = false,
    QueryPagingOptions? Paging = null,
    string? OriginalActorId = null,
    string? AuthenticatedWorkloadId = null,
    bool IsDelegated = false,
    IReadOnlyList<string>? Scopes = null,
    IReadOnlyList<string>? Audience = null,
    string? DelegationId = null) : IRequest<SubmitQueryResult> {
    /// <summary>
    /// Initializes the prior seventeen-member query shape without a delegation identifier.
    /// </summary>
    [OverloadResolutionPriority(1)]
    public SubmitQuery(
        string Tenant,
        string Domain,
        string AggregateId,
        string QueryType,
        byte[] Payload,
        string CorrelationId,
        string UserId,
        string? EntityId = null,
        string? ProjectionType = null,
        string? ProjectionActorType = null,
        bool IsGlobalAdmin = false,
        QueryPagingOptions? Paging = null,
        string? OriginalActorId = null,
        string? AuthenticatedWorkloadId = null,
        bool IsDelegated = false,
        IReadOnlyList<string>? Scopes = null,
        IReadOnlyList<string>? Audience = null)
        : this(
            Tenant,
            Domain,
            AggregateId,
            QueryType,
            Payload,
            CorrelationId,
            UserId,
            EntityId,
            ProjectionType,
            ProjectionActorType,
            IsGlobalAdmin,
            Paging,
            OriginalActorId,
            AuthenticatedWorkloadId,
            IsDelegated,
            Scopes,
            Audience,
            DelegationId: null) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SubmitQuery"/> record using the original query shape.
    /// </summary>
    public SubmitQuery(
        string tenant,
        string domain,
        string aggregateId,
        string queryType,
        byte[] payload,
        string correlationId,
        string userId,
        string? entityId,
        string? projectionType,
        string? projectionActorType,
        bool isGlobalAdmin)
        : this(
            tenant,
            domain,
            aggregateId,
            queryType,
            payload,
            correlationId,
            userId,
            entityId,
            projectionType,
            projectionActorType,
            isGlobalAdmin,
            Paging: null) {
    }

    private readonly string[]? _scopes = Scopes?.ToArray();
    private readonly string[]? _audience = Audience?.ToArray();

    /// <summary>
    /// Gets the OAuth scopes granted to the calling token, when populated by the gateway.
    /// </summary>
    /// <remarks>
    /// Normalized to a freshly materialized array by this property's own <see langword="init"/>
    /// accessor -- not only at construction -- so a <c>with</c> expression that reassigns this
    /// property is normalized identically and can never retain a live reference to a
    /// caller-supplied mutable list.
    /// </remarks>
    public IReadOnlyList<string>? Scopes {
        get => _scopes;
        init => _scopes = value?.ToArray();
    }

    /// <summary>
    /// Gets the token audience values, when populated by the gateway.
    /// </summary>
    /// <remarks>
    /// Normalized to a freshly materialized array by this property's own <see langword="init"/>
    /// accessor -- not only at construction -- so a <c>with</c> expression that reassigns this
    /// property is normalized identically and can never retain a live reference to a
    /// caller-supplied mutable list.
    /// </remarks>
    public IReadOnlyList<string>? Audience {
        get => _audience;
        init => _audience = value?.ToArray();
    }

    /// <summary>
    /// Determines whether this instance and <paramref name="other"/> represent the same query
    /// submission.
    /// </summary>
    /// <param name="other">The other <see cref="SubmitQuery"/> to compare against.</param>
    /// <returns>
    /// <see langword="true"/> when every member is equal; otherwise, <see langword="false"/>.
    /// <see cref="Scopes"/> and <see cref="Audience"/> are compared by ordinal sequence content
    /// rather than by array reference -- the default record-synthesized equality would otherwise
    /// treat two content-identical instances as unequal whenever their collections happen to be
    /// backed by different array instances.
    /// </returns>
    public virtual bool Equals(SubmitQuery? other) {
        if (other is null) {
            return false;
        }

        if (ReferenceEquals(this, other)) {
            return true;
        }

        return EqualityContract == other.EqualityContract
            && Tenant == other.Tenant
            && Domain == other.Domain
            && AggregateId == other.AggregateId
            && QueryType == other.QueryType
            && EqualityComparer<byte[]>.Default.Equals(Payload, other.Payload)
            && CorrelationId == other.CorrelationId
            && UserId == other.UserId
            && EntityId == other.EntityId
            && ProjectionType == other.ProjectionType
            && ProjectionActorType == other.ProjectionActorType
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
        hash.Add(Tenant);
        hash.Add(Domain);
        hash.Add(AggregateId);
        hash.Add(QueryType);
        hash.Add(Payload);
        hash.Add(CorrelationId);
        hash.Add(UserId);
        hash.Add(EntityId);
        hash.Add(ProjectionType);
        hash.Add(ProjectionActorType);
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

    /// <summary>Deconstructs the prior seventeen-member query shape.</summary>
    public void Deconstruct(
        out string Tenant,
        out string Domain,
        out string AggregateId,
        out string QueryType,
        out byte[] Payload,
        out string CorrelationId,
        out string UserId,
        out string? EntityId,
        out string? ProjectionType,
        out string? ProjectionActorType,
        out bool IsGlobalAdmin,
        out QueryPagingOptions? Paging,
        out string? OriginalActorId,
        out string? AuthenticatedWorkloadId,
        out bool IsDelegated,
        out IReadOnlyList<string>? Scopes,
        out IReadOnlyList<string>? Audience) {
        Tenant = this.Tenant;
        Domain = this.Domain;
        AggregateId = this.AggregateId;
        QueryType = this.QueryType;
        Payload = this.Payload;
        CorrelationId = this.CorrelationId;
        UserId = this.UserId;
        EntityId = this.EntityId;
        ProjectionType = this.ProjectionType;
        ProjectionActorType = this.ProjectionActorType;
        IsGlobalAdmin = this.IsGlobalAdmin;
        Paging = this.Paging;
        OriginalActorId = this.OriginalActorId;
        AuthenticatedWorkloadId = this.AuthenticatedWorkloadId;
        IsDelegated = this.IsDelegated;
        Scopes = this.Scopes;
        Audience = this.Audience;
    }

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

/// <summary>
/// Result of processing a <see cref="SubmitQuery"/>.
/// </summary>
public record SubmitQueryResult(
    string CorrelationId,
    JsonElement Payload,
    string? ProjectionType = null,
    QueryResponseMetadata? Metadata = null) {
    /// <summary>
    /// Initializes a new instance of the <see cref="SubmitQueryResult"/> class using the original query result shape.
    /// </summary>
    /// <param name="correlationId">The correlation identifier for the query.</param>
    /// <param name="payload">The query payload.</param>
    /// <param name="projectionType">Optional projection type metadata.</param>
    public SubmitQueryResult(string correlationId, JsonElement payload, string? projectionType)
        : this(correlationId, payload, projectionType, Metadata: null) {
    }
}

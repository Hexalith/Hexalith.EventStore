using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Contracts.Tests.Queries;

public class ProjectionAdapterContractTests {
    private static readonly string[] s_forbiddenExactDependencyNames = [
        "Google.Protobuf",
        "Google.Api.CommonProtos",
    ];

    private static QueryEnvelope CreateEnvelope(
        byte[]? payload = null,
        string? entityId = "party-42",
        QueryPagingOptions? paging = null)
        => new(
            "tenant-a",
            "parties",
            "party",
            "get-party",
            payload ?? JsonSerializer.SerializeToUtf8Bytes(new { id = "party-42" }),
            "corr-1",
            "user-1",
            entityId,
            isGlobalAdmin: false,
            paging);

    private static QueryEnvelope CreateEnvelopeWithDualPrincipal(
        string? originalActorId = "actor-1",
        string? authenticatedWorkloadId = "workload-1",
        bool isDelegated = false,
        IReadOnlyList<string>? scopes = null,
        IReadOnlyList<string>? audience = null,
        string? delegationId = null)
        => new(
            "tenant-a",
            "parties",
            "party",
            "get-party",
            JsonSerializer.SerializeToUtf8Bytes(new { id = "party-42" }),
            "corr-1",
            "user-1",
            "party-42",
            isGlobalAdmin: false,
            paging: null,
            originalActorId,
            authenticatedWorkloadId,
            isDelegated,
            scopes,
            audience,
            delegationId);

    private static string FindRepositoryRoot() {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null) {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props"))
                && Directory.Exists(Path.Combine(directory.FullName, "src", "Hexalith.EventStore.Contracts"))) {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from the test working directory.");
    }

    private static bool IsForbiddenDependencyName(string dependencyName)
        => dependencyName.StartsWith("Dapr.", StringComparison.OrdinalIgnoreCase)
            || dependencyName.StartsWith("Grpc.", StringComparison.OrdinalIgnoreCase)
            || s_forbiddenExactDependencyNames.Contains(dependencyName, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void QueryEnvelope_IsPublicContractsWireDto() {
        QueryEnvelope sut = CreateEnvelope();

        sut.TenantId.ShouldBe("tenant-a");
        sut.Domain.ShouldBe("parties");
        sut.AggregateId.ShouldBe("party");
        sut.QueryType.ShouldBe("get-party");
        sut.CorrelationId.ShouldBe("corr-1");
        sut.UserId.ShouldBe("user-1");
        sut.EntityId.ShouldBe("party-42");
        sut.AggregateIdentity.TenantId.ShouldBe("tenant-a");
    }

    [Fact]
    public void QueryEnvelope_ToString_RedactsPayloadBytes() {
        QueryEnvelope sut = CreateEnvelope([65, 66, 67]);

        string result = sut.ToString();

        result.ShouldContain("[REDACTED 3 bytes]");
        result.ShouldNotContain("ABC");
        result.ShouldNotContain("65");
    }

    [Fact]
    public void QueryEnvelope_DataContractRoundTrip_PreservesWireMembers() {
        QueryEnvelope original = CreateEnvelope(
            [1, 2, 3],
            "party-42",
            new QueryPagingOptions(PageSize: 25, Cursor: "opaque-cursor"));
        var serializer = new DataContractSerializer(typeof(QueryEnvelope));

        using var stream = new MemoryStream();
        serializer.WriteObject(stream, original);
        stream.Position = 0;

        var restored = (QueryEnvelope?)serializer.ReadObject(stream);

        _ = restored.ShouldNotBeNull();
        restored.TenantId.ShouldBe(original.TenantId);
        restored.Domain.ShouldBe(original.Domain);
        restored.AggregateId.ShouldBe(original.AggregateId);
        restored.QueryType.ShouldBe(original.QueryType);
        restored.Payload.ShouldBe(original.Payload);
        restored.CorrelationId.ShouldBe(original.CorrelationId);
        restored.UserId.ShouldBe(original.UserId);
        restored.EntityId.ShouldBe(original.EntityId);
        _ = restored.Paging.ShouldNotBeNull();
        restored.Paging.PageSize.ShouldBe(25);
        restored.Paging.Cursor.ShouldBe("opaque-cursor");
    }

    [Fact]
    public void QueryResult_FromPayload_StoresUtf8JsonPayloadBytes() {
        JsonElement payload = JsonDocument.Parse("{\"name\":\"Ada\"}").RootElement;

        var sut = QueryResult.FromPayload(payload, "party");

        sut.Success.ShouldBeTrue();
        sut.ProjectionType.ShouldBe("party");
        sut.GetPayload().GetProperty("name").GetString().ShouldBe("Ada");
    }

    [Fact]
    public void QueryResult_DataContractRoundTrip_PreservesFailureShape() {
        var original = QueryResult.Failure("No projection state available");
        var serializer = new DataContractSerializer(typeof(QueryResult));

        using var stream = new MemoryStream();
        serializer.WriteObject(stream, original);
        stream.Position = 0;

        var restored = (QueryResult?)serializer.ReadObject(stream);

        _ = restored.ShouldNotBeNull();
        restored.Success.ShouldBeFalse();
        restored.PayloadBytes.ShouldBeNull();
        restored.ErrorMessage.ShouldBe("No projection state available");
    }

    [Fact]
    public void QueryResult_DataContractRoundTrip_PreservesMetadata() {
        JsonElement payload = JsonDocument.Parse("{\"name\":\"Ada\"}").RootElement;
        var servedAt = new DateTimeOffset(2026, 7, 6, 8, 0, 0, TimeSpan.Zero);
        var original = QueryResult.FromPayload(
            payload,
            "party",
            new QueryResponseMetadata(
                ETag: "producer-etag",
                IsStale: false,
                IsDegraded: true,
                ProjectionVersion: "party-v2",
                ServedAt: servedAt,
                Paging: new QueryPagingMetadata(PageSize: 25, Offset: 50, NextCursor: "next-page", TotalCount: 125, HasMore: true),
                WarningCodes: [QueryWarningCodes.DegradedSearch]) {
                Provenance = QueryResponseProvenance.ProjectionBacked,
            });
        var serializer = new DataContractSerializer(typeof(QueryResult));

        using var stream = new MemoryStream();
        serializer.WriteObject(stream, original);
        stream.Position = 0;

        var restored = (QueryResult?)serializer.ReadObject(stream);

        _ = restored.ShouldNotBeNull();
        restored.Success.ShouldBeTrue();
        _ = restored.Metadata.ShouldNotBeNull();
        restored.Metadata.ETag.ShouldBe("producer-etag");
        restored.Metadata.IsStale.ShouldBe(false);
        restored.Metadata.IsDegraded.ShouldBe(true);
        restored.Metadata.ProjectionVersion.ShouldBe("party-v2");
        restored.Metadata.Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);
        restored.Metadata.ServedAt.ShouldBe(servedAt);
        _ = restored.Metadata.Paging.ShouldNotBeNull();
        restored.Metadata.Paging.PageSize.ShouldBe(25);
        restored.Metadata.Paging.Offset.ShouldBe(50);
        restored.Metadata.Paging.NextCursor.ShouldBe("next-page");
        restored.Metadata.Paging.TotalCount.ShouldBe(125);
        restored.Metadata.Paging.HasMore.ShouldBe(true);
        _ = restored.Metadata.WarningCodes.ShouldNotBeNull();
        restored.Metadata.WarningCodes.ShouldContain(QueryWarningCodes.DegradedSearch);
    }

    [Fact]
    public void QueryResponseMetadata_PublicCompatibility_PreservesConstructorAndDefaultsLegacyProvenance() {
        typeof(QueryResponseMetadata)
            .GetConstructor([
                typeof(string),
                typeof(bool?),
                typeof(bool?),
                typeof(bool?),
                typeof(string),
                typeof(DateTimeOffset?),
                typeof(QueryPagingMetadata),
                typeof(IReadOnlyList<string>),
            ])
            .ShouldNotBeNull();

        var warnings = new List<string> { "warning" };
        var metadata = new QueryResponseMetadata(WarningCodes: warnings);
        warnings[0] = "changed";

        metadata.Provenance.ShouldBe(QueryResponseProvenance.Unknown);
        metadata.WarningCodes.ShouldBe(["warning"]);
    }

    [Fact]
    public void QueryResponseProvenance_PublicValues_AreExplicitAndStable() {
        Enum.GetNames<QueryResponseProvenance>()
            .ShouldBe(["Unknown", "ProjectionBacked", "HandlerComputed"]);
        ((int)QueryResponseProvenance.Unknown).ShouldBe(0);
        ((int)QueryResponseProvenance.ProjectionBacked).ShouldBe(1);
        ((int)QueryResponseProvenance.HandlerComputed).ShouldBe(2);
    }

    [Theory]
    [InlineData(QueryResponseProvenance.Unknown)]
    [InlineData(QueryResponseProvenance.ProjectionBacked)]
    [InlineData(QueryResponseProvenance.HandlerComputed)]
    public void QueryResponseProvenance_DataContractRoundTrip_PreservesValue(
        QueryResponseProvenance provenance) {
        var serializer = new DataContractSerializer(typeof(QueryResponseProvenance));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, provenance);
        stream.Position = 0;

        var restored = (QueryResponseProvenance?)serializer.ReadObject(stream);

        restored.ShouldBe(provenance);
    }

    [Fact]
    public void QueryResponseMetadata_DataContractLegacyShape_DefaultsProvenanceToUnknown() {
        var original = new QueryResponseMetadata(IsStale: false) {
            Provenance = QueryResponseProvenance.ProjectionBacked,
        };
        var serializer = new DataContractSerializer(typeof(QueryResponseMetadata));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, original);
        stream.Position = 0;
        var document = XDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        document.Descendants().Where(element => element.Name.LocalName == "Provenance").Remove();

        using var legacyStream = new MemoryStream(Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting)));
        var restored = (QueryResponseMetadata?)serializer.ReadObject(legacyStream);

        _ = restored.ShouldNotBeNull();
        restored.Provenance.ShouldBe(QueryResponseProvenance.Unknown);
    }

    [Theory]
    [InlineData(QueryResponseProvenance.Unknown, "\"Unknown\"")]
    [InlineData(QueryResponseProvenance.ProjectionBacked, "\"ProjectionBacked\"")]
    [InlineData(QueryResponseProvenance.HandlerComputed, "\"HandlerComputed\"")]
    public void QueryResponseProvenance_JsonRoundTrip_UsesCanonicalNames(
        QueryResponseProvenance provenance,
        string expectedJson) {
        string json = JsonSerializer.Serialize(provenance);

        json.ShouldBe(expectedJson);
        JsonSerializer.Deserialize<QueryResponseProvenance>(json).ShouldBe(provenance);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("1")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("\"projectionbacked\"")]
    [InlineData("\"Unexpected\"")]
    public void QueryResponseProvenance_InvalidJson_DefaultsToUnknown(string json) {
        JsonSerializer.Deserialize<QueryResponseProvenance>(json)
            .ShouldBe(QueryResponseProvenance.Unknown);
    }

    [Fact]
    public void ProjectionLifecycleState_PublicValues_AreExplicitAndStable() {
        Enum.GetNames<ProjectionLifecycleState>()
            .ShouldBe(["Unknown", "Current", "Stale", "Rebuilding", "Degraded", "Unavailable", "LocalOnly"]);
        ((int)ProjectionLifecycleState.Unknown).ShouldBe(0);
        ((int)ProjectionLifecycleState.Current).ShouldBe(1);
        ((int)ProjectionLifecycleState.Stale).ShouldBe(2);
        ((int)ProjectionLifecycleState.Rebuilding).ShouldBe(3);
        ((int)ProjectionLifecycleState.Degraded).ShouldBe(4);
        ((int)ProjectionLifecycleState.Unavailable).ShouldBe(5);
        ((int)ProjectionLifecycleState.LocalOnly).ShouldBe(6);
    }

    [Theory]
    [InlineData(ProjectionLifecycleState.Unknown, "\"Unknown\"")]
    [InlineData(ProjectionLifecycleState.Current, "\"Current\"")]
    [InlineData(ProjectionLifecycleState.Stale, "\"Stale\"")]
    [InlineData(ProjectionLifecycleState.Rebuilding, "\"Rebuilding\"")]
    [InlineData(ProjectionLifecycleState.Degraded, "\"Degraded\"")]
    [InlineData(ProjectionLifecycleState.Unavailable, "\"Unavailable\"")]
    [InlineData(ProjectionLifecycleState.LocalOnly, "\"LocalOnly\"")]
    public void ProjectionLifecycleState_JsonRoundTrip_UsesCanonicalNames(
        ProjectionLifecycleState lifecycle,
        string expectedJson) {
        string json = JsonSerializer.Serialize(lifecycle);

        json.ShouldBe(expectedJson);
        JsonSerializer.Deserialize<ProjectionLifecycleState>(json).ShouldBe(lifecycle);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("1")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("\"current\"")]
    [InlineData("\"Unexpected\"")]
    public void ProjectionLifecycleState_InvalidJson_DefaultsToUnknown(string json) {
        JsonSerializer.Deserialize<ProjectionLifecycleState>(json)
            .ShouldBe(ProjectionLifecycleState.Unknown);
    }

    [Fact]
    public void ProjectionLifecycleState_InvalidValueWrite_UsesUnknown() {
        JsonSerializer.Serialize((ProjectionLifecycleState)999).ShouldBe("\"Unknown\"");
    }

    [Theory]
    [InlineData(ProjectionLifecycleState.Unknown)]
    [InlineData(ProjectionLifecycleState.Current)]
    [InlineData(ProjectionLifecycleState.Stale)]
    [InlineData(ProjectionLifecycleState.Rebuilding)]
    [InlineData(ProjectionLifecycleState.Degraded)]
    [InlineData(ProjectionLifecycleState.Unavailable)]
    [InlineData(ProjectionLifecycleState.LocalOnly)]
    public void ProjectionLifecycleState_DataContractRoundTrip_PreservesValue(
        ProjectionLifecycleState lifecycle) {
        var serializer = new DataContractSerializer(typeof(ProjectionLifecycleState));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, lifecycle);
        stream.Position = 0;

        var restored = (ProjectionLifecycleState?)serializer.ReadObject(stream);

        restored.ShouldBe(lifecycle);
    }

    [Fact]
    public void QueryResponseMetadata_DataContractLegacyShape_DefaultsLifecycleToUnknown() {
        var original = new QueryResponseMetadata(IsStale: false) {
            Lifecycle = ProjectionLifecycleState.Current,
        };
        var serializer = new DataContractSerializer(typeof(QueryResponseMetadata));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, original);
        stream.Position = 0;
        var document = XDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        document.Descendants().Where(element => element.Name.LocalName == "Lifecycle").Remove();

        using var legacyStream = new MemoryStream(Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting)));
        var restored = (QueryResponseMetadata?)serializer.ReadObject(legacyStream);

        _ = restored.ShouldNotBeNull();
        restored.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
    }

    [Theory]
    [InlineData(QueryResponseProvenance.Unknown, ProjectionLifecycleState.Current, ProjectionLifecycleState.Unknown)]
    [InlineData(QueryResponseProvenance.HandlerComputed, ProjectionLifecycleState.Current, ProjectionLifecycleState.Unknown)]
    [InlineData(QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Current, ProjectionLifecycleState.Current)]
    [InlineData(QueryResponseProvenance.ProjectionBacked, (ProjectionLifecycleState)999, ProjectionLifecycleState.Unknown)]
    public void ProjectionLifecyclePolicy_Normalize_FailsClosed(
        QueryResponseProvenance provenance,
        ProjectionLifecycleState lifecycle,
        ProjectionLifecycleState expected) {
        ProjectionLifecyclePolicy.Normalize(lifecycle, provenance).ShouldBe(expected);
    }

    [Theory]
    [InlineData(ProjectionLifecycleState.Unknown, true, true)]
    [InlineData(ProjectionLifecycleState.Unknown, false, false)]
    [InlineData(ProjectionLifecycleState.Current, true, false)]
    [InlineData(ProjectionLifecycleState.Stale, false, true)]
    [InlineData(ProjectionLifecycleState.Rebuilding, true, null)]
    [InlineData(ProjectionLifecycleState.Degraded, true, null)]
    [InlineData(ProjectionLifecycleState.Unavailable, true, null)]
    [InlineData(ProjectionLifecycleState.LocalOnly, true, null)]
    public void ProjectionLifecyclePolicy_ProjectIsStale_IsOneWay(
        ProjectionLifecycleState lifecycle,
        bool? fallback,
        bool? expected) {
        ProjectionLifecyclePolicy.ProjectIsStale(lifecycle, fallback).ShouldBe(expected);
    }

    public static TheoryData<ProjectionLifecycleState, bool?, bool?> ProjectIsDegradedCases {
        get {
            var data = new TheoryData<ProjectionLifecycleState, bool?, bool?>();
            foreach (ProjectionLifecycleState lifecycle in Enum.GetValues<ProjectionLifecycleState>()) {
                data.Add(lifecycle, false, lifecycle == ProjectionLifecycleState.Degraded ? true : false);
                data.Add(lifecycle, true, true);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ProjectIsDegradedCases))]
    public void ProjectionLifecyclePolicy_ProjectIsDegraded_PreservesAdditiveFallbackExceptDegraded(
        ProjectionLifecycleState lifecycle,
        bool? fallback,
        bool? expected) {
        ProjectionLifecyclePolicy.ProjectIsDegraded(lifecycle, fallback).ShouldBe(expected);
    }

    [Theory]
    [InlineData(QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Current, true, true)]
    [InlineData(QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Current, false, false)]
    [InlineData(QueryResponseProvenance.Unknown, ProjectionLifecycleState.Current, true, false)]
    [InlineData(QueryResponseProvenance.HandlerComputed, ProjectionLifecycleState.Current, true, false)]
    [InlineData(QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.Stale, true, false)]
    [InlineData(QueryResponseProvenance.ProjectionBacked, ProjectionLifecycleState.LocalOnly, true, false)]
    public void ProjectionLifecyclePolicy_CanMutate_DefaultsFailClosed(
        QueryResponseProvenance provenance,
        ProjectionLifecycleState lifecycle,
        bool isAuthorized,
        bool expected) {
        ProjectionLifecyclePolicy.CanMutate(isAuthorized, provenance, lifecycle).ShouldBe(expected);
    }

    [Theory]
    [InlineData(ProjectionLifecycleState.Unknown, false)]
    [InlineData(ProjectionLifecycleState.Current, true)]
    [InlineData(ProjectionLifecycleState.Stale, false)]
    [InlineData(ProjectionLifecycleState.Rebuilding, false)]
    [InlineData(ProjectionLifecycleState.Degraded, false)]
    [InlineData(ProjectionLifecycleState.Unavailable, false)]
    [InlineData(ProjectionLifecycleState.LocalOnly, false)]
    public void ProjectionLifecyclePolicy_ProjectionBackedAuthorized_IsExhaustive(
        ProjectionLifecycleState lifecycle,
        bool expected) {
        ProjectionLifecyclePolicy.IsProjectionConfirmed(
            QueryResponseProvenance.ProjectionBacked,
            lifecycle).ShouldBe(expected);
        ProjectionLifecyclePolicy.CanMutate(
            isAuthorized: true,
            QueryResponseProvenance.ProjectionBacked,
            lifecycle).ShouldBe(expected);
    }

    [Fact]
    public void QueryResult_SystemTextJsonRoundTrip_PreservesSuccessPayloadAndMetadata() {
        // Regression guard: the projection actor wire path (DefaultProjectionActorInvoker ->
        // ActorProxy.InvokeMethodAsync<QueryEnvelope, QueryResult>) uses System.Text.Json, NOT
        // DataContractSerializer. A second public constructor made STJ throw NotSupportedException
        // ("Deserialization of types without a parameterless constructor, a singular parameterized
        // constructor, or a parameterized constructor annotated with 'JsonConstructorAttribute'")
        // on every projection query response. QueryResult must keep exactly one deserialization
        // constructor so the actor-remoting JSON path round-trips.
        JsonElement payload = JsonDocument.Parse("{\"count\":42}").RootElement;
        var original = QueryResult.FromPayload(
            payload,
            "counter",
            new QueryResponseMetadata(ETag: "etag-1", IsStale: false, ProjectionVersion: "v1"));

        string json = JsonSerializer.Serialize(original);
        QueryResult? restored = JsonSerializer.Deserialize<QueryResult>(json);

        _ = restored.ShouldNotBeNull();
        restored.Success.ShouldBeTrue();
        restored.ProjectionType.ShouldBe("counter");
        restored.GetPayload().GetProperty("count").GetInt32().ShouldBe(42);
        _ = restored.Metadata.ShouldNotBeNull();
        restored.Metadata.ETag.ShouldBe("etag-1");
        restored.Metadata.ProjectionVersion.ShouldBe("v1");
    }

    [Fact]
    public void QueryResult_SystemTextJsonRoundTrip_PreservesFailureShape() {
        // The nonexistent-projection 404 path relies on STJ deserializing a failure QueryResult.
        var original = QueryResult.Failure("No projection state available for this aggregate");

        string json = JsonSerializer.Serialize(original);
        QueryResult? restored = JsonSerializer.Deserialize<QueryResult>(json);

        _ = restored.ShouldNotBeNull();
        restored.Success.ShouldBeFalse();
        restored.PayloadBytes.ShouldBeNull();
        restored.ErrorMessage.ShouldBe("No projection state available for this aggregate");
    }

    [Fact]
    public void QueryResult_DataContractRoundTrip_OldShapeWithoutMetadata_DeserializesWithNullMetadata() {
        JsonElement payload = JsonDocument.Parse("{\"name\":\"Ada\"}").RootElement;
        var original = QueryResult.FromPayload(payload, "party", new QueryResponseMetadata(IsStale: false));
        var serializer = new DataContractSerializer(typeof(QueryResult));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, original);
        stream.Position = 0;
        string xml = Encoding.UTF8.GetString(stream.ToArray());
        var document = XDocument.Parse(xml);
        document.Descendants().Where(e => e.Name.LocalName == "Metadata").Remove();
        string oldShapeXml = document.ToString(SaveOptions.DisableFormatting);

        using var oldShapeStream = new MemoryStream(Encoding.UTF8.GetBytes(oldShapeXml));
        var restored = (QueryResult?)serializer.ReadObject(oldShapeStream);

        _ = restored.ShouldNotBeNull();
        restored.Success.ShouldBeTrue();
        restored.Metadata.ShouldBeNull();
        restored.GetPayload().GetProperty("name").GetString().ShouldBe("Ada");
    }

    [Fact]
    public void QueryResult_PublicCompatibility_MaintainsOriginalConstructorAndFactoryShapes() {
        typeof(QueryResult)
            .GetConstructor([typeof(bool), typeof(byte[]), typeof(string), typeof(string)])
            .ShouldNotBeNull();
        typeof(QueryResult)
            .GetConstructor([typeof(bool), typeof(byte[]), typeof(string), typeof(string), typeof(QueryResponseMetadata)])
            .ShouldNotBeNull();
        typeof(QueryResult)
            .GetMethod(nameof(QueryResult.FromPayload), [typeof(JsonElement), typeof(string)])
            .ShouldNotBeNull();
        typeof(QueryResult)
            .GetMethod(nameof(QueryResult.Failure), [typeof(string)])
            .ShouldNotBeNull();
    }

    [Fact]
    public void QueryResult_PublicCompatibility_AllowsPascalCaseNamedArgumentsAndDeconstruction() {
        var metadata = new QueryResponseMetadata(IsStale: false);
        byte[] bytes = [1, 2, 3];

        var sut = new QueryResult(
            Success: true,
            PayloadBytes: bytes,
            ErrorMessage: null,
            ProjectionType: "party",
            Metadata: metadata);

        var (success, payloadBytes, errorMessage, projectionType, restoredMetadata) = sut;
        var (legacySuccess, legacyPayloadBytes, legacyErrorMessage, legacyProjectionType) = sut;

        success.ShouldBeTrue();
        payloadBytes.ShouldBe(bytes);
        errorMessage.ShouldBeNull();
        projectionType.ShouldBe("party");
        restoredMetadata.ShouldBe(metadata);
        legacySuccess.ShouldBe(success);
        legacyPayloadBytes.ShouldBe(bytes);
        legacyErrorMessage.ShouldBeNull();
        legacyProjectionType.ShouldBe("party");
    }

    [Fact]
    public void QueryPagingMetadata_PublicCompatibility_MaintainsOriginalConstructorAndDeconstruction() {
        typeof(QueryPagingMetadata)
            .GetConstructor([typeof(int), typeof(int?), typeof(string), typeof(long?)])
            .ShouldNotBeNull();

        var sut = new QueryPagingMetadata(PageSize: 25, Offset: 50, NextCursor: "next", TotalCount: 125);

        var (pageSize, offset, nextCursor, totalCount) = sut;

        pageSize.ShouldBe(25);
        offset.ShouldBe(50);
        nextCursor.ShouldBe("next");
        totalCount.ShouldBe(125);
        sut.HasMore.ShouldBeNull();
    }

    [Fact]
    public void IProjectionActor_IsImplementationNeutralProjectionQueryContract() {
        typeof(IProjectionActor)
            .GetInterfaces()
            .Select(i => i.FullName)
            .ShouldNotContain("Dapr.Actors.IActor");

        System.Reflection.MethodInfo method = typeof(IProjectionActor)
            .GetMethod(nameof(IProjectionActor.QueryAsync), [typeof(QueryEnvelope)])
            .ShouldNotBeNull();
        method.ReturnType.ShouldBe(typeof(Task<QueryResult>));
    }

    [Fact]
    public void QueryEnvelope_DataContractRoundTrip_NullEntityId_PreservesNullOnRestore() {
        QueryEnvelope original = CreateEnvelope([1, 2, 3], entityId: null);
        var serializer = new DataContractSerializer(typeof(QueryEnvelope));

        using var stream = new MemoryStream();
        serializer.WriteObject(stream, original);
        stream.Position = 0;

        var restored = (QueryEnvelope?)serializer.ReadObject(stream);

        _ = restored.ShouldNotBeNull();
        restored.EntityId.ShouldBeNull();
        restored.AggregateIdentity.TenantId.ShouldBe(original.TenantId);
    }

    [Fact]
    public void QueryEnvelope_DataContractRoundTrip_PreservesDualPrincipalMembers() {
        QueryEnvelope original = CreateEnvelopeWithDualPrincipal(
            originalActorId: "actor-1",
            authenticatedWorkloadId: "workload-1",
            isDelegated: true,
            scopes: ["parties.read"],
            audience: ["eventstore-api"],
            delegationId: "delegate-service");
        var serializer = new DataContractSerializer(typeof(QueryEnvelope));

        using var stream = new MemoryStream();
        serializer.WriteObject(stream, original);
        stream.Position = 0;

        var restored = (QueryEnvelope?)serializer.ReadObject(stream);

        _ = restored.ShouldNotBeNull();
        restored.OriginalActorId.ShouldBe("actor-1");
        restored.AuthenticatedWorkloadId.ShouldBe("workload-1");
        restored.IsDelegated.ShouldBeTrue();
        restored.Scopes.ShouldBe(["parties.read"]);
        restored.Audience.ShouldBe(["eventstore-api"]);
        restored.DelegationId.ShouldBe("delegate-service");
    }

    // DataContractSerializer round-trip coverage was previously only null vs. non-empty; an
    // explicitly empty (non-null, zero-length) array is a third, distinct wire shape that must
    // also survive the actor boundary without collapsing to null.
    [Fact]
    public void QueryEnvelope_DataContractRoundTrip_EmptyScopesAndAudienceArrays_PreservesEmptyNotNull() {
        QueryEnvelope original = CreateEnvelopeWithDualPrincipal(scopes: [], audience: []);
        var serializer = new DataContractSerializer(typeof(QueryEnvelope));

        using var stream = new MemoryStream();
        serializer.WriteObject(stream, original);
        stream.Position = 0;

        var restored = (QueryEnvelope?)serializer.ReadObject(stream);

        _ = restored.ShouldNotBeNull();
        _ = restored.Scopes.ShouldNotBeNull();
        restored.Scopes.ShouldBeEmpty();
        _ = restored.Audience.ShouldNotBeNull();
        restored.Audience.ShouldBeEmpty();
    }

    [Fact]
    public void QueryEnvelope_LegacyConstructorOverloads_DefaultDualPrincipalFieldsToNull() {
        QueryEnvelope sut = CreateEnvelope();

        sut.OriginalActorId.ShouldBeNull();
        sut.AuthenticatedWorkloadId.ShouldBeNull();
        sut.IsDelegated.ShouldBeFalse();
        sut.Scopes.ShouldBeNull();
        sut.Audience.ShouldBeNull();
        sut.DelegationId.ShouldBeNull();
    }

    [Fact]
    public void QueryAdapterFailureReason_SafeDenialForbidden_IsInternalOnlyMarkerDistinctFromForbidden() {
        QueryAdapterFailureReason.SafeDenialForbidden.ShouldBe("safe-denial-forbidden");
        QueryAdapterFailureReason.SafeDenialForbidden.ShouldNotBe(QueryAdapterFailureReason.Forbidden);
    }

    [Fact]
    public void QueryAdapterFailureReason_SafeDenialMissingProjectionState_IsInternalOnlyMarkerDistinctFromMissingProjectionState() {
        QueryAdapterFailureReason.SafeDenialMissingProjectionState.ShouldBe("safe-denial-missing-projection-state");
        QueryAdapterFailureReason.SafeDenialMissingProjectionState.ShouldNotBe(QueryAdapterFailureReason.MissingProjectionState);
        QueryAdapterFailureReason.SafeDenialMissingProjectionState.ShouldNotBe(QueryAdapterFailureReason.SafeDenialForbidden);
    }

    [Fact]
    public void QueryEnvelope_DataContractNamespace_PinnedToServerActorsNamespace() {
        var serializer = new DataContractSerializer(typeof(QueryEnvelope));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, CreateEnvelope());
        string xml = Encoding.UTF8.GetString(stream.ToArray());

        xml.ShouldContain("http://schemas.datacontract.org/2004/07/Hexalith.EventStore.Server.Actors");
    }

    [Fact]
    public void QueryResult_DataContractNamespace_PinnedToServerActorsNamespace() {
        var serializer = new DataContractSerializer(typeof(QueryResult));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, QueryResult.Failure("test-error"));
        string xml = Encoding.UTF8.GetString(stream.ToArray());

        xml.ShouldContain("http://schemas.datacontract.org/2004/07/Hexalith.EventStore.Server.Actors");
    }

    [Fact]
    public void QueryResult_FromPayload_ThrowsForUndefinedJsonElement() => Should.Throw<ArgumentException>(() => QueryResult.FromPayload(default));

    [Fact]
    public void QueryResult_Failure_ThrowsForNullOrWhitespaceMessage() {
        _ = Should.Throw<ArgumentException>(() => QueryResult.Failure(null!));
        _ = Should.Throw<ArgumentException>(() => QueryResult.Failure(""));
        _ = Should.Throw<ArgumentException>(() => QueryResult.Failure("   "));
    }

    [Fact]
    public void QueryAdapterFailureReason_DefinesStableCoarseCategories() {
        QueryAdapterFailureReason.MissingPayload.ShouldBe("missing-payload");
        QueryAdapterFailureReason.InvalidEnvelope.ShouldBe("invalid-envelope");
        QueryAdapterFailureReason.ActorResponseMismatch.ShouldBe("actor-response-mismatch");
        QueryAdapterFailureReason.UnsupportedQueryType.ShouldBe("unsupported-query-type");
        QueryAdapterFailureReason.SerializationFailure.ShouldBe("serialization-failure");
        QueryAdapterFailureReason.ActorException.ShouldBe("actor-exception");
        QueryAdapterFailureReason.UnknownQueryType.ShouldBe("unknown-query-type");
        QueryAdapterFailureReason.ActorNotFoundInfrastructure.ShouldBe("actor-not-found-infrastructure");
    }

    [Fact]
    public void ContractsAssembly_DoesNotReferenceDaprGrpcOrProtobufAssemblies() {
        string[] referencedAssemblies = typeof(QueryEnvelope).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        foreach (string assemblyName in referencedAssemblies) {
            IsForbiddenDependencyName(assemblyName).ShouldBeFalse(
                $"Contracts assembly must not reference forbidden DAPR/gRPC/protobuf assembly '{assemblyName}'.");
        }
    }

    [Fact]
    public void ContractsProject_DoesNotReferenceDaprGrpcOrProtobufPackages() {
        string projectPath = Path.Combine(FindRepositoryRoot(), "src", "Hexalith.EventStore.Contracts", "Hexalith.EventStore.Contracts.csproj");
        XDocument project = XDocument.Load(projectPath);
        string[] packageReferences = project
            .Descendants("PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        foreach (string packageReference in packageReferences) {
            IsForbiddenDependencyName(packageReference).ShouldBeFalse(
                $"Contracts project must not reference forbidden DAPR/gRPC/protobuf package '{packageReference}'.");
        }
    }

    [Fact]
    public void ContractsRestoredAssets_DoNotContainDaprGrpcOrProtobufPackages() {
        string assetsPath = Path.Combine(FindRepositoryRoot(), "src", "Hexalith.EventStore.Contracts", "obj", "project.assets.json");
        File.Exists(assetsPath).ShouldBeTrue("Restore must produce project.assets.json before dependency graph guards can run.");
        string assetsJson = File.ReadAllText(assetsPath);
        using JsonDocument document = JsonDocument.Parse(assetsJson);
        IEnumerable<string> resolvedPackageNames = document.RootElement
            .GetProperty("targets")
            .EnumerateObject()
            .SelectMany(target => target.Value.EnumerateObject())
            .Select(library => library.Name.Split('/')[0])
            .Concat(document.RootElement
                .GetProperty("libraries")
                .EnumerateObject()
                .Select(library => library.Name.Split('/')[0]))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (string packageName in resolvedPackageNames) {
            IsForbiddenDependencyName(packageName).ShouldBeFalse(
                $"Contracts restored assets must not resolve forbidden DAPR/gRPC/protobuf package '{packageName}'.");
        }
    }

    [Fact]
    public void ContractsSource_DoesNotContainDaprGrpcOrProtobufCouplingTokens() {
        string contractsRoot = Path.Combine(FindRepositoryRoot(), "src", "Hexalith.EventStore.Contracts");
        string[] forbiddenTokens = [
            "using Dapr.Actors",
            ": IActor",
            "Dapr.",
            "Grpc.",
            "Google.Protobuf",
            "Google.Api.CommonProtos",
        ];

        foreach (string file in Directory.EnumerateFiles(contractsRoot, "*.cs", SearchOption.AllDirectories)) {
            string text = File.ReadAllText(file);
            foreach (string forbidden in forbiddenTokens) {
                text.Contains(forbidden, StringComparison.Ordinal).ShouldBeFalse(
                    $"Contracts source file {file} must not reintroduce {forbidden}.");
            }
        }
    }

    [Fact]
    public void PublicProjectionApi_PreservesNeutralQueryWireTypesAndMethodContract() {
        Type queryEnvelope = typeof(QueryEnvelope);
        Type queryResult = typeof(QueryResult);
        Type failureReason = typeof(QueryAdapterFailureReason);
        Type projectionActor = typeof(IProjectionActor);

        queryEnvelope.Namespace.ShouldBe("Hexalith.EventStore.Contracts.Queries");
        queryResult.Namespace.ShouldBe("Hexalith.EventStore.Contracts.Queries");
        failureReason.Namespace.ShouldBe("Hexalith.EventStore.Contracts.Queries");
        projectionActor.Namespace.ShouldBe("Hexalith.EventStore.Contracts.Queries");

        System.Reflection.MethodInfo method = projectionActor
            .GetMethod(nameof(IProjectionActor.QueryAsync), [queryEnvelope])
            .ShouldNotBeNull();
        method.ReturnType.ShouldBe(typeof(Task<QueryResult>));
    }

    [Fact]
    public void ReferenceDocs_DoNotReintroduceContractsDaprActorDependencyGuidance() {
        string repositoryRoot = FindRepositoryRoot();
        string[] docPaths = [
            Path.Combine(repositoryRoot, "docs", "reference", "nuget-packages.md"),
            Path.Combine(repositoryRoot, "docs", "reference", "query-api.md"),
            Path.Combine(repositoryRoot, "docs", "reference", "api", "Hexalith.EventStore.Contracts", "Hexalith.EventStore.Contracts.Queries.IProjectionActor.md"),
            Path.Combine(repositoryRoot, "docs", "reference", "api", "Hexalith.EventStore.Contracts", "Hexalith.EventStore.Contracts.Queries.md"),
        ];
        string[] stalePhrases = [
            "Contracts depends on Dapr.Actors",
            "Contracts reference Dapr.Actors",
            "public DAPR actor interface",
            "IProjectionActor : Dapr.Actors.IActor",
            "Implements Dapr.Actors.IActor",
            "`IProjectionActor` - the DAPR actor interface EventStore routes to",
            "Dapr.Actors` for the public projection actor interface",
        ];

        foreach (string path in docPaths) {
            File.Exists(path).ShouldBeTrue($"Expected documentation file {path} to exist.");
            string text = File.ReadAllText(path);
            foreach (string stalePhrase in stalePhrases) {
                text.ShouldNotContain(stalePhrase, Case.Insensitive);
            }
        }
    }

    [Fact]
    public void NugetPackageReferenceDocs_DoNotListDaprActorsAsContractsDependency() {
        string path = Path.Combine(FindRepositoryRoot(), "docs", "reference", "nuget-packages.md");
        string text = File.ReadAllText(path);

        int contractsStart = text.IndexOf("### Hexalith.EventStore.Contracts", StringComparison.Ordinal);
        contractsStart.ShouldBeGreaterThanOrEqualTo(0);
        int nextPackageStart = text.IndexOf("### Hexalith.EventStore.Client", contractsStart, StringComparison.Ordinal);
        nextPackageStart.ShouldBeGreaterThan(contractsStart);
        string contractsSection = text[contractsStart..nextPackageStart];

        contractsSection.ShouldNotContain("| Dapr.Actors", Case.Insensitive);
        contractsSection.ShouldContain("implementation-neutral projection query contract");
    }
}

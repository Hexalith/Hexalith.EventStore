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
                WarningCodes: [QueryWarningCodes.DegradedSearch]));
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
            .GetMethod(nameof(QueryResult.FromPayload), [typeof(JsonElement), typeof(string)])
            .ShouldNotBeNull();
        typeof(QueryResult)
            .GetMethod(nameof(QueryResult.Failure), [typeof(string)])
            .ShouldNotBeNull();
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

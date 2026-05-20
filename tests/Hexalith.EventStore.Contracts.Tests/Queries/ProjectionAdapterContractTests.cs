using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;

using Dapr.Actors;

using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Contracts.Tests.Queries;

public class ProjectionAdapterContractTests {
    private static QueryEnvelope CreateEnvelope(byte[]? payload = null, string? entityId = "party-42")
        => new(
            "tenant-a",
            "parties",
            "party",
            "get-party",
            payload ?? JsonSerializer.SerializeToUtf8Bytes(new { id = "party-42" }),
            "corr-1",
            "user-1",
            entityId);

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
        QueryEnvelope original = CreateEnvelope([1, 2, 3], "party-42");
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
    public void IProjectionActor_IsPublicDaprActorContract() {
        typeof(IActor).IsAssignableFrom(typeof(IProjectionActor)).ShouldBeTrue();

        _ = typeof(IProjectionActor)
            .GetMethod(nameof(IProjectionActor.QueryAsync), [typeof(QueryEnvelope)])
            .ShouldNotBeNull();
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
}

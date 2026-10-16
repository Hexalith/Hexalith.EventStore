using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

using Microsoft.AspNetCore.Http;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class ProjectionReplayTransportContextTests
{
    [Fact]
    public void GuardedContextPreservesTheHistoricalPublicRequestPropertiesAndJson()
    {
        ProjectionDispatchRequest request = Request();
        typeof(ProjectionRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(static property => property.Name).Order().ShouldBe(new[]
            { "AggregateId", "Domain", "EventEvolutionProof", "Events", "RebuildSourceErased",
                "RebuildSourceHeadSequence", "TenantId", "VerifiedEffectiveEvents" });
        string json = JsonSerializer.Serialize(request.Request, JsonSerializerOptions.Web);
        json.ShouldBe("{\"tenantId\":\"tenant-a\",\"domain\":\"counter\",\"aggregateId\":\"counter-1\",\"events\":[],\"rebuildSourceHeadSequence\":1}");
        ProjectionDispatchRequest publicCopy = JsonSerializer.Deserialize<ProjectionDispatchRequest>(
            JsonSerializer.Serialize(request, JsonSerializerOptions.Web), JsonSerializerOptions.Web)!;
        publicCopy.Request.RebuildSourceHeadMessageId.ShouldBeNull();
        publicCopy.Request.RebuildOwnerGenerations.ShouldBeNull();
        publicCopy.Request.RebuildOwnerIdentities.ShouldBeNull();
        string header = ProjectionReplayTransportContext.Header(request).ShouldNotBeNull();
        ProjectionDispatchRequest bound = ProjectionReplayTransportContext.Bind(publicCopy, header);
        bound.Request.RebuildSourceHeadMessageId.ShouldBe(request.DispatchId);
        bound.Request.RebuildOwnerGenerations.ShouldBeNull();
        bound.Request.RebuildOwnerIdentities.ShouldBeNull();
        ProjectionReplayTransportContext.CaptureCanonical(bound.Request).ShouldBe(ProjectionReplayTransportContext.CaptureCanonical(request.Request));
        JsonSerializer.Serialize(bound, JsonSerializerOptions.Web).ShouldBe(JsonSerializer.Serialize(publicCopy, JsonSerializerOptions.Web));
    }

    [Fact]
    public void CompactContextBindsLongHeadAndOwnerReferencesWithoutChangingPublicBytesOrNativeHeaderLimits()
    {
        ProjectionDispatchRequest original = Request();
        string longId = new('g', 4096);
        ProjectionDispatchRequest request = original with { DispatchId = longId, Request = original.Request with
        {
            RebuildSourceHeadMessageId = longId,
            RebuildOwnerGenerations = new Dictionary<string, string> { ["counter-detail"] = longId },
            RebuildOwnerIdentities = new Dictionary<string, string> { ["counter-detail"] = "owner-1" },
        } };
        string publicJson = JsonSerializer.Serialize(request, JsonSerializerOptions.Web);
        string header = ProjectionReplayTransportContext.Header(request).ShouldNotBeNull();
        System.Text.Encoding.UTF8.GetByteCount(header).ShouldBeLessThan(512);
        header.ShouldNotContain(longId);
        ProjectionDispatchRequest bound = ProjectionReplayTransportContext.Bind(
            JsonSerializer.Deserialize<ProjectionDispatchRequest>(publicJson, JsonSerializerOptions.Web)!, header);
        JsonSerializer.Serialize(bound, JsonSerializerOptions.Web).ShouldBe(publicJson);
        bound.Request.RebuildSourceHeadMessageId.ShouldBe(longId);
        ProjectionReplayTransportContext.CaptureCanonical(bound.Request).ShouldBe(ProjectionReplayTransportContext.CaptureCanonical(request.Request));
        var stored = new DomainProjectionStoredReplayPart(bound, 0, "[]"u8.ToArray())
            { ReplayContext = ProjectionReplayTransportContext.Capture(bound.Request) };
        DomainProjectionStoredReplayPart restored = JsonSerializer.Deserialize<DomainProjectionStoredReplayPart>(
            JsonSerializer.Serialize(stored, JsonSerializerOptions.Web), JsonSerializerOptions.Web)!;
        ProjectionReplayTransportContext.CaptureCanonical(restored.Restore().Header.Request)
            .ShouldBe(ProjectionReplayTransportContext.CaptureCanonical(request.Request));
    }

    [Fact]
    public void PrivateStoredPartsRestoreGuardContextAndReadHistoricalContextAbsentParts()
    {
        ProjectionDispatchRequest request = Request();
        var original = new ProjectionRebuildTransportPartRequest(request, 0, "[]"u8.ToArray());
        string historical = JsonSerializer.Serialize(original, JsonSerializerOptions.Web);
        DomainProjectionStoredReplayPart historicalPart = JsonSerializer.Deserialize<DomainProjectionStoredReplayPart>(historical, JsonSerializerOptions.Web)!;
        historicalPart.Restore().Header.Request.RebuildSourceHeadMessageId.ShouldBeNull();
        JsonSerializer.Serialize(historicalPart, JsonSerializerOptions.Web).ShouldBe(historical);
        var guarded = historicalPart with { ReplayContext = ProjectionReplayTransportContext.Capture(request.Request) };
        DomainProjectionStoredReplayPart roundTrip = JsonSerializer.Deserialize<DomainProjectionStoredReplayPart>(
            JsonSerializer.Serialize(guarded, JsonSerializerOptions.Web), JsonSerializerOptions.Web)!;
        ProjectionRequest restored = roundTrip.Restore().Header.Request;
        restored.RebuildSourceHeadMessageId.ShouldBe(request.DispatchId);
        restored.RebuildOwnerGenerations.ShouldBe(request.Request.RebuildOwnerGenerations);
        restored.RebuildOwnerIdentities.ShouldBe(request.Request.RebuildOwnerIdentities);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"version\":1,\"version\":1}")]
    [InlineData("{\"version\":2,\"headMessageId\":\"head\"}")]
    [InlineData("{\"version\":2,\"headMessageId\":\"head\",\"ownerBindingFingerprint\":\"corrupt\"}")]
    [InlineData("{\"version\":1,\"headMessageId\":\"other\"}")]
    [InlineData("{\"version\":1,\"headMessageId\":\"head\",\"extra\":true}")]
    [InlineData("{\"version\":1,\"headMessageId\":\"head\",\"ownerGenerations\":{\"counter-detail\":\"one\",\"counter-detail\":\"two\"},\"ownerIdentities\":{\"counter-detail\":\"owner\"}}")]
    [InlineData("{\"version\":1,\"headMessageId\":\"head\",\"ownerGenerations\":{\"counter-detail\":\"one\"},\"ownerIdentities\":{\"different\":\"owner\"}}")]
    public void MalformedOrUnboundPrivateContextHasOnlyABoundedTypedValidationOutcome(string value)
    {
        var headers = new HeaderDictionary { [ProjectionReplayTransportContext.HeaderName] = value };
        ProjectionDispatchValidationException failure = Should.Throw<ProjectionDispatchValidationException>(
            () => DomainProjectionReplayContext.Bind(Request(), headers));
        failure.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.UnsupportedCapability);
        failure.InnerException.ShouldBeNull();
    }

    private static ProjectionDispatchRequest Request() => new(new("tenant-a", "counter", "counter-1", [])
    {
        RebuildSourceHeadSequence = 1, RebuildSourceHeadMessageId = "head",
        RebuildOwnerGenerations = new Dictionary<string, string> { ["counter-detail"] = "generation" },
        RebuildOwnerIdentities = new Dictionary<string, string> { ["counter-detail"] = "owner" },
    }, ["counter-detail"], "head", new string('a', 64));
}

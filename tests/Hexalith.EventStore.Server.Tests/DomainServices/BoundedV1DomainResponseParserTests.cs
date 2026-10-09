using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.DomainServices;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

public sealed class BoundedV1DomainResponseParserTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData(",\"eventContractType\":null", ",\"payloadVersion\":null")]
    public async Task AdmitsHistoricalV1CaseMappingAndExactPairPresence(string contract, string version)
    {
        string json = "{\"ISREJECTION\":false,\"EVENTS\":[{\"EventTypeName\":\"Exact.Alias\",\"Payload\":\"e30=\"" + contract + version
            + "}],\"unknown\":{\"value\":[true,1,\"é\"]}}";
        using var stream = Input(json);
        using var parser = new BoundedV1DomainResponseParser(stream, CancellationToken.None);
        DomainServiceWireResult result = await parser.ParseAsync();
        result.Events[0].EventTypeName.ShouldBe("Exact.Alias");
        result.Events[0].Payload.ShouldBe("{}"u8.ToArray());
        result.Events[0].SerializationFormat.ShouldBe("json");
        result.WriterMode.ShouldBeNull();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"events\":null}")]
    [InlineData("{\"events\":[null]}")]
    [InlineData("{\"events\":[],\"EVENTS\":[]}")]
    [InlineData("{\"events\":[],\"unknown\":{\"É\":1,\"é\":2}}")]
    [InlineData("{\"events\":[]} {}")]
    public async Task RefusesRootArrayShapeRecursiveDuplicateAndTrailingTokens(string json)
    {
        using var stream = Input(json);
        using var parser = new BoundedV1DomainResponseParser(stream, CancellationToken.None);
        await Should.ThrowAsync<JsonException>(() => parser.ParseAsync());
    }

    [Theory]
    [InlineData(",\"metadataVersion\":2")]
    [InlineData(",\"eventContractType\":\"canonical\",\"payloadVersion\":1")]
    public async Task RefusesPartialNullPairOrUnsolicitedV2AndClearsEarlierPayloadOwner(string extra)
    {
        string json = "{\"events\":[{\"eventTypeName\":\"alias\",\"payload\":\"e30=\"" + extra + "}]}";
        using var stream = Input(json);
        var parser = new BoundedV1DomainResponseParser(stream, CancellationToken.None);
        await Should.ThrowAsync<InvalidOperationException>(() => parser.ParseAsync());
        parser.Dispose();
        parser.LiveBytes.ShouldBe(0);
        parser.Dispose();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(1024)]
    public async Task AdmitsStandalonePayloadVersionWithoutCanonicalType(int version)
    {
        using var stream = Input("{\"events\":[{\"eventTypeName\":\"Exact.Alias\",\"payload\":\"e30=\",\"payloadVersion\":" + version + "}]}");
        using var parser = new BoundedV1DomainResponseParser(stream, CancellationToken.None);

        DomainServiceWireResult result = await parser.ParseAsync();

        result.Events.ShouldHaveSingleItem().PayloadVersion.ShouldBe(version);
        result.Events[0].EventContractType.ShouldBeNull();
    }

    [Theory]
    [InlineData("writerMode")]
    [InlineData("registryFingerprint")]
    public async Task ExplicitNullEchoIsNotAnAbsentLegacyEcho(string field)
    {
        using var stream = Input("{\"events\":[],\"" + field + "\":null}");
        using var parser = new BoundedV1DomainResponseParser(stream, CancellationToken.None);
        (await Should.ThrowAsync<InvalidOperationException>(() => parser.ParseAsync())).Message.ShouldContain("CapabilityMismatch");
    }

    [Fact]
    public async Task PerEventMetadataAndReadableCeilingsApplyBeforeWireResultConstruction()
    {
        using var stream = Input("{\"events\":[{\"eventTypeName\":\"alias\",\"payload\":\"AAAA\"}]}");
        var parser = new BoundedV1DomainResponseParser(stream, CancellationToken.None, 2);
        (await Should.ThrowAsync<InvalidOperationException>(() => parser.ParseAsync())).Message.ShouldContain("PayloadLimit");
        parser.Dispose();
        parser.LiveBytes.ShouldBe(0);
        using var metadataStream = Input("{\"events\":[{\"eventTypeName\":\"alias\",\"payload\":\"e30=\",\"unknown\":\"" + new string('a', 512 * 1024) + "\"}]}");
        var metadataParser = new BoundedV1DomainResponseParser(metadataStream, CancellationToken.None);
        (await Should.ThrowAsync<InvalidOperationException>(() => metadataParser.ParseAsync())).Message.ShouldContain("MetadataLimit");
        metadataParser.Dispose();
        metadataParser.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public async Task LaterMalformedFieldNeverLeaksAPartialResultAndReleasesAllPriorCandidates()
    {
        using var stream = Input("{\"events\":[{\"eventTypeName\":\"alias\",\"payload\":\"e30=\"}],\"resultPayload\":{}}");
        var parser = new BoundedV1DomainResponseParser(stream, CancellationToken.None);
        await Should.ThrowAsync<JsonException>(() => parser.ParseAsync());
        parser.Dispose();
        parser.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public async Task MoreThan1000EventsFailsEvenWithEmptyPayloads()
    {
        string member = "{\"eventTypeName\":\"alias\",\"payload\":\"\"}";
        using var stream = Input("{\"events\":[" + string.Join(',', Enumerable.Repeat(member, 1001)) + "]}");
        var parser = new BoundedV1DomainResponseParser(stream, CancellationToken.None);
        (await Should.ThrowAsync<InvalidOperationException>(() => parser.ParseAsync())).Message.ShouldContain("ResultLimit");
        parser.Dispose();
        parser.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public async Task EventLocalWhitespaceUsesMetadataLimitBeforeWholeResponseLimit()
    {
        string json = "{\"events\":[{" + new string(' ', 600 * 1024) + "\"eventTypeName\":\"alias\",\"payload\":\"e30=\"}]}";
        using var stream = Input(json);
        var parser = new BoundedV1DomainResponseParser(stream, CancellationToken.None);
        (await Should.ThrowAsync<InvalidOperationException>(() => parser.ParseAsync())).Message.ShouldContain("MetadataLimit");
        stream.Position.ShouldBeLessThan(json.Length);
        parser.Dispose();
        parser.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public async Task ZeroSelectedCountStillAdmitsNoOpAndRejectsFirstEventBeforePayloadAllocation()
    {
        using var noOp = Input("{\"events\":[]}");
        using var parser = new BoundedV1DomainResponseParser(noOp, CancellationToken.None, 0, 0);
        (await parser.ParseAsync()).Events.ShouldBeEmpty();
        using var eventStream = Input("{\"events\":[{}]}");
        using var refused = new BoundedV1DomainResponseParser(eventStream, CancellationToken.None, 0, 0);
        (await Should.ThrowAsync<InvalidOperationException>(() => refused.ParseAsync())).Message.ShouldContain("ResultLimit");
    }

    [Theory]
    [InlineData(63, true)]
    [InlineData(64, false)]
    public async Task DepthCountsRootAsOne(int arrays, bool admitted)
    {
        string nested = new string('[', arrays) + "0" + new string(']', arrays);
        using var stream = Input("{\"events\":[],\"unknown\":" + nested + "}");
        using var parser = new BoundedV1DomainResponseParser(stream, CancellationToken.None);
        if (admitted) { (await parser.ParseAsync()).Events.ShouldBeEmpty(); }
        else { await Should.ThrowAsync<JsonException>(() => parser.ParseAsync()); }
    }

    [Theory]
    [InlineData(999_998, true)]
    [InlineData(999_999, false)]
    public async Task CountsObjectMembersAndArrayElementsAtExactMillionNodeBoundary(int elements, bool admitted)
    {
        // Two root properties plus each unknown-array element count once.
        using var stream = Input("{\"events\":[],\"unknown\":[" + string.Join(',', Enumerable.Repeat("0", elements)) + "]}");
        using var parser = new BoundedV1DomainResponseParser(stream, CancellationToken.None);
        if (admitted) { (await parser.ParseAsync()).Events.ShouldBeEmpty(); }
        else { (await Should.ThrowAsync<InvalidOperationException>(() => parser.ParseAsync())).Message.ShouldContain("ResultLimit"); }
    }

    private static MemoryStream Input(string json) => new(Encoding.UTF8.GetBytes(json));
}

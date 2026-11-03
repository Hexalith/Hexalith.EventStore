using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Contracts.Tests.Routing;

/// <summary>Pins the existing canonical writer's ordinal Unicode and stable equal-name behavior in embedded schemas.</summary>
public sealed class RouteCatalogCanonicalOrderingTests
{
    /// <summary>Sorted and unsorted inputs retain the digests produced by the original stable-sort codec.</summary>
    [Theory]
    [InlineData("{\"é\":{\"z\":1,\"a\":2},\"a\":[1,null,true],\"😀\":{\"\\uE000\":\"b\",\"😀\":\"a\"},\"z\":0}", "b72ba566a94102891e71396ee2c3ce9daf4a724a3aa690ef082cdf1088ce5d7a")]
    [InlineData("{\"a\":[1,null,true],\"z\":0,\"é\":{\"a\":2,\"z\":1},\"😀\":{\"😀\":\"a\",\"\\uE000\":\"b\"}}", "b72ba566a94102891e71396ee2c3ce9daf4a724a3aa690ef082cdf1088ce5d7a")]
    [InlineData("{\"z\":0,\"a\":1,\"a\":2}", "d5eef3b57fd1f66db56b91f611054f92cace7be5fb0709bd71b5a6e8cc37ae84")]
    [InlineData("{\"a\":1,\"a\":2,\"z\":0}", "d5eef3b57fd1f66db56b91f611054f92cace7be5fb0709bd71b5a6e8cc37ae84")]
    [InlineData("{\"a\":2,\"a\":1,\"z\":0}", "e8125b6d9f49c007e201fafc566bb82f6dd1c798f40c096010b48e8fdc215ffe")]
    public void EmbeddedSchemaMatchesPriorCanonicalDigest(string schema, string expected)
    {
        using JsonDocument input = JsonDocument.Parse(schema);
        using JsonDocument output = JsonDocument.Parse("{}");
        var operation = new RouteOperationContract("codec-order", "v1", "public", "agent-eligible",
            input.RootElement, output.RootElement, "", "query", "sample", "GetSample");

        RouteCatalogCodec.ComputeOperationContractDigest(operation).ShouldBe(expected);
    }
}

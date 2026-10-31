using System.Text.Json;
using Hexalith.EventStore.Sample.Counter.Queries;
using Shouldly;

namespace Hexalith.EventStore.Sample.Tests;

/// <summary>Preserves the original projection bytes when its existing result receives an explicit schema type.</summary>
public sealed class CounterProjectionSchemaTests
{
    /// <summary>The named owner result has exactly the anonymous count-only wire shape under both actual/default and Web schema settings.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void TypedCounterResultPreservesExistingProjectionBytes(int count)
    {
        foreach (var options in new[] { new JsonSerializerOptions(), new JsonSerializerOptions(JsonSerializerDefaults.Web) })
            JsonSerializer.SerializeToUtf8Bytes(new CounterStatusPayload(count), options).ShouldBe(JsonSerializer.SerializeToUtf8Bytes(new { count }, options));
    }
}

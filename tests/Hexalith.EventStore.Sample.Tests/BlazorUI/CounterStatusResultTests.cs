extern alias BlazorUI;

using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;

using Shouldly;

using CounterStatusResult = BlazorUI::Hexalith.EventStore.Sample.BlazorUI.Services.CounterStatusResult;

namespace Hexalith.EventStore.Sample.Tests.BlazorUI;

/// <summary>Exercises the sample counter's projection payload and cache mapping.</summary>
public sealed class CounterStatusResultTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidCounterPayload_AcceptsCountAndValidator(bool base64)
    {
        const string Json = "{\"count\":42}";
        string payload = base64 ? JsonSerializer.Serialize(Convert.ToBase64String(Encoding.UTF8.GetBytes(Json))) : Json;
        using var document = JsonDocument.Parse(payload);
        var result = new EventStoreQueryResult("query", document.RootElement, false, "\"fresh\"");

        CounterStatusResult.FromQueryResult(result, null).ShouldBe(new CounterStatusResult(42, "\"fresh\""));
    }

    [Theory]
    [InlineData("\"not-base64\"")]
    [InlineData("\"\\uDEAD\"")]
    [InlineData("\"bm90IGpzb24=\"")]
    [InlineData("\"W10=\"")]
    [InlineData("{}")]
    [InlineData("{\"count\":\"1\"}")]
    [InlineData("{\"count\":2147483648}")]
    [InlineData("{\"count\":1.5}")]
    [InlineData("null")]
    public void InvalidCounterPayload_KeepsLastValidStateAndValidator(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var result = new EventStoreQueryResult("query", document.RootElement, false, "\"bad\"");
        var cached = new CounterStatusResult(12, "\"good\"");

        CounterStatusResult.TryFromQueryResult(result, cached, out CounterStatusResult retained).ShouldBeFalse();
        retained.ShouldBeSameAs(cached);
        CounterStatusResult.FromQueryResult(result, null).ShouldBeSameAs(CounterStatusResult.Empty);
    }

    [Fact]
    public void NotModifiedWithCachedState_KeepsCachedState()
    {
        var result = new EventStoreQueryResult(null, null, IsNotModified: true, "\"good\"");
        var cached = new CounterStatusResult(12, "\"good\"");

        CounterStatusResult.TryFromQueryResult(result, cached, out CounterStatusResult state).ShouldBeTrue();
        state.ShouldBeSameAs(cached);
    }

    [Fact]
    public void NotModifiedWithoutCachedState_IsUnusableAndKeepsNoValidator()
    {
        var result = new EventStoreQueryResult(null, null, IsNotModified: true, "\"caller-supplied\"");

        CounterStatusResult.TryFromQueryResult(result, null, out CounterStatusResult state).ShouldBeFalse();
        state.ShouldBeSameAs(CounterStatusResult.Empty);
        CounterStatusResult.FromQueryResult(result, null).ShouldBeSameAs(CounterStatusResult.Empty);
    }

    [Fact]
    public void NotModifiedForAnotherValidator_IsUnusableAndKeepsCachedState()
    {
        var result = new EventStoreQueryResult(null, null, IsNotModified: true, "\"other\"");
        var cached = new CounterStatusResult(12, "\"good\"");

        CounterStatusResult.TryFromQueryResult(result, cached, out CounterStatusResult state).ShouldBeFalse();
        state.ShouldBeSameAs(cached);
    }

    [Fact]
    public void NotModifiedOverTheEmptyState_IsUnusable()
    {
        var result = new EventStoreQueryResult(null, null, IsNotModified: true, "\"caller-supplied\"");

        CounterStatusResult.TryFromQueryResult(result, CounterStatusResult.Empty, out CounterStatusResult state).ShouldBeFalse();
        state.ShouldBeSameAs(CounterStatusResult.Empty);
    }
}

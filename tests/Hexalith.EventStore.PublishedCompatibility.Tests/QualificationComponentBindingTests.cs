using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Reads the actual finite component format and refuses unsupported backend semantics.</summary>
public sealed class QualificationComponentBindingTests
{
    internal const string Component = "apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: statestore\nspec:\n  type: state.redis\n  version: v1\n  metadata:\n  - name: redisHost\n    value: 127.0.0.1:6380\n  - name: actorStateStore\n    value: \"true\"\n  - name: keyPrefix\n    value: appid\n";

    [Fact]
    public async Task RealPreparedComponentRetainsOnlyItsEffectiveSafeBinding()
    {
        await using var runtime = new QualificationRuntime(); string path = Path.Combine(runtime.Root, "state.yaml");
        await File.WriteAllTextAsync(path, Component, TestContext.Current.CancellationToken);
        var input = await runtime.CaptureScenarioInputAsync(Guid.NewGuid().ToString("N"), "component", path);
        input.Component.ShouldBe(new("127.0.0.1:6380", "statestore", "state.redis", "v1", true, "appid"));
        input.Sha256.ShouldBe(QualificationRuntime.Hash(System.Text.Encoding.UTF8.GetBytes(Component)));
    }

    [Theory]
    [InlineData("6380", "70000")]
    [InlineData("appid", "none")]
    [InlineData("state.redis", "state.foreign")]
    [InlineData("\"true\"", "\"false\"")]
    public void UnsupportedFormatCannotClaimTheFiniteBinding(string before, string after)
        => Should.Throw<InvalidOperationException>(() => QualificationComponentBinding.Capture(Component.Replace(before, after, StringComparison.Ordinal)));
}

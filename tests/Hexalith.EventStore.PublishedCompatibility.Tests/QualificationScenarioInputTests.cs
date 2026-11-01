using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Reads real private prepared bytes while retaining only finite coordinates and digests.</summary>
public sealed class QualificationScenarioInputTests
{
    [Fact]
    public async Task RealPreparedBytesAreCommittedWithoutRetainingPrivateContents()
    {
        await using var runtime = new QualificationRuntime();
        string path = Path.Combine(runtime.Root, "component.yaml");
        string secret = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes("redisPassword: " + secret);
        await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
        var captured = await runtime.CaptureScenarioInputAsync(Guid.NewGuid().ToString("N"), "opaque", path);
        captured.Bytes.ShouldBe(bytes.Length); captured.Sha256.ShouldBe(QualificationRuntime.Hash(bytes));
        captured.Owner.ShouldBe(runtime.Id); runtime.ScenarioInputs.ShouldContain(captured);
        JsonSerializer.Serialize(captured).ShouldNotContain(secret);
    }

    [Fact]
    public async Task ForeignPreparedFileIsRefusedBeforeReading()
    {
        await using var runtime = new QualificationRuntime();
        await Should.ThrowAsync<InvalidOperationException>(() => runtime.CaptureScenarioInputAsync("profile", "component", runtime.Root + "-foreign/absent"));
        runtime.ScenarioInputs.ShouldBeEmpty();
    }
}

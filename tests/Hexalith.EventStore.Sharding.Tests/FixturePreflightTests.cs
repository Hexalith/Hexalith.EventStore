using System.Globalization;
using System.Reflection;
using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Exercises pre-resource isolation and expiry failures without invoking Docker.</summary>
public sealed class FixturePreflightTests
{
    /// <summary>Delayed preflight reaches expiry without negative timers or external commands.</summary>
    [Fact]
    public async Task ExpiryDuringPreflightCreatesNoResourcesAsync()
    {
        await using var fixture = new ShardingFixture();
        string expiry = DateTimeOffset.UtcNow.AddSeconds(1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        await Should.ThrowAsync<OperationCanceledException>(() => fixture.InitializeAsync(readExpiry: () => expiry,
            beforePreflight: _ => Task.Delay(1500), resolveDockerEndpoint: () => Task.FromResult("unix:///var/run/docker.sock")));
        await fixture.AwaitExpiryAsync();
        fixture.ProcessIds.ShouldBeEmpty();
        fixture.CommandCount.ShouldBe(0);
        fixture.Artifacts.ShouldBeEmpty();
        fixture.CleanupVerified.ShouldBeTrue();
    }

    /// <summary>Remote Docker endpoints fail before commands, directories, or resources.</summary>
    /// <param name="endpoint">The rejected effective transport.</param>
    [Theory]
    [InlineData("tcp://127.0.0.1:2375")]
    [InlineData("tcp://remote.example:2376")]
    [InlineData("ssh://remote.example")]
    [InlineData("http://localhost:2375")]
    [InlineData("unix://remote.example/var/run/docker.sock")]
    [InlineData("npipe:////remote/pipe/docker_engine")]
    public async Task RemoteDockerEndpointCreatesNoResourcesAsync(string endpoint)
    {
        if (string.Equals(endpoint, "ssh://remote.example", StringComparison.Ordinal))
        {
            endpoint = new UriBuilder(endpoint) { UserName = Environment.UserName }.Uri.AbsoluteUri;
        }

        await using var fixture = new ShardingFixture();
        string expiry = DateTimeOffset.UtcNow.AddMinutes(1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.InitializeAsync(readExpiry: () => expiry,
            resolveDockerEndpoint: () => Task.FromResult(endpoint)));
        fixture.CommandCount.ShouldBe(0);
        fixture.ProcessIds.ShouldBeEmpty();
        fixture.Artifacts.ShouldBeEmpty();
        fixture.CleanupVerified.ShouldBeTrue();
    }

    /// <summary>Explicit flags override environment, and context overrides host within the environment.</summary>
    [Fact]
    public async Task DockerEndpointResolutionHonorsCliAndEnvironmentPrecedenceAsync()
    {
        var variables = new Dictionary<string, string> { ["DOCKER_HOST"] = "tcp://remote:2375", ["DOCKER_CONTEXT"] = "private-local" };
        string? inspected = null;
        Task<string> Context(string? name) { inspected = name; return Task.FromResult("unix:///context.sock"); }
        string? EnvironmentValue(string name) => variables.GetValueOrDefault(name);
        (await DockerEndpointPolicy.ResolveAsync("unix:///explicit.sock", null, EnvironmentValue, Context)).ShouldBe("unix:///explicit.sock");
        inspected.ShouldBeNull();
        (await DockerEndpointPolicy.ResolveAsync(null, "cli-local", EnvironmentValue, Context)).ShouldBe("unix:///context.sock");
        inspected.ShouldBe("cli-local");
        (await DockerEndpointPolicy.ResolveAsync(null, null, EnvironmentValue, Context)).ShouldBe("unix:///context.sock");
        inspected.ShouldBe("private-local");
        variables.Remove("DOCKER_CONTEXT");
        await Should.ThrowAsync<InvalidOperationException>(() => DockerEndpointPolicy.ResolveAsync(null, null, EnvironmentValue, Context));
        (await DockerEndpointPolicy.ResolveAsync(null, null, _ => null, _ => Task.FromResult("npipe:////./pipe/docker_engine")))
            .ShouldBe("npipe:////./pipe/docker_engine");
    }

    /// <summary>Diagnostic failures cannot skip scratch removal and a later disposal safely retries.</summary>
    [Fact]
    public async Task DiagnosticFailureStillRemovesScratchAndPermitsCleanupRetryAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "sharding-cleanup-unit-" + Guid.NewGuid().ToString("N"));
        string scratch = Path.Combine(root, "scratch");
        string artifacts = Path.Combine(root, "artifacts");
        Directory.CreateDirectory(scratch);
        File.WriteAllText(Path.Combine(scratch, "private.txt"), "disposable");
        File.WriteAllText(artifacts, "blocks diagnostic writes");
        await using var fixture = new ShardingFixture();
        typeof(ShardingFixture).GetField("_scratch", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(fixture, scratch);
        typeof(ShardingFixture).GetField("_artifacts", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(fixture, artifacts);
        try
        {
            await Should.ThrowAsync<InvalidOperationException>(async () => await fixture.DisposeAsync());
            Directory.Exists(scratch).ShouldBeFalse();
            fixture.CleanupVerified.ShouldBeFalse();
            File.Delete(artifacts);
            Directory.CreateDirectory(artifacts);
            await fixture.DisposeAsync();
            fixture.CleanupVerified.ShouldBeTrue();
            using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(artifacts, "cleanup.json")));
            receipt.RootElement.GetProperty("verified").GetBoolean().ShouldBeTrue();
            receipt.RootElement.GetProperty("failureHistory").GetArrayLength().ShouldBeGreaterThan(0);
        }
        finally
        {
            // Ensure an assertion failure still leaves diagnostics writable for final disposal.
            if (File.Exists(artifacts)) { File.Delete(artifacts); Directory.CreateDirectory(artifacts); }
            await fixture.DisposeAsync();
            Directory.Delete(root, recursive: true);
        }
    }
}

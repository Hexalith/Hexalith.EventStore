using System.Text;
using System.Text.Json;
using System.Globalization;

using Hexalith.EventStore.Sharding.TestHost;

using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Deterministic pre-resource checks for deadline, binding and physical identities.</summary>
public sealed class BindingTests
{
    /// <summary>Missing, noncanonical, expired and overly long expiry inputs fail closed.</summary>
    /// <param name="value">The invalid operator input.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026-10-31T12:01:00+00:00")]
    [InlineData("2026-10-31T12:01:00.000Z")]
    [InlineData("2026-10-31T12:01:00z")]
    [InlineData("2026-10-31T12:00:00Z")]
    [InlineData("2026-10-31T11:59:59Z")]
    [InlineData("2026-10-31T13:00:01Z")]
    [InlineData("2026-02-30T12:01:00Z")]
    public void InvalidExpiryRejectsBeforeExecution(string? value)
        => Should.Throw<InvalidOperationException>(() => RunBinding.ParseExpiry(value, new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero)));

    /// <summary>The exact exclusive future second and one-hour limit are accepted.</summary>
    [Fact]
    public void ValidExpiryPreservesExactOperatorSecond()
    {
        var now = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        RunBinding.ParseExpiry("2026-10-31T12:00:01Z", now).ShouldBe(now.AddSeconds(1));
        RunBinding.ParseExpiry("2026-10-31T13:00:00Z", now).ShouldBe(now.AddHours(1));
        Should.Throw<InvalidOperationException>(() => RunBinding.ParseExpiry("2026-10-31T12:00:01Z", now.AddSeconds(1)));
    }

    /// <summary>A missing binding is rejected before the host can listen.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("binding.json")]
    public void MissingBindingRejectsBeforeHostStartup(string? path)
        => Should.Throw<InvalidOperationException>(() => RunBinding.LoadHost(path, AppContext.BaseDirectory));

    /// <summary>Invalid input also prevents fixture resource creation and still permits safe disposal.</summary>
    [Fact]
    public async Task InvalidFixtureBindingCreatesNoResourcesAsync()
    {
        await using var fixture = new ShardingFixture();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.InitializeAsync(readExpiry: () => null));
        fixture.ProcessIds.ShouldBeEmpty();
        fixture.Artifacts.ShouldBeEmpty();
        await fixture.DisposeAsync();
        fixture.CleanupVerified.ShouldBeTrue();
    }

    /// <summary>Well-formed bindings still reject changed artifacts, subjects and missing identities.</summary>
    /// <param name="mutation">The invalid binding field.</param>
    [Theory]
    [InlineData("artifact")]
    [InlineData("subject")]
    [InlineData("runtime")]
    [InlineData("test")]
    [InlineData("configuration")]
    [InlineData("expired")]
    public void InvalidHostBindingRejectsExecution(string mutation)
    {
        string scratch = Path.Combine(Path.GetTempPath(), "sharding-binding-check-" + Guid.NewGuid().ToString("N"));
        string host = Path.Combine(scratch, "host");
        Directory.CreateDirectory(host);
        try
        {
            File.WriteAllText(Path.Combine(host, "host.txt"), "bound host bytes");
            DateTimeOffset now = DateTimeOffset.UtcNow;
            RunBinding valid = CreateBinding(now.AddSeconds(-1), now.AddMinutes(5), RunBinding.HashDirectory(host));
            RunBinding invalid = mutation switch
            {
                "artifact" => valid with { HostFiles = new() },
                "subject" => valid with { CandidateCommit = new string('0', 40) },
                "runtime" => valid with { Runtime = new() },
                "test" => valid with { TestFiles = new() },
                "configuration" => valid with { Configuration = new() },
                "expired" => CreateBinding(now.AddMinutes(-10), now.AddSeconds(-1), RunBinding.HashDirectory(host)),
                _ => throw new InvalidOperationException("Unknown mutation."),
            };
            string path = Path.Combine(scratch, "binding.json");
            File.WriteAllText(path, JsonSerializer.Serialize(invalid));
            Should.Throw<InvalidOperationException>(() => RunBinding.LoadHost(path, host));
            File.WriteAllText(path, JsonSerializer.Serialize(valid));
            RunBinding.LoadHost(path, host).RunId.ShouldBe(valid.RunId);
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }

    /// <summary>The host's real-time token cancels at expiry and cannot reopen new writes.</summary>
    [Fact]
    public async Task HostFenceCancelsAtExclusiveExpiryAsync()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using var fence = new RunFence(CreateBinding(now.AddSeconds(-1), now.AddSeconds(2), new()));
        fence.IsExpired.ShouldBeFalse();
        await Should.ThrowAsync<TaskCanceledException>(() => Task.Delay(TimeSpan.FromSeconds(5), fence.Token));
        fence.Token.IsCancellationRequested.ShouldBeTrue();
        fence.IsExpired.ShouldBeTrue();
    }

    /// <summary>Serialization exposes every member of the persisted state and full returned range.</summary>
    [Fact]
    public void JsonRoundTripPreservesFullExactIdentity()
    {
        var reply = new ShardReply(true, "saved", new ShardState("T|é", "d.e\u0301", 17, 41), 39, 3);
        JsonSerializer.Deserialize<ShardReply>(JsonSerializer.Serialize(reply)).ShouldBe(reply);
        JsonSerializer.Deserialize<ShardRequest>(JsonSerializer.Serialize(new ShardRequest("Reserve", "T|é", "d.e\u0301", 17, 3)))
            .ShouldBe(new ShardRequest("Reserve", "T|é", "d.e\u0301", 17, 3));
    }

    /// <summary>Every test-only Int64 field stays a JSON string with exact boundary digits.</summary>
    /// <param name="value">The maximum or adjacent representable signed counter.</param>
    [Theory]
    [InlineData(long.MaxValue)]
    [InlineData(long.MaxValue - 1)]
    public void Int64BoundaryFieldsUseExactJsonStrings(long value)
    {
        var request = new ShardRequest("Seed", "tenant", "domain", value, SeedCeiling: value);
        var state = new ShardState("tenant", "domain", value, value);
        var reply = new ShardReply(true, "saved", state, value, 1);
        JsonElement requestJson = JsonSerializer.SerializeToElement(request);
        JsonElement stateJson = JsonSerializer.SerializeToElement(state);
        JsonElement replyJson = JsonSerializer.SerializeToElement(reply);
        foreach (JsonElement field in new[]
        {
            requestJson.GetProperty("Generation"), requestJson.GetProperty("SeedCeiling"),
            stateJson.GetProperty("Generation"), stateJson.GetProperty("Ceiling"),
            replyJson.GetProperty("First"), replyJson.GetProperty("State").GetProperty("Generation"),
            replyJson.GetProperty("State").GetProperty("Ceiling"),
        })
        {
            field.ValueKind.ShouldBe(JsonValueKind.String);
            field.GetString().ShouldBe(value.ToString(CultureInfo.InvariantCulture));
        }

        requestJson.Deserialize<ShardRequest>().ShouldBe(request);
        stateJson.Deserialize<ShardState>().ShouldBe(state);
        replyJson.Deserialize<ShardReply>().ShouldBe(reply);
        // The web serializer used at the actor boundary must retain the same precision.
        JsonSerializer.Deserialize<ShardRequest>(JsonSerializer.Serialize(request, JsonSerializerOptions.Web), JsonSerializerOptions.Web).ShouldBe(request);
        JsonSerializer.Deserialize<ShardReply>(JsonSerializer.Serialize(reply, JsonSerializerOptions.Web), JsonSerializerOptions.Web).ShouldBe(reply);
    }

    /// <summary>Physical routes preserve delimiters, case, Unicode composition and member boundaries.</summary>
    [Fact]
    public void PhysicalIdentityIsInjectiveForExactPairs()
    {
        (string Tenant, string Domain)[] pairs = [("a|b", "c"), ("a", "b|c"), ("A", "b|c"), ("é", "x"), ("e\u0301", "x"), ("x", "é"), ("x", "e\u0301"), (" ", ".")];
        pairs.Select(pair => ShardIdentity.Encode(pair.Tenant, pair.Domain)).Distinct(StringComparer.Ordinal).Count().ShouldBe(pairs.Length);
        Should.Throw<ArgumentException>(() => ShardIdentity.Encode("", "d"));
        Should.Throw<EncoderFallbackException>(() => ShardIdentity.Encode("\uD800", "d"));
    }

    private static RunBinding CreateBinding(DateTimeOffset created, DateTimeOffset expiry, Dictionary<string, string> hostFiles)
        => new("binding-unit-check", created.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            expiry.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            "411005b4ccb2352249c83330c54967c6a22beba1", "160331d25451928ff3c3dea2300b65cab4f97c3b",
            "995fcecd16b3421ec9ff666d0884bfb5e436932aa49529c152fb7c439172a1fd", hostFiles,
            new() { ["test"] = "unit-identity" }, new() { ["runtime"] = "unit-identity" }, new() { ["scope"] = "unit-identity" });
}

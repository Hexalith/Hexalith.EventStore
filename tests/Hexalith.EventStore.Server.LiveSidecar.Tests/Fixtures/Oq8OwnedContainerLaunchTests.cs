using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Proves partial PostgreSQL launches never lose ownership or target container names.</summary>
public sealed class Oq8OwnedContainerLaunchTests
{
    /// <summary>Preserves an exact created identity when launch exits unsuccessfully.</summary>
    [Fact]
    public async Task FailedLaunchRetainsExactCid()
        => await CheckAsync(new InvalidOperationException("publication failed"), new string('a', 64));

    /// <summary>Preserves an exact created identity when the launch times out.</summary>
    [Fact]
    public async Task TimedOutLaunchRetainsExactCid()
        => await CheckAsync(new TimeoutException("launch timed out"), new string('a', 64));

    /// <summary>A failed launch without a CID records no ownership.</summary>
    [Fact]
    public async Task FailureWithoutCidRecordsNoContainer()
        => await CheckAsync(new InvalidOperationException("launch failed"), null);

    /// <summary>Names and malformed identities are never retained for teardown.</summary>
    [Fact]
    public async Task InvalidCidRecordsNoContainer()
        => await CheckAsync(new TimeoutException("launch timed out"), "shared-postgresql");

    private static async Task CheckAsync(Exception failure, string? cid)
    {
        string directory = Path.Combine(Path.GetTempPath(), "oq8-cid-control-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "postgresql.cid");
        var owned = new List<string>();
        try
        {
            if (cid is not null)
            {
                File.WriteAllText(path, cid);
            }

            Exception observed = await Should.ThrowAsync<Exception>(() => Oq8OwnedContainerLaunch.RunAsync(
                path, () => Task.FromException<string>(failure), owned.Add));
            observed.ShouldBeSameAs(failure);
            if (cid?.Length == 64)
            {
                owned.ShouldHaveSingleItem().ShouldBe(cid);
            }
            else
            {
                owned.ShouldBeEmpty();
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

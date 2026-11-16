using System.Net;
using System.Text;

using Shouldly;

using StackExchange.Redis;

namespace Hexalith.EventStore.IntegrationTests.Security;

public sealed class ProjectionDeliveryWriterProtocolTestLeaseTests(PrivateAuthenticationRedisFixture fixture) : IClassFixture<PrivateAuthenticationRedisFixture> {
    private string RedisEndpoint => fixture.Endpoint;
    private const string MarkerKey = "projection-delivery-writer-protocol";

    [Fact]
    public async Task ActivateAndRestoreAsync_CleanStore_UsesCutoverHttpContractAndRestoresState() {
        const string sourceCommit = "0123456789abcdef0123456789abcdef01234567";
        using IConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions {
            EndPoints = { RedisEndpoint },
            ConnectTimeout = 5_000,
            SyncTimeout = 5_000,
            AbortOnConnectFail = false,
            AllowAdmin = false,
        }).ConfigureAwait(true);
        IDatabase database = redis.GetDatabase();
        bool markerExisted = await database.KeyExistsAsync(MarkerKey).ConfigureAwait(true);
        HashEntry[] originalState = await database.HashGetAllAsync(MarkerKey).ConfigureAwait(true);
        TimeSpan? originalExpiry = await database.KeyTimeToLiveAsync(MarkerKey).ConfigureAwait(true);
        var lease = new ProjectionDeliveryWriterProtocolTestLease(RedisEndpoint);

        try {
            using var client = new HttpClient(new WriterProtocolTestCutoverHandler(database, sourceCommit)) {
                BaseAddress = new Uri("https://eventstore.test"),
            };
            await lease.ActivateAsync(
                client,
                "admin-token",
                sourceCommit,
                "fixture-test-backup",
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            RedisValue activated = await database.HashGetAsync(MarkerKey, "data").ConfigureAwait(true);
            activated.ToString().ShouldContain(sourceCommit);

            await lease.RestoreAsync().ConfigureAwait(true);
            (await database.KeyExistsAsync(MarkerKey).ConfigureAwait(true)).ShouldBe(markerExisted);
            HashEntry[] restoredState = await database.HashGetAllAsync(MarkerKey).ConfigureAwait(true);
            // Redis can repack a restored hash into a different RDB byte sequence. Its
            // fields and expiry, rather than serialization order, define restored state.
            restoredState.OrderBy(entry => entry.Name.ToString(), StringComparer.Ordinal)
                .ShouldBe(originalState.OrderBy(entry => entry.Name.ToString(), StringComparer.Ordinal));
            TimeSpan? restoredExpiry = await database.KeyTimeToLiveAsync(MarkerKey).ConfigureAwait(true);
            if (originalExpiry is null) {
                restoredExpiry.ShouldBeNull();
            }
            else {
                restoredExpiry.ShouldNotBeNull().ShouldBeLessThanOrEqualTo(originalExpiry.Value);
                restoredExpiry.Value.ShouldBeGreaterThan(originalExpiry.Value - TimeSpan.FromMinutes(1));
            }
        }
        finally {
            try {
                await lease.RestoreAsync().ConfigureAwait(true);
            }
            finally {
                await redis.CloseAsync(allowCommandsToComplete: true).ConfigureAwait(true);
            }
        }
    }

    [Fact]
    public async Task ActivateAsync_ForeignLease_PreservesForeignMarkerAndLease() {
        using var redis = await ConnectionMultiplexer.ConnectAsync(RedisEndpoint);
        IDatabase database = redis.GetDatabase();
        const string lockKey = "projection-delivery-writer-protocol:test-isolation-lock";
        string owner = Guid.NewGuid().ToString("N");
        await database.HashSetAsync(MarkerKey, "data", "foreign-marker");
        (await database.LockTakeAsync(lockKey, owner, TimeSpan.FromMinutes(1))).ShouldBeTrue();
        var lease = new ProjectionDeliveryWriterProtocolTestLease(RedisEndpoint);
        try {
            using var client = new HttpClient { BaseAddress = new Uri("https://eventstore.test") };
            await Should.ThrowAsync<InvalidOperationException>(() => lease.ActivateAsync(client,
                "admin-token", "0123456789abcdef0123456789abcdef01234567", "test-backup", TestContext.Current.CancellationToken));
            await lease.RestoreAsync();
            (await database.HashGetAsync(MarkerKey, "data")).ToString().ShouldBe("foreign-marker");
            (await database.StringGetAsync(lockKey)).ToString().ShouldBe(owner);
        }
        finally {
            await database.LockReleaseAsync(lockKey, owner);
            await database.KeyDeleteAsync(MarkerKey);
        }
    }

    [Fact]
    public async Task ActivateAsync_FailedActivation_RestoresSnapshotAndReleasesLease() {
        using var redis = await ConnectionMultiplexer.ConnectAsync(RedisEndpoint);
        IDatabase database = redis.GetDatabase();
        await database.HashSetAsync(MarkerKey, "data", "previous-marker");
        var lease = new ProjectionDeliveryWriterProtocolTestLease(RedisEndpoint);
        try {
            using var client = new HttpClient(new WriterProtocolTestCutoverHandler(database, "0123456789abcdef0123456789abcdef01234567", HttpStatusCode.Unauthorized)) { BaseAddress = new Uri("https://eventstore.test") };
            await Should.ThrowAsync<InvalidOperationException>(() => lease.ActivateAsync(client,
                "admin-token", "0123456789abcdef0123456789abcdef01234567", "test-backup", TestContext.Current.CancellationToken));
            await lease.RestoreAsync();
            (await database.HashGetAsync(MarkerKey, "data")).ToString().ShouldBe("previous-marker");
            (await database.KeyExistsAsync("projection-delivery-writer-protocol:test-isolation-lock")).ShouldBeFalse();
        }
        finally {
            await lease.RestoreAsync();
            await database.KeyDeleteAsync(MarkerKey);
        }
    }

}


using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.Testing.Tests.Fakes;

public class FakeETagActorTests {
    [Fact]
    public async Task GetCurrentETagAsync_ReturnsConfiguredETag() {
        var sut = new FakeETagActor();
        sut.ConfiguredETag = "test-etag-value";

        string? result = await sut.GetCurrentETagAsync();

        Assert.Equal("test-etag-value", result);
    }

    [Fact]
    public async Task GetCurrentETagAsync_ReturnsNullByDefault() {
        var sut = new FakeETagActor();

        string? result = await sut.GetCurrentETagAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task RegenerateAsync_IncrementsCount() {
        var sut = new FakeETagActor();

        _ = await sut.RegenerateAsync();
        _ = await sut.RegenerateAsync();

        Assert.Equal(2, sut.RegenerateCount);
        Assert.Equal(2, sut.ReceivedNotifications.Count);
    }

    [Fact]
    public async Task RegenerateAsync_UpdatesConfiguredETag() {
        var sut = new FakeETagActor();
        Assert.Null(sut.ConfiguredETag);

        _ = await sut.RegenerateAsync();

        Assert.NotNull(sut.ConfiguredETag);
    }

    [Fact]
    public async Task RegenerateAsync_ReturnsSelfRoutingETag() {
        var sut = new FakeETagActor();

        string etag = await sut.RegenerateAsync();

        // Self-routing format: {base64url(projectionType)}.{base64url-guid}
        Assert.Contains(".", etag);
        Assert.DoesNotContain("+", etag);
        Assert.DoesNotContain("/", etag);
        Assert.DoesNotContain("=", etag);

        // Decode should return the default projection type
        bool decoded = SelfRoutingETag.TryDecode(etag, out string? projectionType, out _);
        Assert.True(decoded);
        Assert.Equal("test-projection", projectionType);
    }

    [Fact]
    public async Task RegenerateAsync_UsesConfiguredProjectionType() {
        var sut = new FakeETagActor { ProjectionType = "counter" };

        string etag = await sut.RegenerateAsync();

        bool decoded = SelfRoutingETag.TryDecode(etag, out string? projectionType, out _);
        Assert.True(decoded);
        Assert.Equal("counter", projectionType);
    }

    [Fact]
    public async Task RegenerateAsync_ThrowsConfiguredException() {
        var sut = new FakeETagActor();
        sut.ConfiguredException = new InvalidOperationException("actor failure");

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.RegenerateAsync());
    }

    [Fact]
    public async Task RegenerateAsync_RecordsAllETags() {
        var sut = new FakeETagActor();

        string e1 = await sut.RegenerateAsync();
        string e2 = await sut.RegenerateAsync();

        Assert.Equal(2, sut.RegeneratedETags.Count);
        Assert.Contains(e1, sut.RegeneratedETags);
        Assert.Contains(e2, sut.RegeneratedETags);
    }

    [Fact]
    public async Task Reset_ClearsAllState() {
        var sut = new FakeETagActor();
        sut.ConfiguredETag = "old-etag";
        _ = await sut.RegenerateAsync();
        sut.ConfiguredException = new Exception("old-exception");

        sut.Reset();

        Assert.Null(sut.ConfiguredETag);
        Assert.Null(sut.ConfiguredException);
        Assert.Equal(0, sut.RegenerateCount);
        Assert.Empty(sut.ReceivedNotifications);
        Assert.Empty(sut.RegeneratedETags);
    }
}

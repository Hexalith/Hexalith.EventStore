
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.Testing.Tests.Fakes;

public class InMemoryStateManagerTests {
    [Fact]
    public async Task SetStateAsync_and_GetStateAsync_returns_value_after_save() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", 42, ct);
        await sut.SaveStateAsync(ct);

        int result = await sut.GetStateAsync<int>("key1", ct);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task GetStateAsync_returns_pending_value_before_save() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", "hello", ct);

        string result = await sut.GetStateAsync<string>("key1", ct);

        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task GetStateAsync_throws_for_missing_key() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();

        _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.GetStateAsync<int>("missing", ct));
    }

    [Fact]
    public async Task RemoveStateAsync_makes_key_unavailable() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", 1, ct);
        await sut.SaveStateAsync(ct);

        await sut.RemoveStateAsync("key1", ct);
        await sut.SaveStateAsync(ct);

        Assert.False(await sut.ContainsStateAsync("key1", ct));
    }

    [Fact]
    public async Task RemoveStateAsync_throws_for_missing_key() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();

        _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.RemoveStateAsync("missing", ct));
    }

    [Fact]
    public async Task ContainsStateAsync_returns_true_for_existing_key() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", "value", ct);

        Assert.True(await sut.ContainsStateAsync("key1", ct));
    }

    [Fact]
    public async Task ContainsStateAsync_returns_false_for_missing_key() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();

        Assert.False(await sut.ContainsStateAsync("missing", ct));
    }

    [Fact]
    public async Task SaveStateAsync_commits_pending_changes_atomically() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("a", 1, ct);
        await sut.SetStateAsync("b", 2, ct);

        Assert.Empty(sut.CommittedState);

        await sut.SaveStateAsync(ct);

        Assert.Equal(2, sut.CommittedState.Count);
        Assert.Equal(1, sut.CommittedState["a"]);
        Assert.Equal(2, sut.CommittedState["b"]);
    }

    [Fact]
    public async Task TryGetStateAsync_returns_false_for_missing_key() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();

        ConditionalValue<int> result = await sut.TryGetStateAsync<int>("missing", ct);

        Assert.False(result.HasValue);
    }

    [Fact]
    public async Task TryGetStateAsync_returns_true_with_value_for_existing_key() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", 99, ct);

        ConditionalValue<int> result = await sut.TryGetStateAsync<int>("key1", ct);

        Assert.True(result.HasValue);
        Assert.Equal(99, result.Value);
    }

    [Fact]
    public async Task AddStateAsync_throws_if_key_already_exists() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", 1, ct);
        await sut.SaveStateAsync(ct);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.AddStateAsync("key1", 2, ct));
    }

    [Fact]
    public async Task AddStateAsync_succeeds_for_new_key() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();
        await sut.AddStateAsync("key1", 42, ct);

        int result = await sut.GetStateAsync<int>("key1", ct);
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ClearCacheAsync_discards_pending_changes() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", 1, ct);
        await sut.ClearCacheAsync(ct);

        Assert.False(await sut.ContainsStateAsync("key1", ct));
    }

    [Fact]
    public async Task TryAddStateAsync_returns_false_if_key_exists() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", 1, ct);
        await sut.SaveStateAsync(ct);

        bool added = await sut.TryAddStateAsync("key1", 2, ct);

        Assert.False(added);
    }

    [Fact]
    public async Task TryRemoveStateAsync_returns_false_for_missing_key() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();

        bool removed = await sut.TryRemoveStateAsync("missing", ct);

        Assert.False(removed);
    }

    [Fact]
    public async Task TryRemoveStateAsync_returns_true_for_existing_key() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", 1, ct);
        await sut.SaveStateAsync(ct);

        bool removed = await sut.TryRemoveStateAsync("key1", ct);

        Assert.True(removed);
    }

    [Fact]
    public async Task SaveStateAsync_applies_removals_to_committed_state() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", 1, ct);
        await sut.SaveStateAsync(ct);

        await sut.RemoveStateAsync("key1", ct);
        await sut.SaveStateAsync(ct);

        Assert.False(sut.CommittedState.ContainsKey("key1"));
    }

    [Fact]
    public async Task GetOrAddStateAsync_returns_existing_value() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", 10, ct);
        await sut.SaveStateAsync(ct);

        int result = await sut.GetOrAddStateAsync("key1", 99, ct);

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task GetOrAddStateAsync_adds_value_if_missing() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();

        int result = await sut.GetOrAddStateAsync("key1", 99, ct);

        Assert.Equal(99, result);
    }

    [Fact]
    public async Task AddOrUpdateStateAsync_adds_value_if_missing() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();

        int result = await sut.AddOrUpdateStateAsync("key1", 10, (_, existing) => existing + 1, ct);

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task AddOrUpdateStateAsync_updates_value_if_exists() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", 10, ct);
        await sut.SaveStateAsync(ct);

        int result = await sut.AddOrUpdateStateAsync("key1", 0, (_, existing) => existing + 1, ct);

        Assert.Equal(11, result);
    }
}

namespace Hexalith.EventStore.Testing.Tests.Fakes;

using Hexalith.EventStore.Testing.Fakes;

public class InMemoryStateManagerTests
{
    [Fact]
    public async Task SetStateAsync_and_GetStateAsync_returns_value_after_save()
    {
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", 42);
        await sut.SaveStateAsync();

        int result = await sut.GetStateAsync<int>("key1");

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task GetStateAsync_returns_pending_value_before_save()
    {
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", "hello");

        string result = await sut.GetStateAsync<string>("key1");

        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task GetStateAsync_throws_for_missing_key()
    {
        var sut = new InMemoryStateManager();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.GetStateAsync<int>("missing"));
    }

    [Fact]
    public async Task RemoveStateAsync_makes_key_unavailable()
    {
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", 1);
        await sut.SaveStateAsync();

        await sut.RemoveStateAsync("key1");
        await sut.SaveStateAsync();

        Assert.False(await sut.ContainsStateAsync("key1"));
    }

    [Fact]
    public async Task RemoveStateAsync_throws_for_missing_key()
    {
        var sut = new InMemoryStateManager();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.RemoveStateAsync("missing"));
    }

    [Fact]
    public async Task ContainsStateAsync_returns_true_for_existing_key()
    {
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", "value");

        Assert.True(await sut.ContainsStateAsync("key1"));
    }

    [Fact]
    public async Task ContainsStateAsync_returns_false_for_missing_key()
    {
        var sut = new InMemoryStateManager();

        Assert.False(await sut.ContainsStateAsync("missing"));
    }

    [Fact]
    public async Task SaveStateAsync_commits_pending_changes_atomically()
    {
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("a", 1);
        await sut.SetStateAsync("b", 2);

        Assert.Empty(sut.CommittedState);

        await sut.SaveStateAsync();

        Assert.Equal(2, sut.CommittedState.Count);
        Assert.Equal(1, sut.CommittedState["a"]);
        Assert.Equal(2, sut.CommittedState["b"]);
    }

    [Fact]
    public async Task TryGetStateAsync_returns_false_for_missing_key()
    {
        var sut = new InMemoryStateManager();

        var result = await sut.TryGetStateAsync<int>("missing");

        Assert.False(result.HasValue);
    }

    [Fact]
    public async Task TryGetStateAsync_returns_true_with_value_for_existing_key()
    {
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", 99);

        var result = await sut.TryGetStateAsync<int>("key1");

        Assert.True(result.HasValue);
        Assert.Equal(99, result.Value);
    }

    [Fact]
    public async Task AddStateAsync_throws_if_key_already_exists()
    {
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", 1);
        await sut.SaveStateAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.AddStateAsync("key1", 2));
    }

    [Fact]
    public async Task AddStateAsync_succeeds_for_new_key()
    {
        var sut = new InMemoryStateManager();
        await sut.AddStateAsync("key1", 42);

        int result = await sut.GetStateAsync<int>("key1");
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ClearCacheAsync_discards_pending_changes()
    {
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", 1);
        await sut.ClearCacheAsync();

        Assert.False(await sut.ContainsStateAsync("key1"));
    }

    [Fact]
    public async Task TryAddStateAsync_returns_false_if_key_exists()
    {
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", 1);
        await sut.SaveStateAsync();

        bool added = await sut.TryAddStateAsync("key1", 2);

        Assert.False(added);
    }

    [Fact]
    public async Task TryRemoveStateAsync_returns_false_for_missing_key()
    {
        var sut = new InMemoryStateManager();

        bool removed = await sut.TryRemoveStateAsync("missing");

        Assert.False(removed);
    }

    [Fact]
    public async Task TryRemoveStateAsync_returns_true_for_existing_key()
    {
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", 1);
        await sut.SaveStateAsync();

        bool removed = await sut.TryRemoveStateAsync("key1");

        Assert.True(removed);
    }

    [Fact]
    public async Task SaveStateAsync_applies_removals_to_committed_state()
    {
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", 1);
        await sut.SaveStateAsync();

        await sut.RemoveStateAsync("key1");
        await sut.SaveStateAsync();

        Assert.False(sut.CommittedState.ContainsKey("key1"));
    }

    [Fact]
    public async Task GetOrAddStateAsync_returns_existing_value()
    {
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", 10);
        await sut.SaveStateAsync();

        int result = await sut.GetOrAddStateAsync("key1", 99);

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task GetOrAddStateAsync_adds_value_if_missing()
    {
        var sut = new InMemoryStateManager();

        int result = await sut.GetOrAddStateAsync("key1", 99);

        Assert.Equal(99, result);
    }

    [Fact]
    public async Task AddOrUpdateStateAsync_adds_value_if_missing()
    {
        var sut = new InMemoryStateManager();

        int result = await sut.AddOrUpdateStateAsync("key1", 10, (_, existing) => existing + 1);

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task AddOrUpdateStateAsync_updates_value_if_exists()
    {
        var sut = new InMemoryStateManager();
        await sut.SetStateAsync("key1", 10);
        await sut.SaveStateAsync();

        int result = await sut.AddOrUpdateStateAsync("key1", 0, (_, existing) => existing + 1);

        Assert.Equal(11, result);
    }
}

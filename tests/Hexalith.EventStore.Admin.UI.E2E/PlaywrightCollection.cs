namespace Hexalith.EventStore.Admin.UI.E2E;

/// <summary>
/// Shares a single <see cref="PlaywrightFixture"/> (browser + test host) across all tests
/// in the collection, avoiding per-test browser startup cost.
/// </summary>
[CollectionDefinition("Playwright")]
public class PlaywrightCollection : ICollectionFixture<PlaywrightFixture>;

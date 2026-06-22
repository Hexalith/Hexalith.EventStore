using System.Globalization;
using System.Xml.Linq;

using Dapr.Client;

using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>
/// Tier 1 coverage of the Epic 11 shared, persisted Data Protection key ring: the DAPR-backed
/// <c>IXmlRepository</c> and the <c>AddEventStoreDataProtection</c> registration extension.
/// </summary>
public sealed class EventStoreDataProtectionTests {
    private const string StoreName = "statestore";
    private const string StateKey = "dataprotection-keys";

    /// <summary>
    /// A cursor sealed by one replica must be readable by another. Both replicas read the same key-ring
    /// from the shared store, so an element stored through one repository instance is visible through a
    /// second instance pointed at the same backing store (a simulated key-ring reload / second replica).
    /// </summary>
    [Fact]
    public void StoredElement_is_visible_through_a_second_repository_over_the_same_store() {
        InMemoryStateStore store = new();
        DaprClient client = store.CreateClient();
        IXmlRepository writer = new DaprXmlRepository(client, StoreName, StateKey);
        XElement element = new("key", new XAttribute("id", "abc-123"), "secret-material");

        writer.StoreElement(element, "abc-123");

        // Second instance == a different replica / a fresh host after restart reading the same store.
        IXmlRepository reader = new DaprXmlRepository(client, StoreName, StateKey);
        IReadOnlyCollection<XElement> reloaded = reader.GetAllElements();

        reloaded.Count.ShouldBe(1);
        reloaded.Single().Attribute("id")!.Value.ShouldBe("abc-123");
        reloaded.Single().Value.ShouldBe("secret-material");
    }

    /// <summary>
    /// Multiple keys accumulate in the ring rather than overwriting each other.
    /// </summary>
    [Fact]
    public void StoreElement_appends_rather_than_replaces() {
        InMemoryStateStore store = new();
        IXmlRepository repository = new DaprXmlRepository(store.CreateClient(), StoreName, StateKey);

        repository.StoreElement(new XElement("key", new XAttribute("id", "one")), "one");
        repository.StoreElement(new XElement("key", new XAttribute("id", "two")), "two");

        IReadOnlyCollection<XElement> elements = repository.GetAllElements();
        elements.Count.ShouldBe(2);
        elements.Select(e => e.Attribute("id")!.Value).ShouldBe(["one", "two"]);
    }

    /// <summary>
    /// Empty store yields an empty ring (no exception), which is the cold-start case before any key exists.
    /// </summary>
    [Fact]
    public void GetAllElements_on_empty_store_returns_empty() {
        InMemoryStateStore store = new();
        IXmlRepository repository = new DaprXmlRepository(store.CreateClient(), StoreName, StateKey);

        repository.GetAllElements().ShouldBeEmpty();
    }

    /// <summary>
    /// A concurrent writer that commits between this writer's read and its compare-and-swap must not be
    /// clobbered: the first save fails the ETag check and the retry loop re-reads and merges, so both the
    /// concurrent element and ours survive.
    /// </summary>
    [Fact]
    public void StoreElement_retries_on_concurrent_write_and_preserves_both_elements() {
        InMemoryStateStore store = new();
        IXmlRepository repository = new DaprXmlRepository(store.CreateClient(), StoreName, StateKey);

        // A different replica commits a key after our read but before our CAS, on the very next save.
        store.InjectConcurrentWriteBeforeNextSave = () =>
            store.ForceWrite([new XElement("key", new XAttribute("id", "from-other-replica")).ToString(SaveOptions.DisableFormatting)]);

        repository.StoreElement(new XElement("key", new XAttribute("id", "ours")), "ours");

        IReadOnlyCollection<XElement> elements = repository.GetAllElements();
        elements.Select(e => e.Attribute("id")!.Value).ShouldBe(["from-other-replica", "ours"]);
        store.SaveAttempts.ShouldBeGreaterThan(1); // proves the first CAS was rejected and retried
    }

    /// <summary>
    /// Production wiring (<c>PersistToStateStore = true</c>) installs the DAPR-backed repository into the
    /// Data Protection key-management options, so the ring is persisted to the shared store.
    /// </summary>
    [Fact]
    public void AddEventStoreDataProtection_with_persistence_registers_dapr_xml_repository() {
        ServiceCollection services = new();
        services.AddSingleton(new InMemoryStateStore().CreateClient());
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["EventStore:DataProtection:PersistToStateStore"] = "true",
                ["EventStore:DataProtection:StateStoreName"] = StoreName,
                ["EventStore:DataProtection:StateKey"] = StateKey,
            })
            .Build();

        services.AddEventStoreDataProtection(configuration, "Hexalith.Tenants");

        using ServiceProvider provider = services.BuildServiceProvider();
        KeyManagementOptions keyManagement = provider
            .GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        keyManagement.XmlRepository.ShouldBeOfType<DaprXmlRepository>();
    }

    /// <summary>
    /// Local/dev wiring (persistence disabled — the default) leaves the framework's ephemeral key ring in
    /// place: no custom <c>IXmlRepository</c> is installed, so the host starts without a state store.
    /// </summary>
    [Fact]
    public void AddEventStoreDataProtection_without_persistence_does_not_register_xml_repository() {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();

        services.AddEventStoreDataProtection(configuration, "Hexalith.Tenants");

        using ServiceProvider provider = services.BuildServiceProvider();
        KeyManagementOptions keyManagement = provider
            .GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        keyManagement.XmlRepository.ShouldBeNull();
    }

    /// <summary>
    /// In-memory emulation of a DAPR state store with monotonic ETags, exercising the get-and-etag /
    /// first-write-wins compare-and-swap surface the repository depends on. NSubstitute proxies the
    /// abstract <see cref="DaprClient"/> and routes the two state methods to this store.
    /// </summary>
    private sealed class InMemoryStateStore {
        private List<string>? _value;
        private long _version;

        public int SaveAttempts { get; private set; }

        public Func<List<string>>? InjectConcurrentWriteBeforeNextSave { get; set; }

        public List<string> ForceWrite(List<string> value) {
            _value = [.. value];
            _version++;
            return _value;
        }

        public DaprClient CreateClient() {
            DaprClient client = Substitute.For<DaprClient>();

            client.GetStateAndETagAsync<List<string>>(StoreName, StateKey, Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult((
                    _value is null ? null! : new List<string>(_value),
                    _version.ToString(CultureInfo.InvariantCulture))));

            client.TrySaveStateAsync(StoreName, StateKey, Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<StateOptions?>(), Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
                .Returns(call => {
                    SaveAttempts++;

                    if (InjectConcurrentWriteBeforeNextSave is { } inject) {
                        InjectConcurrentWriteBeforeNextSave = null;
                        _ = inject();
                    }

                    string etag = call.ArgAt<string>(3);
                    long expected = string.IsNullOrEmpty(etag) ? 0L : long.Parse(etag, CultureInfo.InvariantCulture);
                    if (expected != _version) {
                        return Task.FromResult(false);
                    }

                    _value = call.ArgAt<List<string>>(2);
                    _version++;
                    return Task.FromResult(true);
                });

            return client;
        }
    }
}

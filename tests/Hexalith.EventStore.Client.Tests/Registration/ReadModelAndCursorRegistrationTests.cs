using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Client.Registration;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Registration;

public class ReadModelAndCursorRegistrationTests {
    private sealed record SampleModel(int Count, string Text);

    [Fact]
    public void BatchCanonicalSerializer_MatchesDefaultDaprJsonOptions_SoBatchValuesRoundTrip() {
        // Batch values (and the fingerprint material) are serialized with fixed DAPR-default (Web) options
        // for cross-deployment stability, but DaprReadModelStore.GetAsync deserializes with
        // DaprClient.JsonSerializerOptions. If DAPR's default options ever diverge from the Web defaults,
        // batch-written values would silently fail to round-trip. Pin the assumption here so a DAPR upgrade
        // that changes the default breaks the build instead of production reads.
        JsonSerializerOptions daprDefault = new DaprClientBuilder().Build().JsonSerializerOptions;
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        daprDefault.PropertyNamingPolicy.ShouldBe(web.PropertyNamingPolicy);
        daprDefault.PropertyNameCaseInsensitive.ShouldBe(web.PropertyNameCaseInsensitive);
        daprDefault.NumberHandling.ShouldBe(web.NumberHandling);

        var value = new SampleModel(5, "hi");
        ReadOnlyMemory<byte> canonical = ReadModelBatchCanonicalJson.Serialize(value);
        JsonSerializer.Deserialize<SampleModel>(canonical.Span, daprDefault).ShouldBe(value);
    }

    [Fact]
    public void AddEventStoreReadModelStore_RegistersResolvableDaprReadModelStore() {
        var services = new ServiceCollection();
        _ = services.AddSingleton(Substitute.For<DaprClient>());

        IServiceCollection result = services.AddEventStoreReadModelStore();

        result.ShouldBeSameAs(services);
        using ServiceProvider provider = services.BuildServiceProvider();
        IReadModelStore store = provider.GetRequiredService<IReadModelStore>();
        _ = store.ShouldBeOfType<DaprReadModelStore>();
    }

    [Fact]
    public void AddEventStoreReadModelStore_BindsSameSingletonToEveryStoreInterface() {
        var services = new ServiceCollection();
        _ = services.AddSingleton(Substitute.For<DaprClient>());

        _ = services.AddEventStoreReadModelStore();

        using ServiceProvider provider = services.BuildServiceProvider();
        IReadModelStore single = provider.GetRequiredService<IReadModelStore>();
        IReadModelBatchStore batch = provider.GetRequiredService<IReadModelBatchStore>();
        IReadModelBatchStagingStore staging = provider.GetRequiredService<IReadModelBatchStagingStore>();

        _ = single.ShouldBeOfType<DaprReadModelStore>();
        ReferenceEquals(single, batch).ShouldBeTrue("one DaprReadModelStore singleton must back both interfaces");
        ReferenceEquals(single, staging).ShouldBeTrue("the same singleton must back phased staging");
    }

    [Fact]
    public void AddEventStoreReadModelStore_RepeatRegistration_IsIdempotent() {
        var services = new ServiceCollection();
        _ = services.AddSingleton(Substitute.For<DaprClient>());

        _ = services.AddEventStoreReadModelStore();
        _ = services.AddEventStoreReadModelStore();

        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetServices<IReadModelStore>().Count().ShouldBe(1);
        ReferenceEquals(
            provider.GetRequiredService<IReadModelStore>(),
            provider.GetRequiredService<IReadModelBatchStore>()).ShouldBeTrue();
        ReferenceEquals(
            provider.GetRequiredService<IReadModelStore>(),
            provider.GetRequiredService<IReadModelBatchStagingStore>()).ShouldBeTrue();
    }

    [Fact]
    public void AddEventStoreReadModelStore_CustomStoreOverride_IsRespected() {
        var services = new ServiceCollection();
        _ = services.AddSingleton(Substitute.For<DaprClient>());
        IReadModelStore custom = Substitute.For<IReadModelStore>();
        _ = services.AddSingleton(custom);

        _ = services.AddEventStoreReadModelStore();

        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<IReadModelStore>().ShouldBeSameAs(custom);
        // The custom store is not batch-capable, so the default DAPR batch store provides batching.
        _ = provider.GetRequiredService<IReadModelBatchStore>().ShouldBeOfType<DaprReadModelStore>();
        _ = provider.GetRequiredService<IReadModelBatchStagingStore>().ShouldBeOfType<DaprReadModelStore>();
    }

    [Fact]
    public void AddEventStoreReadModelStore_InvalidBatchOptions_ThrowsAtRegistration() {
        var services = new ServiceCollection();
        _ = services.AddSingleton(Substitute.For<DaprClient>());

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => services.AddEventStoreReadModelStore(o => o.MaxOperations = 0));
    }

    [Fact]
    public void AddEventStoreQueryCursorCodec_RegistersResolvableCodec() {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());

        IServiceCollection result = services.AddEventStoreQueryCursorCodec("Hexalith.EventStore.Tests.Cursor.v1");

        result.ShouldBeSameAs(services);
        using ServiceProvider provider = services.BuildServiceProvider();
        IQueryCursorCodec codec = provider.GetRequiredService<IQueryCursorCodec>();
        _ = codec.ShouldBeOfType<QueryCursorCodec>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddEventStoreQueryCursorCodec_RejectsInvalidPurpose(string? purpose) {
        var services = new ServiceCollection();

        _ = Should.Throw<ArgumentException>(() => services.AddEventStoreQueryCursorCodec(purpose!));
    }
}

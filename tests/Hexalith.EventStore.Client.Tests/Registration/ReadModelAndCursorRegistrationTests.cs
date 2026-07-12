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
    public void AddEventStoreReadModelStore_BindsSameSingletonToBothStoreInterfaces() {
        var services = new ServiceCollection();
        _ = services.AddSingleton(Substitute.For<DaprClient>());

        _ = services.AddEventStoreReadModelStore();

        using ServiceProvider provider = services.BuildServiceProvider();
        IReadModelStore single = provider.GetRequiredService<IReadModelStore>();
        IReadModelBatchStore batch = provider.GetRequiredService<IReadModelBatchStore>();

        _ = single.ShouldBeOfType<DaprReadModelStore>();
        ReferenceEquals(single, batch).ShouldBeTrue("one DaprReadModelStore singleton must back both interfaces");
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

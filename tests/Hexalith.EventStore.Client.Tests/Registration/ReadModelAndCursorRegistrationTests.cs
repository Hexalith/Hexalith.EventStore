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

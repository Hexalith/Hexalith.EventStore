using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;

/// <summary>Approved supplement section 6: the real Server default remains one concrete no-op without backend access.</summary>
public sealed class PayloadProtectionRegistrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OmittedAndDisabledRegistrationPreserveExactlyOneRealNoOp(bool explicitlyDisabled)
    {
        var services = new ServiceCollection();
        IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventStore:PayloadProtection:Mode"] = "Disabled",
            ["EventStore:PayloadProtection:Backend"] = "PlatformCustody",
            ["EventStore:PayloadProtection:PlatformCustody:BindingName"] = "unused",
        }).Build();
        services.AddEventStoreServer(configuration);
        bool backendResolved = false;
        services.AddSingleton<DaprClient>(_ => { backendResolved = true; throw new InvalidOperationException("Backend must remain untouched."); });
        if (explicitlyDisabled) { services.AddEventStorePayloadProtection(configuration.GetSection("EventStore:PayloadProtection")); }
        ServiceDescriptor[] descriptors = services.Where(d => d.ServiceType == typeof(IEventPayloadProtectionService)).ToArray();
        descriptors.Length.ShouldBe(1); descriptors[0].ImplementationType.ShouldBe(typeof(NoOpEventPayloadProtectionService));
        await using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<IEventPayloadProtectionService>().ShouldBeOfType<NoOpEventPayloadProtectionService>();
        if (explicitlyDisabled)
        {
            // Invoke only this guard; unrelated Server startup prerequisites remain owned by their existing lanes.
            var guard = new PayloadProtectionStartupValidator(provider, provider.GetRequiredService<PayloadProtectionOptions>(), services);
            await guard.StartAsync(default);
        }
        backendResolved.ShouldBeFalse();
    }
}

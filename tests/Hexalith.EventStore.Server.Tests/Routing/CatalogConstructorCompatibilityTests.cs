using System.Reflection;

using Dapr.Actors.Client;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Preserves the old binary constructor surface while selecting the new DI-aware constructors without ambiguity.</summary>
public sealed class CatalogConstructorCompatibilityTests
{
    /// <summary>Both packaged signatures remain callable and ActivatorUtilities chooses the explicitly preferred constructors.</summary>
    [Fact]
    public void LegacySignaturesRemainAndPreferredConstructorsActivate()
    {
        typeof(RouteCatalogAuthority).GetConstructor([typeof(IRouteCatalogStateStore), typeof(IRouteCatalogTrustValidator),
            typeof(IOptions<RouteCatalogHostOptions>), typeof(IRouteCatalogHostObserver), typeof(IRouteCatalogQuiescenceVerifier)])
            .ShouldNotBeNull();
        typeof(StreamsController).GetConstructor([typeof(IActorProxyFactory), typeof(ITenantValidator), typeof(IRbacValidator),
            typeof(ILogger<StreamsController>), typeof(IEventPayloadProtectionService), typeof(IConfiguration)]).ShouldNotBeNull();
        typeof(RouteCatalogAuthority).GetConstructors().Single(constructor => constructor.GetParameters().Length == 6)
            .GetCustomAttribute<ActivatorUtilitiesConstructorAttribute>().ShouldNotBeNull();
        typeof(StreamsController).GetConstructors().Single(constructor => constructor.GetParameters().Length == 8)
            .GetCustomAttribute<ActivatorUtilitiesConstructorAttribute>().ShouldNotBeNull();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<RouteCatalogHostOptions>();
        services.AddSingleton(Substitute.For<IRouteCatalogStateStore>());
        services.AddSingleton(Substitute.For<IRouteCatalogTrustValidator>());
        services.AddSingleton(Substitute.For<IActorProxyFactory>());
        services.AddSingleton(Substitute.For<ITenantValidator>());
        services.AddSingleton(Substitute.For<IRbacValidator>());
        using var provider = services.BuildServiceProvider();

        ActivatorUtilities.CreateInstance<RouteCatalogAuthority>(provider).ShouldNotBeNull();
        ActivatorUtilities.CreateInstance<StreamsController>(provider).ShouldNotBeNull();
    }
}

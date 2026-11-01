using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Verifies supported catalog-fenced registration without convention processor replacement.</summary>
public sealed class CustomDomainSecurityCompositionTests
{
    /// <summary>Retains a custom processor while registering the actual catalog guard once through the public seam.</summary>
    [Fact]
    public void PublicSecurityCompositionRetainsCustomProcessorAndRegistersActualCatalogGuardIdempotently()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        IDomainProcessor processor = Substitute.For<IDomainProcessor>();
        builder.Services.AddKeyedSingleton("custom", processor);
        builder.Services.AddEventStoreDomainServiceSecurity(builder.Configuration);
        builder.Services.AddEventStoreDomainServiceSecurity(builder.Configuration);
        using WebApplication app = builder.Build();
        using IServiceScope scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredKeyedService<IDomainProcessor>("custom").ShouldBeSameAs(processor);
        scope.ServiceProvider.GetRequiredService<IDomainRouteCatalogGuard>().ShouldBeOfType<DomainRouteCatalogGuard>();
        scope.ServiceProvider.GetRequiredService<DomainRouteCatalogFilter>().ShouldNotBeNull();
        app.Services.GetRequiredService<IRouteCatalogAuthority>().ShouldBeOfType<RouteCatalogAuthority>();
        builder.Services.Count(item => item.ServiceType == typeof(IDomainRouteCatalogGuard)).ShouldBe(1);
        builder.Services.Count(item => item.ServiceType == typeof(DomainRouteCatalogFilter)).ShouldBe(1);
    }
}

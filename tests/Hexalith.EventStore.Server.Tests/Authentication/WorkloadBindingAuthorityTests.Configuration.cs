using System.Text;
using System.Text.Json;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authentication;

public sealed partial class WorkloadBindingAuthorityTests
{
    /// <summary>Configuration preserves every protocol-permitted family without making client-selected names authoritative.</summary>
    [Theory]
    [InlineData("eventstore:bound-retention")]
    [InlineData("eventstore:bound-storage:region")]
    public void ConfiguredCustomExactBindingFamilyRetainsIssuanceAndDenialPolicy(string family)
    {
        _options.Grants[0].Bindings[family] = ["approved-value"];
        using var json = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Authentication = new { WorkloadAuthority = _options } })));
        IConfiguration configuration = new ConfigurationBuilder().AddJsonStream(json).Build();
        IHostEnvironment environment = NonconformingDevelopmentEnvironment();
        using ServiceProvider services = new ServiceCollection().AddSingleton(environment)
            .AddEventStoreWorkloadBindingAuthority(configuration).BuildServiceProvider();
        var authority = new WorkloadBindingAuthority(services.GetRequiredService<IOptionsMonitor<WorkloadAuthorityOptions>>(), environment, TimeProvider.System);
        WorkloadAuthorizationDetails details = Details();
        details.Bindings[family] = "approved-value";
        string token = authority.Issue("synthetic-client", _secret, Scope(details), details).ShouldNotBeNull();
        new JsonWebToken(token).GetClaim(family).Value.ShouldBe("approved-value");
        details.Bindings[family] = "not-allowed";
        authority.Issue("synthetic-client", _secret, Scope(details), details).ShouldBeNull();
        details.Bindings.Remove(family);
        details.Bindings[family + "-client-selected"] = "approved-value";
        authority.Issue("synthetic-client", _secret, Scope(details), details).ShouldBeNull();
        details.Bindings.Remove(family + "-client-selected");
        details.Bindings[family] = "approved-value";
        details.Bindings.Remove("eventstore:bound-tenant");
        authority.Issue("synthetic-client", _secret, Scope(details), details).ShouldBeNull();
    }
}

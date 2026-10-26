namespace Hexalith.EventStore.Server.Tests.Authentication;

using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

/// <summary>Exercises the real configuration provider, including colon-delimited security claim names.</summary>
public sealed class WorkloadAuthorityConfigurationTests
{
    /// <summary>Preserves exact tenant and domain binding allowlists supplied by environment/JSON configuration.</summary>
    [Fact]
    public void ExactBindingClaimNamesSurviveConfigurationBinding()
    {
        IConfigurationRoot configuration = Configuration();
        using ServiceProvider services = new ServiceCollection().AddEventStoreWorkloadBindingAuthority(configuration).BuildServiceProvider();
        WorkloadAuthorityGrant grant = services.GetRequiredService<IOptionsMonitor<WorkloadAuthorityOptions>>().CurrentValue.Grants.Single();
        grant.Bindings.Keys.Order().ShouldBe(new[] { "eventstore:bound-domain", "eventstore:bound-tenant" });
        grant.Bindings["eventstore:bound-tenant"].ShouldBe(["tenant-a", "tenant-b"]);
        grant.Bindings["eventstore:bound-domain"].ShouldBe(["projects"]);
    }

    /// <summary>Rejects configured names outside the existing platform prefix.</summary>
    [Fact]
    public void UnknownBindingNamesFailClosed()
    {
        IConfigurationRoot configuration = Configuration();
        configuration["Authentication:WorkloadAuthority:Grants:0:Bindings:client:chosen-authority:0"] = "anything";
        using ServiceProvider services = new ServiceCollection().AddEventStoreWorkloadBindingAuthority(configuration).BuildServiceProvider();
        Should.Throw<OptionsValidationException>(() => services.GetRequiredService<IOptionsMonitor<WorkloadAuthorityOptions>>().CurrentValue);
    }

    /// <summary>Uses reloaded current allowlists rather than the original configuration snapshot.</summary>
    [Fact]
    public void ReloadRebindsCurrentExactAllowlist()
    {
        IConfigurationRoot configuration = Configuration();
        using ServiceProvider services = new ServiceCollection().AddEventStoreWorkloadBindingAuthority(configuration).BuildServiceProvider();
        IOptionsMonitor<WorkloadAuthorityOptions> monitor = services.GetRequiredService<IOptionsMonitor<WorkloadAuthorityOptions>>();
        monitor.CurrentValue.Grants.Single().Bindings["eventstore:bound-domain"].ShouldBe(["projects"]);
        configuration["Authentication:WorkloadAuthority:Grants:0:Bindings:eventstore:bound-domain:0"] = "folders";
        configuration.Reload();
        monitor.CurrentValue.Grants.Single().Bindings["eventstore:bound-domain"].ShouldBe(["folders"]);
    }

    /// <summary>Compacted configuration arrays must never move another grant's allowlists onto a client.</summary>
    [Fact]
    public void SparseGrantIndexesPreserveEachGrantIdentityAndBindingAllowlist()
    {
        IConfigurationRoot configuration = SparseConfiguration();
        using ServiceProvider services = new ServiceCollection().AddEventStoreWorkloadBindingAuthority(configuration).BuildServiceProvider();
        WorkloadAuthorityGrant[] grants = services.GetRequiredService<IOptionsMonitor<WorkloadAuthorityOptions>>().CurrentValue.Grants;
        grants.Select(grant => grant.GrantId).ShouldBe(["grant-0", "grant-2", "grant-3"]);
        WorkloadAuthorityGrant third = grants.Single(grant => grant.GrantId == "grant-3");
        third.ClientId.ShouldBe("client-3");
        third.Bindings["eventstore:bound-tenant"].ShouldBe(["tenant-3"]);
        foreach (int index in new[] { 0, 2, 3 })
        {
            WorkloadAuthorityGrant grant = grants.Single(grant => grant.GrantId == "grant-" + index);
            grant.Bindings["eventstore:bound-tenant"].ShouldBe(["tenant-" + index]);
            grant.Bindings["eventstore:bound-domain"].ShouldBe(["domain-" + index]);
        }
    }

    /// <summary>Reloading a compacted array changes only the grant identified by its configured identity.</summary>
    [Fact]
    public void SparseGrantReloadPreservesOtherGrantAllowlists()
    {
        IConfigurationRoot configuration = SparseConfiguration();
        using ServiceProvider services = new ServiceCollection().AddEventStoreWorkloadBindingAuthority(configuration).BuildServiceProvider();
        IOptionsMonitor<WorkloadAuthorityOptions> monitor = services.GetRequiredService<IOptionsMonitor<WorkloadAuthorityOptions>>();
        monitor.CurrentValue.Grants.Single(grant => grant.GrantId == "grant-2").Bindings["eventstore:bound-tenant"].ShouldBe(["tenant-2"]);
        configuration["Authentication:WorkloadAuthority:Grants:2:Bindings:eventstore:bound-tenant:0"] = "reloaded-tenant";
        configuration.Reload();
        monitor.CurrentValue.Grants.Single(grant => grant.GrantId == "grant-2").Bindings["eventstore:bound-tenant"].ShouldBe(["reloaded-tenant"]);
        monitor.CurrentValue.Grants.Single(grant => grant.GrantId == "grant-3").Bindings["eventstore:bound-tenant"].ShouldBe(["tenant-3"]);
    }

    /// <summary>Missing or ambiguous grant identities cannot transfer configured authority.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrDuplicateConfiguredGrantIdentityFailsClosed(bool duplicate)
    {
        IConfigurationRoot configuration = Configuration();
        configuration[duplicate ? "Authentication:WorkloadAuthority:Grants:2:GrantId" : "Authentication:WorkloadAuthority:Grants:0:GrantId"]
            = duplicate ? "exact-config" : null;
        using ServiceProvider services = new ServiceCollection().AddEventStoreWorkloadBindingAuthority(configuration).BuildServiceProvider();
        Should.Throw<OptionsValidationException>(() => services.GetRequiredService<IOptionsMonitor<WorkloadAuthorityOptions>>().CurrentValue);
    }

    /// <summary>Programmatic ambiguity also fails instead of choosing one target by position.</summary>
    [Fact]
    public void DuplicateBoundGrantIdentityFailsClosed()
    {
        IConfigurationRoot configuration = Configuration();
        using ServiceProvider services = new ServiceCollection().AddEventStoreWorkloadBindingAuthority(configuration)
            .Configure<WorkloadAuthorityOptions>(options => options.Grants = [.. options.Grants, options.Grants[0]]).BuildServiceProvider();
        Should.Throw<OptionsValidationException>(() => services.GetRequiredService<IOptionsMonitor<WorkloadAuthorityOptions>>().CurrentValue);
    }

    /// <summary>Malformed configuration never drops a mandatory family or accepts an out-of-profile value.</summary>
    [Theory]
    [InlineData("scalar")]
    [InlineData("bad-index")]
    [InlineData("empty-family")]
    [InlineData("too-long-name")]
    [InlineData("blank-value")]
    [InlineData("too-long-value")]
    [InlineData("many-families")]
    public void MalformedConfiguredBindingListsFailClosed(string fault)
    {
        IConfigurationRoot configuration = Configuration();
        string prefix = "Authentication:WorkloadAuthority:Grants:0:Bindings:";
        string family = "eventstore:bound-extra";
        if (fault == "many-families")
            for (int index = 0; index < 15; index++) configuration[prefix + family + index + ":0"] = "value";
        else configuration[prefix + (fault == "too-long-name" ? family + new string('x', 128) : family)
            + (fault is "scalar" or "empty-family" ? "" : fault == "bad-index" ? ":invalid" : ":0")]
            = fault == "empty-family" ? null : fault == "blank-value" ? " " : fault == "too-long-value" ? new string('x', 257) : "value";
        using ServiceProvider services = new ServiceCollection().AddEventStoreWorkloadBindingAuthority(configuration).BuildServiceProvider();
        Should.Throw<OptionsValidationException>(() => services.GetRequiredService<IOptionsMonitor<WorkloadAuthorityOptions>>().CurrentValue);
    }

    private static IConfigurationRoot SparseConfiguration()
    {
        Dictionary<string, string?> inputs = [];
        foreach (int index in new[] { 0, 2, 3 })
        {
            string prefix = $"Authentication:WorkloadAuthority:Grants:{index}:";
            inputs[prefix + "GrantId"] = "grant-" + index;
            inputs[prefix + "ClientId"] = "client-" + index;
            inputs[prefix + "Bindings:eventstore:bound-tenant:0"] = "tenant-" + index;
            inputs[prefix + "Bindings:eventstore:bound-domain:0"] = "domain-" + index;
        }
        return new ConfigurationBuilder().AddInMemoryCollection(inputs).Build();
    }

    private static IConfigurationRoot Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Authentication:WorkloadAuthority:Grants:0:GrantId"] = "exact-config",
        ["Authentication:WorkloadAuthority:Grants:0:Bindings:eventstore:bound-tenant:0"] = "tenant-a",
        ["Authentication:WorkloadAuthority:Grants:0:Bindings:eventstore:bound-tenant:1"] = "tenant-b",
        ["Authentication:WorkloadAuthority:Grants:0:Bindings:eventstore:bound-domain:0"] = "projects",
    }).Build();
}

using System.Diagnostics;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Proves computational reuse preserves fresh state, ETag, local-host and trust admission on every attempt.</summary>
public sealed class ScopedRouteCatalogAcquisitionTests
{
#if DEBUG
    private static bool ReuseExpected => true;
#else
    private static bool ReuseExpected => false;
#endif
    /// <summary>Only the complete actual private selection enables a scope, even when acquisition otherwise refuses.</summary>
    [Theory]
    [InlineData("enabled")]
    [InlineData("flag")]
    [InlineData("technical")]
    [InlineData("fixture")]
    [InlineData("profile")]
    [InlineData("environment")]
    [InlineData("missing-environment")]
    public async Task RequiresEveryEffectivePrivateSelector(string changed)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        RouteCatalogHostOptions local = PrivateOptions(fixture);
        if (changed == "flag") { local.ScopedDecodeReuse = false; local.SerializeLocalLeaseRelease = false; local.RetainValidatedCatalogBytes = false; }
        if (changed == "technical") local.RequireTechnicalHttpBinding = false;
        if (changed == "fixture") local.DevelopmentFixture = false;
        if (changed == "profile") local.Profile = "Production";
        IHostEnvironment environment = WebApplication.CreateBuilder(new WebApplicationOptions
        { EnvironmentName = changed == "environment" ? Environments.Production : Environments.Development }).Environment;
        RouteCatalogDecodeScope? observed = null;
        bool? schedulingObserved = null;
        bool? hostMemoObserved = null;
        RouteCatalogAuthority? authority = null;
        var store = Substitute.For<IRouteCatalogStateStore>();
        store.Coordinates.Returns(fixture.Store.Coordinates);
        store.ReadAsync(Arg.Any<CancellationToken>()).Returns(call =>
        {
            observed ??= RouteCatalogDecodeScope.Current;
            schedulingObserved ??= RouteCatalogLocalAdmissionTurn.IsHeldBy(authority!);
            hostMemoObserved ??= RouteCatalogDecodeScope.Current?.HasHostMemo == true;
            return fixture.Store.ReadAsync(call.Arg<CancellationToken>());
        });
        store.CompareExchangeAsync(Arg.Any<RouteCatalogActivationState>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => fixture.Store.CompareExchangeAsync(call.Arg<RouteCatalogActivationState>(), call.Arg<string>(), call.Arg<CancellationToken>()));
        var options = Options.Create(local);
        authority = new RouteCatalogAuthority(store, new DevelopmentRouteCatalogTrustValidator(options, environment), options,
            new ObservedRouteCatalogHost(_ => Task.FromResult(fixture.Observations[0])),
            environment: changed == "missing-environment" ? null : environment);
        try { await using var lease = await authority.AcquireAsync(TestContext.Current.CancellationToken); }
        catch (RouteCatalogUnavailableException) when (changed is "fixture" or "profile" or "environment") { }
        if (changed == "enabled" && ReuseExpected) observed.ShouldNotBeNull().Count.ShouldBe(0);
        else observed.ShouldBeNull();
        schedulingObserved.ShouldBe(changed == "enabled" && ReuseExpected);
        hostMemoObserved.ShouldBe(changed == "enabled" && ReuseExpected);
        RouteCatalogDecodeScope.Current.ShouldBeNull();
    }

    /// <summary>The exact SDK bootstrap options and real host environment select reuse without raw profile keys.</summary>
    [Fact]
    public async Task ActualPrivateBootstrapCompositionEnablesScope()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        string directory = Directory.CreateTempSubdirectory("scoped-catalog-composition-").FullName;
        try
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
            builder.Configuration["EventStore:RouteCatalog:ScopedDecodeReuse"] = "true";
            builder.Configuration["EventStore:RouteCatalog:SerializeLocalLeaseRelease"] = "true";
            builder.Configuration["EventStore:RouteCatalog:RetainValidatedCatalogBytes"] = "true";
            var section = builder.Configuration.GetSection("EventStore:RouteCatalog:DevelopmentBootstrap");
            section["Directory"] = directory;
            section["HostId"] = fixture.Hosts[0].HostId;
            section["CoordinatorHostId"] = fixture.Hosts[1].HostId;
            section["AttemptId"] = "scoped-catalog-composition";
            section["BindingVersion"] = "2";
            for (int index = 0; index < fixture.Hosts.Length; index++) section["RequiredHostIds:" + index] = fixture.Hosts[index].HostId;
            RouteCatalogDecodeScope? observed = null;
            bool schedulingObserved = false;
            bool hostMemoObserved = false;
            builder.Services.AddSingleton<IRouteCatalogStateStore>(fixture.Store);
            builder.Services.AddEventStoreDevelopmentRouteCatalogBootstrap(builder.Configuration, (services, _) =>
            {
                observed = RouteCatalogDecodeScope.Current;
                schedulingObserved = RouteCatalogLocalAdmissionTurn.IsHeldBy((RouteCatalogAuthority)services.GetRequiredService<IRouteCatalogAuthority>());
                hostMemoObserved = RouteCatalogDecodeScope.Current?.HasHostMemo == true;
                return Task.FromResult(fixture.Observations[0]);
            });
            using var provider = builder.Services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<RouteCatalogHostOptions>>().Value;
            options.Identity = ObservedRouteCatalogHost.Identity(fixture.Observations[0]);
            options.ScopedDecodeReuse.ShouldBeTrue();
            options.SerializeLocalLeaseRelease.ShouldBeTrue();
            options.RetainValidatedCatalogBytes.ShouldBeTrue();
            builder.Configuration["EventStore:RouteCatalog:Profile"].ShouldBeNull();
            builder.Configuration["EventStore:RouteCatalog:DevelopmentFixture"].ShouldBeNull();
            builder.Configuration["EventStore:RouteCatalog:RequireTechnicalHttpBinding"].ShouldBeNull();

            using var activity = new Activity("private-acquisition-control").Start();
            await using var lease = await provider.GetRequiredService<IRouteCatalogAuthority>().AcquireAsync(TestContext.Current.CancellationToken);

            if (ReuseExpected) observed.ShouldNotBeNull().Count.ShouldBe(0);
            else observed.ShouldBeNull();
            schedulingObserved.ShouldBe(ReuseExpected);
            hostMemoObserved.ShouldBe(ReuseExpected);
            if (ReuseExpected)
            {
                var reuse = activity.Events.Single(item => item.Name == "private.route_catalog.decode_scope").Tags.ToDictionary();
                reuse["enabled"].ShouldBe(true);
                reuse["completed"].ShouldBe(true);
                ((int)reuse["capture_count"]!).ShouldBeGreaterThan(1);
                ((int)reuse["cache_hits"]!).ShouldBeGreaterThan(0);
                activity.Events.Where(item => item.Name == "private.route_catalog.validate_phase")
                    .Select(item => item.Tags.Single(tag => tag.Key == "phase").Value).ShouldBe(["binding", "profile", "observer", "trust"]);
            }
            else activity.Events.ShouldBeEmpty();
            lease.Snapshot.RootDigest.ShouldBe(fixture.Candidate().RootDigest);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>Every refused CAS re-reads real serialized state and reruns actual host and trust checks, even for identical catalog bytes.</summary>
    [Theory]
    [InlineData("epoch")]
    [InlineData("new-catalog")]
    [InlineData("different-bytes-same-root")]
    [InlineData("host")]
    [InlineData("trust-proof")]
    public async Task ContentionCannotReuseStateHostOrTrustAuthority(string changed)
    {
        var fixture = new RouteCatalogTestFixture();
        RouteCatalogActivation first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        RouteCatalogActivation successor = fixture.Candidate(2, first.RootDigest);
        var store = new ControlledCatalogContentionStore(fixture.Store);
        var options = Options.Create(PrivateOptions(fixture));
        IHostEnvironment environment = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development }).Environment;
        var actualTrust = new DevelopmentRouteCatalogTrustValidator(options, environment);
        int trustCalls = 0, observations = 0;
        bool hostChanged = false;
        RouteCatalogDecodeScope? observed = null;
        var trust = Substitute.For<IRouteCatalogTrustValidator>();
        trust.ValidateAsync(Arg.Any<RouteCatalogActivation>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            trustCalls++;
            return actualTrust.ValidateAsync(call.Arg<RouteCatalogActivation>(), call.Arg<string>(), call.Arg<CancellationToken>());
        });
        var observer = new ObservedRouteCatalogHost(_ =>
        {
            observations++;
            observed = RouteCatalogDecodeScope.Current;
            if (ReuseExpected) observed.ShouldNotBeNull().Count.ShouldBeGreaterThan(0);
            else observed.ShouldBeNull();
            return Task.FromResult(hostChanged ? fixture.Observations[0] with { AuthenticationProfile = "changed-local-authentication"u8.ToArray() }
                : fixture.Observations[0]);
        });
        store.Conflict = async (_, token) =>
        {
            hostChanged = changed == "host";
            (RouteCatalogActivationState state, string etag) = await fixture.Store.ReadAsync(token);
            RouteCatalogActivationState next = changed switch
            {
                "epoch" => state with { Epoch = state.Epoch + 1 },
                "new-catalog" => state with { Epoch = state.Epoch + 1, Committed = successor },
                "different-bytes-same-root" => state with { Committed = state.Committed! with { CanonicalCatalog = successor.CanonicalCatalog } },
                "trust-proof" => state with { Committed = state.Committed! with { TrustProof = "changed-trust-proof" } },
                _ => state,
            };
            (await fixture.Store.CompareExchangeAsync(next, etag, token)).ShouldBeTrue();
            store.AllowExchange = true;
        };
        var authority = new RouteCatalogAuthority(store, trust, options, observer, environment: environment);

        if (changed == "different-bytes-same-root")
            (await Should.ThrowAsync<RouteCatalogValidationException>(() => authority.AcquireAsync(TestContext.Current.CancellationToken)))
                .Diagnostics.Single().Code.ShouldBe("root-digest-mismatch");
        else if (changed is "host" or "trust-proof")
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.AcquireAsync(TestContext.Current.CancellationToken));
        else
        {
            await using var lease = await authority.AcquireAsync(TestContext.Current.CancellationToken);
            lease.Snapshot.RootDigest.ShouldBe(changed == "new-catalog" ? successor.RootDigest : first.RootDigest);
            lease.Snapshot.Epoch.ShouldBe(2);
            store.Attempts.ShouldBe(2);
        }

        store.Reads.ShouldBe(changed is "epoch" or "new-catalog" ? 3 : 2); // Successful disposal performs its own fresh release read.
        if (changed != "different-bytes-same-root") observations.ShouldBe(2);
        if (changed is not ("host" or "different-bytes-same-root")) trustCalls.ShouldBe(2);
        if (ReuseExpected)
        {
            observed.ShouldNotBeNull().Count.ShouldBe(0);
            observed.RetainedBytes.ShouldBe(0);
        }
        else observed.ShouldBeNull();
        RouteCatalogDecodeScope.Current.ShouldBeNull();
        (await fixture.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
    }

    private static RouteCatalogHostOptions PrivateOptions(RouteCatalogTestFixture fixture) => new()
    {
        Identity = fixture.Hosts[0], Profile = "Development", DevelopmentFixture = true,
        RequireTechnicalHttpBinding = true, ScopedDecodeReuse = true, SerializeLocalLeaseRelease = true, RetainValidatedCatalogBytes = true,
    };
}

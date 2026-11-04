using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Separates the existing local queue, durable work and fresh validation without granting authority.</summary>
public sealed class CatalogAcquisitionDiagnosticsTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SupportedRegistrationKeepsOptionalLoggerResolutionAdvisory(bool loggerResolutionThrows, bool development)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var log = new DispatchDiagnosticLog();
        using var logging = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(log));
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<RouteCatalogAuthority>>(_ => loggerResolutionThrows
            ? throw new InvalidOperationException("private-optional-logger-resolution") : logging.CreateLogger<RouteCatalogAuthority>());
        services.AddSingleton<IRouteCatalogStateStore>(fixture.Store);
        services.AddSingleton<IRouteCatalogTrustValidator>(Trust());
        Hexalith.EventStore.Contracts.Routing.RouteCatalogDecodeScope? observedScope = null;
        services.AddSingleton<IRouteCatalogHostObserver>(new ObservedRouteCatalogHost(_ =>
        {
            observedScope = Hexalith.EventStore.Contracts.Routing.RouteCatalogDecodeScope.Current;
            return Task.FromResult(fixture.Observations[0]);
        }));
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(development ? Environments.Development : Environments.Production);
        services.AddSingleton(environment);
        services.AddSingleton(Options.Create(new RouteCatalogHostOptions
        {
            Identity = fixture.Hosts[0], Profile = "Development", DevelopmentFixture = true,
            RequireTechnicalHttpBinding = true, ScopedDecodeReuse = true,
        }));
        services.AddEventStoreRouteCatalog(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        await using (await provider.GetRequiredService<IRouteCatalogAuthority>().AcquireAsync(TestContext.Current.CancellationToken))
        {
            if (loggerResolutionThrows) log.Records.ShouldBeEmpty();
            else
            {
                log.Records.Where(record => Equals(record["Stage"], "CatalogStateRead")).Select(record => record["Status"])
                    .ShouldBe(new object[] { "Started", "Completed", "Started", "Completed" });
                foreach (string stage in new[] { "CatalogAdmissionWait", "CatalogActivationDigest", "CatalogHostCapture", "CatalogTrust", "CatalogCompareExchange", "CatalogSnapshot" })
                    CommandDispatchDiagnosticsTests.AssertClosed(log, stage, "Completed");
            }
        }
#if DEBUG
        if (development) observedScope.ShouldNotBeNull().Count.ShouldBe(0);
        else observedScope.ShouldBeNull();
#else
        observedScope.ShouldBeNull();
#endif
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        CommandDispatchDiagnosticsTests.AssertPrivate(log);
    }

    [Fact]
    public async Task QueueCancellationDoesNotReadOrRetainAnAdditionalFence()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var log = new DispatchDiagnosticLog();
        using var logging = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(log));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueCapture = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = Substitute.For<IRouteCatalogHostObserver>();
        observer.ObserveAsync(Arg.Any<Hexalith.EventStore.Contracts.Routing.RouteCatalogActivation>(), Arg.Any<RouteCatalogStateCoordinates>(), Arg.Any<CancellationToken>())
            .Returns(async _ => { entered.SetResult(); await continueCapture.Task; return fixture.Hosts[0]; });
        var authority = Authority(fixture, observer, Trust(), logging);
        Task<RouteCatalogDispatchLease> first = authority.AcquireAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var canceled = new CancellationTokenSource();
        Task<RouteCatalogDispatchLease> queued = authority.AcquireAsync(canceled.Token);
        canceled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => queued);
        log.Records.Count(record => Equals(record["Stage"], "CatalogStateRead") && Equals(record["Status"], "Started")).ShouldBe(1);
        log.Records.ShouldContain(record => Equals(record["Stage"], "CatalogAdmissionWait") && Equals(record["Status"], "Failed") && Equals(record["OperationCanceled"], true));
        continueCapture.SetResult();
        await using (await first) { }
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        CommandDispatchDiagnosticsTests.AssertPrivate(log);
    }

    [Theory]
    [InlineData("capture")]
    [InlineData("trust")]
    public async Task OriginalValidationFailureRetainsItsBoundaryAndNoFence(string boundary)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var log = new DispatchDiagnosticLog();
        using var logging = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(log));
        var original = new InvalidOperationException("private-authority-failure");
        var observer = Substitute.For<IRouteCatalogHostObserver>();
        observer.ObserveAsync(Arg.Any<Hexalith.EventStore.Contracts.Routing.RouteCatalogActivation>(), Arg.Any<RouteCatalogStateCoordinates>(), Arg.Any<CancellationToken>())
            .Returns(_ => boundary == "capture" ? Task.FromException<Hexalith.EventStore.Contracts.Routing.RouteCatalogHostIdentity>(original) : Task.FromResult(fixture.Hosts[0]));
        var trust = Trust();
        if (boundary == "trust") trust.ValidateAsync(Arg.Any<Hexalith.EventStore.Contracts.Routing.RouteCatalogActivation>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(original));
        var authority = Authority(fixture, observer, trust, logging);

        (await Should.ThrowAsync<InvalidOperationException>(() => authority.AcquireAsync(default))).ShouldBeSameAs(original);

        CommandDispatchDiagnosticsTests.AssertClosed(log, boundary == "capture" ? "CatalogHostCapture" : "CatalogTrust", "Failed");
        log.Records.ShouldNotContain(record => Equals(record["Stage"], "CatalogCompareExchange") || Equals(record["Stage"], "CatalogSnapshot"));
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        CommandDispatchDiagnosticsTests.AssertPrivate(log);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisabledOrThrowingLoggingRemainsAdvisory(bool throwing)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var log = new DispatchDiagnosticLog(enabled: throwing, throwOnWrite: throwing);
        using var logging = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(log));
        var authority = Authority(fixture, new ObservedRouteCatalogHost(_ => Task.FromResult(fixture.Observations[0])), Trust(), logging);
        await using (await authority.AcquireAsync(default)) { }
        log.Records.ShouldBeEmpty();
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    private static IRouteCatalogTrustValidator Trust()
    {
        var trust = Substitute.For<IRouteCatalogTrustValidator>();
        trust.ValidateAsync(Arg.Any<Hexalith.EventStore.Contracts.Routing.RouteCatalogActivation>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        return trust;
    }

    private static RouteCatalogAuthority Authority(RouteCatalogTestFixture fixture, IRouteCatalogHostObserver observer,
        IRouteCatalogTrustValidator trust, ILoggerFactory logging) => new(fixture.Store, trust,
            Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts[0], Profile = "Development" }), observer, null,
            logging.CreateLogger<RouteCatalogAuthority>());
}

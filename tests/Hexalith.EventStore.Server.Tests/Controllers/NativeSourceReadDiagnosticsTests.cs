using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.Server.Tests.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.ServiceDefaults;
using Microsoft.AspNetCore.Http;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

/// <summary>Proves private diagnostics cannot turn a canceled inspection into accepted authority.</summary>
public sealed class NativeSourceReadDiagnosticsTests
{
    /// <summary>Every required private selection is necessary, and malformed configuration stays observational.</summary>
    [Theory]
    [InlineData("EventStore:Testing:NativeSourceReadDiagnostics:Enabled", null)]
    [InlineData("EventStore:Testing:NativeSourceReadDiagnostics:Enabled", "false")]
    [InlineData("EventStore:Testing:NativeSourceReadDiagnostics:Enabled", "invalid")]
    public void IncompletePrivateSelectionLeavesOriginalTaskUnobserved(string key, string? value)
    {
        Dictionary<string, string?> settings = PrivateSettings();
        settings[key] = value;
        var entries = new List<LogEntry>();
        var task = Task.FromResult(new SourceProofInspection(SourceProofInspectionStatus.Unsupported, false, 0, false, null));

        NativeSourceReadDiagnostics.Observe(task, new("private-tenant", "private-domain", "private-aggregate"), false,
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            Options.Create(PrivateOptions()), DevelopmentEnvironment(),
            new TestLogger<NativeSourceReadDiagnosticsTests>(entries), CancellationToken.None).ShouldBeSameAs(task);

        entries.ShouldBeEmpty();
    }

    /// <summary>Raw selector keys cannot spoof the effective SDK options or the real host environment.</summary>
    [Theory]
    [InlineData("fixture")]
    [InlineData("profile")]
    [InlineData("binding")]
    [InlineData("environment")]
    public void RawConfigurationCannotReplaceEffectivePrivateSelection(string changed)
    {
        var entries = new List<LogEntry>();
        var task = Task.FromResult(new SourceProofInspection(SourceProofInspectionStatus.Unsupported, false, 0, false, null));
        RouteCatalogHostOptions options = PrivateOptions();
        if (changed == "fixture") options.DevelopmentFixture = false;
        if (changed == "profile") options.Profile = "Production";
        if (changed == "binding") options.RequireTechnicalHttpBinding = false;
        IHostEnvironment environment = DevelopmentEnvironment();
        if (changed == "environment") environment.EnvironmentName = Environments.Production;
        NativeSourceReadDiagnostics.Observe(task, new("private-tenant", "private-domain", "private-aggregate"), false,
            new ConfigurationBuilder().AddInMemoryCollection(PrivateSettings()).Build(),
            Options.Create(options), environment, new TestLogger<NativeSourceReadDiagnosticsTests>(entries), CancellationToken.None).ShouldBeSameAs(task);
        entries.ShouldBeEmpty();
    }

    /// <summary>The actual SDK bootstrap enables diagnostics even though its effective option keys are absent from raw configuration.</summary>
    [Fact]
    public void RealPrivateBootstrapCompositionEnablesDiagnosticBranch()
    {
        string directory = Directory.CreateTempSubdirectory("private-native-diagnostic-").FullName;
        try
        {
            var fixture = new RouteCatalogTestFixture();
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
            builder.Configuration["EventStore:Testing:NativeSourceReadDiagnostics:Enabled"] = "true";
            builder.Configuration["EventStore:Testing:AtomicNativeMetadataInspection:Enabled"] = "true";
            builder.Configuration["EventStore:RouteCatalog:SerializeLocalLeaseRelease"] = "true";
            builder.Configuration["EventStore:RouteCatalog:RetainValidatedCatalogBytes"] = "true";
            var section = builder.Configuration.GetSection("EventStore:RouteCatalog:DevelopmentBootstrap");
            section["Directory"] = directory;
            section["HostId"] = fixture.Hosts[0].HostId;
            section["CoordinatorHostId"] = fixture.Hosts[1].HostId;
            section["AttemptId"] = "private-native-diagnostic";
            section["BindingVersion"] = "2";
            for (int index = 0; index < fixture.Hosts.Length; index++) section["RequiredHostIds:" + index] = fixture.Hosts[index].HostId;
            builder.Services.AddSingleton<IRouteCatalogStateStore>(fixture.Store);
            builder.Services.AddEventStoreDevelopmentRouteCatalogBootstrap(builder.Configuration, (_, _) => Task.FromResult(fixture.Observations[0]));
            using var provider = builder.Services.BuildServiceProvider();
            builder.Configuration["EventStore:RouteCatalog:DevelopmentFixture"].ShouldBeNull();
            builder.Configuration["EventStore:RouteCatalog:Profile"].ShouldBeNull();
            builder.Configuration["EventStore:RouteCatalog:RequireTechnicalHttpBinding"].ShouldBeNull();
            var entries = new List<LogEntry>();
            var task = Task.FromResult(new SourceProofInspection(SourceProofInspectionStatus.Unsupported, false, 0, false, null));
            NativeSourceReadDiagnostics.Observe(task, new("private-tenant", "private-domain", "private-aggregate"), false,
                builder.Configuration, provider.GetRequiredService<IOptions<RouteCatalogHostOptions>>(),
                provider.GetRequiredService<IHostEnvironment>(), new TestLogger<NativeSourceReadDiagnosticsTests>(entries), CancellationToken.None).ShouldBeSameAs(task);
#if DEBUG
            AtomicNativeMetadataInspectionPolicy.IsEnabled(builder.Configuration, provider.GetRequiredService<IOptions<RouteCatalogHostOptions>>(),
                provider.GetRequiredService<IHostEnvironment>()).ShouldBeTrue();
            entries.Count.ShouldBe(2);
            entries[0].Message.ShouldContain("SerializeLocalLeaseReleaseConfigured=True");
            entries[0].Message.ShouldContain("RetainValidatedCatalogBytesConfigured=True");
            entries[1].Message.ShouldContain("Status=unsupported");
#else
            AtomicNativeMetadataInspectionPolicy.IsEnabled(builder.Configuration, provider.GetRequiredService<IOptions<RouteCatalogHostOptions>>(),
                provider.GetRequiredService<IHostEnvironment>()).ShouldBeFalse();
            entries.ShouldBeEmpty();
#endif
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>Every private atomic selector is necessary; raw keys cannot replace effective options or the host environment.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("false")]
    [InlineData("malformed")]
    [InlineData("fixture")]
    [InlineData("profile")]
    [InlineData("binding")]
    [InlineData("environment")]
    [InlineData("options")]
    public void AtomicMetadataRequiresExplicitActualPrivateSelection(string changed)
    {
        Dictionary<string, string?> settings = PrivateSettings();
        settings["EventStore:Testing:AtomicNativeMetadataInspection:Enabled"] = changed switch
        { "missing" => null, "false" => "false", "malformed" => "invalid", _ => "true" };
        RouteCatalogHostOptions options = PrivateOptions();
        if (changed == "fixture") options.DevelopmentFixture = false;
        if (changed == "profile") options.Profile = "Production";
        if (changed == "binding") options.RequireTechnicalHttpBinding = false;
        IHostEnvironment environment = DevelopmentEnvironment();
        if (changed == "environment") environment.EnvironmentName = Environments.Production;

        AtomicNativeMetadataInspectionPolicy.IsEnabled(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            changed == "options" ? null : Options.Create(options), environment).ShouldBeFalse();
    }

    /// <summary>The original atomic task can complete late for diagnosis, without completing the canceled request.</summary>
    [Fact]
    public async Task LateAtomicPairLogsTotalTimeAndNeverSuppliesLateAuthority()
    {
        var entries = new List<LogEntry>();
        var source = new TaskCompletionSource<SourceProofInspectionPair>();
        using var canceled = new CancellationTokenSource();
        Task<SourceProofInspectionPair> observed = NativeSourceReadDiagnostics.ObservePair(source.Task,
            new("private-tenant", "private-domain", "private-aggregate"),
            new ConfigurationBuilder().AddInMemoryCollection(PrivateSettings()).Build(), Options.Create(PrivateOptions()),
            DevelopmentEnvironment(), new TestLogger<NativeSourceReadDiagnosticsTests>(entries), canceled.Token);
        observed.ShouldBeSameAs(source.Task);
        Task<SourceProofInspectionPair> request = observed.WaitAsync(canceled.Token);
        canceled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => request);
        var initial = new SourceProofInspection(SourceProofInspectionStatus.Available, true, 3, false, null);
        source.SetResult(new(initial, initial));
        request.IsCanceled.ShouldBeTrue();
        await observed;
#if DEBUG
        entries.Count.ShouldBe(2);
        entries[1].Message.ShouldContain("InitialStatus=available");
        entries[1].Message.ShouldContain("FinalStatus=available");
        entries[1].Message.ShouldContain("TotalElapsedMilliseconds=");
        entries[1].Message.ShouldContain("RequestCanceled=True");
        entries[1].Message.ShouldContain("LateAuthorityAccepted=false");
        entries[1].Message.ShouldNotContain("private-");
#else
        entries.ShouldBeEmpty();
#endif
    }

    /// <summary>A late original result can be diagnosed after cancellation, but the request remains canceled.</summary>
    [Fact]
    public async Task LateCompletionObservesOneOriginalTaskWithoutAcceptingIt()
    {
        var entries = new List<LogEntry>();
        var source = new TaskCompletionSource<SourceProofInspection>();
        using var canceled = new CancellationTokenSource();
        var identity = new AggregateIdentity("private-tenant", "private-domain", "private-aggregate");
        Task<SourceProofInspection> observed = NativeSourceReadDiagnostics.Observe(source.Task, identity, true,
            new ConfigurationBuilder().AddInMemoryCollection(PrivateSettings()).Build(),
            Options.Create(PrivateOptions()), DevelopmentEnvironment(),
            new TestLogger<NativeSourceReadDiagnosticsTests>(entries), canceled.Token);
        observed.ShouldBeSameAs(source.Task);
        Task<SourceProofInspection> request = observed.WaitAsync(canceled.Token);
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        var proof = new SourceProof(1, identity.TenantId, identity.Domain, identity.AggregateId,
            "private-incarnation", 3, "private-message", new string('a', 64), 1, false);

        source.SetResult(new(SourceProofInspectionStatus.Available, true, 3, false, proof));

        request.IsCanceled.ShouldBeTrue();
        (await observed).Proof.ShouldBeSameAs(proof);
#if DEBUG
        entries.Count.ShouldBe(2);
        entries[1].Message.ShouldContain("Status=available");
        entries[1].Message.ShouldContain("Phase=recheck");
        entries[1].Message.ShouldContain("CurrentSequence=3");
        entries[1].Message.ShouldContain("RequestCanceled=True");
        entries[1].Message.ShouldContain("LateAuthorityAccepted=false");
        string logs = string.Join('\n', entries.Select(entry => entry.Message));
        logs.ShouldNotContain("private-");
        logs.ShouldNotContain(proof.CumulativeDigest);
#else
        entries.ShouldBeEmpty();
#endif
    }

    /// <summary>Detached completions cannot restore identifiers after the captured request privacy decision.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CompletionOutsideOriginalExecutionContextRetainsCapturedPrivacy(bool pair, bool protect)
    {
        var entries = new List<LogEntry>();
        DefaultHttpContext context = new();
        context.Request.Method = "POST";
        context.Request.Path = protect ? "/api/v1/streams/read/workload" : "/ordinary";
        bool captured = ReadDiagnosticsPrivacy.IsProtectedRequest(context);
        var identity = new AggregateIdentity("private-tenant", "private-domain", "private-aggregate");
        var value = new SourceProofInspection(SourceProofInspectionStatus.Available, true, 17, false, null);
        Task completion;
        if (pair)
        {
            TaskCompletionSource<SourceProofInspectionPair> source = new();
            NativeSourceReadDiagnostics.ObservePair(source.Task, identity,
                new ConfigurationBuilder().AddInMemoryCollection(PrivateSettings()).Build(), Options.Create(PrivateOptions()),
                DevelopmentEnvironment(), new TestLogger<NativeSourceReadDiagnosticsTests>(entries), CancellationToken.None, captured).ShouldBeSameAs(source.Task);
            context.Request.Path = "/ordinary";
            using (ExecutionContext.SuppressFlow()) completion = Task.Run(() => source.SetResult(new(value, value)));
            await completion;
            (await source.Task).Initial.ShouldBeSameAs(value);
        }
        else
        {
            TaskCompletionSource<SourceProofInspection> source = new();
            NativeSourceReadDiagnostics.Observe(source.Task, identity, false,
                new ConfigurationBuilder().AddInMemoryCollection(PrivateSettings()).Build(), Options.Create(PrivateOptions()),
                DevelopmentEnvironment(), new TestLogger<NativeSourceReadDiagnosticsTests>(entries), CancellationToken.None, captured).ShouldBeSameAs(source.Task);
            context.Request.Path = "/ordinary";
            using (ExecutionContext.SuppressFlow()) completion = Task.Run(() => source.SetResult(value));
            await completion;
            (await source.Task).ShouldBeSameAs(value);
        }
#if DEBUG
        entries.Count.ShouldBe(protect ? 0 : 2);
        if (!protect) entries[1].Message.ShouldContain(pair ? "InitialSequence=17" : "CurrentSequence=17");
#else
        entries.ShouldBeEmpty();
#endif
    }

    private static Dictionary<string, string?> PrivateSettings() => new()
    {
        ["EventStore:Testing:NativeSourceReadDiagnostics:Enabled"] = "true",
        ["EventStore:RouteCatalog:DevelopmentFixture"] = "true",
        ["EventStore:RouteCatalog:Profile"] = "Development",
        ["EventStore:RouteCatalog:RequireTechnicalHttpBinding"] = "true",
    };

    private static RouteCatalogHostOptions PrivateOptions() => new()
    { DevelopmentFixture = true, Profile = "Development", RequireTechnicalHttpBinding = true };

    private static IHostEnvironment DevelopmentEnvironment() => new Microsoft.Extensions.Hosting.Internal.HostingEnvironment
    { EnvironmentName = Environments.Development };
}

using System.Reflection;

using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Extensions;
using Hexalith.EventStore.Server.Routing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Extensions;

/// <summary>Composed-gateway extension loading verifies every package against its declaration before registration.</summary>
public sealed class EventStoreExtensionRegistrationTests
{
    [Fact]
    public void ExtensionApiOwnsTheSupportedMajorWindow()
    {
        EventStoreExtensionApi.CurrentMajor.ShouldBe(1);
        EventStoreExtensionApi.SupportedMajors.ShouldBe([1]);
        EventStoreExtensionApi.IsSupported(1).ShouldBeTrue();
        EventStoreExtensionApi.IsSupported(0).ShouldBeFalse();
        EventStoreExtensionApi.IsSupported(2).ShouldBeFalse();
    }

    [Fact]
    public async Task DeclaredExtensionRegistersAdaptersAndReportsReadinessAsync()
    {
        string package = UniqueName();
        Assembly assembly = ExtensionProbeAssembly.Create(package, "probe", 1);
        ServiceCollection services = new();
        _ = services.AddLogging();
        _ = services.AddEventStoreExtensions([new EventStoreExtensionDeclaration(package, "probe", 1)], Resolver(assembly));
        await using ServiceProvider provider = services.BuildServiceProvider();

        IIdempotencyIntentAdapter adapter = provider.GetServices<IIdempotencyIntentAdapter>().ShouldHaveSingleItem();
        adapter.AdapterId.ShouldBe("extension-probe:adapter");
        EventStoreLoadedExtension loaded = provider.GetRequiredService<EventStoreExtensionCatalog>().Extensions.ShouldHaveSingleItem();
        loaded.ShouldBe(new EventStoreLoadedExtension(package, "probe", 1, 1));
        provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .Single(registration => registration.Name == EventStoreExtensionServiceCollectionExtensions.HealthCheckName)
            .Tags.ShouldContain("ready");
        HealthReport report = await provider.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(registration => registration.Name == "eventstore-extensions", TestContext.Current.CancellationToken);
        HealthReportEntry entry = report.Entries["eventstore-extensions"];
        entry.Status.ShouldBe(HealthStatus.Healthy);
        entry.Data["probe"].ShouldBe(1);
        entry.Description.ShouldNotBeNull().ShouldContain("probe@1 (" + package + ")");
    }

    [Fact]
    public void NoDeclaredExtensionLeavesServicesUnchanged()
    {
        ServiceCollection services = new();
        _ = services.AddEventStoreExtensions(new ConfigurationBuilder().Build());
        services.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BootstrapRegistersAnUndeclaredPackageAndReusesADeclaredPackage(bool declared)
    {
        string package = UniqueName();
        Assembly assembly = ExtensionProbeAssembly.Create(package, "probe", 1);
        ServiceCollection services = new();
        int before = ExtensionProbeAdapters.CreationCount;
        if (declared) services.AddEventStoreExtensions([new EventStoreExtensionDeclaration(package, "probe", 1)], Resolver(assembly));
        IIdempotencyIntentAdapter? first = services.Select(descriptor => descriptor.ImplementationInstance)
            .OfType<IIdempotencyIntentAdapter>().SingleOrDefault();
        RunScopedRouteCatalogGateway.RegisterLegacyAdapters(services, [assembly]);
        ExtensionProbeAdapters.CreationCount.ShouldBe(before + 1);
        using ServiceProvider provider = services.BuildServiceProvider();
        IIdempotencyIntentAdapter adapter = provider.GetServices<IIdempotencyIntentAdapter>().ShouldHaveSingleItem();
        if (declared) adapter.ShouldBeSameAs(first);
        _ = new IdempotencyIntentAdapterRegistry([adapter], new CanonicalIdempotencyIntentEncoder());
    }

    [Fact]
    public void AConflictingLegacyPackageStillFailsTheRegistryAlongsideADeclaredPackage()
    {
        string package = UniqueName();
        Assembly declared = ExtensionProbeAssembly.Create(package, "probe", 1);
        Assembly legacy = ExtensionProbeAssembly.Create(UniqueName(), null, 1);
        ServiceCollection services = new();
        services.AddEventStoreExtensions([new EventStoreExtensionDeclaration(package, "probe", 1)], Resolver(declared));
        RunScopedRouteCatalogGateway.RegisterLegacyAdapters(services, [declared, legacy]);
        using ServiceProvider provider = services.BuildServiceProvider();
        IIdempotencyIntentAdapter[] adapters = [.. provider.GetServices<IIdempotencyIntentAdapter>()];
        adapters.Length.ShouldBe(2);
        Should.Throw<InvalidOperationException>(() => new IdempotencyIntentAdapterRegistry(adapters, new CanonicalIdempotencyIntentEncoder()))
            .Message.ShouldContain("Multiple trusted idempotency adapters");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void UnsupportedMajorRefusesBeforeAnyPackageLoads(int major)
    {
        bool loaded = false;
        EventStoreExtensionException refusal = Should.Throw<EventStoreExtensionException>(() => new ServiceCollection()
            .AddEventStoreExtensions([new EventStoreExtensionDeclaration("Probe.Unsupported", "probe", major)], _ => { loaded = true; throw new FileNotFoundException(); }));
        refusal.Condition.ShouldBe(EventStoreExtensionConditions.UnsupportedMajor);
        refusal.Message.ShouldContain("'Probe.Unsupported'");
        refusal.Message.ShouldContain("major " + major);
        refusal.Message.ShouldContain("supports majors 1");
        loaded.ShouldBeFalse();
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("package")]
    public void DuplicateIdentityOrPackageRefusesNamingBothPackages(string duplicate)
    {
        EventStoreExtensionDeclaration first = new("Probe.First", "probe", 1);
        EventStoreExtensionDeclaration second = duplicate == "identity" ? new("Probe.Second", "probe", 1) : new("probe.first", "other", 1);
        EventStoreExtensionException refusal = Should.Throw<EventStoreExtensionException>(() => new ServiceCollection()
            .AddEventStoreExtensions([first, second], _ => throw new FileNotFoundException()));
        refusal.Condition.ShouldBe(EventStoreExtensionConditions.Duplicate);
        refusal.Message.ShouldContain("Extension " + duplicate);
        refusal.Message.ShouldContain("'Probe.First'");
        refusal.Message.ShouldContain(duplicate == "identity" ? "'Probe.Second'" : "'probe.first'");
    }

    [Theory]
    [InlineData("other", 1)]
    [InlineData("probe", 2)]
    public void DeclarationDriftNamesPackageDeclaredAndActualValues(string actualId, int actualMajor)
    {
        string package = UniqueName();
        Assembly assembly = ExtensionProbeAssembly.Create(package, actualId, actualMajor);
        ServiceCollection services = new();
        EventStoreExtensionException refusal = Should.Throw<EventStoreExtensionException>(() => services
            .AddEventStoreExtensions([new EventStoreExtensionDeclaration(package, "probe", 1)], Resolver(assembly)));
        refusal.Condition.ShouldBe(EventStoreExtensionConditions.DeclarationDrift);
        refusal.Message.ShouldContain("'" + package + "' is declared as 'probe' major 1");
        refusal.Message.ShouldContain("declares '" + actualId + "' major " + actualMajor);
        services.ShouldBeEmpty();
    }

    [Fact]
    public void MissingIdentityAttributeRefuses()
    {
        string package = UniqueName();
        Assembly assembly = ExtensionProbeAssembly.Create(package, null, 1);
        EventStoreExtensionException refusal = Should.Throw<EventStoreExtensionException>(() => new ServiceCollection()
            .AddEventStoreExtensions([new EventStoreExtensionDeclaration(package, "probe", 1)], Resolver(assembly)));
        refusal.Condition.ShouldBe(EventStoreExtensionConditions.AttributeMissing);
        refusal.Message.ShouldContain(package);
    }

    [Fact]
    public void UnavailablePackageRefuses()
    {
        EventStoreExtensionException refusal = Should.Throw<EventStoreExtensionException>(() => new ServiceCollection()
            .AddEventStoreExtensions([new EventStoreExtensionDeclaration("Probe.Missing", "probe", 1)], _ => throw new FileNotFoundException()));
        refusal.Condition.ShouldBe(EventStoreExtensionConditions.Unavailable);
        refusal.Message.ShouldContain("'Probe.Missing'");
        refusal.InnerException.ShouldBeOfType<FileNotFoundException>();
    }

    [Fact]
    public void AdapterSourceOutsideThePackageRefuses()
    {
        string package = UniqueName();
        Assembly assembly = ExtensionProbeAssembly.Create(package, "probe", 1, foreignSource: true);
        EventStoreExtensionException refusal = Should.Throw<EventStoreExtensionException>(() => new ServiceCollection()
            .AddEventStoreExtensions([new EventStoreExtensionDeclaration(package, "probe", 1)], Resolver(assembly)));
        refusal.Condition.ShouldBe(EventStoreExtensionConditions.AdapterSourceUnavailable);
        refusal.Message.ShouldContain(typeof(ExtensionProbeAdapters).FullName!);
    }

    [Fact]
    public void DuplicateAdaptersAcrossExtensionsFailTheSharedRegistry()
    {
        string first = UniqueName();
        string second = UniqueName();
        Dictionary<string, Assembly> assemblies = new(StringComparer.Ordinal)
        {
            [first] = ExtensionProbeAssembly.Create(first, "first", 1),
            [second] = ExtensionProbeAssembly.Create(second, "second", 1),
        };
        ServiceCollection services = new();
        _ = services.AddEventStoreExtensions(
            [new EventStoreExtensionDeclaration(first, "first", 1), new EventStoreExtensionDeclaration(second, "second", 1)],
            name => assemblies[name]);
        using ServiceProvider provider = services.BuildServiceProvider();
        Should.Throw<InvalidOperationException>(() => new IdempotencyIntentAdapterRegistry(
            provider.GetServices<IIdempotencyIntentAdapter>(), new CanonicalIdempotencyIntentEncoder()))
            .Message.ShouldContain("Multiple trusted idempotency adapters");
    }

    [Fact]
    public void ExtensionsRegisterOncePerGateway()
    {
        string package = UniqueName();
        Assembly assembly = ExtensionProbeAssembly.Create(package, "probe", 1, adapters: false);
        ServiceCollection services = new();
        _ = services.AddEventStoreExtensions([new EventStoreExtensionDeclaration(package, "probe", 1)], Resolver(assembly));
        Should.Throw<EventStoreExtensionException>(() => services.AddEventStoreExtensions([], Resolver(assembly)))
            .Condition.ShouldBe(EventStoreExtensionConditions.DuplicateRegistration);
    }

    [Fact]
    public void ConfigurationDeclarationsAreReadInOrder()
    {
        IConfiguration configuration = Configuration(new Dictionary<string, string?>
        {
            ["EventStore:Extensions:Packages:0:PackageId"] = "Hexalith.Probe.EventStoreExtensions",
            ["EventStore:Extensions:Packages:0:ExtensionId"] = "probe",
            ["EventStore:Extensions:Packages:0:ApiMajor"] = "1",
            ["EventStore:Extensions:Packages:1:PackageId"] = "Hexalith.Other.EventStoreExtensions",
            ["EventStore:Extensions:Packages:1:ExtensionId"] = "other",
            ["EventStore:Extensions:Packages:1:ApiMajor"] = "2",
        });
        EventStoreExtensionLoader.Read(configuration).ShouldBe([
            new EventStoreExtensionDeclaration("Hexalith.Probe.EventStoreExtensions", "probe", 1),
            new EventStoreExtensionDeclaration("Hexalith.Other.EventStoreExtensions", "other", 2)]);
        Should.Throw<EventStoreExtensionException>(() => new ServiceCollection().AddEventStoreExtensions(configuration))
            .Condition.ShouldBe(EventStoreExtensionConditions.UnsupportedMajor);
    }

    [Theory]
    [InlineData("PackageId", "../Probe")]
    [InlineData("PackageId", "Probe, Version=1.0.0.0")]
    [InlineData("PackageId", "")]
    [InlineData("PackageId", "Probe\n")]
    [InlineData("ExtensionId", " ")]
    [InlineData("ApiMajor", "one")]
    [InlineData("ApiMajor", "-1")]
    [InlineData("ApiMajor", "0")]
    [InlineData("Secret", "value")]
    public void MalformedConfigurationEntryRefuses(string key, string value)
    {
        Dictionary<string, string?> values = new()
        {
            ["EventStore:Extensions:Packages:0:PackageId"] = "Hexalith.Probe.EventStoreExtensions",
            ["EventStore:Extensions:Packages:0:ExtensionId"] = "probe",
            ["EventStore:Extensions:Packages:0:ApiMajor"] = "1",
        };
        values["EventStore:Extensions:Packages:0:" + key] = value;
        EventStoreExtensionException refusal = Should.Throw<EventStoreExtensionException>(() => EventStoreExtensionLoader.Read(Configuration(values)));
        refusal.Condition.ShouldBe(EventStoreExtensionConditions.InvalidDeclaration);
        refusal.Message.ShouldContain("EventStore:Extensions:Packages:0");
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static Func<string, Assembly> Resolver(Assembly assembly) => name => name == assembly.GetName().Name ? assembly : throw new FileNotFoundException(name);

    private static string UniqueName() => "Probe.Extension." + Guid.NewGuid().ToString("N");
}

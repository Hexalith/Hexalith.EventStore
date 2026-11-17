using System.Globalization;
using System.Reflection;
using System.Text;

using Hexalith.EventStore.Contracts.Snapshots;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Tests.Actors;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;

/// <summary>Exercises the real actor-host registration contract without starting a sidecar.</summary>
public sealed class FoldedSnapshotActorConfigurationTests
{
    private const string Prefix = "EventStore:Snapshots:ActorQualifications";

    [Fact]
    public void AbsentConfigurationPreservesUnenrolledHost()
    {
        var services = new ServiceCollection();
        services.AddEventStoreServer(new ConfigurationBuilder().Build());
        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetService<FoldedSnapshotQualificationRegistry>().ShouldBeNull();
    }

    [Fact]
    public async Task ExistingActorHostStartupConsumesExactDeploymentDeclarations()
    {
        var services = new ServiceCollection();
        List<LogEntry> logs = [];
        services.AddSingleton<ILogger<FoldedSnapshotQualificationHostedService>>(
            new TestLogger<FoldedSnapshotQualificationHostedService>(logs));
        Dictionary<string, string?> values = Values();
        foreach ((string key, string? value) in Values(1, "inventory", "InventoryAggregate", "inventory-fold-v1")) values.Add(key, value);
        services.AddEventStoreServer(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        using ServiceProvider provider = services.BuildServiceProvider();

        await ActivatorUtilities.CreateInstance<FoldedSnapshotQualificationHostedService>(provider).StartAsync(CancellationToken.None);

        logs.ShouldBeEmpty();
        FoldedSnapshotQualificationRegistry registry = provider.GetRequiredService<FoldedSnapshotQualificationRegistry>();
        FoldedSnapshotQualification orders = registry.Find("orders", "OrderAggregate").ShouldNotBeNull();
        orders.StateType.ShouldBe(typeof(object));
        orders.ProfileId.ShouldBe("orders-fold-v1");
        orders.ParityEvidenceId.ShouldBe("orders-parity-v1");
        orders.Limits.ShouldBe(SnapshotTestLimits.ForState());
        registry.FindByProfile("inventory", "inventory-fold-v1").ShouldNotBeNull();
        registry.Find("orders", "OtherAggregate").ShouldBeNull();
        registry.Find("ORDERS", "OrderAggregate").ShouldBeNull();
        registry.FindByProfile("inventory", "orders-fold-v1").ShouldBeNull();
    }

    [Theory]
    [InlineData("unknown-entry")]
    [InlineData("unknown-limit")]
    [InlineData("computed-limit")]
    [InlineData("invalid-number")]
    [InlineData("invalid-limit")]
    [InlineData("inconsistent-limits")]
    [InlineData("missing-limits")]
    [InlineData("scalar-limits")]
    [InlineData("missing-domain")]
    [InlineData("missing-aggregate")]
    [InlineData("missing-profile")]
    [InlineData("missing-parity")]
    [InlineData("blank-domain")]
    [InlineData("blank-aggregate")]
    [InlineData("blank-profile")]
    [InlineData("blank-parity")]
    [InlineData("noncanonical-index")]
    [InlineData("sparse-index")]
    [InlineData("scalar-entry")]
    [InlineData("scalar-array")]
    public void MalformedDeclarationRefusesAtomicallyWithoutRetainingPrivateValues(string mutation)
    {
        const string sentinel = "credential-sentinel";
        Dictionary<string, string?> values = Values();
        string entry = Prefix + ":0:";
        switch (mutation)
        {
            case "unknown-entry": values[entry + "StateType"] = sentinel; break;
            case "unknown-limit": values[entry + "Limits:Unknown"] = sentinel; break;
            case "computed-limit": values[entry + "Limits:DomainReservationBytes"] = sentinel; break;
            case "invalid-number": values[entry + "Limits:MaximumDepth"] = sentinel; break;
            case "invalid-limit": values[entry + "Limits:MaximumDepth"] = "65"; break;
            case "inconsistent-limits": values[entry + "Limits:MaximumActorWorkspaceBytes"] = "1"; break;
            case "missing-limits": values = values.Where(item => !item.Key.Contains(":Limits:", StringComparison.Ordinal)).ToDictionary(); break;
            case "scalar-limits": values[entry + "Limits"] = sentinel; break;
            case "missing-domain": values.Remove(entry + "Domain"); break;
            case "missing-aggregate": values.Remove(entry + "AggregateType"); break;
            case "missing-profile": values.Remove(entry + "ProfileId"); break;
            case "missing-parity": values.Remove(entry + "ParityEvidenceId"); break;
            case "blank-domain": values[entry + "Domain"] = " "; break;
            case "blank-aggregate": values[entry + "AggregateType"] = " "; break;
            case "blank-profile": values[entry + "ProfileId"] = " " + sentinel; break;
            case "blank-parity": values[entry + "ParityEvidenceId"] = sentinel + " "; break;
            case "noncanonical-index": values = values.ToDictionary(item => item.Key.Replace(":0:", ":00:", StringComparison.Ordinal), item => item.Value); break;
            case "sparse-index": values = values.ToDictionary(item => item.Key.Replace(":0:", ":1:", StringComparison.Ordinal), item => item.Value); break;
            case "scalar-entry": values[Prefix + ":0"] = sentinel; break;
            case "scalar-array": values[Prefix] = sentinel; break;
        }
        var services = new ServiceCollection();
        services.AddSingleton("foreign-preserved");
        int count = services.Count;

        InvalidOperationException failure = Should.Throw<InvalidOperationException>(() =>
            services.AddEventStoreServer(new ConfigurationBuilder().AddInMemoryCollection(values).Build()));

        failure.Message.ShouldBe("SnapshotActorQualificationConfigurationInvalid");
        failure.ToString().ShouldNotContain(sentinel);
        failure.InnerException.ShouldBeNull();
        services.Count.ShouldBe(count);
    }

    /// <summary>Every finite constructor parameter is required; none receives an implicit numeric default.</summary>
    public static IEnumerable<object[]> LimitNames()
        => typeof(FoldedSnapshotCaptureLimits).GetConstructors().Single().GetParameters().Select(parameter => new object[] { parameter.Name! });

    [Theory]
    [MemberData(nameof(LimitNames))]
    public void EveryCaptureLimitMustBeExplicit(string name)
    {
        Dictionary<string, string?> values = Values();
        string key = values.Keys.Single(item => item.EndsWith(":" + name, StringComparison.OrdinalIgnoreCase));
        values.Remove(key);
        Should.Throw<InvalidOperationException>(() => new ServiceCollection().AddEventStoreServer(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build())).Message.ShouldBe("SnapshotActorQualificationConfigurationInvalid");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("\"\"")]
    public void PresentEmptyConfigurationDoesNotSilentlyDisappear(string value)
    {
        using var json = new MemoryStream(Encoding.UTF8.GetBytes("{\"EventStore\":{\"Snapshots\":{\"ActorQualifications\":" + value + "}}}"));
        IConfiguration configuration = new ConfigurationBuilder().AddJsonStream(json).Build();
        Should.Throw<InvalidOperationException>(() => new ServiceCollection().AddEventStoreServer(configuration))
            .Message.ShouldBe("SnapshotActorQualificationConfigurationInvalid");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateAggregateOrProfileRefusesBeforeRegistration(bool duplicateProfile)
    {
        Dictionary<string, string?> values = Values();
        foreach ((string key, string? value) in Values(1, "orders", duplicateProfile ? "OtherAggregate" : "OrderAggregate",
            duplicateProfile ? "orders-fold-v1" : "other-profile")) values.Add(key, value);
        var services = new ServiceCollection();
        Should.Throw<InvalidOperationException>(() => services.AddEventStoreServer(new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
        services.ShouldBeEmpty();
    }

    [Fact]
    public void ExistingCodeRegistrationIsPreservedAndDuplicateConfigRefuses()
    {
        var services = new ServiceCollection();
        services.AddEventStoreFoldedSnapshotActorQualification("orders", "OrderAggregate", "orders-fold-v1", "orders-parity-v1", SnapshotTestLimits.ForState());
        int before = services.Count;
        Should.Throw<InvalidOperationException>(() => services.AddEventStoreServer(new ConfigurationBuilder().AddInMemoryCollection(Values()).Build()));
        services.Count.ShouldBe(before);
    }

    private static Dictionary<string, string?> Values(int index = 0, string domain = "orders", string aggregate = "OrderAggregate", string profile = "orders-fold-v1")
    {
        string entry = Prefix + ":" + index.ToString(CultureInfo.InvariantCulture) + ":";
        var values = new Dictionary<string, string?>
        {
            [entry + "Domain"] = domain, [entry + "AggregateType"] = aggregate,
            [entry + "ProfileId"] = profile, [entry + "ParityEvidenceId"] = "orders-parity-v1",
        };
        FoldedSnapshotCaptureLimits limits = SnapshotTestLimits.ForState();
        foreach (ParameterInfo parameter in typeof(FoldedSnapshotCaptureLimits).GetConstructors().Single().GetParameters())
        {
            PropertyInfo property = typeof(FoldedSnapshotCaptureLimits).GetProperty(parameter.Name!, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)!;
            values.Add(entry + "Limits:" + property.Name, Convert.ToString(property.GetValue(limits), CultureInfo.InvariantCulture));
        }
        return values;
    }
}

namespace Hexalith.EventStore.Server.Tests.Telemetry;

using System.IO;
using System.Reflection;

using Hexalith.EventStore.CommandApi.Telemetry;
using Hexalith.EventStore.Server.Telemetry;

using Shouldly;

/// <summary>
/// Story 6.1 Task 7: OpenTelemetry registration tests.
/// Verifies ActivitySource registration, naming conventions, and tag namespace compliance.
/// </summary>
public class OpenTelemetryRegistrationTests {
    [Fact]
    public void ServiceDefaults_RegistersBothActivitySources() {
        string repositoryRoot = FindRepositoryRoot();
        string extensionsPath = Path.Combine(
            repositoryRoot,
            "src",
            "Hexalith.EventStore.ServiceDefaults",
            "Extensions.cs");

        File.Exists(extensionsPath).ShouldBeTrue($"Expected ServiceDefaults file at '{extensionsPath}'");

        string source = File.ReadAllText(extensionsPath);
        source.ShouldContain(".AddSource(\"Hexalith.EventStore.CommandApi\")");
        source.ShouldContain(".AddSource(\"Hexalith.EventStore\")");
    }

    [Fact]
    public void EventStoreActivitySource_AllActivityNamesMatchArchitecture() {
        // Assert -- all activity names follow EventStore.{Component}.{Action} pattern
        EventStoreActivitySource.ProcessCommand.ShouldStartWith("EventStore.Actor.");
        EventStoreActivitySource.IdempotencyCheck.ShouldStartWith("EventStore.Actor.");
        EventStoreActivitySource.TenantValidation.ShouldStartWith("EventStore.Actor.");
        EventStoreActivitySource.StateRehydration.ShouldStartWith("EventStore.Actor.");
        EventStoreActivitySource.DomainServiceInvoke.ShouldStartWith("EventStore.DomainService.");
        EventStoreActivitySource.EventsPersist.ShouldStartWith("EventStore.Events.");
        EventStoreActivitySource.EventsPublish.ShouldStartWith("EventStore.Events.");
        EventStoreActivitySource.EventsDrain.ShouldStartWith("EventStore.Events.");
        EventStoreActivitySource.EventsPublishDeadLetter.ShouldStartWith("EventStore.Events.");
        EventStoreActivitySource.StateMachineTransition.ShouldStartWith("EventStore.Actor.");
    }

    [Fact]
    public void EventStoreActivitySource_AllTagKeysFollowNamespace() {
        // Arrange -- get all public const string fields starting with "Tag"
        FieldInfo[] tagFields = typeof(EventStoreActivitySource)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.Name.StartsWith("Tag", StringComparison.Ordinal))
            .ToArray();

        tagFields.Length.ShouldBeGreaterThan(0, "Expected at least one tag constant");

        // Assert -- all tag constants use the eventstore.* namespace
        foreach (FieldInfo field in tagFields) {
            string? value = field.GetValue(null) as string;
            value.ShouldNotBeNull($"Tag field {field.Name} should have a string value");
            value.ShouldStartWith("eventstore.");
        }
    }

    [Fact]
    public void EventStoreActivitySource_SourceNameIsCorrect() {
        EventStoreActivitySource.SourceName.ShouldBe("Hexalith.EventStore");
        EventStoreActivitySource.Instance.Name.ShouldBe("Hexalith.EventStore");
    }

    [Fact]
    public void EventStoreActivitySources_CommandApiSourceNameIsCorrect() {
        EventStoreActivitySources.CommandApi.Name.ShouldBe("Hexalith.EventStore.CommandApi");
    }

    [Fact]
    public void EventStoreActivitySources_SubmitConstantMatchesArchitecture() {
        EventStoreActivitySources.Submit.ShouldBe("EventStore.CommandApi.Submit");
    }

    [Fact]
    public void EventStoreActivitySources_QueryStatusConstantMatchesArchitecture() {
        EventStoreActivitySources.QueryStatus.ShouldBe("EventStore.CommandApi.QueryStatus");
    }

    [Fact]
    public void EventStoreActivitySources_ReplayConstantMatchesArchitecture() {
        EventStoreActivitySources.Replay.ShouldBe("EventStore.CommandApi.Replay");
    }

    private static string FindRepositoryRoot() {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null) {
            if (File.Exists(Path.Combine(current.FullName, "Hexalith.EventStore.slnx"))) {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root from test base directory.");
    }
}

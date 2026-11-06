using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Verifies the catalog fixture's native launch arguments without starting a sidecar or enrolling a control plane.</summary>
[Collection("RouteCatalogSidecarConfiguration")]
public sealed class RouteCatalogDevelopmentSidecarTests
{
    /// <summary>Explicit ports select the caller's control plane, with unchanged defaults only for absent overrides.</summary>
    [Theory]
    [InlineData(null, null, "localhost:50005", "localhost:50006")]
    [InlineData("54321", "54322", "localhost:54321", "localhost:54322")]
    [InlineData("54321", null, "localhost:54321", "localhost:50006")]
    [InlineData(null, "54322", "localhost:50005", "localhost:54322")]
    public async Task ConstructorUsesConfiguredPortsWithoutAppendingSharedFallbackAsync(
        string? placement, string? scheduler, string expectedPlacement, string expectedScheduler)
    {
        string? originalPlacement = Environment.GetEnvironmentVariable("EVENTSTORE_TEST_PLACEMENT_PORT");
        string? originalScheduler = Environment.GetEnvironmentVariable("EVENTSTORE_TEST_SCHEDULER_PORT");
        try
        {
            Environment.SetEnvironmentVariable("EVENTSTORE_TEST_PLACEMENT_PORT", placement);
            Environment.SetEnvironmentVariable("EVENTSTORE_TEST_SCHEDULER_PORT", scheduler);
            await using var sidecar = new RouteCatalogDevelopmentSidecar("configuration-check", "unused-resources", "unused-configuration");
            string[] arguments = JsonSerializer.Deserialize<string[]>(sidecar.Arguments).ShouldNotBeNull();

            arguments.Count(argument => argument == "--placement-host-address").ShouldBe(1);
            arguments.Count(argument => argument == "--scheduler-host-address").ShouldBe(1);
            arguments[Array.IndexOf(arguments, "--placement-host-address") + 1].ShouldBe(expectedPlacement);
            arguments[Array.IndexOf(arguments, "--scheduler-host-address") + 1].ShouldBe(expectedScheduler);

            sidecar.DisableControlPlane();
            arguments = JsonSerializer.Deserialize<string[]>(sidecar.Arguments).ShouldNotBeNull();
            arguments[Array.IndexOf(arguments, "--placement-host-address") + 1].ShouldBeEmpty();
            arguments[Array.IndexOf(arguments, "--scheduler-host-address") + 1].ShouldBeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable("EVENTSTORE_TEST_PLACEMENT_PORT", originalPlacement);
            Environment.SetEnvironmentVariable("EVENTSTORE_TEST_SCHEDULER_PORT", originalScheduler);
        }
    }

    /// <summary>Malformed explicit configuration fails before the fixture can allocate sockets or start a process.</summary>
    [Theory]
    [InlineData("EVENTSTORE_TEST_PLACEMENT_PORT", " ")]
    [InlineData("EVENTSTORE_TEST_PLACEMENT_PORT", "0")]
    [InlineData("EVENTSTORE_TEST_PLACEMENT_PORT", "65536")]
    [InlineData("EVENTSTORE_TEST_PLACEMENT_PORT", "localhost:50005")]
    [InlineData("EVENTSTORE_TEST_SCHEDULER_PORT", " ")]
    [InlineData("EVENTSTORE_TEST_SCHEDULER_PORT", "0")]
    [InlineData("EVENTSTORE_TEST_SCHEDULER_PORT", "65536")]
    [InlineData("EVENTSTORE_TEST_SCHEDULER_PORT", "localhost:50006")]
    public void ConstructorRejectsMalformedOverrideWithoutFallingBack(string variable, string configured)
    {
        string? original = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, configured);
            Should.Throw<InvalidOperationException>(() => new RouteCatalogDevelopmentSidecar(
                "configuration-check", "unused-resources", "unused-configuration")).Message.ShouldContain(variable);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, original);
        }
    }
}

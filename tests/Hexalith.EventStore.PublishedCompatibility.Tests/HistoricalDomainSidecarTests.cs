using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Checks the newly retained actual selected Domain-sidecar coordinates, without reconstructing historical captures.</summary>
public sealed class HistoricalDomainSidecarTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("backend")]
    [InlineData("endpoint")]
    [InlineData("app")]
    [InlineData("pid")]
    [InlineData("lifetime")]
    public void SelectedDomainSidecarMustRemainBoundToItsActualRuntime(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create();
        fixture.Validate();
        int index = fixture.DaprLaunches.FindIndex(launch => launch.Role == "Domain");
        var selected = fixture.DaprLaunches[index];
        if (mutation == "missing") fixture.DaprLaunches.RemoveAt(index);
        else fixture.DaprLaunches[index] = mutation switch
        {
            "backend" => selected with { Components = selected.Components + "-foreign" },
            "endpoint" => selected with { SidecarPort = selected.SidecarPort + 1 },
            "app" => selected with { AppId = selected.AppId + "-foreign" },
            "pid" => selected with { Pid = selected.Pid + 1 },
            _ => selected with { Ended = selected.Observed.AddTicks(-1) },
        };
        fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(() => fixture.Validate()).Message.ShouldContain("sidecar");
    }

    [Fact]
    public void NullDockerLabelsRemainInTheIndependentSharedInventory()
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create();
        for (int index = 0; index < fixture.Commands.Count; index++)
        {
            var command = fixture.Commands[index];
            if (command.Command.FileName != "docker" || !command.Command.Arguments.Contains("inspect") || command.ExitCode != 0) continue;
            using var document = JsonDocument.Parse(command.StandardOutput);
            var root = System.Text.Json.Nodes.JsonNode.Parse(command.StandardOutput)!;
            var item = document.RootElement[0];
            if (item.TryGetProperty("Config", out var config) && config.TryGetProperty("Labels", out var labels)
                && labels.ValueKind == JsonValueKind.Object && labels.TryGetProperty("p1r.owner", out var owner) && owner.ValueKind == JsonValueKind.Null)
            {
                root[0]!["Config"]!["Labels"] = null;
                string stdout = root.ToJsonString();
                fixture.Commands[index] = command with { StandardOutput = stdout, OutputHash = QualificationRuntime.Hash(System.Text.Encoding.UTF8.GetBytes(stdout + command.StandardError)) };
            }
        }
        fixture.Validate();
    }
}

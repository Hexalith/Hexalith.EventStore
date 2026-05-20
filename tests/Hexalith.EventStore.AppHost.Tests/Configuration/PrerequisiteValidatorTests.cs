using Hexalith.EventStore.AppHost;

namespace Hexalith.EventStore.AppHost.Tests.Configuration;

public class PrerequisiteValidatorTests {
    [Fact]
    public void GetMissingPrerequisites_UsesLightweightServerVersionProbeWithExtendedTimeout() {
        var runner = new SlowDockerProbeRunner(PrerequisiteValidator.CommandTimeout - TimeSpan.FromSeconds(1));

        IReadOnlyList<string> errors = PrerequisiteValidator.GetMissingPrerequisites(
            runner,
            PrerequisiteValidator.CommandTimeout);

        errors.ShouldBeEmpty();
        runner.Calls.ShouldContain(c =>
            c.Command == "docker"
            && c.Args == "version --format \"{{.Server.Version}}\""
            && c.Timeout == PrerequisiteValidator.CommandTimeout);
    }

    [Fact]
    public void GetMissingPrerequisites_WhenDockerFails_ReturnsDockerError() {
        var runner = new ScriptedRunner(
            docker: new PrerequisiteCommandResult(false, null),
            dapr: new PrerequisiteCommandResult(true, "CLI version: 1.0" + Environment.NewLine + "Runtime version: 1.0"));

        IReadOnlyList<string> errors = PrerequisiteValidator.GetMissingPrerequisites(runner, PrerequisiteValidator.CommandTimeout);

        errors.ShouldHaveSingleItem();
        errors[0].ShouldContain("Docker");
    }

    [Fact]
    public void GetMissingPrerequisites_WhenDaprCliMissing_ReturnsDaprCliError() {
        var runner = new ScriptedRunner(
            docker: new PrerequisiteCommandResult(true, "24.0.0"),
            dapr: new PrerequisiteCommandResult(false, null));

        IReadOnlyList<string> errors = PrerequisiteValidator.GetMissingPrerequisites(runner, PrerequisiteValidator.CommandTimeout);

        errors.ShouldHaveSingleItem();
        errors[0].ShouldContain("DAPR CLI");
    }

    [Fact]
    public void GetMissingPrerequisites_WhenDaprRuntimeNotInitialized_ReturnsDaprRuntimeError() {
        var runner = new ScriptedRunner(
            docker: new PrerequisiteCommandResult(true, "24.0.0"),
            dapr: new PrerequisiteCommandResult(true, "CLI version: 1.17.1" + Environment.NewLine + "Runtime version: n/a"));

        IReadOnlyList<string> errors = PrerequisiteValidator.GetMissingPrerequisites(runner, PrerequisiteValidator.CommandTimeout);

        errors.ShouldHaveSingleItem();
        errors[0].ShouldContain("DAPR runtime");
    }

    [Fact]
    public void GetMissingPrerequisites_WhenBothFail_ReturnsTwoErrors() {
        var runner = new ScriptedRunner(
            docker: new PrerequisiteCommandResult(false, null),
            dapr: new PrerequisiteCommandResult(false, null));

        IReadOnlyList<string> errors = PrerequisiteValidator.GetMissingPrerequisites(runner, PrerequisiteValidator.CommandTimeout);

        errors.Count.ShouldBe(2);
        errors.ShouldContain(e => e.Contains("Docker"));
        errors.ShouldContain(e => e.Contains("DAPR CLI"));
    }

    private sealed class ScriptedRunner(PrerequisiteCommandResult docker, PrerequisiteCommandResult dapr) : IPrerequisiteCommandRunner {
        public PrerequisiteCommandResult Run(string command, string args, TimeSpan timeout) {
            return command switch {
                "docker" => docker,
                "dapr" => dapr,
                _ => new PrerequisiteCommandResult(false, $"Unknown command: {command}")
            };
        }
    }

    private sealed class SlowDockerProbeRunner(TimeSpan minimumDockerTimeout) : IPrerequisiteCommandRunner {
        public List<(string Command, string Args, TimeSpan Timeout)> Calls { get; } = [];

        public PrerequisiteCommandResult Run(string command, string args, TimeSpan timeout) {
            Calls.Add((command, args, timeout));

            if (command == "docker" && args == "version --format \"{{.Server.Version}}\"") {
                return new PrerequisiteCommandResult(timeout >= minimumDockerTimeout, "Docker Desktop");
            }

            if (command == "dapr" && args == "--version") {
                return new PrerequisiteCommandResult(
                    true,
                    "CLI version: 1.17.1" + Environment.NewLine + "Runtime version: 1.17.7");
            }

            return new PrerequisiteCommandResult(false, null);
        }
    }
}


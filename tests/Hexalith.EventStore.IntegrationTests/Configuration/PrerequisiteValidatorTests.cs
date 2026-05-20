using Hexalith.EventStore.AppHost;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.Configuration;

public class PrerequisiteValidatorTests {
    [Fact]
    public void GetMissingPrerequisites_UsesLightweightServerVersionProbeWithExtendedTimeout() {
        var runner = new SlowDockerProbeRunner(TimeSpan.FromSeconds(90));

        IReadOnlyList<string> errors = PrerequisiteValidator.GetMissingPrerequisites(
            runner,
            PrerequisiteValidator.CommandTimeout);

        errors.ShouldBeEmpty();
        runner.Calls.ShouldContain(c =>
            c.Command == "docker"
            && c.Args == "version --format \"{{.Server.Version}}\""
            && c.Timeout >= TimeSpan.FromSeconds(90));
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

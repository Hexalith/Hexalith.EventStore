using System.Diagnostics;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Verifies the Story 1.20 proof packet's Dapr process-ownership contract behavior.
/// </summary>
public sealed class ProofPacketDaprConflictProcessContractTests {
    private const string PacketRelativePath =
        "_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md";

    /// <summary>
    /// Verifies tagged processes remain gate-owned after ancestry loss while terminated inventory
    /// rows are ignored and a live untagged process remains external.
    /// </summary>
    [Fact]
    public void ProcessContractDistinguishesOwnedExternalAndExitedProcesses() {
        string root = FindRepositoryRoot();
        string packet = File.ReadAllText(Path.Combine(root, PacketRelativePath));
        const string startMarker = "# dapr-conflict-process-contract-start";
        const string endMarker = "# dapr-conflict-process-contract-end";
        int start = packet.IndexOf(startMarker, StringComparison.Ordinal);
        int bodyStart = start < 0 ? -1 : packet.IndexOf('\n', start) + 1;
        int end = bodyStart <= 0 ? -1 : packet.IndexOf(endMarker, bodyStart, StringComparison.Ordinal);

        start.ShouldBeGreaterThanOrEqualTo(0, "The Dapr process contract must retain its start marker.");
        bodyStart.ShouldBeGreaterThan(0, "The Dapr process contract must follow its start marker.");
        end.ShouldBeGreaterThan(bodyStart, "The Dapr process contract must retain its end marker.");
        packet.LastIndexOf(startMarker, StringComparison.Ordinal).ShouldBe(
            start,
            "The Dapr process contract start marker must be unique.");
        packet.LastIndexOf(endMarker, StringComparison.Ordinal).ShouldBe(
            end,
            "The Dapr process contract end marker must be unique.");

        string processContract = packet[bodyStart..end];
        string script = "set -euo pipefail\n" + processContract +
            """

            DAPR_GATE_PROCESS_ROOT_PID=1
            DAPR_GATE_PROCESS_TOKEN="proof-packet-test-token"
            export STORY_1_20_DAPR_GATE_TOKEN="$DAPR_GATE_PROCESS_TOKEN"
            owned_pid=''
            external_pid=''
            cleanup() {
              test -z "$owned_pid" || kill "$owned_pid" 2>/dev/null || true
              test -z "$external_pid" || kill "$external_pid" 2>/dev/null || true
              test -z "$owned_pid" || wait "$owned_pid" 2>/dev/null || true
              test -z "$external_pid" || wait "$external_pid" 2>/dev/null || true
            }
            trap cleanup EXIT

            sleep 30 &
            owned_pid=$!
            env -u STORY_1_20_DAPR_GATE_TOKEN sleep 30 &
            external_pid=$!

            ! dapr_pid_is_live_external "$owned_pid"
            dapr_pid_is_live_external "$external_pid"
            ! dapr_row_is_live_external "$owned_pid" "$external_pid"
            ! dapr_row_is_live_external "$external_pid" "$owned_pid"
            dapr_row_is_live_external "$external_pid" ''
            exited_pid="$external_pid"
            kill "$external_pid"
            wait "$external_pid" 2>/dev/null || true
            external_pid=''
            ! dapr_pid_is_live_external "$exited_pid"
            ! dapr_row_is_live_external "$exited_pid" ''
            """ + "\n";
        script = script.Replace("\r\n", "\n", StringComparison.Ordinal);

        ProcessResult result = RunBashScript(script);
        result.ExitCode.ShouldBe(
            0,
            "The packet must distinguish owned PID pairs, live external processes, and stale inventory PIDs. "
                + $"Standard output: {result.StandardOutput}; standard error: {result.StandardError}");
    }

    private static ProcessResult RunBashScript(string script) {
        using var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = "bash",
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            },
        };
        process.StartInfo.ArgumentList.Add("-s");

        process.Start().ShouldBeTrue("bash must be available to execute the proof-packet process contract.");
        process.StandardInput.Write(script);
        process.StandardInput.Close();
        process.WaitForExit(5000).ShouldBeTrue("The Dapr process contract must finish within five seconds.");
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static string FindRepositoryRoot() {
        foreach (string startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            .Distinct(StringComparer.Ordinal)) {
            DirectoryInfo? directory = new(startPath);
            while (directory is not null) {
                if (File.Exists(Path.Combine(
                    directory.FullName,
                    PacketRelativePath.Replace('/', Path.DirectorySeparatorChar)))) {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing the Story 1.20 proof packet.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}

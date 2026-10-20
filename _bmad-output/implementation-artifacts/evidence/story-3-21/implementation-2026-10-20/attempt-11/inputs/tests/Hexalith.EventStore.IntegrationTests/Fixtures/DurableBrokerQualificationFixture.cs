using System.Diagnostics;
using System.Text.Json;

namespace Hexalith.EventStore.IntegrationTests.Fixtures;

/// <summary>
/// Runs the separate Story 3.21 candidate lane against real, owned Dapr hosts.
/// Retains negative evidence and never promotes Docker results to Kubernetes proof.
/// </summary>
public sealed class DurableBrokerQualificationFixture : IAsyncDisposable
{
    private Process? _process;

    /// <summary>Gets the retained evidence directory, including failures.</summary>
    public string EvidenceDirectory { get; } = Path.Combine(
        Path.GetTempPath(), $"story-3-21-integration-{Guid.NewGuid():N}");

    /// <summary>Exercises both immutable candidates and all bounded fault scenarios.</summary>
    /// <param name="cancellationToken">Cancels this owned process and its children.</param>
    /// <returns>The independently validated technical qualification result.</returns>
    public async Task<JsonDocument> QualifyAsync(CancellationToken cancellationToken)
    {
        string root = FindRepositoryRoot();
        int executionCode = await RunAsync(root,
            ["tools/qualify-dapr-brokers.py", "--profile", "deploy/dapr/qualification/runtime-lock.json",
                "--brokers", "rabbitmq,kafka", "--output", EvidenceDirectory],
            cancellationToken).ConfigureAwait(false);

        // Integrity verification accepts truthful negative packets so the caller
        // can distinguish a failed guarantee from a broken or missing receipt.
        int verificationCode = await RunAsync(root,
            ["tools/validate-dapr-broker-qualification.py", EvidenceDirectory, "--allow-failures"],
            cancellationToken).ConfigureAwait(false);
        if (verificationCode != 0)
        {
            throw new InvalidOperationException($"Broker packet integrity failed; inspect {EvidenceDirectory}.");
        }

        JsonDocument packet = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(EvidenceDirectory, "packet.json"), cancellationToken).ConfigureAwait(false));
        if (executionCode != 0)
        {
            packet.Dispose();
            throw new InvalidOperationException($"Broker qualification FAIL; retained matrix and diagnostics: {EvidenceDirectory}.");
        }

        return packet;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_process is not null)
        {
            if (!_process.HasExited)
            {
                await StopOwnedProcessAsync(_process).ConfigureAwait(false);
            }

            _process.Dispose();
            _process = null;
        }
    }

    private async Task<int> RunAsync(string root, string[] arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("python3")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        _process = Process.Start(start) ?? throw new InvalidOperationException("Could not start qualification process.");
        Task<string> output = _process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = _process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            string combined = await output.ConfigureAwait(false) + await error.ConfigureAwait(false);
            await File.WriteAllTextAsync(EvidenceDirectory +
                (arguments[0].Contains("validate-", StringComparison.Ordinal) ? ".validation.log" : ".execution.log"),
                combined, cancellationToken).ConfigureAwait(false);
            return _process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            await StopOwnedProcessAsync(_process).ConfigureAwait(false);
            throw;
        }
        finally
        {
            if (_process.HasExited)
            {
                _process.Dispose();
                _process = null;
            }
        }
    }

    private static async Task StopOwnedProcessAsync(Process process)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var stop = new ProcessStartInfo("kill") { UseShellExecute = false };
            stop.ArgumentList.Add("-TERM");
            stop.ArgumentList.Add(process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            using Process? signal = Process.Start(stop);
            if (signal is not null)
            {
                await signal.WaitForExitAsync().ConfigureAwait(false);
            }

            // SIGTERM lets the producer's finally block remove only labeled
            // owned containers and preserve a complete negative packet.
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2)).ConfigureAwait(false);
        }
        else
        {
            throw new PlatformNotSupportedException("The owned disposable broker lane requires Linux or macOS for graceful cleanup.");
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("EventStore repository root was not found.");
    }
}

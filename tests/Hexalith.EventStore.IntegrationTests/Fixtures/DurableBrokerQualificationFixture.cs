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
    public string EvidenceDirectory { get; } = Environment.GetEnvironmentVariable("EVENTSTORE_DAPR_QUALIFICATION_OUTPUT") ?? Path.Combine(
        Path.GetTempPath(), $"story-3-21-integration-{Guid.NewGuid():N}");

    /// <summary>Exercises both immutable candidates and all bounded fault scenarios.</summary>
    /// <param name="cancellationToken">Cancels this owned process and its children.</param>
    /// <returns>The independently validated technical qualification result.</returns>
    public async Task<JsonDocument> QualifyAsync(CancellationToken cancellationToken)
    {
        string root = FindRepositoryRoot();
        string profile = Environment.GetEnvironmentVariable("EVENTSTORE_DAPR_QUALIFICATION_PROFILE") ?? "deploy/dapr/qualification/runtime-lock.json";
        string? platformConfiguration = Environment.GetEnvironmentVariable("EVENTSTORE_DAPR_PLATFORM_CONFIG");
        int executionCode = await RunAsync(root,
            QualificationArguments(profile, platformConfiguration, EvidenceDirectory),
            cancellationToken).ConfigureAwait(false);

        // Integrity verification accepts truthful negative packets so the caller
        // can distinguish a failed guarantee from a broken or missing receipt.
        string[] verificationArguments = ["tools/validate-dapr-broker-qualification.py", EvidenceDirectory, "--allow-failures"];
        if (platformConfiguration is not null)
        {
            verificationArguments = [.. verificationArguments, "--platform-config", platformConfiguration];
        }

        int verificationCode = await RunAsync(root, verificationArguments, cancellationToken).ConfigureAwait(false);
        if (verificationCode != 0)
        {
            throw new InvalidOperationException($"Broker packet integrity failed; inspect {EvidenceDirectory}.");
        }

        JsonDocument packet = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(EvidenceDirectory, "packet.json"), cancellationToken).ConfigureAwait(false));
        if (executionCode is not (0 or 1))
        {
            packet.Dispose();
            throw new InvalidOperationException($"Broker qualification did not produce bounded outcomes; inspect {EvidenceDirectory}.");
        }

        return packet;
    }

    /// <summary>Builds the original matrix invocation with an explicitly paired optional Platform input.</summary>
    /// <param name="profile">The explicitly selected immutable qualification lock.</param>
    /// <param name="platformConfiguration">The separate bounded configuration, or null for historical profiles.</param>
    /// <param name="output">The fresh evidence directory.</param>
    /// <returns>Arguments passed directly to Python without shell interpretation.</returns>
    internal static string[] QualificationArguments(string profile, string? platformConfiguration, string output)
    {
        bool platformProfile = string.Equals(Path.GetFileName(profile),
            "runtime-lock-1.19.1-platform-config-v1.json", StringComparison.Ordinal);
        if (platformProfile != (platformConfiguration is not null) || platformConfiguration is not null && string.IsNullOrWhiteSpace(platformConfiguration))
        {
            throw new ArgumentException("Platform configuration and the explicit platform-config-v1 profile are required together.");
        }

        string[] arguments = ["tools/qualify-dapr-brokers.py", "--profile", profile, "--brokers", "rabbitmq,kafka", "--output", output];
        return platformConfiguration is null ? arguments : [.. arguments, "--platform-config", platformConfiguration];
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
        start.ArgumentList.Add("-B");
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

using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;

using PactNet;
using PactNet.Exceptions;
using PactNet.Verifier;

namespace Hexalith.EventStore.ProviderVerification;

internal static class PactInteractionVerifier
{
    public static async Task<InteractionVerificationResult> VerifyAsync(
        int index,
        InteractionDefinition interaction,
        string pactDirectory,
        Uri baseAddress,
        ProviderStateCoordinator coordinator,
        TimeSpan requestTimeout)
    {
        coordinator.BeginInteraction(interaction.ProviderState);
        var stopwatch = Stopwatch.StartNew();
        string resultCode;
        try
        {
            string normalizedPact = CreateNormalizedPact(interaction, pactDirectory);
            try
            {
                resultCode = await RunIsolatedAsync(
                    normalizedPact,
                    baseAddress,
                    interaction.Description,
                    interaction.ProviderState,
                    requestTimeout).ConfigureAwait(false);
            }
            finally
            {
                TryDelete(normalizedPact);
            }
        }
        finally
        {
            _ = coordinator.ForceCleanup(interaction.ProviderState);
        }

        IReadOnlyList<ProviderStateEvent> events = coordinator.SnapshotEvents();
        if (!events.Any(item => item.Action == "setup" && item.ResultCode == "state.setup.succeeded"))
        {
            resultCode = "interaction.state-setup-missing";
        }
        else if (!events.Any(item => item.Action == "teardown"
            && item.ResultCode is "state.teardown.succeeded" or "state.teardown.forced"))
        {
            resultCode = "interaction.state-teardown-missing";
        }

        return new InteractionVerificationResult(
            index,
            interaction.Description,
            interaction.PactFile,
            interaction.ProviderState,
            resultCode,
            stopwatch.ElapsedMilliseconds,
            events);
    }

    internal static int RunIsolated(string[] args)
    {
        if (args.Length != 6
            || !int.TryParse(args[5], out int timeoutSeconds)
            || timeoutSeconds < 1
            || timeoutSeconds > 120
            || !Uri.TryCreate(args[2], UriKind.Absolute, out Uri? baseAddress))
        {
            return 2;
        }

        try
        {
            var config = new PactVerifierConfig
            {
                LogLevel = PactLogLevel.Error,
                Outputters = [new DiscardingPactOutput()],
            };
            using var verifier = new PactVerifier("Hexalith.EventStore", config);
            verifier
                .WithHttpEndpoint(baseAddress)
                .WithFileSource(new FileInfo(args[1]))
                .WithProviderStateUrl(
                    new Uri(baseAddress, "/__provider-state"),
                    options => options.WithTeardown())
                .WithFilter(args[3], args[4])
                .WithRequestTimeout(TimeSpan.FromSeconds(timeoutSeconds))
                .Verify();
            return 0;
        }
        catch (PactFailureException)
        {
            return 1;
        }
        catch (Exception)
        {
            return 2;
        }
    }

    internal static string CreateNormalizedPact(InteractionDefinition interaction, string pactDirectory)
    {
        string pactPath = Path.Combine(pactDirectory, interaction.PactFile);
        byte[] snapshot = JsonInput.ReadSnapshot(pactPath, 2 * 1024 * 1024);
        if (!string.Equals(
            VerificationInputLoader.ComputeSha256(snapshot),
            interaction.PactSha256,
            StringComparison.Ordinal))
        {
            throw new ProviderVerificationInputException("input.pact.hash-changed");
        }

        ReadOnlySpan<byte> jsonBytes = snapshot;
        if (jsonBytes.Length >= 3 && jsonBytes[0] == 0xEF && jsonBytes[1] == 0xBB && jsonBytes[2] == 0xBF)
        {
            jsonBytes = jsonBytes[3..];
        }

        JsonObject root = JsonNode.Parse(jsonBytes)?.AsObject()
            ?? throw new ProviderVerificationInputException("input.pact.normalization-failed");
        foreach (JsonNode? interactionNode in root["interactions"]?.AsArray() ?? [])
        {
            _ = interactionNode?.AsObject().Remove("metadata");
        }

        string temporaryPath = Path.Combine(Path.GetTempPath(), $"eventstore-pact-{Guid.NewGuid():N}.json");
        File.WriteAllText(temporaryPath, root.ToJsonString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return temporaryPath;
    }

    private static async Task<string> RunIsolatedAsync(
        string pactPath,
        Uri baseAddress,
        string description,
        string providerState,
        TimeSpan requestTimeout)
    {
        string assemblyPath = typeof(PactInteractionVerifier).Assembly.Location;
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(assemblyPath);
        startInfo.ArgumentList.Add("--internal-verify");
        startInfo.ArgumentList.Add(pactPath);
        startInfo.ArgumentList.Add(baseAddress.AbsoluteUri);
        startInfo.ArgumentList.Add(description);
        startInfo.ArgumentList.Add(providerState);
        startInfo.ArgumentList.Add(Math.Ceiling(requestTimeout.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture));
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("verifier-process-unavailable");
        Task standardOutputDrain = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
        Task standardErrorDrain = process.StandardError.BaseStream.CopyToAsync(Stream.Null);
        TimeSpan terminationTimeout = TimeSpan.FromSeconds(2);
        using var timeout = new CancellationTokenSource(requestTimeout + terminationTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            if (!await WaitForTerminationAsync(
                process,
                standardOutputDrain,
                standardErrorDrain,
                terminationTimeout).ConfigureAwait(false))
            {
                return "interaction.verifier-termination-failed";
            }

            return "interaction.timeout";
        }

        if (!await DrainAsync(standardOutputDrain, standardErrorDrain, terminationTimeout).ConfigureAwait(false))
        {
            TryKill(process);
            return "interaction.verifier-drain-failed";
        }

        return process.ExitCode switch
        {
            0 => "interaction.passed",
            1 => "interaction.contract-failed",
            _ => "interaction.verifier-failed",
        };
    }

    private static async Task<bool> WaitForTerminationAsync(
        Process process,
        Task standardOutputDrain,
        Task standardErrorDrain,
        TimeSpan timeout)
    {
        try
        {
            await process.WaitForExitAsync().WaitAsync(timeout).ConfigureAwait(false);
            return await DrainAsync(standardOutputDrain, standardErrorDrain, timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static async Task<bool> DrainAsync(Task standardOutputDrain, Task standardErrorDrain, TimeSpan timeout)
    {
        try
        {
            await Task.WhenAll(standardOutputDrain, standardErrorDrain).WaitAsync(timeout).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The verifier exited between the bounded state check and kill request.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Report generation never includes the temporary path; cleanup scans surface leftovers.
        }
        catch (UnauthorizedAccessException)
        {
            // Report generation never includes the temporary path; cleanup scans surface leftovers.
        }
    }
}

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
        TimeSpan requestTimeout,
        string acceptedToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acceptedToken);
        coordinator.BeginInteraction(interaction.ProviderState);
        var stopwatch = Stopwatch.StartNew();
        string resultCode;
        try
        {
            string normalizedPact = CreateNormalizedPact(interaction, pactDirectory, acceptedToken);
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
                if (!TryDeleteNormalizedPact(normalizedPact, out string cleanupCode))
                {
                    throw new ProviderVerificationInputException(cleanupCode);
                }
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

    internal static string CreateNormalizedPact(
        InteractionDefinition interaction,
        string pactDirectory,
        string? acceptedToken = null)
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

        if (!string.IsNullOrWhiteSpace(acceptedToken))
        {
            ReplaceCredentialPlaceholder(root, interaction, acceptedToken);
        }

        string temporaryPath = Path.Combine(Path.GetTempPath(), $"eventstore-pact-{Guid.NewGuid():N}.json");
        byte[] normalizedBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            .GetBytes(root.ToJsonString());
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = 4096,
            Options = FileOptions.WriteThrough,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        try
        {
            using var stream = new FileStream(temporaryPath, options);
            stream.Write(normalizedBytes);
            stream.Flush(flushToDisk: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (File.Exists(temporaryPath)
                && !TryDeleteNormalizedPact(temporaryPath, out string cleanupCode))
            {
                throw new ProviderVerificationInputException(cleanupCode);
            }

            throw new ProviderVerificationInputException("input.pact.normalization-write-failed");
        }

        return temporaryPath;
    }

    /// <summary>
    /// Deletes one secret-bearing normalized Pact, retrying transient file-system failures.
    /// </summary>
    /// <param name="path">The normalized Pact path.</param>
    /// <param name="failureCode">A stable support-safe failure code when cleanup does not succeed.</param>
    /// <param name="deleteFile">An optional test seam for the delete operation.</param>
    /// <returns><see langword="true"/> when the file was deleted or no longer exists.</returns>
    internal static bool TryDeleteNormalizedPact(
        string path,
        out string failureCode,
        Action<string>? deleteFile = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        deleteFile ??= File.Delete;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                deleteFile(path);
                failureCode = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Retry a bounded number of times. No path or credential data is included in diagnostics.
            }
        }

        failureCode = "interaction.normalized-pact-cleanup-failed";
        return false;
    }

    private static void ReplaceCredentialPlaceholder(
        JsonObject root,
        InteractionDefinition interaction,
        string acceptedToken)
    {
        JsonObject[] selectedInteractions =
        [
            .. (root["interactions"]?.AsArray() ?? [])
                .OfType<JsonObject>()
                .Where(candidate => IsSelectedInteraction(candidate, interaction)),
        ];
        if (selectedInteractions.Length != 1)
        {
            throw new ProviderVerificationInputException("input.pact.selected-interaction-invalid");
        }

        JsonObject headers = selectedInteractions[0]["request"]?["headers"]?.AsObject()
            ?? throw new ProviderVerificationInputException("input.pact.authorization-header-invalid");
        string[] authorizationKeys =
        [
            .. headers.Select(static entry => entry.Key)
                .Where(static key => string.Equals(key, "Authorization", StringComparison.OrdinalIgnoreCase)),
        ];
        if (authorizationKeys.Length != 1
            || headers[authorizationKeys[0]] is not JsonValue authorizationValue
            || !authorizationValue.TryGetValue(out string? authorization)
            || !string.Equals(authorization, "Bearer FC_CONTRACT_TOKEN", StringComparison.Ordinal))
        {
            throw new ProviderVerificationInputException("input.pact.authorization-header-invalid");
        }

        headers[authorizationKeys[0]] = string.Concat("Bearer", " ", acceptedToken);
    }

    private static bool IsSelectedInteraction(JsonObject candidate, InteractionDefinition interaction)
        => string.Equals(candidate["description"]?.GetValue<string>(), interaction.Description, StringComparison.Ordinal)
            && candidate["providerStates"] is JsonArray providerStates
            && providerStates.Count == 1
            && string.Equals(
                providerStates[0]?["name"]?.GetValue<string>(),
                interaction.ProviderState,
                StringComparison.Ordinal);

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

}

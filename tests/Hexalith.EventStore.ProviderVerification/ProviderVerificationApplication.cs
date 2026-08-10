namespace Hexalith.EventStore.ProviderVerification;

internal static class ProviderVerificationApplication
{
    internal const int SuccessExitCode = 0;
    internal const int InputFailureExitCode = 2;
    internal const int IdentityFailureExitCode = 3;
    internal const int ContractFailureExitCode = 4;
    internal const int CleanupFailureExitCode = 5;
    internal const int ReportFailureExitCode = 6;

    public static async Task<int> RunAsync(string[] args)
    {
        if (!ProviderVerificationOptions.TryParse(args, out ProviderVerificationOptions? options, out string parseCode))
        {
            TryWriteMinimalReport(args, parseCode);
            return InputFailureExitCode;
        }

        return await RunAsync(options!, CancellationToken.None).ConfigureAwait(false);
    }

    internal static async Task<int> RunAsync(
        ProviderVerificationOptions options,
        CancellationToken cancellationToken,
        Func<ProviderStateCoordinator, string, TimeSpan, CancellationToken, ProviderVerificationTimeline, Task<ProviderVerificationHost>>? startHostAsync = null,
        Func<string>? findRepositoryRoot = null)
    {
        var timeline = new ProviderVerificationTimeline();
        var hostMetadata = new ProviderHostMetadata(
            "Kestrel",
            "production-gateway",
            "http",
            "IPv4",
            "loopback",
            "os-assigned-ephemeral");
        var reasons = new List<string>();
        var interactions = new List<InteractionVerificationResult>();
        VerificationInputs? inputs = null;
        ProviderVerificationHost? host = null;
        Uri? baseAddress = null;
        string reportOutputPath = options.ReportOutputPath;
        bool hostStarted = false;
        bool ready = false;
        bool hostStopped = false;
        bool portClosed = false;
        int exitCode = SuccessExitCode;
        string? repositoryRoot = null;
        try
        {
            if (!SafePath.TryResolveOutputFile(options.ReportOutputPath, out reportOutputPath, out string reportPathCode))
            {
                throw new ProviderVerificationInputException(reportPathCode);
            }

            findRepositoryRoot ??= FindRepositoryRoot;
            repositoryRoot = findRepositoryRoot();
            if (string.IsNullOrWhiteSpace(repositoryRoot))
            {
                throw new ProviderVerificationInputException("input.repository-root.unavailable");
            }

            inputs = VerificationInputLoader.Load(options, repositoryRoot);
            if (!inputs.Identity.ApprovalAuthorized || !inputs.Identity.RuntimeMatches)
            {
                reasons.AddRange(inputs.Identity.ReasonCodes);
                exitCode = IdentityFailureExitCode;
            }

            var coordinator = new ProviderStateCoordinator(inputs.ProviderStates);
            timeline.BeginStartup();
            startHostAsync ??= static (stateCoordinator, root, timeout, token, runTimeline) =>
                ProviderVerificationHost.StartAsync(stateCoordinator, root, timeout, token, runTimeline);
            host = await startHostAsync(
                coordinator,
                repositoryRoot,
                options.StartupTimeout,
                cancellationToken,
                timeline).ConfigureAwait(false);
            baseAddress = host.BaseAddress;
            hostStarted = true;
            ready = true;
            for (int index = 0; index < inputs.Interactions.Count; index++)
            {
                InteractionVerificationResult result = await PactInteractionVerifier.VerifyAsync(
                    index + 1,
                    inputs.Interactions[index],
                    inputs.PactDirectory,
                    host.BaseAddress,
                    coordinator,
                    options.RequestTimeout).ConfigureAwait(false);
                interactions.Add(result);
                if (result.ResultCode != "interaction.passed")
                {
                    reasons.Add("contract.interaction-failed");
                    exitCode = Math.Max(exitCode, ContractFailureExitCode);
                }
            }
        }
        catch (ProviderVerificationInputException exception)
        {
            reasons.Add(exception.Code);
            exitCode = InputFailureExitCode;
        }
        catch (OperationCanceledException)
        {
            reasons.Add("run.timeout");
            exitCode = ContractFailureExitCode;
        }
        catch (Exception)
        {
            reasons.Add("host.start-or-run.failed");
            exitCode = ContractFailureExitCode;
        }
        finally
        {
            timeline.CompletePendingFailures();
            if (!timeline.CleanupStarted)
            {
                timeline.BeginCleanup();
                if (host is not null)
                {
                    bool cleanupSucceeded = true;
                    try
                    {
                        await host.StopAsync(options.CleanupTimeout).ConfigureAwait(false);
                        hostStopped = true;
                    }
                    catch (Exception)
                    {
                        cleanupSucceeded = false;
                        reasons.Add("cleanup.host-stop.failed");
                        exitCode = CleanupFailureExitCode;
                    }

                    try
                    {
                        await host.DisposeAsync().AsTask().WaitAsync(options.CleanupTimeout).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        cleanupSucceeded = false;
                        reasons.Add("cleanup.host-dispose.failed");
                        exitCode = CleanupFailureExitCode;
                    }

                    portClosed = baseAddress is not null
                        && await ProviderVerificationHost.IsPortClosedAsync(baseAddress, options.CleanupTimeout).ConfigureAwait(false);
                    if (!portClosed)
                    {
                        cleanupSucceeded = false;
                        reasons.Add("cleanup.port-open");
                        exitCode = CleanupFailureExitCode;
                    }

                    timeline.MarkHostCleanup(hostStopped, portClosed);
                    timeline.CompleteCleanup(cleanupSucceeded ? "cleanup.succeeded" : "cleanup.failed");
                }
                else
                {
                    timeline.CompleteCleanup("cleanup.not-required");
                }
            }

            if (timeline.CleanupResultCode == "cleanup.failed")
            {
                reasons.Add("cleanup.failed");
                exitCode = CleanupFailureExitCode;
            }

            if (inputs is not null)
            {
                AppendNotRunResults(inputs.Interactions, interactions);
            }

            hostStarted = hostStarted || timeline.HostBound;
            ready = ready || timeline.ReadinessResultCode == "readiness.succeeded";
            hostStopped = hostStopped || timeline.HostStopped;
            portClosed = portClosed || timeline.PortClosed;
            int requested = inputs?.Interactions.Count ?? 0;
            int setupEvents = interactions.Sum(result => result.StateEvents.Count(item => item.Action == "setup"));
            int teardownEvents = interactions.Sum(result => result.StateEvents.Count(item => item.Action == "teardown"));
            bool complete = VerificationCompleteness.IsComplete(requested, interactions);
            if (requested > 0 && !complete)
            {
                reasons.Add("report.completeness.failed");
                exitCode = Math.Max(exitCode, ContractFailureExitCode);
            }

            if (!hostStopped && hostStarted)
            {
                reasons.Add("cleanup.host-not-stopped");
            }

            bool passed = exitCode == SuccessExitCode
                && complete
                && hostStopped
                && portClosed
                && inputs?.Identity.ApprovalAuthorized == true
                && inputs.Identity.RuntimeMatches;
            ProviderVerificationTiming timing = timeline.CompleteRun(
                passed ? "run.succeeded" : "run.failed");
            var report = new ProviderVerificationReport(
                "hexalith.eventstore.provider-verification.v1",
                passed ? "passed" : "failed",
                reasons.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                requested,
                interactions.Count,
                inputs?.ProviderStates.Count ?? 0,
                setupEvents,
                teardownEvents,
                complete,
                hostStarted,
                ready,
                hostStopped,
                portClosed,
                hostMetadata,
                timing,
                inputs?.Identity,
                inputs?.Hashes.OrderBy(item => item.Kind, StringComparer.Ordinal).ThenBy(item => item.Name, StringComparer.Ordinal).ToArray()
                    ?? [],
                interactions);
            if (!SafeReportWriter.TryWrite(reportOutputPath, report, out string reportFailureCode))
            {
                Console.Error.WriteLine(reportFailureCode);
                exitCode = ReportFailureExitCode;
            }
        }

        return exitCode;
    }

    internal static void AppendNotRunResults(
        IReadOnlyList<InteractionDefinition> requested,
        ICollection<InteractionVerificationResult> reported)
    {
        for (int index = reported.Count; index < requested.Count; index++)
        {
            InteractionDefinition interaction = requested[index];
            reported.Add(new InteractionVerificationResult(
                index + 1,
                interaction.Description,
                interaction.PactFile,
                interaction.ProviderState,
                "interaction.not-run",
                0,
                [
                    new ProviderStateEvent(
                        interaction.ProviderState,
                        "setup",
                        "state.setup.not-run",
                        0),
                    new ProviderStateEvent(
                        interaction.ProviderState,
                        "teardown",
                        "state.teardown.not-run",
                        0),
                ]));
        }
    }

    private static void TryWriteMinimalReport(string[] args, string reasonCode)
    {
        int index = Array.IndexOf(args, "--report-output");
        if (index < 0 || index + 1 >= args.Length)
        {
            return;
        }

        var timeline = new ProviderVerificationTimeline();
        timeline.BeginCleanup();
        timeline.CompleteCleanup("cleanup.not-required");
        var report = new ProviderVerificationReport(
            "hexalith.eventstore.provider-verification.v1",
            "failed",
            [reasonCode],
            0,
            0,
            0,
            0,
            0,
            false,
            false,
            false,
            false,
            false,
            new ProviderHostMetadata(
                "Kestrel",
                "production-gateway",
                "http",
                "IPv4",
                "loopback",
                "os-assigned-ephemeral"),
            timeline.CompleteRun("run.failed"),
            null,
            [],
            []);
        _ = SafeReportWriter.TryWrite(args[index + 1], report, out _);
    }

    internal static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new ProviderVerificationInputException("input.repository-root.unavailable");
    }
}

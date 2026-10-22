using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Actors;

/// <summary>Runs production HTTP actor admission against owned Redis/Dapr processes, including process replacement.</summary>
[Trait("Category", "LiveSidecar")]
public sealed class ActorCallbackAdmissionProcessTests
{
    /// <summary>Gets the optional destination for retaining the complete owned qualification evidence.</summary>
    public const string EvidenceDirectoryVariable = "HEXALITH_ACTOR_ADMISSION_EVIDENCE_DIRECTORY";

    [Fact]
    public async Task ProductionCallbacksRemainIsolatedAndReplayExactlyAfterProcessReplacementAsync()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "tools", "qualify-internal-actor-admission.py")))
            root = root.Parent;
        string repository = root?.FullName ?? throw new DirectoryNotFoundException("Cannot locate the owned actor admission runner.");
        string evidence = Environment.GetEnvironmentVariable(EvidenceDirectoryVariable)
            ?? Path.Combine(Path.GetTempPath(), "actor-admission-evidence-" + Guid.NewGuid().ToString("N"));
        var start = new ProcessStartInfo("python3")
        {
            WorkingDirectory = repository,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(Path.Combine(repository, "tools", "qualify-internal-actor-admission.py"));
        start.ArgumentList.Add("--evidence");
        start.ArgumentList.Add(evidence);
        start.ArgumentList.Add("--owner-pid");
        start.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Cannot launch the owned process qualification.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromMinutes(10));
        try
        {
            await process.WaitForExitAsync(bound.Token);
        }
        finally
        {
            if (!process.HasExited)
            {
                // SIGTERM lets the Python runner finish independent, bounded resource cleanup. Killing
                // the process tree would bypass container cleanup. Its owner monitor also covers test-host exit.
                var stop = new ProcessStartInfo("kill") { UseShellExecute = false };
                stop.ArgumentList.Add("-TERM");
                stop.ArgumentList.Add(process.Id.ToString(CultureInfo.InvariantCulture));
                using Process signal = Process.Start(stop) ?? throw new InvalidOperationException("Cannot signal qualification cleanup.");
                await signal.WaitForExitAsync(CancellationToken.None);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }

        string diagnostics = $"Evidence: {evidence}{Environment.NewLine}{await output}{Environment.NewLine}{await error}";
        process.ExitCode.ShouldBe(0, diagnostics);
        using JsonDocument result = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(evidence, "result.json"), CancellationToken.None));
        result.RootElement.GetProperty("qualification_passed").GetBoolean().ShouldBeTrue(diagnostics);
        result.RootElement.GetProperty("cleanup_passed").GetBoolean().ShouldBeTrue(diagnostics);
        result.RootElement.GetProperty("passed").GetBoolean().ShouldBeTrue(diagnostics);
    }
}

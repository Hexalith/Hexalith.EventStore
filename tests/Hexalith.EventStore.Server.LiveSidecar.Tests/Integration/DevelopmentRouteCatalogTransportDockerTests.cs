using System.Diagnostics;
using Hexalith.EventStore.Aspire;
using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Verifies extraction containers remain selectable by invocation cleanup while stopped.</summary>
public sealed class DevelopmentRouteCatalogTransportDockerTests
{
    /// <summary>Exposes stopped extraction containers to exact invocation cleanup while retaining transport ownership.</summary>
    [Fact(Timeout = 30000)]
    public async Task StoppedExtractionRetainsBothOwnersForTimeoutCleanup()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
        string owner = "eventstore-transport-" + Guid.NewGuid().ToString("N");
        string authenticationOwner = Environment.GetEnvironmentVariable("HEXALITH_PRIVATE_AUTH_OWNER") ?? Guid.NewGuid().ToString("N");
        string label = "hexalith.eventstore.authentication-test-owner=" + authenticationOwner;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(25));
        try
        {
            string identity = (await DockerAsync(DevelopmentRouteCatalogTransportDocker.ExtractionArguments(
                owner, "redis:7.4-alpine", "redis-server", authenticationOwner), deadline.Token)).Trim();
            identity.Length.ShouldBe(64);
            (await DockerAsync(["inspect", "--format", "{{.State.Status}}", identity], deadline.Token)).Trim().ShouldBe("created");
            string[] selected = (await DockerAsync(["ps", "--all", "--filter", "label=" + label,
                "--format", "{{.ID}}", "--no-trunc"], deadline.Token)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            selected.ShouldContain(identity);
            (await DockerAsync(["inspect", "--format", "{{index .Config.Labels \"hexalith.eventstore.transport\"}}", identity], deadline.Token)).Trim().ShouldBe(owner);
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            string[] remaining = (await DockerAsync(["ps", "--all", "--filter", "label=" + label,
                "--filter", "label=hexalith.eventstore.transport=" + owner, "--format", "{{.ID}}", "--no-trunc"], cleanup.Token)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (string identity in remaining)
            {
                (await DockerAsync(["inspect", "--format", "{{index .Config.Labels \"hexalith.eventstore.transport\"}}", identity], cleanup.Token)).Trim().ShouldBe(owner);
                await DockerAsync(["rm", "--force", identity], cleanup.Token);
            }
        }
    }

    /// <summary>Ignores absent or invalid authentication owners without altering the transport label.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("not-an-owner")]
    public void InvalidAuthenticationOwnersDoNotChangeTransportCustody(string? authenticationOwner)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
        string[] arguments = DevelopmentRouteCatalogTransportDocker.ExtractionArguments("transport-owner", "image", "placement", authenticationOwner);
        arguments.ShouldContain("hexalith.eventstore.transport=transport-owner");
        arguments.ShouldNotContain(value => value.StartsWith("hexalith.eventstore.authentication-test-owner=", StringComparison.Ordinal));
    }

    private static async Task<string> DockerAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("docker") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Docker could not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(cancellationToken); }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(output, errors);
            throw;
        }
        string result = await output;
        string error = await errors;
        if (process.ExitCode != 0) throw new InvalidOperationException("Owned Docker control failed: " + error);
        return result;
    }
}

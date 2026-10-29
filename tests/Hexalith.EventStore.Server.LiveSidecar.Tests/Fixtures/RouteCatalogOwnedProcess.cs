using System.Diagnostics;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Bounds only an owned infrastructure process and kills its complete tree on cancellation.</summary>
internal static class RouteCatalogOwnedProcess
{
    internal static async Task<string> RunAsync(ProcessStartInfo start, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        start.UseShellExecute = false;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start owned fixture infrastructure tooling.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(), errors = process.StandardError.ReadToEndAsync();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try { await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false); }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(cleanup.Token).ConfigureAwait(false);
            throw;
        }
        if (process.ExitCode != 0)
            throw new InvalidOperationException("Owned fixture infrastructure tooling failed: " + await errors.ConfigureAwait(false));
        return await output.ConfigureAwait(false);
    }
}

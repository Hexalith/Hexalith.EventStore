using System.Diagnostics;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Bounds an owned infrastructure process and waits for its killed descendants before returning cancellation.</summary>
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
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            string stdout = await output.WaitAsync(deadline.Token).ConfigureAwait(false);
            string stderr = await errors.WaitAsync(deadline.Token).ConfigureAwait(false);
            if (process.ExitCode != 0) throw new InvalidOperationException("Owned fixture infrastructure tooling failed: " + stderr);
            return stdout;
        }
        catch
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var descendants = CaptureDescendants(process.Id);
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cleanup.Token).ConfigureAwait(false);
            // WaitForExitAsync acknowledges only the parent after Kill(entireProcessTree:true).
            // Pipe closure also does not prove a child has exited: a child may close both pipes first.
            while (descendants.Any(StillAlive)) await Task.Delay(TimeSpan.FromMilliseconds(10), cleanup.Token).ConfigureAwait(false);
            await Task.WhenAll(output, errors).WaitAsync(cleanup.Token).ConfigureAwait(false);
            throw;
        }
    }

    private static IReadOnlyList<OwnedDescendant> CaptureDescendants(int owner)
    {
        if (!OperatingSystem.IsLinux()) return [];
        var processes = Directory.EnumerateDirectories("/proc").Select(path => int.TryParse(Path.GetFileName(path), out int pid)
            ? Read(pid) : null).OfType<OwnedDescendant>().ToArray();
        var owned = new HashSet<int> { owner };
        var descendants = new List<OwnedDescendant>();
        bool changed;
        do
        {
            changed = false;
            foreach (var process in processes)
                if (owned.Contains(process.Parent) && owned.Add(process.Pid)) { descendants.Add(process); changed = true; }
        } while (changed);
        return descendants;
    }

    private static bool StillAlive(OwnedDescendant captured)
        => Read(captured.Pid) is { } current && current.StartTime == captured.StartTime && current.State != "Z";

    private static OwnedDescendant? Read(int pid)
    {
        try
        {
            string stat = File.ReadAllText("/proc/" + pid + "/stat");
            // comm is parenthesized and may itself contain spaces or closing parentheses.
            string[] fields = stat[(stat.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return new(pid, int.Parse(fields[1], System.Globalization.CultureInfo.InvariantCulture), fields[19], fields[0]);
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private sealed record OwnedDescendant(int Pid, int Parent, string StartTime, string State);
}

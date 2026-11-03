using System.Diagnostics;
using System.Text.Json;

namespace Hexalith.EventStore.Aspire;

/// <summary>Retains an exact owned native process and bounds its shutdown; diagnostic bytes stay private.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
internal sealed class DevelopmentRouteCatalogTransportProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly Task<string> _output;
    private readonly Task<string> _errors;
    private readonly string _logPath;
    private bool _disposed;

    /// <summary>Starts one native owner and captures its exact process and kernel identities.</summary>
    /// <param name="start">The private native launch definition.</param>
    /// <param name="logPath">The private diagnostic path retained during shutdown.</param>
    /// <param name="receiptPrefix">The private custody prefix persisted before waiting for service readiness.</param>
    internal DevelopmentRouteCatalogTransportProcess(ProcessStartInfo start, string logPath, string receiptPrefix)
    {
        start.UseShellExecute = false;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        _logPath = logPath;
        _process = Process.Start(start) ?? throw new InvalidOperationException("Owned Development transport could not start.");
        _output = _process.StandardOutput.ReadToEndAsync();
        _errors = _process.StandardError.ReadToEndAsync();
        Id = _process.Id;
        try
        {
            DevelopmentRouteCatalogTransport.WritePrivate(receiptPrefix + ".pid.json", JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, pid = Id }));
            StartTime = _process.StartTime.ToUniversalTime();
            string stat = File.ReadAllText("/proc/" + Id + "/stat");
            NativeStart = stat[(stat.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries)[19];
            DevelopmentRouteCatalogTransport.WritePrivate(receiptPrefix + ".identity.json", JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1, pid = Id, start = NativeStart, startTimeUtc = StartTime,
            }));
        }
        catch
        {
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);
            _process.WaitForExit(5000);
            _process.Dispose();
            throw;
        }
    }

    /// <summary>The captured process ID, never used without the retained process handle.</summary>
    internal int Id { get; }

    /// <summary>The captured native start instant.</summary>
    internal DateTime StartTime { get; }

    /// <summary>The captured kernel start counter used to distinguish PID reuse.</summary>
    internal string NativeStart { get; }

    /// <summary>Refuses an exited owner before any child launch.</summary>
    internal void RequireRunning()
    {
        if (_process.HasExited) throw new InvalidOperationException("Owned Development transport is unavailable; no unauthenticated fallback is permitted.");
    }

    /// <summary>Stops only this retained process tree, then retains private diagnostics.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (!_process.HasExited) _process.Kill(entireProcessTree: true);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
        string output = await _output.WaitAsync(deadline.Token).ConfigureAwait(false);
        string errors = await _errors.WaitAsync(deadline.Token).ConfigureAwait(false);
        await File.WriteAllTextAsync(_logPath, output + errors, deadline.Token).ConfigureAwait(false);
        File.SetUnixFileMode(_logPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        _process.Dispose();
        _disposed = true;
    }
}

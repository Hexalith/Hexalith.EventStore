using System.Text;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Drains one process pipe while retaining at most a fixed number of characters.</summary>
internal sealed class BoundedProcessOutput
{
    /// <summary>The maximum retained characters per redirected pipe.</summary>
    internal const int Limit = 4 * 1024 * 1024;
    private readonly TaskCompletionSource _overflow = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly StringBuilder _retained = new();
    private readonly object _gate = new();

    /// <summary>Gets the bounded pipe-drain task.</summary>
    internal Task<string> Completion { get; }

    /// <summary>Gets a signal emitted immediately when the retained limit is exceeded.</summary>
    internal Task Overflow => _overflow.Task;

    /// <summary>Gets whether any observed output exceeded the retained limit.</summary>
    internal bool Overflowed => _overflow.Task.IsCompleted;

    /// <summary>Starts draining the pipe before the process can block on output.</summary>
    internal BoundedProcessOutput(StreamReader reader) => Completion = DrainAsync(reader);

    /// <summary>Gets the bounded prefix even when an inherited pipe has not closed.</summary>
    internal string Snapshot()
    {
        lock (_gate) return _retained.ToString();
    }

    private async Task<string> DrainAsync(StreamReader reader)
    {
        char[] buffer = new char[8192];
        int count;
        while ((count = await reader.ReadAsync(buffer)) != 0)
        {
            lock (_gate)
            {
                int remaining = Limit - _retained.Length;
                _retained.Append(buffer, 0, Math.Min(count, remaining));
                if (count > remaining) _overflow.TrySetResult();
            }
        }
        return Snapshot();
    }
}

using System.Text;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Streams command output into a fixed character budget, preserving exact small results.</summary>
internal static class BoundedCommandCapture
{
    /// <summary>Consumes a stream without materializing unbounded output.</summary>
    /// <param name="reader">The redirected command stream.</param>
    /// <param name="maximum">The maximum retained characters.</param>
    /// <param name="cancellationToken">The command deadline.</param>
    /// <returns>Exact output when it fits, otherwise a marked bounded tail.</returns>
    internal static async Task<string> ReadAsync(TextReader reader, int maximum, CancellationToken cancellationToken)
    {
        char[] buffer = new char[2048];
        var retained = new StringBuilder();
        bool truncated = false;
        while (true)
        {
            int count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0) break;
            retained.Append(buffer, 0, count);
            if (retained.Length > maximum)
            {
                retained.Remove(0, retained.Length - maximum);
                truncated = true;
            }
        }

        return truncated ? "<truncated>\n" + retained : retained.ToString();
    }
}

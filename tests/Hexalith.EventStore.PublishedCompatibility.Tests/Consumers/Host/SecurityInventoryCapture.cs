using System.Text.Json;

namespace PublishedConsumer;

/// <summary>Retains only contemporaneous committed-byte digests from the actual protected fixture process.</summary>
internal static class SecurityInventoryCapture
{
    private static readonly object _gate = new();

    internal static void Record(string code, string phase, DateTimeOffset began, IReadOnlyDictionary<string, string?> hashes)
    {
        string path = Environment.GetEnvironmentVariable("P1R_SECURITY_INVENTORY_PATH")
            ?? throw new InvalidOperationException("The protected fixture requires its owner-private state capture.");
        string owner = Environment.GetEnvironmentVariable("P1R_QUALIFICATION_OWNER")!;
        if (!Path.IsPathFullyQualified(path) || Path.GetFileName(Path.GetDirectoryName(path)) != owner
            || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("The protected state capture left its actual owner.");
        string line = JsonSerializer.Serialize(new { owner, pid = Environment.ProcessId, code, phase, began, ended = DateTimeOffset.UtcNow, hashes });
        lock (_gate)
        {
            using var file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(file);
            writer.WriteLine(line);
        }
    }
}

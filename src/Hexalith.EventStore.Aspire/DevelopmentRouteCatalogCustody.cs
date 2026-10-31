using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.EventStore.Aspire;

/// <summary>Owns an exclusive private profile and immutable, hash-bound stopped-composition generations.</summary>
internal sealed class DevelopmentRouteCatalogCustody : IDisposable
{
    private readonly FileStream _ownership;
    private readonly DevelopmentRouteCatalogCompositionOptions _options;
    private bool _disposed;

    internal DevelopmentRouteCatalogCustody(DevelopmentRouteCatalogCompositionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options with { DigestKeys = new Dictionary<string, string>(options.DigestKeys, StringComparer.Ordinal) };
        if (!Path.IsPathFullyQualified(options.CustodyDirectory)
            || !Identifier(options.AuthorityComponentName) || !Identifier(options.AuthorityStateKey)
            || !Identifier(options.ActiveDigestKeyGeneration) || !Identifier(options.SecretMapGeneration)
            || !Identifier(options.SecretStore) || !Identifier(options.LogicalSecretName)
            || options.DigestKeys.Count is < 1 or > 32 || !options.DigestKeys.ContainsKey(options.ActiveDigestKeyGeneration)
            || options.DigestKeys.Any(pair => !Identifier(pair.Key) || !ValidKey(pair.Value)))
            throw new InvalidOperationException("An absolute private owner profile and complete retained digest ring are required; restore the existing profile or select an owned isolated backend.");
        if (OperatingSystem.IsWindows()) System.IO.Directory.CreateDirectory(options.CustodyDirectory);
        else System.IO.Directory.CreateDirectory(options.CustodyDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        RequirePrivate(options.CustodyDirectory, directory: true);
        _ownership = new FileStream(Path.Combine(options.CustodyDirectory, "owner.lock"), PrivateFile(FileMode.OpenOrCreate));
        try
        {
            string binding = Path.Combine(options.CustodyDirectory, "owner.json");
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(_options);
            if (File.Exists(binding))
            {
                RequirePrivate(binding);
                var previous = JsonSerializer.Deserialize<DevelopmentRouteCatalogCompositionOptions>(ReadBounded(binding))
                    ?? throw Refusal();
                if (previous.CustodyDirectory != options.CustodyDirectory || previous.AuthorityComponentName != options.AuthorityComponentName
                    || previous.AuthorityStateKey != options.AuthorityStateKey || previous.ActiveDigestKeyGeneration != options.ActiveDigestKeyGeneration
                    || previous.SecretMapGeneration != options.SecretMapGeneration || previous.SecretStore != options.SecretStore
                    || previous.LogicalSecretName != options.LogicalSecretName || previous.DigestKeys.Count != options.DigestKeys.Count
                    || options.DigestKeys.Any(pair => !previous.DigestKeys.TryGetValue(pair.Key, out string? value) || value != pair.Value))
                    throw new InvalidOperationException("Retained owner identity or digest material changed. Restore the existing profile; key rotation requires a separately qualified transition with retained readers.");
            }
            else WriteNew(binding, bytes);
            string currentPath = Path.Combine(options.CustodyDirectory, "current.json");
            if (!File.Exists(currentPath)) return;
            RequirePrivate(currentPath);
            var current = JsonSerializer.Deserialize<Dictionary<string, string>>(ReadBounded(currentPath)) ?? throw Refusal();
            if (!current.TryGetValue("generation", out string? generation) || !Identifier(generation)) throw Refusal();
            string retained = Path.Combine(options.CustodyDirectory, generation);
            RequirePrivate(retained, directory: true);
            if (current.Count > 4096 || new[] { "catalog.json", "activation.json", "authority.backend.json", "authority.component.json", "authority.dependencies.json" }
                .Any(required => !current.ContainsKey(required))
                || !System.IO.Directory.EnumerateFileSystemEntries(retained).Select(Path.GetFileName).Order(StringComparer.Ordinal)
                    .SequenceEqual(current.Keys.Where(key => key != "generation").Order(StringComparer.Ordinal), StringComparer.Ordinal)) throw Refusal();
            foreach (var pair in current.Where(pair => pair.Key != "generation"))
            {
                if (Path.GetFileName(pair.Key) != pair.Key || pair.Value.Length != 64 || pair.Value.Any(character => !char.IsAsciiHexDigitLower(character))) throw Refusal();
                string file = Path.Combine(retained, pair.Key); RequirePrivate(file);
                if (Convert.ToHexStringLower(SHA256.HashData(ReadBounded(file))) != pair.Value) throw Refusal();
            }
            PredecessorBytes = ReadBounded(Path.Combine(retained, "catalog.json"));
            var activation = JsonSerializer.Deserialize<RouteCatalogActivation>(ReadBounded(Path.Combine(retained, "activation.json"))) ?? throw Refusal();
            if (!activation.CanonicalCatalog.AsSpan().SequenceEqual(PredecessorBytes)) throw Refusal();
            PredecessorRootDigest = activation.RootDigest;
            _ = RouteCatalogCodec.Decode(PredecessorBytes, PredecessorRootDigest);
            BackendBytes = ReadBounded(Path.Combine(retained, "authority.backend.json"));
        }
        catch { _ownership.Dispose(); throw; }
    }

    internal byte[]? PredecessorBytes { get; }
    internal string? PredecessorRootDigest { get; }
    internal byte[]? BackendBytes { get; }

    internal void Retain(string runDirectory)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DevelopmentRouteCatalogCustody));
        string activationPath = Path.Combine(runDirectory, "activation.json");
        if (!File.Exists(activationPath)) return; // Failed initial starts do not manufacture committed custody.
        var activation = JsonSerializer.Deserialize<RouteCatalogActivation>(ReadBounded(activationPath)) ?? throw Refusal();
        foreach (var host in activation.RequiredHosts)
        {
            string receipt = Path.Combine(runDirectory, host.HostId + ".committed.json");
            if (!File.Exists(receipt)) throw new InvalidOperationException("The stopped cohort has no complete committed custody; keep its private run directory for owner recovery.");
            var readback = JsonSerializer.Deserialize<RouteCatalogGenerationReadback>(ReadBounded(receipt)) ?? throw Refusal();
            if (!readback.Serving || readback.RootDigest != activation.RootDigest || !readback.RequiredHosts.Contains(host)) throw Refusal();
        }
        string name = "generation-" + Guid.NewGuid().ToString("N");
        string target = Path.Combine(_options.CustodyDirectory, name);
        if (OperatingSystem.IsWindows()) System.IO.Directory.CreateDirectory(target);
        else System.IO.Directory.CreateDirectory(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Dictionary<string, string> inventory = new(StringComparer.Ordinal) { ["generation"] = name };
        foreach (string source in System.IO.Directory.EnumerateFiles(runDirectory).Where(file => !file.EndsWith(".tmp", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            RequirePrivate(source);
            byte[] bytes = ReadBounded(source);
            string filename = Path.GetFileName(source);
            WriteNew(Path.Combine(target, filename), bytes);
            inventory.Add(filename, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }
        if (!inventory.ContainsKey("authority.backend.json") || !inventory.ContainsKey("authority.component.json")) throw Refusal();
        string pending = Path.Combine(_options.CustodyDirectory, "current-" + Guid.NewGuid().ToString("N") + ".tmp");
        WriteNew(pending, JsonSerializer.SerializeToUtf8Bytes(inventory));
        File.Move(pending, Path.Combine(_options.CustodyDirectory, "current.json"), overwrite: true);
    }

    public void Dispose() { if (_disposed) return; _disposed = true; _ownership.Dispose(); }

    internal static void WriteNew(string path, byte[] bytes) { using var stream = new FileStream(path, PrivateFile(FileMode.CreateNew)); stream.Write(bytes); }
    internal static void RequirePrivate(string path, bool directory = false)
    {
        FileSystemInfo entry = directory ? new DirectoryInfo(path) : new FileInfo(path);
        if (!entry.Exists || entry.LinkTarget is not null || (entry.Attributes & FileAttributes.ReparsePoint) != 0
            || !OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0) throw Refusal();
    }
    private static FileStreamOptions PrivateFile(FileMode mode)
    {
        var options = new FileStreamOptions { Mode = mode, Access = FileAccess.ReadWrite, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return options;
    }
    private static byte[] ReadBounded(string path) => new FileInfo(path).Length <= 16 * 1024 * 1024 ? File.ReadAllBytes(path) : throw Refusal();
    private static bool Identifier(string? value) => value is { Length: > 0 and <= 128 } && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
    private static bool ValidKey(string value) { try { return Convert.FromBase64String(value).Length >= 32; } catch (FormatException) { return false; } }
    private static InvalidOperationException Refusal() => new("Owner catalog custody is incomplete, changed or not private. Restore the exact retained profile and fleet before starting.");
}

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Testing.CatalogInvocation;

/// <summary>Private, link-free, bounded atomic IPC; files are technical test data only.</summary>
internal sealed class CatalogInvocationFiles
{
    /// <summary>The maximum serialized private message size.</summary>
    internal const int MaximumBytes = 8 * 1024 * 1024;
    /// <summary>Closed request/result serialization with unknown fields refused.</summary>
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private readonly string _directory;
    private readonly ulong _inode;
    private readonly ulong _device;

    /// <summary>Captures the existing private directory identity and verifies its link-free ancestry.</summary>
    /// <param name="directory">The canonical existing private directory.</param>
    internal CatalogInvocationFiles(string directory)
    {
        if (!OperatingSystem.IsLinux() || !TestEnvironmentDescriptorFile.IsLocalAbsolutePath(directory)
            || Path.GetFullPath(directory) != directory.TrimEnd(Path.DirectorySeparatorChar))
            throw new IOException("Private catalog IPC requires a canonical absolute Linux path.");
        _directory = directory;
        CheckParents();
        byte[] identity = Stat(directory);
        RequirePrivate(identity, directory: true);
        _inode = BitConverter.ToUInt64(identity, 32);
        _device = BitConverter.ToUInt64(identity, 136);
    }

    /// <summary>Resolves one validated identity only beneath the unchanged private directory.</summary>
    /// <param name="id">The one-shot random identity.</param>
    /// <param name="extension">The protocol-owned file suffix.</param>
    /// <returns>The exact private protocol path.</returns>
    internal string PathFor(string id, string extension)
    {
        if (id.Length != 32 || id.Any(character => !char.IsAsciiHexDigitLower(character)))
            throw new IOException("Invalid one-shot request identity.");
        Check();
        return Path.Combine(_directory, id + extension);
    }

    /// <summary>Observes pending atomically published requests in stable order.</summary>
    /// <returns>The private request paths.</returns>
    internal string[] Pending()
    {
        Check();
        return System.IO.Directory.GetFiles(_directory, "*.request").Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Claims one private regular request by atomic rename without replacing a prior claim.</summary>
    /// <param name="id">The one-shot request identity.</param>
    internal void Claim(string id)
    {
        string path = PathFor(id, ".request");
        RequirePrivate(Stat(path), directory: false);
        File.Move(path, PathFor(id, ".working"), overwrite: false);
    }

    /// <summary>Reads a bounded private regular message and refuses changed identity or unknown JSON fields.</summary>
    /// <typeparam name="T">The closed protocol message type.</typeparam>
    /// <param name="id">The one-shot identity.</param>
    /// <param name="extension">The protocol-owned suffix.</param>
    /// <param name="token">The complete operation deadline.</param>
    /// <returns>The verified deserialized message.</returns>
    internal async Task<T> ReadAsync<T>(string id, string extension, CancellationToken token)
    {
        string path = PathFor(id, extension);
        byte[] before = Stat(path);
        RequirePrivate(before, directory: false);
        using FileStream stream = TestEnvironmentDescriptorFile.OpenWithoutBlocking(path);
        if (!stream.CanSeek || stream.Length is < 2 or > MaximumBytes) throw new IOException("Private IPC size refused.");
        byte[] bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
        byte[] after = Stat(path);
        if (BitConverter.ToUInt64(before, 32) != BitConverter.ToUInt64(after, 32)
            || BitConverter.ToUInt64(before, 136) != BitConverter.ToUInt64(after, 136)) throw new IOException("Private IPC identity changed.");
        RequirePrivate(after, directory: false);
        Check();
        return JsonSerializer.Deserialize<T>(bytes, Json) ?? throw new IOException("Private IPC body missing.");
    }

    /// <summary>Publishes a bounded private message using create-new bytes and atomic rename.</summary>
    /// <typeparam name="T">The closed protocol message type.</typeparam>
    /// <param name="id">The one-shot identity.</param>
    /// <param name="extension">The protocol-owned suffix.</param>
    /// <param name="value">The private message.</param>
    /// <param name="token">The complete operation deadline.</param>
    internal async Task WriteAsync<T>(string id, string extension, T value, CancellationToken token)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        if (bytes.Length > MaximumBytes) throw new IOException("Private IPC size refused.");
        string temporary = PathFor(id, extension + ".tmp");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write,
            Share = FileShare.None, Options = FileOptions.Asynchronous };
        if (OperatingSystem.IsLinux()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        var stream = new FileStream(temporary, options);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(bytes, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }
        Check();
        File.Move(temporary, PathFor(id, extension), overwrite: false);
    }

    private void Check()
    {
        CheckParents();
        byte[] identity = Stat(_directory);
        RequirePrivate(identity, directory: true);
        if (BitConverter.ToUInt64(identity, 32) != _inode || BitConverter.ToUInt64(identity, 136) != _device)
            throw new IOException("Private IPC directory replaced.");
    }

    private void CheckParents()
    {
        for (DirectoryInfo? item = new(_directory); item is not null; item = item.Parent)
            if (!item.Exists || item.LinkTarget is not null || (item.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Private IPC ancestors must be existing directories without links.");
    }

    private static void RequirePrivate(byte[] stat, bool directory)
    {
        ushort mode = BitConverter.ToUInt16(stat, 28);
        if (BitConverter.ToUInt32(stat, 20) != GetEffectiveUserId() || (mode & 0x3f) != 0
            || (mode & 0xf000) != (directory ? 0x4000 : 0x8000)
            || (!directory && BitConverter.ToUInt32(stat, 16) != 1))
            throw new IOException("Private IPC ownership, permissions or file type refused.");
    }

    private static byte[] Stat(string path)
    {
        byte[] buffer = new byte[256];
        if (Statx(-100, path, 0x100, 0x7ff, buffer) != 0) throw new IOException("Private IPC identity observation failed.");
        return buffer;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054:Use LibraryImportAttribute instead of DllImportAttribute", Justification = "The fixed Linux statx record is read without enabling unsafe code.")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int Statx(int directory, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, uint mask, [Out] byte[] buffer);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054:Use LibraryImportAttribute instead of DllImportAttribute", Justification = "Read the current Linux uid without enabling unsafe code.")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint GetEffectiveUserId();
}

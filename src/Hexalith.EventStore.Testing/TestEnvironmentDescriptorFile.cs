using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace Hexalith.EventStore.Testing;

/// <summary>Opens private descriptor files without following final links or blocking on FIFOs.</summary>
internal static class TestEnvironmentDescriptorFile
{
    /// <summary>Rejects UNC and device namespace paths before any filesystem operation.</summary>
    /// <param name="path">The explicit path supplied through the local private channel.</param>
    /// <returns>Whether the path is fully qualified and uses an ordinary local path namespace.</returns>
    internal static bool IsLocalAbsolutePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        string normalized = path.Replace('\\', '/');
        return !normalized.StartsWith("//", StringComparison.Ordinal)
            && !normalized.StartsWith("/??/", StringComparison.Ordinal)
            && !normalized.StartsWith("/Device/", StringComparison.OrdinalIgnoreCase)
            && Path.IsPathFullyQualified(path);
    }

    /// <summary>Opens a held read handle without following final links or blocking on a Unix FIFO.</summary>
    /// <param name="path">The absolute local descriptor path.</param>
    /// <returns>The held stream; the reader verifies its seekability and bounded size.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The returned FileStream owns the SafeFileHandle; constructor failure disposes it explicitly.")]
    internal static FileStream OpenWithoutBlocking(string path)
    {
        if (!IsLocalAbsolutePath(path)) throw new IOException("Require an ordinary absolute local metadata path.");
        if (OperatingSystem.IsWindows())
        {
            // OPEN_REPARSE_POINT opens the final entry itself; inspect that held handle before reading it.
            SafeFileHandle windowsHandle = CreateFile(path, 0x80000000, FileShare.Read | FileShare.Delete, nint.Zero, FileMode.Open, 0x00200000 | 0x40000000, nint.Zero);
            try
            {
                byte[] attributes = new byte[8];
                bool unverified = windowsHandle.IsInvalid || !GetFileInformationByHandleEx(windowsHandle, 9, attributes, (uint)attributes.Length)
                    || (BitConverter.ToUInt32(attributes) & (uint)FileAttributes.ReparsePoint) != 0;
                return unverified
                    ? throw new IOException("Runner metadata could not be opened without following a link.")
                    : new FileStream(windowsHandle, FileAccess.Read, 8192, isAsync: true);
            }
            catch
            {
                windowsHandle.Dispose();
                throw;
            }
        }

        int flags = OperatingSystem.IsMacOS() ? 0x4 | 0x100 | 0x1000000 : 0x800 | LinuxNoFollow(RuntimeInformation.ProcessArchitecture) | 0x80000;
        int descriptor = Open(path, flags);
        if (descriptor < 0)
        {
            throw new IOException("The selected report could not be opened safely.");
        }

        SafeFileHandle handle = new(descriptor, ownsHandle: true);
        try
        {
            while (Flock(descriptor, 1 | 4) != 0)
            {
                if (Marshal.GetLastPInvokeError() != 4)
                {
                    throw new IOException("Runner metadata sharing cannot be verified.");
                }
            }

            return new FileStream(handle, FileAccess.Read, 8192, isAsync: false);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    /// <summary>Gets the Linux <c>O_NOFOLLOW</c> value, which differs by architecture.</summary>
    /// <param name="architecture">The current process architecture.</param>
    /// <returns>The architecture's no-follow open flag; on Arm and PowerPC <c>0x20000</c> is <c>O_LARGEFILE</c>.</returns>
    internal static int LinuxNoFollow(Architecture architecture) =>
        architecture is Architecture.Arm or Architecture.Arm64 or Architecture.Armv6 or Architecture.Ppc64le ? 0x8000 : 0x20000;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054:Use LibraryImportAttribute instead of DllImportAttribute", Justification = "The safe Windows handle is opened directly without enabling generated unsafe code.")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, FileShare share, nint securityAttributes, FileMode creationDisposition, uint flags, nint template);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054:Use LibraryImportAttribute instead of DllImportAttribute", Justification = "The fixed eight-byte attribute record is inspected through the existing held safe handle.")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int informationClass, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] information, uint bufferSize);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054:Use LibraryImportAttribute instead of DllImportAttribute", Justification = "This small nonblocking Unix open does not require enabling unsafe generated code.")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2101:Specify marshaling for P/Invoke string arguments", Justification = "Unix open requires UTF-8 bytes, explicitly marshaled as LPUTF8Str.")]
    [DllImport("libc", EntryPoint = "open", SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054:Use LibraryImportAttribute instead of DllImportAttribute", Justification = "This nonblocking Unix sharing check uses the existing safe descriptor without generated unsafe code.")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("libc", EntryPoint = "flock", SetLastError = true)]
    private static extern int Flock(int descriptor, int operation);

}

using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

using Probe.Declared;

namespace Probe.Plugin;

/// <summary>Exercises normal resolution and explicit context/native escapes in owned fixture processes.</summary>
public static class Entry
{
    /// <summary>Runs one requested scenario; all effects use the runner's private temporary path.</summary>
    public static void Run(string mode, string managedPath, string nativePath, string effectsPath,
        Action<string> record)
    {
        record("plugin-entered");
        switch (mode)
        {
            case "pinned-managed":
                DeclaredEffect.Write(effectsPath);
                record("declared-effect-returned");
                break;
            case "ordinary-undeclared-managed":
                AssemblyLoadContext owner = AssemblyLoadContext.GetLoadContext(typeof(Entry).Assembly)!;
                ExecuteForeign(owner.LoadFromAssemblyName(new AssemblyName("Probe.Undeclared")), effectsPath);
                break;
            case "explicit-default-managed":
                ExecuteForeign(AssemblyLoadContext.Default.LoadFromAssemblyPath(managedPath), effectsPath);
                record("foreign-effect-returned");
                break;
            case "explicit-other-managed":
                var other = new AssemblyLoadContext("fixture-explicit-other", isCollectible: true);
                try
                {
                    ExecuteForeign(other.LoadFromAssemblyPath(managedPath), effectsPath);
                    record("foreign-effect-returned");
                }
                finally
                {
                    other.Unload();
                }

                break;
            case "ordinary-undeclared-native":
                _ = NativeWrite(effectsPath);
                break;
            case "explicit-native":
                IntPtr handle = NativeLibrary.Load(nativePath);
                record("native-load-returned");
                try
                {
                    IntPtr export = NativeLibrary.GetExport(handle, "probe_write");
                    var write = Marshal.GetDelegateForFunctionPointer<NativeWriteDelegate>(export);
                    if (write(effectsPath) != 0)
                    {
                        throw new IOException("Owned native marker write failed.");
                    }

                    record("native-export-returned");
                }
                finally
                {
                    NativeLibrary.Free(handle);
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode));
        }

        record("plugin-returned");
    }

    private static void ExecuteForeign(Assembly assembly, string effectsPath)
        => assembly.GetType("Probe.Undeclared.UndeclaredEffect", throwOnError: true)!
            .GetMethod("Write", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [effectsPath]);

    [DllImport("probe_unmanifested", EntryPoint = "probe_write", CallingConvention = CallingConvention.Cdecl)]
    private static extern int NativeWrite([MarshalAs(UnmanagedType.LPUTF8Str)] string effectsPath);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NativeWriteDelegate([MarshalAs(UnmanagedType.LPUTF8Str)] string effectsPath);
}

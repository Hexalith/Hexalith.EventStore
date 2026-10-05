using System.Runtime.InteropServices;

namespace Probe.Plugin;

/// <summary>Calls the owned native fixture export with the runner's temporary effect-file path.</summary>
/// <param name="effectsPath">The runner-owned path receiving the marker.</param>
/// <returns>Zero when the owned marker was written; otherwise a fixture write failure.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int NativeWriteDelegate([MarshalAs(UnmanagedType.LPUTF8Str)] string effectsPath);

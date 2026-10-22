using System.Runtime.InteropServices;

namespace Hexalith.EventStore.Server.Tests.Projections;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate nint BrotliAllocatorCallback(nint opaque, nuint size);

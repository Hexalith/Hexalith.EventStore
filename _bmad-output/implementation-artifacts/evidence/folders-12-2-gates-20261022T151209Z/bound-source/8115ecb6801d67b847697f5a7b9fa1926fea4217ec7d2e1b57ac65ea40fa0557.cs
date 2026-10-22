using System.Runtime.InteropServices;

namespace Hexalith.EventStore.Server.Tests.Projections;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate nuint NativeAllocationCapacity(nint address);

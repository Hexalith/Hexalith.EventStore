using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>Counts the real fixture BrotliStream decoder's native buffers through its owning allocator callbacks.</summary>
internal sealed class ObservedBrotliDecoderAllocation : IDisposable
{
    private readonly BrotliAllocatorCallback _allocate;
    private readonly BrotliFreeCallback _free;
    private readonly Dictionary<nint, long> _buffers = [];
    private readonly nint _library;
    private readonly nint _allocatorLibrary;
    private long _current;
    internal long PeakBytes { get; private set; }

    internal ObservedBrotliDecoderAllocation(BrotliStream stream)
    {
        string library = OperatingSystem.IsWindows() ? "System.IO.Compression.Native.dll"
            : OperatingSystem.IsMacOS() ? "libSystem.IO.Compression.Native.dylib" : "libSystem.IO.Compression.Native.so";
        _library = NativeLibrary.Load(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), library));
        NativeAllocationCapacity? capacity = null;
        if (OperatingSystem.IsLinux())
        {
            _allocatorLibrary = NativeLibrary.Load("libc.so.6");
            capacity = Marshal.GetDelegateForFunctionPointer<NativeAllocationCapacity>(NativeLibrary.GetExport(_allocatorLibrary, "malloc_usable_size"));
        }
        _allocate = (_, size) =>
        {
            nint address = Marshal.AllocHGlobal(checked((nint)size));
            // Linux counts the allocator's actual usable capacity. Other hosts
            // conservatively include alignment/header slack per allocation.
            long bytes = capacity is not null ? checked((long)capacity(address)) : checked((long)size + 4 * IntPtr.Size);
            _buffers.Add(address, bytes);
            _current += bytes;
            PeakBytes = Math.Max(PeakBytes, _current);
            return address;
        };
        _free = (_, address) =>
        {
            if (address == 0) return;
            _current -= _buffers[address];
            _buffers.Remove(address);
            Marshal.FreeHGlobal(address);
        };
        BrotliCreateDecoder create = Marshal.GetDelegateForFunctionPointer<BrotliCreateDecoder>(
            NativeLibrary.GetExport(_library, "BrotliDecoderCreateInstance"));
        nint pointer = create(_allocate, _free, 0);
        pointer.ShouldNotBe(0);
        FieldInfo stateField = typeof(BrotliDecoder).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var state = (SafeHandle)Activator.CreateInstance(stateField.FieldType, nonPublic: true)!;
        typeof(SafeHandle).GetMethod("SetHandle", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, [pointer]);
        object decoder = new BrotliDecoder();
        stateField.SetValue(decoder, state);
        typeof(BrotliStream).GetField("_decoder", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(stream, decoder);
    }

    public void Dispose()
    {
        // The owning BrotliStream must dispose its actual SafeHandle first,
        // exercising these same callbacks rather than a mirrored decode.
        _current.ShouldBe(0);
        _buffers.ShouldBeEmpty();
        GC.KeepAlive(_allocate);
        GC.KeepAlive(_free);
        if (_allocatorLibrary != 0) NativeLibrary.Free(_allocatorLibrary);
        NativeLibrary.Free(_library);
    }
}

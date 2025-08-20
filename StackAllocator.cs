
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Allocator;

public class StackAllocator(int size)
{
    private readonly byte[] buffer = new byte[size];
    private readonly int size = size;
    private readonly List<int> markers = [];
    private int marker = 0;

    public int Marker => marker;
    public int Size => size;
    public int FreeBytes => size - marker;
    public int UsedBytes => marker;

    public Span<T> AllocElements<T>(int count) where T : struct
        => AllocBytes<T>(Unsafe.SizeOf<T>() * count);

    public Span<T> AllocBytes<T>(int sizeBytes) where T : struct
    {
        if (!TryAllocBytes(sizeBytes, out Span<T> result, out AllocFailReason reason))
        {
            throw reason switch
            {
                AllocFailReason.Overflow
                    => new InvalidOperationException("Stack Overflow"),

                AllocFailReason.SizeNegative
                    => new ArgumentException("sizeBytes is Never Negative"),
                    
                _ => new InvalidOperationException("Unknown allocation failure"),
            };
        }

        return result;
    }

    public bool TryAllocElements<T>(int count, Span<T> result) where T : struct
        => TryAllocBytes(Unsafe.SizeOf<T>() * count, out result);

    public bool TryAllocBytes<T>(int sizeBytes, out Span<T> result) where T : struct
        => TryAllocBytes(sizeBytes, out result, out _);

    private bool TryAllocBytes<T>(int sizeBytes, out Span<T> result, out AllocFailReason reason) where T : struct
    {
        if (sizeBytes == 0)
        {
            result = Span<T>.Empty;
            reason = AllocFailReason.None;

            return true;
        }

        reason = ValidateAlloc(sizeBytes);
        if (reason != AllocFailReason.None)
        {
            result = null;
            
            return false;
        }

        Span<byte> span = new(buffer, marker, sizeBytes);
        markers.Add(marker);
        marker += sizeBytes;

        result = MemoryMarshal.Cast<byte, T>(span);

        return true;
    }

    private AllocFailReason ValidateAlloc(int sizeBytes)
    {
        if (sizeBytes < 0)
            return AllocFailReason.SizeNegative;

        if (marker + sizeBytes > size)
            return AllocFailReason.Overflow;

        return AllocFailReason.None;
    }

    public void Free(int marker)
    {
        if (!TryFree(marker))
            throw new InvalidOperationException("Invaild marker");
    }

    public bool TryFree(int marker)
    {
        if (marker < 0 || marker > this.marker)
            return false;

        int idx = markers.LastIndexOf(marker);

        if (idx == -1)
            return false;

        markers.RemoveRange(idx, markers.Count - idx);
        this.marker = marker;

        return true;
    }

    public void Clear()
    {
        this.marker = 0;
        markers.Clear();
    }
}

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Allocator;

public class DualStackAllocator(int size)
{
    private enum Dir { Up, Down };

    private readonly byte[] buffer = new byte[size];
    private readonly int size = size;
    private readonly List<int> markersUp = [];
    private readonly List<int> markersDown = [];
    private int upMarker = 0;
    private int downMarker = size;

    public int UpMarker => upMarker;
    public int DownMarker => downMarker;
    public int Size => size;
    public int FreeBytes => downMarker - upMarker;
    public int UsedBytes => upMarker + (size - downMarker);

    public Span<T> AllocUpElements<T>(int count) where T : struct
        => AllocBytes<T>(Unsafe.SizeOf<T>() * count, Dir.Up);

    public Span<T> AllocDownElements<T>(int count) where T : struct
        => AllocBytes<T>(Unsafe.SizeOf<T>() * count, Dir.Down);

    public Span<T> AllocUpBytes<T>(int sizeBytes) where T : struct
        => AllocBytes<T>(sizeBytes, Dir.Up);
        
    public Span<T> AllocDownBytes<T>(int sizeBytes) where T : struct
        => AllocBytes<T>(sizeBytes, Dir.Down);

    private Span<T> AllocBytes<T>(int sizeBytes, Dir dir) where T : struct
    {
        if (!TryAllocBytes(sizeBytes, dir, out Span<T> result, out AllocFailReason reason))
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

    public bool TryAllocDownElemnets<T>(int count) where T : struct
        => TryAllocBytes<T>(Unsafe.SizeOf<T>() * count, Dir.Down, out _, out _);

    public bool TryAllocUpElemnets<T>(int count) where T : struct
        => TryAllocBytes<T>(Unsafe.SizeOf<T>() * count, Dir.Up, out _, out _);

    public bool TryAllocUpBytes<T>(int sizeBytes) where T : struct
        => TryAllocBytes<T>(sizeBytes, Dir.Up, out _, out _);

    public bool TryAllocDownBytes<T>(int sizeBytes) where T : struct
        => TryAllocBytes<T>(sizeBytes, Dir.Down, out _, out _);

    private bool TryAllocBytes<T>(int sizeBytes, Dir dir, out Span<T> result, out AllocFailReason reason) where T : struct
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

        Span<byte> span;

        if (dir == Dir.Up)
        {
            span = new(buffer, upMarker, sizeBytes);
            markersUp.Add(upMarker);
            upMarker += sizeBytes;
        }
        else
        {
            span = new(buffer, downMarker - sizeBytes, sizeBytes);
            markersDown.Add(downMarker);
            downMarker -= sizeBytes;
        }

        result = MemoryMarshal.Cast<byte, T>(span);

        return true;
    }

    private AllocFailReason ValidateAlloc(int sizeBytes)
    {
        if (sizeBytes < 0)
            return AllocFailReason.SizeNegative;

        if (upMarker + sizeBytes > downMarker)
            return AllocFailReason.Overflow;

        return AllocFailReason.None;
    }


    public void FreeUp(int marker)
    {
        if (!TryFreeUp(marker))
            throw new InvalidOperationException("Invaild marker");
    }


    public void FreeDown(int marker)
    {
        if (!TryFreeDown(marker))
            throw new InvalidOperationException("Invaild marker");
    }

    public bool TryFreeUp(int marker)
    {
        if (marker < 0 || marker > upMarker)
            return false;
            
        int idx = markersUp.LastIndexOf(marker);

        if (idx == -1)
            return false;

        markersUp.RemoveRange(idx, markersUp.Count - idx);
        upMarker = marker;

        return true;
    }

    public bool TryFreeDown(int marker)
    {
        if (marker > downMarker || marker < upMarker)
            return false;

        int idx = markersDown.LastIndexOf(marker);

        if (idx == -1)
            return false;

        markersDown.RemoveRange(idx, markersDown.Count - idx);
        downMarker = marker;

        return true;
    }

    public void ClearUp()
    {
        upMarker = 0;
        markersUp.Clear();
    }

    public void ClearDown()
    {
        downMarker = size;
        markersDown.Clear();
    }

    public void Clear()
    {
        upMarker = 0;
        downMarker = size;
        markersUp.Clear();
        markersDown.Clear();
    }
}
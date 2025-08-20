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

    /// <summary>
    /// Up 방향 요소 개수 기반 메모리 할당 (LIFO). 실패 시 예외 발생.
    /// </summary>
    /// <typeparam name="T">할당할 값 타입(구조체)</typeparam>
    /// <param name="count">할당할 요소 개수</param>
    /// <returns>할당된 Span<T> (길이: count)</returns>
    public Span<T> AllocUpElements<T>(int count) where T : struct
        => AllocBytes<T>(Unsafe.SizeOf<T>() * count, Dir.Up);

    /// <summary>
    /// Down 방향 요소 개수 기반 메모리 할당 (LIFO). 실패 시 예외 발생.
    /// </summary>
    /// <typeparam name="T">할당할 값 타입(구조체)</typeparam>
    /// <param name="count">할당할 요소 개수</param>
    /// <returns>할당된 Span<T> (길이: count)</returns>
    public Span<T> AllocDownElements<T>(int count) where T : struct
        => AllocBytes<T>(Unsafe.SizeOf<T>() * count, Dir.Down);

    /// <summary>
    /// Up 방향 바이트 단위 메모리 할당 (LIFO). 실패 시 예외 발생.
    /// </summary>
    /// <typeparam name="T">할당할 값 타입(구조체)</typeparam>
    /// <param name="sizeBytes">할당할 바이트 수</param>
    /// <returns>할당된 Span<T> (길이: sizeBytes / Unsafe.SizeOf<T>())</returns>
    /// <remarks>sizeBytes가 T의 크기 배수가 아니면 올림 정렬되어 할당됩니다.</remarks>
    public Span<T> AllocUpBytes<T>(int sizeBytes) where T : struct
        => AllocBytes<T>(sizeBytes, Dir.Up);
        
    /// <summary>
    /// Down 방향 바이트 단위 메모리 할당 (LIFO). 실패 시 예외 발생.
    /// </summary>
    /// <typeparam name="T">할당할 값 타입(구조체)</typeparam>
    /// <param name="sizeBytes">할당할 바이트 수</param>
    /// <returns>할당된 Span<T> (길이: sizeBytes / Unsafe.SizeOf<T>())</returns>
    /// <remarks>sizeBytes가 T의 크기 배수가 아니면 올림 정렬되어 할당됩니다.</remarks>
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

    /// <summary>
    /// Down 방향 요소 개수 기반 Try 메모리 할당 (LIFO). 실패 시 false/Span.Empty 반환.
    /// </summary>
    /// <typeparam name="T">할당할 값 타입(구조체)</typeparam>
    /// <param name="count">할당할 요소 개수</param>
    /// <returns>성공 시 true, 실패 시 false</returns>
    public bool TryAllocDownElemnets<T>(int count) where T : struct
        => TryAllocBytes<T>(Unsafe.SizeOf<T>() * count, Dir.Down, out _, out _);

    /// <summary>
    /// Up 방향 요소 개수 기반 Try 메모리 할당 (LIFO). 실패 시 false/Span.Empty 반환.
    /// </summary>
    /// <typeparam name="T">할당할 값 타입(구조체)</typeparam>
    /// <param name="count">할당할 요소 개수</param>
    /// <returns>성공 시 true, 실패 시 false</returns>
    public bool TryAllocUpElemnets<T>(int count) where T : struct
        => TryAllocBytes<T>(Unsafe.SizeOf<T>() * count, Dir.Up, out _, out _);

    /// <summary>
    /// Up 방향 바이트 단위 Try 메모리 할당 (LIFO). 실패 시 false/Span.Empty 반환.
    /// </summary>
    /// <typeparam name="T">할당할 값 타입(구조체)</typeparam>
    /// <param name="sizeBytes">할당할 바이트 수</param>
    /// <returns>성공 시 true, 실패 시 false</returns>
    /// <remarks>sizeBytes가 T의 크기 배수가 아니면 올림 정렬되어 할당됩니다.</remarks>
    public bool TryAllocUpBytes<T>(int sizeBytes) where T : struct
        => TryAllocBytes<T>(sizeBytes, Dir.Up, out _, out _);

    /// <summary>
    /// Down 방향 바이트 단위 Try 메모리 할당 (LIFO). 실패 시 false/Span.Empty 반환.
    /// </summary>
    /// <typeparam name="T">할당할 값 타입(구조체)</typeparam>
    /// <param name="sizeBytes">할당할 바이트 수</param>
    /// <returns>성공 시 true, 실패 시 false</returns>
    /// <remarks>sizeBytes가 T의 크기 배수가 아니면 올림 정렬되어 할당됩니다.</remarks>
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

        int typeSize = Unsafe.SizeOf<T>();
        if (sizeBytes % typeSize != 0)
        {
            sizeBytes = (sizeBytes + typeSize - 1) / typeSize * typeSize;
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


    /// <summary>
    /// Up 방향 marker까지 롤백(해제). marker는 AllocUp 시점의 UpMarker 값이어야 함. 실패 시 예외.
    /// </summary>
    /// <param name="marker">롤백할 marker 값(이전 Up 할당 시점의 UpMarker)</param>
    public void FreeUp(int marker)
    {
        if (!TryFreeUp(marker))
            throw new InvalidOperationException("Invaild marker");
    }


    /// <summary>
    /// Down 방향 marker까지 롤백(해제). marker는 AllocDown 시점의 DownMarker 값이어야 함. 실패 시 예외.
    /// </summary>
    /// <param name="marker">롤백할 marker 값(이전 Down 할당 시점의 DownMarker)</param>
    public void FreeDown(int marker)
    {
        if (!TryFreeDown(marker))
            throw new InvalidOperationException("Invaild marker");
    }

    /// <summary>
    /// Up 방향 marker까지 롤백(해제). marker는 AllocUp 시점의 UpMarker 값이어야 함. 실패 시 false.
    /// </summary>
    /// <param name="marker">롤백할 marker 값(이전 Up 할당 시점의 UpMarker)</param>
    /// <returns>성공 시 true, 실패 시 false</returns>
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

    /// <summary>
    /// Down 방향 marker까지 롤백(해제). marker는 AllocDown 시점의 DownMarker 값이어야 함. 실패 시 false.
    /// </summary>
    /// <param name="marker">롤백할 marker 값(이전 Down 할당 시점의 DownMarker)</param>
    /// <returns>성공 시 true, 실패 시 false</returns>
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

    /// <summary>
    /// Up 방향 전체 할당 해제(초기화). upMarker=0, markersUp 리스트 초기화.
    /// </summary>
    public void ClearUp()
    {
        upMarker = 0;
        markersUp.Clear();
    }

    /// <summary>
    /// Down 방향 전체 할당 해제(초기화). downMarker=size, markersDown 리스트 초기화.
    /// </summary>
    public void ClearDown()
    {
        downMarker = size;
        markersDown.Clear();
    }

    /// <summary>
    /// Up/Down 전체 할당 해제(초기화). upMarker=0, downMarker=size, markersUp/markersDown 리스트 초기화.
    /// </summary>
    public void Clear()
    {
        upMarker = 0;
        downMarker = size;
        markersUp.Clear();
        markersDown.Clear();
    }
}
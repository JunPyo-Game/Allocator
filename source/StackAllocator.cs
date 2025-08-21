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

    /// <summary>
    /// 요소 개수 기반 메모리 할당 (LIFO). 실패 시 예외 발생.
    /// </summary>
    /// <typeparam name="T">할당할 값 타입(구조체)</typeparam>
    /// <param name="count">할당할 요소 개수</param>
    /// <returns>할당된 Span<T> (길이: count)</returns>
    public Span<T> AllocElements<T>(int count) where T : struct
        => AllocBytes<T>(Unsafe.SizeOf<T>() * count);

    /// <summary>
    /// 바이트 단위 메모리 할당 (LIFO). 실패 시 예외 발생.
    /// </summary>
    /// <typeparam name="T">할당할 값 타입(구조체)</typeparam>
    /// <param name="sizeBytes">할당할 바이트 수</param>
    /// <returns>할당된 Span<T> (길이: sizeBytes / Unsafe.SizeOf<T>())</returns>
    /// <remarks>sizeBytes가 T의 크기 배수가 아니면 올림 정렬되어 할당됩니다.</remarks>
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

    /// <summary>
    /// 요소 개수 기반 Try 메모리 할당 (LIFO). 실패 시 false/Span.Empty 반환.
    /// </summary>
    /// <typeparam name="T">할당할 값 타입(구조체)</typeparam>
    /// <param name="count">할당할 요소 개수</param>
    /// <param name="result">[out] 성공 시 할당된 Span<T>, 실패 시 Span.Empty</param>
    /// <returns>성공 시 true, 실패 시 false</returns>
    public bool TryAllocElements<T>(int count, Span<T> result) where T : struct
        => TryAllocBytes(Unsafe.SizeOf<T>() * count, out result);

    /// <summary>
    /// 바이트 단위 Try 메모리 할당 (LIFO). 실패 시 false/Span.Empty 반환.
    /// </summary>
    /// <typeparam name="T">할당할 값 타입(구조체)</typeparam>
    /// <param name="sizeBytes">할당할 바이트 수</param>
    /// <param name="result">[out] 성공 시 할당된 Span<T>, 실패 시 Span.Empty</param>
    /// <returns>성공 시 true, 실패 시 false</returns>
    /// <remarks>sizeBytes가 T의 크기 배수가 아니면 올림 정렬되어 할당됩니다.</remarks>
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

    /// <summary>
    /// 지정 marker까지 롤백(해제). marker는 Alloc 시점의 Marker 프로퍼티 값이어야 함. 실패 시 예외.
    /// </summary>
    /// <param name="marker">롤백할 marker 값(이전 할당 시점의 Marker)</param>
    public void Free(int marker)
    {
        if (!TryFree(marker))
            throw new InvalidOperationException("Invaild marker");
    }

    /// <summary>
    /// 지정 marker까지 롤백(해제). marker는 Alloc 시점의 Marker 프로퍼티 값이어야 함. 실패 시 false.
    /// </summary>
    /// <param name="marker">롤백할 marker 값(이전 할당 시점의 Marker)</param>
    /// <returns>성공 시 true, 실패 시 false</returns>
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

    /// <summary>
    /// 모든 할당 해제(초기화). marker=0, markers 리스트도 초기화됨.
    /// </summary>
    public void Clear()
    {
        this.marker = 0;
        markers.Clear();
    }
}

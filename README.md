# Allocator Library

<br>

## StackAllocator

**용도:** 단일 방향 LIFO 임시 메모리 할당/롤백

**특징:**
- 내부적으로 byte[] 버퍼와 marker, marker 리스트로 관리
- 멀티스레드 안전성 없음 (단일 스레드 임시 버퍼/알고리즘용)

**주요 API**
| 유형 | 메서드 | 설명 |
|---|---|---|
| Throw | `AllocElements<T>(int count)` | 요소 개수 기반 할당, 실패 시 예외 |
| Throw | `AllocBytes<T>(int sizeBytes)` | 바이트 단위 할당, 실패 시 예외 |
| Throw | `Free(int marker)` | marker 기반 롤백/해제, 실패 시 예외 |
| Try | `TryAllocElements<T>(int count, Span<T> result)` | 요소 개수 기반 Try 할당, 실패 시 false/Span.Empty |
| Try | `TryAllocBytes<T>(int sizeBytes, out Span<T> result)` | 바이트 단위 Try 할당, 실패 시 false/Span.Empty |
| Try | `TryFree(int marker)` | marker 기반 롤백/해제, 실패 시 false |
| - | `Clear()` | 전체 해제 |

**사용 예시**
```csharp
var alloc = new StackAllocator(1024);
var span = alloc.AllocElements<int>(10);
// ... 사용 ...
alloc.Free(alloc.Marker); // 롤백
alloc.Clear(); // 전체 해제
```

---

<br>

## DualStackAllocator

**용도:** 양방향(Up/Down) 임시 메모리 할당/롤백

**특징:**
- Up(0→)과 Down(←size)에서 각각 LIFO 방식 할당/해제
- 멀티스레드 안전성 없음 (단일 스레드 임시 버퍼/알고리즘용)

**주요 API**
| 유형 | 메서드 | 설명 |
|---|---|---|
| Throw | `AllocUpElements<T>(int count)` / `AllocDownElements<T>(int count)` | Up/Down 방향 요소 개수 기반 할당, 실패 시 예외 |
| Throw | `AllocUpBytes<T>(int sizeBytes)` / `AllocDownBytes<T>(int sizeBytes)` | Up/Down 방향 바이트 단위 할당, 실패 시 예외 |
| Throw | `FreeUp(int marker)` / `FreeDown(int marker)` | Up/Down marker 롤백/해제, 실패 시 예외 |
| Try | `TryAllocUpElemnets<T>(int count)` / `TryAllocDownElemnets<T>(int count)` | Up/Down 방향 요소 개수 기반 Try 할당, 실패 시 false/Span.Empty |
| Try | `TryAllocUpBytes<T>(int sizeBytes)` / `TryAllocDownBytes<T>(int sizeBytes)` | Up/Down 방향 바이트 단위 Try 할당, 실패 시 false/Span.Empty |
| Try | `TryFreeUp(int marker)` / `TryFreeDown(int marker)` | Up/Down marker 롤백/해제, 실패 시 false |
| - | `ClearUp()` / `ClearDown()` / `Clear()` | Up/Down/전체 해제 |

**사용 예시**
```csharp
var alloc = new DualStackAllocator(1024);
var up = alloc.AllocUpElements<int>(10);
var down = alloc.AllocDownElements<byte>(20);
// ... 사용 ...
alloc.FreeUp(alloc.UpMarker); // Up 롤백
alloc.FreeDown(alloc.DownMarker); // Down 롤백
alloc.Clear(); // 전체 해제
```

---

## 공통 특징

- Span<T> 기반 안전한 임시 메모리 할당/롤백 지원
- marker 기반 롤백/해제, Try/예외 패턴 모두 지원
- C# 최신 문법 활용, 단위 테스트 권장

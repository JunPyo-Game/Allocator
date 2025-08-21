
# Allocator Library

<br>

## StackAllocator

**동작 방식:**
- 내부적으로 byte[] 버퍼와 marker(정수), markers(리스트)를 사용합니다.
- marker는 현재 할당된 끝 위치(버퍼 오프셋)를 나타냅니다.
- AllocElements/AllocBytes 호출 시 marker 위치부터 원하는 크기만큼 Span<T>를 잘라 반환하고, marker를 증가시킵니다.
- 할당 전 marker 값을 저장해두면, Free(marker)로 해당 시점까지 롤백(해제)할 수 있습니다.
- Try 계열은 실패 시 false/Span.Empty, 예외 계열은 실패 시 예외를 던집니다.

<br>

**용도:** 단일 방향 LIFO 임시 메모리 할당/롤백

<br>

**특징:**
- 내부적으로 byte[] 버퍼와 marker, marker 리스트로 관리
- 멀티스레드 안전성 없음 (단일 스레드 임시 버퍼/알고리즘용)

<br>

**주요 API**
| 메서드 | 설명 |
|---|---|
| `AllocElements<T>(int count)` | 요소 개수 기반 할당, 실패 시 예외 |
| `AllocBytes<T>(int sizeBytes)` | 바이트 단위 할당, 실패 시 예외 |
| `TryAllocElements<T>(int count, Span<T> result)` | 요소 개수 기반 Try 할당, 실패 시 false/Span.Empty |
| `TryAllocBytes<T>(int sizeBytes, out Span<T> result)` | 바이트 단위 Try 할당, 실패 시 false/Span.Empty |
| `Free(int marker)` | marker 기반 롤백/해제, 실패 시 예외 |
| `TryFree(int marker)` | marker 기반 롤백/해제, 실패 시 false |
| `Clear()` | 전체 해제 |

<br>

**사용 예시**

```csharp
var alloc = new StackAllocator(1024);
int marker = alloc.Marker;
var span = alloc.AllocElements<int>(10);
// ... 사용 ...
alloc.Free(marker); // 롤백
alloc.Clear(); // 전체 해제
```

<br>

**단일 프레임 버퍼 예시**
```csharp
var alloc = new StackAllocator(4096);
while (true) // 프레임 루프
{
	// 프레임 시작 시 전체 해제(버퍼 초기화)
	alloc.Clear();
	// 프레임 내 임시 데이터 할당
	var tempInts = alloc.AllocElements<int>(100);
	var tempBytes = alloc.AllocBytes<byte>(256);
	// ... 임시 데이터 사용 ...
	// (프레임 끝에서 별도 해제 불필요)
}
```

---

<br>


## DualStackAllocator

**동작 방식:**
- 내부적으로 byte[] 버퍼, upMarker(0부터 증가), downMarker(끝에서 감소), markersUp/markersDown(각 방향 롤백용 리스트)를 사용합니다.
- AllocUpElements/AllocUpBytes는 upMarker 위치부터 앞으로 할당, marker와 리스트를 증가시킵니다.
- AllocDownElements/AllocDownBytes는 downMarker 위치부터 뒤로 할당, marker와 리스트를 감소시킵니다.
- Up/Down 각각 할당 전 marker를 저장해두면, FreeUp/FreeDown(marker)로 해당 시점까지 롤백(해제)할 수 있습니다.
- Up/Down 영역이 겹치지 않도록 upMarker + sizeBytes > downMarker 조건으로 경계 체크합니다.
- Try 계열은 실패 시 false/Span.Empty, 예외 계열은 실패 시 예외를 던집니다.

<br>

**용도:** 양방향(Up/Down) 임시 메모리 할당/롤백

<br>

**특징:**
- Up(0→)과 Down(←size)에서 각각 LIFO 방식 할당/해제
- 멀티스레드 안전성 없음 (단일 스레드 임시 버퍼/알고리즘용)

<br>

**주요 API**
| 메서드 | 설명 |
|---|---|
| `AllocUpElements<T>(int count)` | Up 방향 요소 개수 기반 할당, 실패 시 예외 |
| `AllocDownElements<T>(int count)` | Down 방향 요소 개수 기반 할당, 실패 시 예외 |
| `AllocUpBytes<T>(int sizeBytes)` | Up 방향 바이트 단위 할당, 실패 시 예외 |
| `AllocDownBytes<T>(int sizeBytes)` | Down 방향 바이트 단위 할당, 실패 시 예외 |
| `TryAllocUpElemnets<T>(int count)` | Up 방향 요소 개수 기반 Try 할당, 실패 시 false/Span.Empty |
| `TryAllocDownElemnets<T>(int count)` | Down 방향 요소 개수 기반 Try 할당, 실패 시 false/Span.Empty |
| `TryAllocUpBytes<T>(int sizeBytes)` | Up 방향 바이트 단위 Try 할당, 실패 시 false/Span.Empty |
| `TryAllocDownBytes<T>(int sizeBytes)` | Down 방향 바이트 단위 Try 할당, 실패 시 false/Span.Empty |
| `FreeUp(int marker)` | Up marker 롤백/해제, 실패 시 예외 |
| `FreeDown(int marker)` | Down marker 롤백/해제, 실패 시 예외 |
| `TryFreeUp(int marker)` | Up marker 롤백/해제, 실패 시 false |
| `TryFreeDown(int marker)` | Down marker 롤백/해제, 실패 시 false |
| `ClearUp()` | Up 해제 |
| `ClearDown()` | Down 해제 |
| `Clear()` | 전체 해제 |

<br>

**사용 예시**
```csharp
var alloc = new DualStackAllocator(1024);
int upMarker = alloc.UpMarker;
int downMarker = alloc.DownMarker;
var up = alloc.AllocUpElements<int>(10);
var down = alloc.AllocDownElements<byte>(20);
// ... 사용 ...
alloc.FreeUp(upMarker); // Up 롤백
alloc.FreeDown(downMarker); // Down 롤백
alloc.Clear(); // 전체 해제
```

---

## ObjectPool

**동작 방식:**
- 지정된 팩토리(createFunc)로 객체를 생성하고, 반환된 객체를 Stack 구조로 재사용합니다.
- onGet, onRelease, onDestroy 콜백으로 객체의 상태 초기화/정리/파기 등 커스텀 동작을 지정할 수 있습니다.
- 최대 크기(maxSize) 초과 시 반환 객체는 파기(onDestroy/Dispose)됩니다.
- onDestroy를 지정하지 않으면, T가 IDisposable을 구현한 경우 자동으로 Dispose가 호출됩니다.
- onDestroy를 지정한 경우, 반드시 콜백 내에서 IDisposable.Dispose() 호출 책임이 있습니다.
- 중복 반환, null 반환 등은 예외 또는 Try 패턴으로 안전하게 처리합니다.

<br>

**용도:** 반복적으로 생성/파괴되는 객체의 재사용(메모리/GC 절감, 성능 향상)

<br>

**특징:**
- Stack 기반 LIFO 구조, 최대 크기 제한, 콜백 지원, 중복 반환 방지, IDisposable 자동 처리
- 멀티스레드 안전성 없음(단일 스레드 용도)

<br>


**주요 API**
| 메서드 | 설명 |
|---|---|
| `Get()` | 객체 할당(없으면 새로 생성) |
| `Release(T element)` | 객체 반환(중복/Null 등 예외) |
| `TryRelease(T element)` | 객체 반환 시도, 성공/실패(bool)만 반환 |
| `Clear()` | 풀 전체 비우기(onDestroy/Dispose 호출) |
| `Dispose()` | 풀 전체 정리(Dispose 패턴) |
| `HasElement(T element)` | 해당 객체가 풀에 포함되어 있는지 확인 |
| `CountAll` | 전체 객체 수 |
| `CountInactive` | 비활성 객체 수 |
| `CountActive` | 활성 객체 수 |

<br>

**생성자 주요 매개변수**
- `createFunc`: 객체 생성 델리게이트(필수)
- `onGet`: 객체 할당 시 호출되는 콜백(선택)
- `onRelease`: 객체 반환 시 호출되는 콜백(선택)
- `onDestroy`: 풀에서 파기될 때 호출되는 콜백(선택)
- `collectionCheck`: 중복 반환 검사 여부(기본값 true)
- `defaultCapacity`: 초기 풀 크기(기본값 10)
- `maxSize`: 최대 풀 크기(기본값 100)

<br>


**사용 예시**
```csharp
var pool = new ObjectPool<MyClass>(
	() => new MyClass(),
	onGet: obj => obj.Reset(),
	onRelease: obj => obj.Cleanup(),
	onDestroy: obj => obj.Dispose(),
	defaultCapacity: 10,
	maxSize: 100);

var obj = pool.Get();
// ... 사용 ...
pool.Release(obj); // 실패 시 예외 발생

// 단순 성공/실패만 확인
if (!pool.TryRelease(obj))
	Console.WriteLine("Release 실패");

pool.Clear();
pool.Dispose();
```

---

---

# # Allocator Library（日本語バージョン）

## StackAllocator

**動作方式：**
- 内部的に byte[] バッファと marker（整数）、markers（リスト）で管理します。
- marker は現在の割り当て終端位置（バッファオフセット）を示します。
- AllocElements/AllocBytes 呼び出し時、marker 位置から指定サイズ分の Span<T> を返し、marker を増加させます。
- 割り当て前の marker を保存しておけば、Free(marker) でその時点までロールバック（解放）できます。
- Try 系は失敗時 false/Span.Empty、例外系は失敗時に例外を投げます。

**用途：** 単一方向 LIFO 一時メモリ割り当て/ロールバック

**特徴：**
- 内部的に byte[] バッファと marker、marker リストで管理
- マルチスレッド安全性なし（単一スレッド一時バッファ/アルゴリズム用）

**主なAPI**
| メソッド | 説明 |
|---|---|
| `AllocElements<T>(int count)` | 要素数指定割り当て、失敗時例外 |
| `AllocBytes<T>(int sizeBytes)` | バイト単位割り当て、失敗時例外 |
| `TryAllocElements<T>(int count, Span<T> result)` | 要素数指定Try割り当て、失敗時false/Span.Empty |
| `TryAllocBytes<T>(int sizeBytes, out Span<T> result)` | バイト単位Try割り当て、失敗時false/Span.Empty |
| `Free(int marker)` | marker基準ロールバック/解放、失敗時例外 |
| `TryFree(int marker)` | marker基準ロールバック/解放、失敗時false |
| `Clear()` | 全解放 |

**使用例**
```csharp
var alloc = new StackAllocator(1024);
int marker = alloc.Marker;
var span = alloc.AllocElements<int>(10);
// ... 利用 ...
alloc.Free(marker); // ロールバック
alloc.Clear(); // 全解放
```

**単一フレームバッファ例**
```csharp
var alloc = new StackAllocator(4096);
while (true) // フレームループ
{
	alloc.Clear(); // フレーム開始時に全解放
	var tempInts = alloc.AllocElements<int>(100);
	var tempBytes = alloc.AllocBytes<byte>(256);
	// ... 一時データ利用 ...
	// （フレーム終了時の個別解放不要）
}
```

---

## DualStackAllocator

**動作方式：**
- 内部的に byte[] バッファ、upMarker（0から増加）、downMarker（末尾から減少）、markersUp/markersDown（各方向ロールバック用リスト）で管理します。
- AllocUpElements/AllocUpBytes は upMarker 位置から前方に割り当て、marker/リストを増加。
- AllocDownElements/AllocDownBytes は downMarker 位置から後方に割り当て、marker/リストを減少。
- Up/Down それぞれ割り当て前の marker を保存しておけば、FreeUp/FreeDown(marker) でその時点までロールバック（解放）できます。
- Up/Down 領域が重ならないよう upMarker + sizeBytes > downMarker で境界チェック。
- Try 系は失敗時 false/Span.Empty、例外系は失敗時に例外を投げます。

**用途：** 双方向（Up/Down）一時メモリ割り当て/ロールバック

**特徴：**
- Up（0→）と Down（←size）でそれぞれLIFO方式割り当て/解放
- マルチスレッド安全性なし（単一スレッド一時バッファ/アルゴリズム用）

**主なAPI**
| メソッド | 説明 |
|---|---|
| `AllocUpElements<T>(int count)` | Up方向要素数指定割り当て、失敗時例外 |
| `AllocDownElements<T>(int count)` | Down方向要素数指定割り当て、失敗時例外 |
| `AllocUpBytes<T>(int sizeBytes)` | Up方向バイト単位割り当て、失敗時例外 |
| `AllocDownBytes<T>(int sizeBytes)` | Down方向バイト単位割り当て、失敗時例外 |
| `TryAllocUpElemnets<T>(int count)` | Up方向要素数指定Try割り当て、失敗時false/Span.Empty |
| `TryAllocDownElemnets<T>(int count)` | Down方向要素数指定Try割り当て、失敗時false/Span.Empty |
| `TryAllocUpBytes<T>(int sizeBytes)` | Up方向バイト単位Try割り当て、失敗時false/Span.Empty |
| `TryAllocDownBytes<T>(int sizeBytes)` | Down方向バイト単位Try割り当て、失敗時false/Span.Empty |
| `FreeUp(int marker)` | Up markerロールバック/解放、失敗時例外 |
| `FreeDown(int marker)` | Down markerロールバック/解放、失敗時例外 |
| `TryFreeUp(int marker)` | Up markerロールバック/解放、失敗時false |
| `TryFreeDown(int marker)` | Down markerロールバック/解放、失敗時false |
| `ClearUp()` | Up解放 |
| `ClearDown()` | Down解放 |
| `Clear()` | 全解放 |

**使用例**
```csharp
var alloc = new DualStackAllocator(1024);
int upMarker = alloc.UpMarker;
int downMarker = alloc.DownMarker;
var up = alloc.AllocUpElements<int>(10);
var down = alloc.AllocDownElements<byte>(20);
// ... 利用 ...
alloc.FreeUp(upMarker); // Upロールバック
alloc.FreeDown(downMarker); // Downロールバック
alloc.Clear(); // 全解放
```

---

## ObjectPool

**動作方式：**
- 指定したファクトリ（createFunc）でオブジェクトを生成し、返却されたオブジェクトをスタック構造で再利用します。
- onGet, onRelease, onDestroy コールバックで、オブジェクトの初期化・クリーンアップ・破棄などのカスタム処理が可能です。
- 最大サイズ（maxSize）を超えた場合、返却オブジェクトは onDestroy または Dispose で破棄されます。
- onDestroy を指定しない場合、T が IDisposable を実装していれば自動的に Dispose が呼ばれます。
- onDestroy を指定した場合、コールバック内で必ず Dispose を呼ぶ責任があります。
- 重複返却や null 返却は例外または Try パターンで安全に処理されます。

**用途：** 繰り返し生成・破棄されるオブジェクトの再利用（メモリ・GC削減、パフォーマンス向上）

**特徴：**
- StackベースLIFO構造、最大サイズ制限、コールバック対応、重複返却防止、IDisposable自動処理
- マルチスレッド安全性なし（単一スレッド用）

**主なAPI**
| メソッド | 説明 |
|---|---|
| `ObjectPool(Func<T> createFunc, ...)` | コンストラクタ。生成・初期化・クリーンアップ・破棄コールバック、プールサイズ等を指定 |
| `Get()` | オブジェクト取得（なければ新規生成）|
| `Release(T element)` | オブジェクト返却（重複・null時は例外）|
| `TryRelease(T element)` | 返却を試み、成功/失敗（bool）を返す |
| `Clear()` | プール全体をクリア（onDestroy/Dispose呼び出し）|
| `Dispose()` | プール全体を破棄（Disposeパターン）|
| `HasElement(T element)` | 指定オブジェクトがプールに含まれているか確認 |
| `CountAll/CountInactive/CountActive` | 全体/非アクティブ/アクティブ数 |

**コンストラクタ主なパラメータ**
- `createFunc`: オブジェクト生成デリゲート（必須）
- `onGet`: 取得時コールバック（任意）
- `onRelease`: 返却時コールバック（任意）
- `onDestroy`: 破棄時コールバック（任意）
- `collectionCheck`: 重複返却チェック（デフォルトtrue）
- `defaultCapacity`: 初期プールサイズ（デフォルト10）
- `maxSize`: 最大プールサイズ（デフォルト100）

**使用例**
```csharp
var pool = new ObjectPool<MyClass>(
	() => new MyClass(),
	onGet: obj => obj.Reset(),
	onRelease: obj => obj.Cleanup(),
	onDestroy: obj => obj.Dispose(),
	defaultCapacity: 10,
	maxSize: 100);

var obj = pool.Get();
// ... 利用 ...
pool.Release(obj); // 失敗時は例外

if (!pool.TryRelease(obj))
	Console.WriteLine("Release失敗");

pool.Clear();
pool.Dispose();
```

---
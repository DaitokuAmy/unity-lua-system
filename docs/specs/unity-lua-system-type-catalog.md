# Unity Lua System Type Catalog

## 文書の目的

本書は、Unity Lua Systemを構成するクラス、構造体、interface、enum、Attributeおよび生成型の一覧を管理する。

各型の責務と公開範囲を先に整理し、実装中に責務が特定のクラスへ集中することを防ぐ。本書は詳細なAPI仕様ではなく、システム全体の型構成を確認するための索引として使用する。

## 表記

| 状態 | 意味 |
| --- | --- |
| 採用 | 現在の設計で採用する方針 |
| 検討 | 必要性はあるが、名前や詳細を確定していない |
| 将来 | 初期実装には含めず、後の段階で追加する候補 |

## アセンブリ構成

```text
UnityLuaSystem.Runtime
├── Public API
├── Binding Attributes
├── Runtime Internals
├── Value Conversion
└── Native Interop

UnityLuaSystem.UniTask
└── Optional UniTask Adapter

UnityLuaSystem.SourceGenerator
├── Binding Generator
├── Lua API Metadata Generator
└── Binding Validator

UnityLuaSystem.Editor
└── LuaCATS Definition Exporter

UnityLuaSystem.Tests
├── Runtime Tests
├── Binding Tests
└── Native Integration Tests
```

`UnityLuaSystem.Runtime` はasmdefの `No Engine References` を有効にし、`UnityEngine` に依存しない。

`UnityLuaSystem.UniTask` はUniTaskが導入されている場合だけ有効になる独立asmdefとし、コアの `UnityLuaSystem.Runtime` からUniTaskへ依存させない。

## Public API

### LuaRuntime

| 項目 | 内容 |
| --- | --- |
| 種別 | `sealed class` |
| 状態 | 採用 |
| 所有権 | `IDisposable` |

1つの独立したLua環境を所有する中心クラス。

主な責務:

- `lua_State` の生成と破棄
- Luaコードの実行
- 型付きLua関数の取得
- C#モジュールおよび生成済みバインディングの登録
- Runtime固有のLua参照、C#オブジェクト、非同期処理の管理
- `Tick` による保留中の非同期処理の進行
- 所有スレッドの検証

詳細は `lua-runtime-public-api.md` を参照する。

### LuaRuntimeOptions

| 項目 | 内容 |
| --- | --- |
| 種別 | `sealed class` |
| 状態 | 採用 |

`LuaRuntime` の生成設定を保持する。

初期候補:

- メモリ上限
- 有効にするLua標準ライブラリ
- デバッグ検証の有効化

### LuaStandardLibraries

| 項目 | 内容 |
| --- | --- |
| 種別 | `[Flags] enum` |
| 状態 | 採用 |

Runtimeで有効にするLua標準ライブラリを指定する。

想定する値:

- `None`
- `Base`
- `Coroutine`
- `Table`
- `String`
- `Math`
- `Utf8`
- `Package`
- `IO`
- `OS`
- `Debug`
- `Safe`
- `All`

`Safe` の具体的な構成は別途決定する。

### LuaMemoryStatistics

| 項目 | 内容 |
| --- | --- |
| 種別 | `readonly struct` |
| 状態 | 検討 |

Runtime固有のネイティブメモリ統計を表す。

候補となる情報:

- 現在の使用量
- 最大使用量
- allocation回数
- メモリ上限到達回数

### LuaException

| 項目 | 内容 |
| --- | --- |
| 種別 | `sealed class` |
| 状態 | 採用 |

Luaコードのロード、実行、型変換およびCoroutine再開に失敗した場合の例外。

保持する情報:

- `LuaStatus`
- chunk名
- Luaスタックトレース
- 元になったC#例外

### LuaStatus

| 項目 | 内容 |
| --- | --- |
| 種別 | `enum` |
| 状態 | 採用 |

Lua C APIの実行結果をC#向けに表現する。

Luaの数値定数を公開APIへ直接露出させず、意味のある名前へ変換する。

## 型付きLua関数

### LuaFunction

| 項目 | 内容 |
| --- | --- |
| 種別 | `sealed class`群 |
| 状態 | 採用 |
| 所有権 | `IDisposable` |

戻り値があるLua関数への型付き参照。

```text
LuaFunction<TResult>
LuaFunction<T1, TResult>
LuaFunction<T1, T2, TResult>
...
```

主な操作:

- `Invoke`
- `InvokeAsync`
- `Dispose`

取得時点で引数型と戻り値型を確定し、呼び出し時の型指定とboxingを避ける。

### LuaAction

| 項目 | 内容 |
| --- | --- |
| 種別 | `sealed class`群 |
| 状態 | 採用 |
| 所有権 | `IDisposable` |

戻り値がないLua関数への型付き参照。

```text
LuaAction
LuaAction<T1>
LuaAction<T1, T2>
...
```

主な操作:

- `Invoke`
- `InvokeAsync`
- `Dispose`

## C#バインディングの公開型

### LuaModuleAttribute

| 項目 | 内容 |
| --- | --- |
| 種別 | `sealed class : Attribute` |
| 状態 | 採用 |

C#型をLuaの名前付きモジュールとして公開できることを宣言する。

static関数だけでなく、インスタンス関数を持つ型にも使用できる。

```csharp
[LuaModule("game")]
public sealed partial class GameLuaApi {
}
```

### LuaObjectAttribute

| 項目 | 内容 |
| --- | --- |
| 種別 | `sealed class : Attribute` |
| 状態 | 採用 |

C#インスタンスをLua userdataとして渡せることを宣言する。

対象インスタンスは、Luaへ公開されたC#関数の引数または戻り値として使用できる。

```csharp
[LuaObject]
public sealed partial class Player {
    [LuaFunction]
    public int GetHp() {
        return 100;
    }
}
```

Lua側ではuserdataとして受け取り、生成されたメソッドバインディングを通して操作する。

```lua
local player = game.get_player()
local hp = player:get_hp()
```

### LuaFunctionAttribute

| 項目 | 内容 |
| --- | --- |
| 種別 | `sealed class : Attribute` |
| 状態 | 採用 |

C#メソッドをLuaから呼び出せる関数として公開する。

対象:

- staticメソッド
- `LuaModuleAttribute` 対象型のインスタンスメソッド
- `LuaObjectAttribute` 対象型のインスタンスメソッド
- 同期戻り値
- `Task` / `Task<T>`
- `ValueTask` / `ValueTask<T>`
- `IEnumerator`

### LuaModuleRegistration

| 項目 | 内容 |
| --- | --- |
| 種別 | `sealed class` |
| 状態 | 採用 |
| 所有権 | `IDisposable` |

Luaへ登録したC#モジュールインスタンスの登録状態を所有する。

破棄時に次を行う。

- Lua側のモジュールを無効化
- Runtimeが保持するC#インスタンスへの強参照を解放
- 既存のLuaクロージャからの呼び出しをエラーにする

### ILuaAwaitable

| 項目 | 内容 |
| --- | --- |
| 種別 | `interface` |
| 状態 | 採用 |

Lua Coroutineが待機できるC#非同期処理の完了状態、結果型、結果取得操作を表す。

### ILuaAwaitableAdapter

| 項目 | 内容 |
| --- | --- |
| 種別 | `interface` |
| 状態 | 採用 |

任意のC#非同期戻り値を `ILuaAwaitable` へ変換する。外部の非同期ライブラリ連携はこのinterfaceを実装した別アセンブリへ分離する。

UniTask連携では `runtime.UseUniTask()` によってアダプターをRuntimeへ登録する。登録後は `UniTask` / `UniTask<T>` を返す `[LuaFunction]` を通常のモジュール関数と同様に呼び出せる。

### LuaBindingRegistration

| 項目 | 内容 |
| --- | --- |
| 種別 | `sealed class` |
| 状態 | 検討 |
| 所有権 | `IDisposable` |

生成済みstaticバインディング一式の登録状態を所有する。

staticバインディングをRuntimeの寿命と常に一致させる場合、独立した公開型を設けずRuntime内部で管理する可能性もある。

### ILuaGeneratedModuleBinding

| 項目 | 内容 |
| --- | --- |
| 種別 | `interface` |
| 状態 | 検討 |

Source Generatorが作成したモジュール単位のバインディングを `LuaRuntime` から使用するための契約。

通常の利用者が手動で実装することは想定しない。

### ILuaGeneratedApiMetadata

| 項目 | 内容 |
| --- | --- |
| 種別 | `interface` |
| 状態 | 採用 |

Source Generatorがコンパイル時に収集したLua APIとXMLドキュメントコメントをEditorへ公開する契約。

生成実装は `UNITY_EDITOR` の場合だけコンパイルし、Playerビルドへドキュメント文字列を含めない。Editor側の `LuaDefinitionExporter` は全アセンブリの実装を収集し、LuaCATS定義ファイルへ統合する。

## C#オブジェクトの受け渡し

### 基本方針

`LuaObjectAttribute` が付いたC#型のインスタンスを、Lua関数の引数および戻り値として使用できるようにする。

```csharp
[LuaModule("game")]
public sealed partial class GameLuaApi {
    private readonly Player _player;

    [LuaFunction("get_player")]
    public Player GetPlayer() {
        return _player;
    }
}
```

Luaへは生のマネージドポインターを渡さない。userdataにはRuntime内でのみ有効なオブジェクトIDを保持する。

```text
Lua userdata
    ↓ objectId
LuaObjectRegistry
    ↓
C# instance
```

### 所有権

初期方針:

- C#がインスタンスそのものを所有する
- Lua userdataが存在する間はRuntimeがC#インスタンスを強参照する
- Lua GCでuserdataが回収されたらRuntime側の参照数を減らす
- Lua GCからC#インスタンスの `Dispose` を自動的に呼ばない
- Runtime破棄時にオブジェクト対応表を破棄する
- 別Runtimeのuserdataを受け入れない

同じRuntimeへ同じC#インスタンスを複数回渡した場合は、同じオブジェクトIDとLua userdataを再利用する方針を基本とする。

## Runtime内部型

以下の型は `internal` とし、通常の利用者へ公開しない。

### LuaStateHandle

| 種別 | 状態 | 責務 |
| --- | --- | --- |
| `sealed class` | 検討 | `lua_State*` の保持と破棄状態の管理 |

`SafeHandle` を使用するかは、Lua stateを所有スレッドで破棄する制約を踏まえて決定する。

### LuaStackGuard

| 種別 | 状態 | 責務 |
| --- | --- | --- |
| `ref struct` | 採用 | Lua API呼び出し前後のスタック位置を復元 |

例外発生時を含め、公開API呼び出しの終了時にLuaスタックを元の位置へ戻す。

### LuaRegistryReference

| 種別 | 状態 | 責務 |
| --- | --- | --- |
| `sealed class` または内部ID構造体 | 採用 | Lua Registryに保存した関数、table、threadの参照管理 |

### LuaObjectRegistry

| 種別 | 状態 | 責務 |
| --- | --- | --- |
| `sealed class` | 採用 | Runtime固有のC#インスタンスとobjectIdの対応管理 |

管理する情報:

- objectIdからC#インスタンスへの参照
- C#インスタンスからobjectIdへの逆引き
- userdata参照数
- 対象型の生成済みバインディング
- Runtime識別情報

### LuaBindingRegistry

| 種別 | 状態 | 責務 |
| --- | --- | --- |
| `sealed class` | 採用 | bindingIdと生成済みC#呼び出し処理の対応管理 |

Runtimeごとに生成し、別Runtimeと登録情報を共有しない。

### LuaCallbackDispatcher

| 種別 | 状態 | 責務 |
| --- | --- | --- |
| `static class` | 採用 | Native Bridgeから受け取った呼び出しを対象RuntimeとbindingIdへ振り分ける共通入口 |

staticに保持するのは状態を持たない共通入口だけとする。C#インスタンスなどのRuntime固有状態は保持しない。

### LuaContinuationRegistry

| 種別 | 状態 | 責務 |
| --- | --- | --- |
| `sealed class` | 採用 | 未完了の `Task`、Lua Coroutine、再開情報の対応管理 |

Runtimeごとに所有し、`Tick` から所有スレッド上で処理する。

### LuaPendingContinuation

| 種別 | 状態 | 責務 |
| --- | --- | --- |
| `sealed class` または `struct` | 検討 | 1件の非同期待機と再開状態を保持 |

### LuaThreadGuard

| 種別 | 状態 | 責務 |
| --- | --- | --- |
| `readonly struct` またはRuntime内部処理 | 採用 | `LuaRuntime` の所有スレッド検証 |

独立した型にするか、`LuaRuntime` のprivate処理にするかは実装時に決定する。

## 値変換内部型

### ILuaConverter<T>

| 項目 | 内容 |
| --- | --- |
| 種別 | `interface` または静的変換規約 |
| 状態 | 検討 |

C#型とLua値を相互変換する。

必要な変換:

- C#からLuaスタックへのpush
- LuaスタックからC#値のread
- 対応型の検証
- 型変換エラーの生成

interface呼び出しのコストやboxingを避けるため、生成済みの静的変換コードへ置き換える可能性がある。

### LuaValueReader

| 種別 | 状態 | 責務 |
| --- | --- | --- |
| `ref struct` | 検討 | Luaスタックから型付き引数や戻り値を読み取る |

### LuaValueWriter

| 種別 | 状態 | 責務 |
| --- | --- | --- |
| `ref struct` | 検討 | 型付きC#値をLuaスタックへ書き込む |

### LuaObjectConverter<T>

| 種別 | 状態 | 責務 |
| --- | --- | --- |
| 生成型 | 採用 | `LuaObjectAttribute` 対象インスタンスとLua userdataを相互変換する |

## Native Interop型

### LuaNative

| 項目 | 内容 |
| --- | --- |
| 種別 | `static class` |
| 状態 | 採用 |
| 公開範囲 | `internal` |

P/Invoke宣言を集約する。

公開APIから直接使用させない。

### LuaNativeCallback

| 項目 | 内容 |
| --- | --- |
| 種別 | `delegate` |
| 状態 | 採用 |
| 公開範囲 | `internal` |

Native Bridgeから共通C#ディスパッチャーを呼び出すための関数ポインター型。

コールバックdelegateはGCされないよう、システムの寿命中に強参照を保持する。

### LuaNativeResult

| 項目 | 内容 |
| --- | --- |
| 種別 | `struct` |
| 状態 | 検討 |
| 公開範囲 | `internal` |

Native BridgeとC#の間で、成功、戻り値数、yield要求、エラーを受け渡すためのABI固定構造体。

## 生成型

Attribute付きメソッドを実行時Reflectionで呼び出さず、Source Generatorで以下の型を生成する。

現段階の動作検証実装ではAttributeをReflectionで読み取るフォールバック経路を使用する。公開APIとLua上の表現を先に検証するための実装であり、boxingを避けるプロダクション経路は以下の生成型へ置き換える。

### GeneratedLuaBindings

プロジェクト内の生成済みバインディングをまとめる入口。

```csharp
internal sealed class GeneratedLuaBindings : ILuaGeneratedModuleBinding {
}
```

### `{TypeName}LuaModuleBinding`

`LuaModuleAttribute` 対象型ごとに生成する。

責務:

- メソッド名とbindingIdの登録
- Lua tableの生成
- 引数の型付き読み取り
- C#メソッド呼び出し
- 戻り値の型付き書き込み
- `Task` / `ValueTask` の判定
- C#例外の捕捉

### `{TypeName}LuaObjectBinding`

`LuaObjectAttribute` 対象型ごとに生成する。

責務:

- userdata用metatableの登録
- `:` 呼び出しから対象C#インスタンスの取得
- インスタンスメソッドの呼び出し
- userdataのGC通知
- Runtimeおよび型の検証

### `{TypeName}LuaConverter`

必要なC#型ごとに生成する値変換処理。

すべての型に生成するのではなく、Attribute対象メソッドのシグネチャから必要なものだけを生成する。

## Source Generator型

### LuaBindingSourceGenerator

| 種別 | 状態 | 責務 |
| --- | --- | --- |
| `sealed class : ISourceGenerator` | 採用 | Attribute対象型を収集し、コンパイル中にバインディングコードを生成 |

### LuaBindingValidator

| 種別 | 状態 | 責務 |
| --- | --- | --- |
| `static class` | 採用 | 未対応シグネチャ、名前重複、AOT非対応構造をビルド前に検出 |

検証対象:

- 未対応の引数型と戻り値型
- Lua上の名前重複
- ジェネリックメソッド
- `ref` / `out` 引数
- Runtimeをまたぐオブジェクト
- `LuaObjectAttribute` のないC#参照型の受け渡し
- 対応していない非同期戻り値

## Lua Coroutine関連型

### 内部Coroutine

`LuaFunction.InvokeAsync` が使用するLua Coroutineは内部実装とし、通常の利用者へ公開しない。

候補となる内部型:

- `LuaAsyncInvocation`
- `LuaCoroutineHandle`
- `LuaCoroutineState`

型名と分割方法は、非同期処理の詳細設計時に決定する。

### Lua本来のCoroutine公開API

Luaの明示的な `yield` / `resume` をC#から操作する公開型は将来設計とする。

通常の `InvokeAsync` と混同しない独立したAPIとして定義する。

## Native Bridge

Native BridgeはC言語で実装するためC#の型一覧には含まれないが、システム構成上の主要要素として管理する。

想定ファイル:

```text
unity_lua_bridge.h
unity_lua_bridge.c
```

主な責務:

- Lua C APIのABIをC#向けに固定
- C#コールバックの共通入口
- C#コールバック完了後の `lua_error`
- `lua_yieldk` と継続関数
- Runtimeコンテキストの関連付け
- ネイティブメモリ統計と上限管理

## 型の依存関係

```text
LuaRuntime
├── LuaRuntimeOptions
├── LuaBindingRegistry
├── LuaObjectRegistry
├── LuaContinuationRegistry
├── LuaFunction / LuaAction
├── LuaModuleRegistration
└── LuaNative

LuaModuleAttribute
LuaObjectAttribute
LuaFunctionAttribute
    ↓ Source Generator
GeneratedLuaBindings
├── TypeLuaModuleBinding
├── TypeLuaObjectBinding
└── TypeLuaConverter
    ↓ registration
LuaBindingRegistry
    ↓ dispatch
LuaCallbackDispatcher
    ↓ P/Invoke
Native Bridge
    ↓
Lua 5.4.9
```

## 今後追加する設計文書

本書を索引として、以下を個別に設計する。

1. Attributeおよびコード生成仕様
2. C#オブジェクトとLua userdataの所有権仕様
3. 値変換と対応型一覧
4. 非同期関数と `Tick` の状態遷移
5. Lua本来のCoroutine公開API
6. Native Bridge API
7. メモリ制限と実行制限

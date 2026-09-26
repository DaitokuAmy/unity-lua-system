# LuaRuntime Public API Specification

## 文書の目的

本書は、利用者へ公開する `LuaRuntime` と、その操作によって返される公開型の設計案を定義する。

Native Bridge、Luaスタック操作、P/Invoke、バインディングコード生成などの内部実装は本書の対象外とする。

本書のAPIは設計段階の案であり、実装開始前にシグネチャを確定する。

## 基本方針

- `LuaRuntime` は `UnityEngine` に依存しない
- Runtime用asmdefの `No Engine References` を有効にする
- 1つの `LuaRuntime` が1つの独立したLua環境を所有する
- Luaのグローバル変数、Registry、C#関数登録、C#インスタンス登録をRuntime間で共有しない
- 1つの `LuaRuntime` を複数スレッドから同時に操作しない
- 複数スレッドでLuaを使用する場合は、スレッドごとに `LuaRuntime` を生成する
- 公開APIでは `object`、`object[]`、`params object[]` を使用しない
- Lua関数は取得時に引数型と戻り値型を確定する
- 非同期呼び出しのために使用するLua Coroutineは、通常の利用者から隠蔽する
- Lua本来のCoroutineを直接操作するAPIは、通常の関数呼び出しとは別の高度なAPIとして扱う

## LuaRuntime

### 基本形

```csharp
public sealed class LuaRuntime : IDisposable {
    public LuaRuntime();
    public LuaRuntime(LuaRuntimeOptions options);

    public bool IsDisposed { get; }

    public void Validate(string source, string chunkName = "chunk");
    public void Execute(string source, string chunkName = "chunk");
    public TResult Execute<TResult>(string source, string chunkName = "chunk");

    public LuaFunction<TResult> GetFunction<TResult>(string name);
    public LuaFunction<T1, TResult> GetFunction<T1, TResult>(string name);
    public LuaFunction<T1, T2, TResult> GetFunction<T1, T2, TResult>(string name);

    public LuaAction GetAction(string name);
    public LuaAction<T1> GetAction<T1>(string name);
    public LuaAction<T1, T2> GetAction<T1, T2>(string name);

    public LuaModuleRegistration RegisterModule<TModule>(TModule module)
        where TModule : class;
    public LuaModuleRegistration RegisterModule<TModule>();
    public LuaBindingRegistration RegisterBindings(ILuaGeneratedBindings bindings);

    public void Tick();
    public void Dispose();
}
```

関数およびActionのジェネリック引数は、初期実装では0～8引数まで提供する案とする。

### 生成

既定設定で生成する。

```csharp
using var runtime = new LuaRuntime();
```

メモリ上限や標準ライブラリなどを指定する場合は、オプションを渡す。

```csharp
var options = new LuaRuntimeOptions {
    MemoryLimitBytes = 64 * 1024 * 1024,
    StandardLibraries = LuaStandardLibraries.Safe,
};

using var runtime = new LuaRuntime(options);
```

### Runtime間の独立性

次の2つのRuntimeは完全に独立したLua環境として扱う。

```csharp
using var firstRuntime = new LuaRuntime();
using var secondRuntime = new LuaRuntime();
```

`firstRuntime` で設定したグローバル変数、Lua関数、C#モジュールおよびCoroutineは、`secondRuntime` から参照できない。

異なるRuntimeから取得した関数や登録ハンドルを、別のRuntimeへ渡して使用することは禁止する。

### スレッド制約

`LuaRuntime` は、生成したスレッドを所有スレッドとして記録する。

以下の操作は所有スレッドでのみ許可する。

- Luaコードの実行
- Lua関数の取得と呼び出し
- C#モジュールの登録と解除
- `Tick`
- `Dispose`

別スレッドから操作された場合は例外を送出する。

複数スレッドで実行する場合は、それぞれでRuntimeを生成する。

```csharp
// Thread A
using var runtimeA = new LuaRuntime();

// Thread B
using var runtimeB = new LuaRuntime();
```

## Luaコードの実行

`Validate` は、Luaソース文字列を実行せずにロードし、構文だけを検証する。

```csharp
runtime.Validate(source, "sample.lua");
```

構文エラーの場合は `LuaException` を送出する。Luaコードを実行しないため、実行時エラーは検出しない。

`Execute` は、Luaソース文字列をロードして即座に実行する。

```csharp
runtime.Execute("value = 10");
```

単一の戻り値を取得する場合は型を指定する。

```csharp
var result = runtime.Execute<int>("return 1 + 2");
```

`Execute` は同期APIとする。未完了のC#非同期関数をLuaコード内から呼び出す場合は、対象Lua関数を `InvokeAsync` で実行する。同期実行中にyieldしようとした場合はLuaエラーとする。

`Execute` はLuaソースやチャンクを実行するAPIであり、定義済みLua関数の呼び出しには使用しない。

## Lua関数の取得と呼び出し

### 戻り値がある関数

関数の取得時に、引数型と戻り値型を指定する。

```csharp
using var add = runtime.GetFunction<int, int, int>("add");

var result = add.Invoke(10, 20);
```

`GetFunction<int, int, int>` は次のシグネチャを表す。

```text
引数1: int
引数2: int
戻り値: int
```

引数なしの場合は、型引数が戻り値型だけになる。

```csharp
using var getVersion = runtime.GetFunction<string>("get_version");

var version = getVersion.Invoke();
```

### 戻り値がない関数

戻り値がないLua関数は `GetAction` で取得する。

```csharp
using var notify = runtime.GetAction<string>("notify");

notify.Invoke("completed");
```

### 非同期呼び出し

取得方法は同期呼び出しと共通とする。

```csharp
using var loadPlayer = runtime.GetFunction<string, PlayerData>("load_player");

var player = await loadPlayer.InvokeAsync("player-id");
```

`InvokeAsync` はライブラリ内部でLua Coroutineを生成し、その上で対象関数を実行する。

Luaから呼び出したC#関数が未完了の `Task` または `ValueTask` を返した場合は、内部Coroutineを一時停止する。非同期処理が完了した後、Runtimeの所有スレッドでCoroutineを再開する。

C#関数が `IEnumerator` を返した場合も内部Coroutineを一時停止する。最初の `MoveNext()` は関数呼び出し時に実行し、以降は `Tick` ごとに1回実行する。`MoveNext()` が `false` を返した時点でLua Coroutineを再開する。`IEnumerator.Current` の値は初期仕様では使用しない。

通常の利用者は、この内部Coroutineを直接操作しない。

同期 `Invoke` の途中で未完了のC#非同期関数が返された場合は、明確な例外を送出する。

## LuaFunction

Lua関数は型付きの参照として取得する。

```csharp
public sealed class LuaFunction<TResult> : IDisposable {
    public TResult Invoke();
    public ValueTask<TResult> InvokeAsync();
    public void Dispose();
}

public sealed class LuaFunction<T1, TResult> : IDisposable {
    public TResult Invoke(T1 argument1);
    public ValueTask<TResult> InvokeAsync(T1 argument1);
    public void Dispose();
}

public sealed class LuaFunction<T1, T2, TResult> : IDisposable {
    public TResult Invoke(T1 argument1, T2 argument2);
    public ValueTask<TResult> InvokeAsync(T1 argument1, T2 argument2);
    public void Dispose();
}
```

`LuaFunction` はLua Registry上の関数参照を所有するため、使用後に破棄する。

```csharp
using var function = runtime.GetFunction<int>("get_value");
```

Runtimeが先に破棄された場合、そのRuntimeから取得したすべての `LuaFunction` を無効とする。以後の呼び出しは `ObjectDisposedException` を送出する。

## LuaAction

戻り値のないLua関数を表す。

```csharp
public sealed class LuaAction : IDisposable {
    public void Invoke();
    public ValueTask InvokeAsync();
    public void Dispose();
}

public sealed class LuaAction<T1> : IDisposable {
    public void Invoke(T1 argument1);
    public ValueTask InvokeAsync(T1 argument1);
    public void Dispose();
}
```

所有権と破棄規則は `LuaFunction` と同じとする。

## C#モジュールの登録

### インスタンスの登録

Attributeが付与されたインスタンスをLua tableとして登録する。

```csharp
var playerApi = new PlayerLuaApi();

using var registration = runtime.RegisterModule(playerApi);
```

Lua側では次のように呼び出す。

```lua
local hp = player.get_hp()
player.damage(10)
```

登録中はRuntimeが対象インスタンスを強参照する。

`LuaModuleRegistration.Dispose` が呼ばれた場合は、Lua側のモジュールとC#インスタンスへの参照を解除する。

Runtimeが先に破棄された場合は、所属する登録をすべて無効とする。

### C#インスタンスをLuaへ返す

`LuaObjectAttribute` が付与されたC#型は、Luaへ公開したC#関数の引数または戻り値として使用できる。

```csharp
[LuaObject]
public sealed partial class Player {
    [LuaFunction]
    public int GetHp() {
        return 100;
    }
}

[LuaModule("game")]
public sealed partial class GameLuaApi {
    [LuaFunction("get_player")]
    public Player GetPlayer() {
        return _player;
    }
}
```

Lua側では、戻されたインスタンスをuserdataとして扱う。

```lua
local player = game.get_player()
local hp = player:get_hp()
```

userdataには生のマネージドポインターを格納せず、所属Runtime内でのみ有効なオブジェクトIDを格納する。

同じC#インスタンスを同じRuntimeへ複数回渡した場合は、同じLua userdataとして扱うことを基本方針とする。別Runtimeへ生成済みuserdataを渡すことは禁止する。

C#がインスタンスの所有者となり、Lua GCから対象インスタンスの `Dispose` を自動的に呼び出さない。

### static関数の登録

static関数はAttributeから生成されたバインディングを登録する。

```csharp
using var registration = runtime.RegisterBindings(
    GeneratedLuaBindings.Instance);
```

具体的なコード生成APIと生成クラス名は、Attributeおよびバインディング仕様で別途決定する。

## Tick

`Tick` はRuntimeの所有スレッドで、保留中の処理を進める。

```csharp
runtime.Tick();
```

主に次の処理を対象とする。

- 完了した `Task` および `ValueTask` の結果取得
- 待機中の `IEnumerator` の `MoveNext()` を1回実行
- 非同期呼び出しで停止している内部Lua Coroutineの再開
- 非同期処理の例外とキャンセルの反映

`Tick` はUnityの `Update` に依存しない。

Unity利用側では、必要に応じて次のように呼び出す。

```csharp
private void Update() {
    _runtime.Tick();
}
```

非同期機能を使用しない場合に `Tick` が必要かどうかは、実装時に明確化する。

## Dispose

`LuaRuntime.Dispose` は次を行う。

- 新しいLua呼び出しの受付停止
- 保留中の非同期処理のキャンセル
- Lua Coroutineの無効化
- 登録済みC#モジュールの解除
- Lua Registry参照の解放
- `lua_State` の破棄
- ネイティブメモリの解放

`Dispose` は複数回呼び出しても安全な実装とする。

`Dispose` 後の公開メソッド呼び出しは、状態確認用プロパティを除いて `ObjectDisposedException` を送出する。

## LuaRuntimeOptions

初期案は次のとおりとする。

```csharp
public sealed class LuaRuntimeOptions {
    public long? MemoryLimitBytes { get; init; }
    public LuaStandardLibraries StandardLibraries { get; init; }
}
```

命令数制限、エラーハンドラー、ログ出力などは、必要性を確認してから追加する。

## エラー

Lua側のエラーは `LuaException` としてC#へ通知する。

```csharp
public sealed class LuaException : Exception {
    public LuaStatus Status { get; }
    public string ChunkName { get; }
    public string LuaTraceback { get; }
}
```

型変換に失敗した場合は、期待したC#型、実際のLua型、対象引数または戻り値の位置をエラーへ含める。

## 本書で未確定の事項

- `ValueTask` / `ValueTask<T>` をLuaへ公開するC#関数の戻り値として扱う時期
- 標準で提供する関数引数の最大数
- 複数戻り値のC#表現
- 関数名を単純なグローバル名だけにするか、`module.function` 形式へ対応するか
- `Tick` を非同期機能利用時に必須とするか、スケジューラーを差し替え可能にするか
- staticバインディングの生成クラス名と登録方法
- キャンセルを `CancellationToken` で公開するか

## 対象外

以下は別の仕様書で定義する。

- Lua本来のCoroutineをC#から明示的に操作するAPI
- Attributeの詳細
- バインディングコード生成
- Lua tableおよびuserdataの公開API
- Unity Coroutineとの連携
- Unity固有型のバインディング

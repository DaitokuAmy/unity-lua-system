# Unity Lua System

Lua 5.4.9公式ソースを利用し、UnityのC#からLuaを実行するためのライブラリです。

Unity固有のGameObjectやComponentをLuaへ公開する機能は持たず、純粋なLua実行環境とC#連携に範囲を限定しています。`UnityLuaSystem.Runtime` は `UnityEngine` に依存しません。

## 主な機能

- Luaソースのロードと実行
- C#から型付きLua関数を呼び出し
- LuaからC#のstatic関数、インスタンス関数を呼び出し
- C#オブジェクトをLua userdataとして受け渡し
- `Task` / `Task<T>` の待機
- `UniTask` / `UniTask<T>` の待機
- `IEnumerator` を `LuaRuntime.Tick()` ごとに進行
- LuaRuntimeごとに独立したLua環境を保持
- Source Generatorによる同期C#関数の型付きバインディング生成

## 動作環境

- Unity 6000.3以降
- Lua 5.4.9

現在、Native Pluginを同梱しているプラットフォームは次のとおりです。

| プラットフォーム | アーキテクチャ |
| --- | --- |
| Windows Editor / Standalone | x86_64 |
| Android | ARM64 |

macOS、iOSおよびその他のプラットフォーム用Native Pluginは未収録です。

## インストール

### Git URLから導入する場合

UnityのPackage Managerで「Install package from git URL」を選び、次を指定します。

```text
https://github.com/DaitokuAmy/unity-lua-system.git?path=Packages/com.daitokuamy.unityluasystem
```

特定のリリースを利用する場合は、末尾にタグを指定します。次の例では `0.8.0` タグを指定しています。

```text
https://github.com/DaitokuAmy/unity-lua-system.git?path=Packages/com.daitokuamy.unityluasystem#0.8.0
```

`0.8.0` は、利用したいリリースタグへ置き換えてください。

リポジトリが非公開の場合は、UnityがGitHubへアクセスできる認証設定が必要です。

### ローカルPackageとして導入する場合

このリポジトリを取得し、利用側プロジェクトの `Packages/manifest.json` へ次を追加します。

```json
{
  "dependencies": {
    "com.daitokuamy.unityluasystem": "file:../unity-lua-system/Packages/com.daitokuamy.unityluasystem"
  }
}
```

パスは利用側プロジェクトからの相対位置に合わせて変更してください。

## 基本的な使い方

### Luaコードを実行する

```csharp
using UnityLuaSystem;

using var runtime = new LuaRuntime();

runtime.Execute("value = 10", "initialize.lua");
var result = runtime.Execute<int>("return value + 20", "calculation.lua");
```

第2引数の `chunkName` は、エラー発生時にLuaコードの読み込み元を識別する名前です。ファイルを読み込んだ場合はファイル名を渡すことを推奨します。

### Luaコードの構文だけを確認する

```csharp
runtime.Validate(source, "sample.lua");
```

`Validate` はLuaコードを実行せずに構文を確認します。構文エラーの場合は `LuaException` を送出しますが、実行時エラーは検出しません。

### 配列を受け渡す

C#の一次元配列は、Luaでは添字が1から始まるtableとして扱われます。

```csharp
using var sum = runtime.GetFunction<int[], int>("sum");
var result = sum.Invoke(new[] { 10, 20, 30 });
```

```lua
function sum(values)
    local result = 0
    for index = 1, #values do
        result = result + values[index]
    end
    return result
end
```

`bool[]`、`int[]`、`long[]`、`float[]`、`double[]`、`string[]` に対応します。C#モジュールの引数と戻り値では、`[LuaObject]` 型の配列も使用できます。Lua側では添字が1から連続するtableを使用してください。多次元配列には対応しません。

### Luaファイルを実行する

Luaファイルを `TextAsset` として参照し、その内容を `Execute` へ渡します。

```csharp
using UnityEngine;
using UnityLuaSystem;

public sealed class LuaRunner : MonoBehaviour {
    [SerializeField]
    private TextAsset _luaScript;

    private LuaRuntime _runtime;

    private void Start() {
        _runtime = new LuaRuntime();
        _runtime.Execute(_luaScript.text, _luaScript.name);
    }

    private void OnDestroy() {
        _runtime?.Dispose();
    }
}
```

関数定義だけが書かれたLuaファイルも、`Execute` によって関数がRuntimeへ登録されます。

## C#からLua関数を呼ぶ

Lua側に関数を定義します。

```lua
function add(left, right)
    return left + right
end

function set_message(message)
    print(message)
end
```

C#側では、関数を取得する時点で引数型と戻り値型を指定します。

```csharp
using var add = runtime.GetFunction<int, int, int>("add");
using var setMessage = runtime.GetAction<string>("set_message");

var result = add.Invoke(10, 20);
setMessage.Invoke("called from C#");
```

`LuaFunction`、`LuaAction` および `LuaModuleRegistration` はLua側の参照を所有するため、不要になった時点で `Dispose` してください。

## LuaからC#関数を呼ぶ

### インスタンス関数を登録する

```csharp
using UnityLuaSystem;

[LuaModule("game")]
public sealed class GameLuaModule {
    [LuaFunction("create_message")]
    public string CreateMessage(string value) {
        return $"C# received: {value}";
    }
}
```

Runtimeへインスタンスを登録します。

```csharp
using var registration = runtime.RegisterModule(new GameLuaModule());
```

Luaからはモジュール名と関数名を使用します。

```lua
local message = game.create_message("hello")
```

### static関数を登録する

```csharp
[LuaModule("calculator")]
public sealed class CalculatorLuaModule {
    [LuaFunction("add")]
    public static int Add(int left, int right) {
        return left + right;
    }
}
```

```csharp
using var registration = runtime.RegisterModule<CalculatorLuaModule>();
```

```lua
local result = calculator.add(10, 20)
```

## C#オブジェクトをLuaへ渡す

`LuaObject` を付けた型は、C#関数の引数または戻り値としてLuaへ渡せます。

```csharp
[LuaObject]
public sealed class Player {
    private int _health = 100;

    [LuaFunction("get_health")]
    public int GetHealth() {
        return _health;
    }

    [LuaFunction("heal")]
    public int Heal(int amount) {
        _health += amount;
        return _health;
    }
}

[LuaModule("game")]
public sealed class GameLuaModule {
    private readonly Player _player = new Player();

    [LuaFunction("get_player")]
    public Player GetPlayer() {
        return _player;
    }
}
```

```lua
local player = game.get_player()
local health = player:get_health()
local healed = player:heal(10)
```

Luaへ渡したオブジェクトは、そのオブジェクトを渡した `LuaRuntime` 内でのみ有効です。

## 非同期処理

### Taskを返すC#関数

```csharp
[LuaFunction("load_async")]
public async Task<string> LoadAsync(string value) {
    await Task.Delay(1000);
    return $"loaded: {value}";
}
```

Lua側では通常の関数と同じように呼び出します。

```lua
function run_async()
    local result = game.load_async("data")
    return result
end
```

C#側では `InvokeAsync` を使用し、毎フレーム `Tick` を呼びます。

```csharp
private LuaRuntime _runtime;

private void Update() {
    _runtime?.Tick();
}

private async void RunAsync() {
    using var function = _runtime.GetFunction<string>("run_async");
    var result = await function.InvokeAsync();
}
```

未完了のC#非同期処理を呼び出すLua関数は、同期版の `Invoke` ではなく `InvokeAsync` で実行してください。

### IEnumeratorを返すC#関数

`IEnumerator` はUnityの `StartCoroutine` を使用せず、`LuaRuntime.Tick()` ごとに1ステップ進みます。

```csharp
[LuaFunction("run_operation")]
public IEnumerator RunOperation() {
    while (!IsCompleted()) {
        yield return null;
    }
}
```

`WaitForSeconds` など、Unity Coroutine固有のyield命令は処理しません。利用者が管理する処理の完了待ちを目的とした機能です。

## UniTask連携

UniTaskを使用する場合は、利用側プロジェクトへUniTaskを追加します。

```text
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11
```

Runtime生成後に一度だけ `UseUniTask` を呼びます。

```csharp
using UnityLuaSystem;
using UnityLuaSystem.UniTask;

var runtime = new LuaRuntime();
runtime.UseUniTask();
```

登録後は `UniTask` / `UniTask<T>` を返す関数を、`Task` と同様にLuaから呼び出せます。

```csharp
[LuaFunction("load_unitask_async")]
public async UniTask<string> LoadUniTaskAsync() {
    await UniTask.Delay(1000);
    return "completed";
}
```

UniTask連携は独立したasmdefに分離されており、コアの `UnityLuaSystem.Runtime` はUniTaskへ依存しません。

## Source Generator

PackageにはSource Generatorが同梱されています。利用者による生成操作や生成ファイルの管理は必要ありません。

コンパイル時に `[LuaModule]` と `[LuaFunction]` を検出し、同期C#関数を直接呼び出すバインディングを自動生成します。生成対象外の関数は既存のReflection経路へフォールバックします。

現在の生成対象は次のとおりです。

- publicな同期関数
- `bool`、`int`、`long`、`float`、`double`、`string`
- `[LuaObject]` が付いた型
- 対応型および `[LuaObject]` 型の一次元配列

`Task`、`UniTask`、`IEnumerator` などの非同期関数は、現在はReflection経路で処理します。利用者側の呼び出し方は生成経路とReflection経路で変わりません。

## 対応している値型

現在、LuaとC#の間で直接変換できる基本型は次のとおりです。

- `bool`
- `int`
- `long`
- `float`
- `double`
- `string`
- 対応する基本型の一次元配列
- `[LuaObject]` が付いた参照型

Lua関数およびActionの型付きAPIは、現在0～2引数まで実装されています。

## Runtimeとスレッド

- 1つの `LuaRuntime` は、1つの独立した `lua_State` を所有する
- Runtime間でグローバル変数や登録オブジェクトを共有しない
- `LuaRuntime` は生成したスレッドからのみ操作する
- 複数スレッドでLuaを使う場合は、スレッドごとにRuntimeを生成する
- `LuaRuntime`、関数参照、モジュール登録は明示的に破棄する

## サンプル

このリポジトリには、以下のサンプルがあります。

- `Assets/LuaScripts/sample.lua`
- `Assets/Scripts/Runtime/LuaSampleRunner.cs`
- `Assets/Scripts/Runtime/LuaBindingSample.cs`
- `Assets/Scenes/Sample.unity`

サンプルでは次の処理を確認できます。

- C#からLua関数を呼び出す
- Luaからstatic / インスタンスC#関数を呼び出す
- `LuaObject` をLuaへ渡す
- Lua tableとC#配列を相互変換する
- `Task`、`UniTask`、`IEnumerator` の完了後にLua処理を再開する

## ライセンス

[MIT License](LICENSE.md)

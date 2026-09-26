using UnityEngine;
using UnityLuaSystem;
using UnityLuaSystem.UniTask;

namespace UnityLuaSystemSamples {
    /// <summary>
    /// サンプルシーンでLuaコードを実行するコンポーネント
    /// </summary>
    public sealed class LuaSampleRunner : MonoBehaviour {
        [SerializeField, Tooltip("実行するLuaスクリプト")]
        private TextAsset _luaScript;

        private LuaRuntime _runtime;

        /// <summary>
        /// LuaRuntimeを生成してサンプルを開始
        /// </summary>
        private void Start() {
            _runtime = new LuaRuntime();
            _runtime.UseUniTask();
            Run();
        }

        /// <summary>
        /// 待機中のLua処理を進行
        /// </summary>
        private void Update() {
            if (_runtime != null && !_runtime.IsDisposed) {
                _runtime.Tick();
            }
        }

        /// <summary>
        /// LuaRuntimeを破棄
        /// </summary>
        private void OnDestroy() {
            _runtime?.Dispose();
            _runtime = null;
        }

        /// <summary>
        /// Luaコードを実行して整数の戻り値をConsoleへ出力
        /// </summary>
        [ContextMenu("Run Lua Sample")]
        public async void Run() {
            if (_luaScript == null) {
                Debug.LogError("Lua script is not assigned.", this);
                return;
            }

            using var staticModule = _runtime.RegisterModule<SampleStaticModule>();
            using var instanceModule = _runtime.RegisterModule(new SampleInstanceModule());
            _runtime.Execute(_luaScript.text, _luaScript.name);

            using var add = _runtime.GetFunction<int, int, int>("add");
            using var multiply = _runtime.GetFunction<double, double, double>("multiply");
            using var greet = _runtime.GetFunction<string, string>("greet");
            using var isEven = _runtime.GetFunction<int, bool>("is_even");
            using var sumArray = _runtime.GetFunction<int[], int>("sum_array");
            using var setMessage = _runtime.GetAction<string>("set_message");
            using var getMessage = _runtime.GetFunction<string>("get_message");
            using var runCSharpBindingSample = _runtime.GetFunction<string>("run_csharp_binding_sample");
            using var runCSharpArraySample = _runtime.GetFunction<int>("run_csharp_array_sample");
            using var runCSharpAsyncSample = _runtime.GetFunction<string>("run_csharp_async_sample");
            using var runCSharpUniTaskSample = _runtime.GetFunction<string>("run_csharp_unitask_sample");
            using var runCSharpCoroutineSample = _runtime.GetFunction<string>("run_csharp_coroutine_sample");

            setMessage.Invoke("Message from C#");

            Debug.Log($"Lua add: 10 + 20 = {add.Invoke(10, 20)}", this);
            Debug.Log($"Lua multiply: 1.5 * 4 = {multiply.Invoke(1.5, 4.0)}", this);
            Debug.Log($"Lua greet: {greet.Invoke("Unity")}", this);
            Debug.Log($"Lua is_even: 42 = {isEven.Invoke(42)}", this);
            Debug.Log($"C# array to Lua table: {sumArray.Invoke(new[] { 10, 20, 30 })}", this);
            Debug.Log($"Lua message: {getMessage.Invoke()}", this);
            Debug.Log($"Lua to C#: {runCSharpBindingSample.Invoke()}", this);
            Debug.Log($"Lua table and C# array binding: {runCSharpArraySample.Invoke()}", this);

            Debug.Log($"Lua async C#: {await runCSharpAsyncSample.InvokeAsync()}", this);
            Debug.Log($"Lua UniTask C#: {await runCSharpUniTaskSample.InvokeAsync()}", this);
            Debug.Log($"Lua IEnumerator: {await runCSharpCoroutineSample.InvokeAsync()}", this);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityLuaSystem;
using UnityLuaSystem.UniTask;

namespace UnityLuaSystemSamples {
    /// <summary>
    /// サンプルシーンでLuaコードを実行し、結果をuGUIへ表示するコンポーネント
    /// 同期呼び出し、C#バインディング、非同期処理の動作確認をまとめて行う
    /// </summary>
    public sealed class LuaSampleRunner : MonoBehaviour {
        private static readonly Color RunningColor = new(1f, 0.85f, 0.25f);
        private static readonly Color SuccessColor = new(0.3f, 1f, 0.45f);
        private static readonly Color FailedColor = new(1f, 0.35f, 0.35f);

        [SerializeField, Tooltip("実行するLuaスクリプト")]
        private TextAsset _luaScript;
        [SerializeField, Tooltip("実行状態を表示するテキスト")]
        private Text _statusText;
        [SerializeField, Tooltip("各サンプルの実行結果を表示するテキスト")]
        private Text _resultText;
        [SerializeField, Tooltip("サンプルを再実行するボタン")]
        private Button _runButton;

        private readonly List<string> _results = new();
        private LuaRuntime _runtime;
        private bool _isRunning;

        /// <summary>
        /// LuaランタイムとuGUIを初期化してサンプルを開始
        /// </summary>
        private void Start() {
            if (!ValidateUIReferences()) {
                enabled = false;
                return;
            }

            _runButton.onClick.AddListener(Run);

            _runtime = new LuaRuntime();
            _runtime.UseUniTask();
            Run();
        }

        /// <summary>
        /// 待機中のLua処理をフレームごとに進行
        /// </summary>
        private void Update() {
            if (_runtime != null && !_runtime.IsDisposed) {
                _runtime.Tick();
            }
        }

        /// <summary>
        /// uGUIのイベント購読を解除してLuaランタイムを破棄
        /// </summary>
        private void OnDestroy() {
            _runButton?.onClick.RemoveListener(Run);
            _runtime?.Dispose();
            _runtime = null;
        }

        /// <summary>
        /// Luaサンプルを実行し、各処理の結果をuGUIとConsoleへ出力
        /// 実行中の多重呼び出しは無視する
        /// </summary>
        [ContextMenu("Run Lua Sample")]
        public async void Run() {
            if (_isRunning) {
                return;
            }

            if (!ValidateUIReferences()) {
                return;
            }

            _results.Clear();
            UpdateResultText();

            // シーンの設定漏れを画面上でも判別できるよう、実行前に必須参照を検証する。
            if (_luaScript == null) {
                SetFailed("Lua script is not assigned.", null);
                return;
            }

            if (_runtime == null || _runtime.IsDisposed) {
                SetFailed("Lua runtime is not available. Enter Play Mode to run the sample.", null);
                return;
            }

            _isRunning = true;
            _runButton.interactable = false;
            SetStatus("RUNNING - Executing Lua and C# binding samples...", RunningColor);

            try {
                // モジュールはサンプル実行中だけ登録し、完了時に確実に登録解除する。
                using var staticModule = _runtime.RegisterModule<SampleStaticModule>();
                using var instanceModule = _runtime.RegisterModule(new SampleInstanceModule());
                _runtime.Execute(_luaScript.text, _luaScript.name);

                // Lua側に定義された各関数を型付きデリゲートとして取得する。
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

                // 同期呼び出しとC#バインディングの結果を順番に検証する。
                setMessage.Invoke("Message from C#");
                AddResult($"Lua add: 10 + 20 = {add.Invoke(10, 20)}");
                AddResult($"Lua multiply: 1.5 * 4 = {multiply.Invoke(1.5, 4.0)}");
                AddResult($"Lua greet: {greet.Invoke("Unity")}");
                AddResult($"Lua is_even: 42 = {isEven.Invoke(42)}");
                AddResult($"C# array to Lua table: {sumArray.Invoke(new[] { 10, 20, 30 })}");
                AddResult($"Lua message: {getMessage.Invoke()}");
                AddResult($"Lua to C#: {runCSharpBindingSample.Invoke()}");
                AddResult($"Lua table and C# array binding: {runCSharpArraySample.Invoke()}");

                // LuaがC#側の完了を待ってから再開できることをそれぞれ確認する。
                AddResult($"Lua async C#: {await runCSharpAsyncSample.InvokeAsync()}");
                AddResult($"Lua UniTask C#: {await runCSharpUniTaskSample.InvokeAsync()}");
                AddResult($"Lua IEnumerator: {await runCSharpCoroutineSample.InvokeAsync()}");

                SetStatus($"SUCCESS - {_results.Count} checks completed.", SuccessColor);
            }
            catch (Exception exception) {
                SetFailed($"Sample failed: {exception.Message}", exception);
            }
            finally {
                _isRunning = false;
                if (_runButton != null) {
                    _runButton.interactable = true;
                }
            }
        }

        /// <summary>
        /// 成功した処理を結果一覧とConsoleへ追加
        /// </summary>
        /// <param name="message">表示する処理結果</param>
        private void AddResult(string message) {
            _results.Add($"[PASS] {message}");
            UpdateResultText();
            Debug.Log(message, this);
        }

        /// <summary>
        /// 失敗状態をuGUIとConsoleへ出力
        /// </summary>
        /// <param name="message">表示するエラー概要</param>
        /// <param name="exception">発生した例外。例外がない場合はnull</param>
        private void SetFailed(string message, Exception exception) {
            _results.Add($"[FAIL] {message}");
            UpdateResultText();
            SetStatus($"FAILED - {message}", FailedColor);

            if (exception == null) {
                Debug.LogError(message, this);
            }
            else {
                Debug.LogException(exception, this);
            }
        }

        /// <summary>
        /// 現在の実行状態と表示色を更新
        /// </summary>
        /// <param name="message">表示する状態</param>
        /// <param name="color">状態の表示色</param>
        private void SetStatus(string message, Color color) {
            _statusText.text = message;
            _statusText.color = color;
        }

        /// <summary>
        /// 蓄積した処理結果をuGUIへ反映
        /// </summary>
        private void UpdateResultText() {
            if (_resultText != null) {
                _resultText.text = string.Join("\n", _results);
            }
        }

        /// <summary>
        /// シーンに必要なuGUI参照が設定されていることを検証
        /// </summary>
        /// <returns>すべての参照が設定されている場合はtrue</returns>
        private bool ValidateUIReferences() {
            if (_statusText != null && _resultText != null && _runButton != null) {
                return true;
            }

            Debug.LogError("Sample UI references are not assigned.", this);
            return false;
        }
    }
}

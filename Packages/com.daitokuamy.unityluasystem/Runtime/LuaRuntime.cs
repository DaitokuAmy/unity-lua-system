using System;
using System.Runtime.InteropServices;
using System.Text;

namespace UnityLuaSystem {
    /// <summary>
    /// 独立したLua実行環境を管理するクラス
    /// </summary>
    public sealed partial class LuaRuntime : IDisposable {
        /// <summary>Lua関数から戻り値を受け取らないことを示す数</summary>
        private const int NoResult = 0;
        /// <summary>Lua関数から単一の戻り値を受け取ることを示す数</summary>
        private const int SingleResult = 1;
        /// <summary>Luaスタックの最上段を示すインデックス</summary>
        private const int TopStackIndex = -1;

        private readonly int _ownerThreadId;

        private IntPtr _state;

        /// <summary>Runtimeが破棄済みかどうか</summary>
        public bool IsDisposed => _state == IntPtr.Zero;

        /// <summary>
        /// 既定設定でLua実行環境を生成
        /// </summary>
        public LuaRuntime() : this(new LuaRuntimeOptions()) {
        }

        /// <summary>
        /// 設定を指定してLua実行環境を生成
        /// </summary>
        /// <param name="options">Runtimeの生成設定</param>
        public LuaRuntime(LuaRuntimeOptions options) {
            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            _ownerThreadId = Environment.CurrentManagedThreadId;
            _state = LuaNative.NewState();
            if (_state == IntPtr.Zero) {
                throw new InvalidOperationException("Failed to create a Lua state.");
            }

            if (options.OpenStandardLibraries) {
                LuaNative.OpenLibraries(_state);
            }
        }

        /// <summary>
        /// Luaソースを実行せずに構文を検証
        /// </summary>
        /// <param name="source">検証するLuaソース</param>
        /// <param name="chunkName">エラー表示に使用するチャンク名</param>
        public void Validate(string source, string chunkName = "chunk") {
            ValidateAccess();
            ValidateSource(source, chunkName);

            using var stack = new LuaStackGuard(_state);
            Load(source, chunkName);
        }

        /// <summary>
        /// Luaソースをロードして実行
        /// </summary>
        /// <param name="source">実行するLuaソース</param>
        /// <param name="chunkName">エラー表示に使用するチャンク名</param>
        public void Execute(string source, string chunkName = "chunk") {
            ValidateAccess();
            ValidateSource(source, chunkName);

            using var stack = new LuaStackGuard(_state);
            Load(source, chunkName);
            Call(0, NoResult, chunkName);
        }

        /// <summary>
        /// Luaソースをロードして実行し、単一の戻り値を取得
        /// </summary>
        /// <typeparam name="TResult">戻り値のC#型</typeparam>
        /// <param name="source">実行するLuaソース</param>
        /// <param name="chunkName">エラー表示に使用するチャンク名</param>
        /// <returns>Luaコードが返した値</returns>
        public TResult Execute<TResult>(string source, string chunkName = "chunk") {
            ValidateAccess();
            ValidateSource(source, chunkName);

            using var stack = new LuaStackGuard(_state);
            Load(source, chunkName);
            Call(0, SingleResult, chunkName);
            return LuaValueConverter<TResult>.Read(_state, TopStackIndex);
        }

        /// <summary>
        /// 引数なしで戻り値があるLua関数を取得
        /// </summary>
        /// <typeparam name="TResult">戻り値のC#型</typeparam>
        /// <param name="name">取得するグローバル関数名</param>
        /// <returns>型付きLua関数参照</returns>
        public LuaFunction<TResult> GetFunction<TResult>(string name) {
            return new LuaFunction<TResult>(CreateFunctionReference(name));
        }

        /// <summary>
        /// 引数が1つで戻り値があるLua関数を取得
        /// </summary>
        /// <typeparam name="T1">第1引数のC#型</typeparam>
        /// <typeparam name="TResult">戻り値のC#型</typeparam>
        /// <param name="name">取得するグローバル関数名</param>
        /// <returns>型付きLua関数参照</returns>
        public LuaFunction<T1, TResult> GetFunction<T1, TResult>(string name) {
            return new LuaFunction<T1, TResult>(CreateFunctionReference(name));
        }

        /// <summary>
        /// 引数が2つで戻り値があるLua関数を取得
        /// </summary>
        /// <typeparam name="T1">第1引数のC#型</typeparam>
        /// <typeparam name="T2">第2引数のC#型</typeparam>
        /// <typeparam name="TResult">戻り値のC#型</typeparam>
        /// <param name="name">取得するグローバル関数名</param>
        /// <returns>型付きLua関数参照</returns>
        public LuaFunction<T1, T2, TResult> GetFunction<T1, T2, TResult>(string name) {
            return new LuaFunction<T1, T2, TResult>(CreateFunctionReference(name));
        }

        /// <summary>
        /// 引数なしで戻り値がないLua関数を取得
        /// </summary>
        /// <param name="name">取得するグローバル関数名</param>
        /// <returns>型付きLua関数参照</returns>
        public LuaAction GetAction(string name) {
            return new LuaAction(CreateFunctionReference(name));
        }

        /// <summary>
        /// 引数が1つで戻り値がないLua関数を取得
        /// </summary>
        /// <typeparam name="T1">第1引数のC#型</typeparam>
        /// <param name="name">取得するグローバル関数名</param>
        /// <returns>型付きLua関数参照</returns>
        public LuaAction<T1> GetAction<T1>(string name) {
            return new LuaAction<T1>(CreateFunctionReference(name));
        }

        /// <summary>
        /// 引数が2つで戻り値がないLua関数を取得
        /// </summary>
        /// <typeparam name="T1">第1引数のC#型</typeparam>
        /// <typeparam name="T2">第2引数のC#型</typeparam>
        /// <param name="name">取得するグローバル関数名</param>
        /// <returns>型付きLua関数参照</returns>
        public LuaAction<T1, T2> GetAction<T1, T2>(string name) {
            return new LuaAction<T1, T2>(CreateFunctionReference(name));
        }

        /// <summary>
        /// Lua実行環境を破棄
        /// </summary>
        public void Dispose() {
            ValidateThread();
            if (_state == IntPtr.Zero) {
                return;
            }

            FailAllAsyncCalls(new ObjectDisposedException(nameof(LuaRuntime)));
            LuaNative.Close(_state);
            _state = IntPtr.Zero;
            ReleaseBindingHandles();
        }

        /// <summary>
        /// 引数なしで戻り値があるLua関数参照を実行
        /// </summary>
        internal TResult Invoke<TResult>(int reference) {
            ValidateAccess();
            using var stack = new LuaStackGuard(_state);
            PushFunction(reference);
            Call(0, SingleResult, null);
            return LuaValueConverter<TResult>.Read(_state, TopStackIndex);
        }

        /// <summary>
        /// 引数が1つで戻り値があるLua関数参照を実行
        /// </summary>
        internal TResult Invoke<T1, TResult>(int reference, T1 argument1) {
            ValidateAccess();
            using var stack = new LuaStackGuard(_state);
            PushFunction(reference);
            LuaValueConverter<T1>.Push(_state, argument1);
            Call(1, SingleResult, null);
            return LuaValueConverter<TResult>.Read(_state, TopStackIndex);
        }

        /// <summary>
        /// 引数が2つで戻り値があるLua関数参照を実行
        /// </summary>
        internal TResult Invoke<T1, T2, TResult>(int reference, T1 argument1, T2 argument2) {
            ValidateAccess();
            using var stack = new LuaStackGuard(_state);
            PushFunction(reference);
            LuaValueConverter<T1>.Push(_state, argument1);
            LuaValueConverter<T2>.Push(_state, argument2);
            Call(2, SingleResult, null);
            return LuaValueConverter<TResult>.Read(_state, TopStackIndex);
        }

        /// <summary>
        /// 引数なしで戻り値がないLua関数参照を実行
        /// </summary>
        internal void InvokeAction(int reference) {
            ValidateAccess();
            using var stack = new LuaStackGuard(_state);
            PushFunction(reference);
            Call(0, NoResult, null);
        }

        /// <summary>
        /// 引数が1つで戻り値がないLua関数参照を実行
        /// </summary>
        internal void InvokeAction<T1>(int reference, T1 argument1) {
            ValidateAccess();
            using var stack = new LuaStackGuard(_state);
            PushFunction(reference);
            LuaValueConverter<T1>.Push(_state, argument1);
            Call(1, NoResult, null);
        }

        /// <summary>
        /// 引数が2つで戻り値がないLua関数参照を実行
        /// </summary>
        internal void InvokeAction<T1, T2>(int reference, T1 argument1, T2 argument2) {
            ValidateAccess();
            using var stack = new LuaStackGuard(_state);
            PushFunction(reference);
            LuaValueConverter<T1>.Push(_state, argument1);
            LuaValueConverter<T2>.Push(_state, argument2);
            Call(2, NoResult, null);
        }

        /// <summary>
        /// Lua Registry上の参照を解放
        /// </summary>
        internal void ReleaseReference(int reference) {
            ValidateThread();
            if (_state != IntPtr.Zero) {
                LuaNative.ReleaseReference(_state, LuaNative.RegistryIndex, reference);
            }
        }

        /// <summary>
        /// 文字列を終端null付きUTF-8バイト列へ変換
        /// </summary>
        private static byte[] ToNullTerminatedUtf8(string value) {
            var valueBytes = Encoding.UTF8.GetBytes(value);
            var bytes = new byte[valueBytes.Length + 1];
            Buffer.BlockCopy(valueBytes, 0, bytes, 0, valueBytes.Length);
            return bytes;
        }

        /// <summary>
        /// 実行するLuaソースとチャンク名を検証
        /// </summary>
        private static void ValidateSource(string source, string chunkName) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }
            if (string.IsNullOrEmpty(chunkName)) {
                throw new ArgumentException("Chunk name must not be empty.", nameof(chunkName));
            }
        }

        /// <summary>
        /// グローバルLua関数のRegistry参照を生成
        /// </summary>
        private LuaReferenceHandle CreateFunctionReference(string name) {
            ValidateAccess();
            if (string.IsNullOrEmpty(name)) {
                throw new ArgumentException("Function name must not be empty.", nameof(name));
            }

            using var stack = new LuaStackGuard(_state);
            var type = LuaNative.GetGlobal(_state, ToNullTerminatedUtf8(name));
            if (type != LuaValueType.Function) {
                throw new LuaException(LuaStatus.RuntimeError, $"The global value '{name}' is '{type}', not a function.");
            }

            var reference = LuaNative.CreateReference(_state, LuaNative.RegistryIndex);
            return new LuaReferenceHandle(this, reference);
        }

        /// <summary>
        /// Luaソースをチャンクとしてロード
        /// </summary>
        private void Load(string source, string chunkName) {
            var sourceBytes = Encoding.UTF8.GetBytes(source);
            var status = (LuaStatus)LuaNative.LoadBuffer(_state, sourceBytes, new UIntPtr((uint)sourceBytes.Length), ToNullTerminatedUtf8(chunkName), IntPtr.Zero);
            ThrowIfLuaError(status, chunkName);
        }

        /// <summary>
        /// Luaスタック上の関数を保護呼び出し
        /// </summary>
        private void Call(int argumentCount, int resultCount, string chunkName) {
            var status = (LuaStatus)LuaNative.ProtectedCall(_state, argumentCount, resultCount, 0, IntPtr.Zero, IntPtr.Zero);
            ThrowIfLuaError(status, chunkName);
        }

        /// <summary>
        /// Lua Registry上の関数参照をスタックへ積む
        /// </summary>
        private void PushFunction(int reference) {
            var type = LuaNative.RawGetInteger(_state, LuaNative.RegistryIndex, reference);
            if (type != LuaValueType.Function) {
                throw new LuaException(LuaStatus.RuntimeError, "The Lua function reference is no longer valid.");
            }
        }

        /// <summary>
        /// メインLua stateの文字列を取得
        /// </summary>
        private string GetString(int index) {
            return GetString(_state, index);
        }

        /// <summary>
        /// 指定したLua stateの文字列を取得
        /// </summary>
        private string GetString(IntPtr state, int index) {
            var pointer = LuaNative.ToStringPointer(state, index, out var nativeLength);
            if (pointer == IntPtr.Zero) {
                return string.Empty;
            }

            var length = checked((int)nativeLength.ToUInt64());
            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Runtimeへ現在のスレッドからアクセスできることを検証
        /// </summary>
        private void ValidateAccess() {
            ValidateThread();
            if (_state == IntPtr.Zero) {
                throw new ObjectDisposedException(nameof(LuaRuntime));
            }
        }

        /// <summary>
        /// 現在のスレッドがRuntimeの所有スレッドであることを検証
        /// </summary>
        private void ValidateThread() {
            if (Environment.CurrentManagedThreadId != _ownerThreadId) {
                throw new InvalidOperationException("LuaRuntime can only be accessed from the thread that created it.");
            }
        }

        /// <summary>
        /// Luaの実行結果がエラーの場合に例外を送出
        /// </summary>
        private void ThrowIfLuaError(LuaStatus status, string chunkName) {
            if (status == LuaStatus.Ok) {
                return;
            }

            var message = GetString(TopStackIndex);
            throw new LuaException(status, string.IsNullOrEmpty(message) ? $"Lua failed with status {status}." : message, chunkName);
        }
    }
}

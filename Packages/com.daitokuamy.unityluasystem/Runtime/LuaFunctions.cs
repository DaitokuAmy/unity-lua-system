using System;
using System.Threading.Tasks;

namespace UnityLuaSystem {
    /// <summary>
    /// 引数なしで戻り値があるLua関数への参照
    /// </summary>
    public sealed class LuaFunction<TResult> : IDisposable {
        private readonly LuaReferenceHandle _handle;

        /// <summary>
        /// 型付きLua関数参照を生成
        /// </summary>
        /// <param name="handle">Lua Registry上の関数参照</param>
        internal LuaFunction(LuaReferenceHandle handle) {
            _handle = handle;
        }

        /// <summary>
        /// Lua関数を同期実行
        /// </summary>
        public TResult Invoke() {
            return _handle.Runtime.Invoke<TResult>(_handle.Reference);
        }

        /// <summary>
        /// Lua関数をCoroutine上で非同期実行
        /// </summary>
        public ValueTask<TResult> InvokeAsync() {
            return _handle.Runtime.InvokeAsync<TResult>(_handle.Reference);
        }

        /// <summary>
        /// Lua関数への参照を解放
        /// </summary>
        public void Dispose() {
            _handle.Dispose();
        }
    }

    /// <summary>
    /// 引数が1つで戻り値があるLua関数への参照
    /// </summary>
    public sealed class LuaFunction<T1, TResult> : IDisposable {
        private readonly LuaReferenceHandle _handle;

        /// <summary>
        /// 型付きLua関数参照を生成
        /// </summary>
        /// <param name="handle">Lua Registry上の関数参照</param>
        internal LuaFunction(LuaReferenceHandle handle) {
            _handle = handle;
        }

        /// <summary>
        /// Lua関数を同期実行
        /// </summary>
        /// <param name="argument1">第1引数</param>
        public TResult Invoke(T1 argument1) {
            return _handle.Runtime.Invoke<T1, TResult>(_handle.Reference, argument1);
        }

        /// <summary>
        /// Lua関数をCoroutine上で非同期実行
        /// </summary>
        /// <param name="argument1">第1引数</param>
        public ValueTask<TResult> InvokeAsync(T1 argument1) {
            return _handle.Runtime.InvokeAsync<T1, TResult>(_handle.Reference, argument1);
        }

        /// <summary>
        /// Lua関数への参照を解放
        /// </summary>
        public void Dispose() {
            _handle.Dispose();
        }
    }

    /// <summary>
    /// 引数が2つで戻り値があるLua関数への参照
    /// </summary>
    public sealed class LuaFunction<T1, T2, TResult> : IDisposable {
        private readonly LuaReferenceHandle _handle;

        /// <summary>
        /// 型付きLua関数参照を生成
        /// </summary>
        /// <param name="handle">Lua Registry上の関数参照</param>
        internal LuaFunction(LuaReferenceHandle handle) {
            _handle = handle;
        }

        /// <summary>
        /// Lua関数を同期実行
        /// </summary>
        /// <param name="argument1">第1引数</param>
        /// <param name="argument2">第2引数</param>
        public TResult Invoke(T1 argument1, T2 argument2) {
            return _handle.Runtime.Invoke<T1, T2, TResult>(_handle.Reference, argument1, argument2);
        }

        /// <summary>
        /// Lua関数をCoroutine上で非同期実行
        /// </summary>
        /// <param name="argument1">第1引数</param>
        /// <param name="argument2">第2引数</param>
        public ValueTask<TResult> InvokeAsync(T1 argument1, T2 argument2) {
            return _handle.Runtime.InvokeAsync<T1, T2, TResult>(_handle.Reference, argument1, argument2);
        }

        /// <summary>
        /// Lua関数への参照を解放
        /// </summary>
        public void Dispose() {
            _handle.Dispose();
        }
    }

    /// <summary>
    /// 引数なしで戻り値がないLua関数への参照
    /// </summary>
    public sealed class LuaAction : IDisposable {
        private readonly LuaReferenceHandle _handle;

        /// <summary>
        /// 型付きLua関数参照を生成
        /// </summary>
        /// <param name="handle">Lua Registry上の関数参照</param>
        internal LuaAction(LuaReferenceHandle handle) {
            _handle = handle;
        }

        /// <summary>
        /// Lua関数を同期実行
        /// </summary>
        public void Invoke() {
            _handle.Runtime.InvokeAction(_handle.Reference);
        }

        /// <summary>
        /// Lua関数をCoroutine上で非同期実行
        /// </summary>
        public ValueTask InvokeAsync() {
            return _handle.Runtime.InvokeActionAsync(_handle.Reference);
        }

        /// <summary>
        /// Lua関数への参照を解放
        /// </summary>
        public void Dispose() {
            _handle.Dispose();
        }
    }

    /// <summary>
    /// 引数が1つで戻り値がないLua関数への参照
    /// </summary>
    public sealed class LuaAction<T1> : IDisposable {
        private readonly LuaReferenceHandle _handle;

        /// <summary>
        /// 型付きLua関数参照を生成
        /// </summary>
        /// <param name="handle">Lua Registry上の関数参照</param>
        internal LuaAction(LuaReferenceHandle handle) {
            _handle = handle;
        }

        /// <summary>
        /// Lua関数を同期実行
        /// </summary>
        /// <param name="argument1">第1引数</param>
        public void Invoke(T1 argument1) {
            _handle.Runtime.InvokeAction(_handle.Reference, argument1);
        }

        /// <summary>
        /// Lua関数をCoroutine上で非同期実行
        /// </summary>
        /// <param name="argument1">第1引数</param>
        public ValueTask InvokeAsync(T1 argument1) {
            return _handle.Runtime.InvokeActionAsync(_handle.Reference, argument1);
        }

        /// <summary>
        /// Lua関数への参照を解放
        /// </summary>
        public void Dispose() {
            _handle.Dispose();
        }
    }

    /// <summary>
    /// 引数が2つで戻り値がないLua関数への参照
    /// </summary>
    public sealed class LuaAction<T1, T2> : IDisposable {
        private readonly LuaReferenceHandle _handle;

        /// <summary>
        /// 型付きLua関数参照を生成
        /// </summary>
        /// <param name="handle">Lua Registry上の関数参照</param>
        internal LuaAction(LuaReferenceHandle handle) {
            _handle = handle;
        }

        /// <summary>
        /// Lua関数を同期実行
        /// </summary>
        /// <param name="argument1">第1引数</param>
        /// <param name="argument2">第2引数</param>
        public void Invoke(T1 argument1, T2 argument2) {
            _handle.Runtime.InvokeAction(_handle.Reference, argument1, argument2);
        }

        /// <summary>
        /// Lua関数をCoroutine上で非同期実行
        /// </summary>
        /// <param name="argument1">第1引数</param>
        /// <param name="argument2">第2引数</param>
        public ValueTask InvokeAsync(T1 argument1, T2 argument2) {
            return _handle.Runtime.InvokeActionAsync(_handle.Reference, argument1, argument2);
        }

        /// <summary>
        /// Lua関数への参照を解放
        /// </summary>
        public void Dispose() {
            _handle.Dispose();
        }
    }
}

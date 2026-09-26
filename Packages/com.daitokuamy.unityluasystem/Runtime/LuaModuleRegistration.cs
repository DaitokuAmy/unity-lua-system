using System;

namespace UnityLuaSystem {
    /// <summary>
    /// Luaへ登録したC#モジュールの有効期間を管理するクラス
    /// </summary>
    public sealed class LuaModuleRegistration : IDisposable {
        private readonly string _moduleName;
        private readonly LuaRuntime.BindingContext[] _bindings;

        private LuaRuntime _runtime;

        /// <summary>
        /// Luaモジュールの登録情報を生成
        /// </summary>
        /// <param name="runtime">登録先のLuaRuntime</param>
        /// <param name="moduleName">Luaへ公開したモジュール名</param>
        /// <param name="bindings">登録したC#メソッドのバインディング</param>
        internal LuaModuleRegistration(LuaRuntime runtime, string moduleName, LuaRuntime.BindingContext[] bindings) {
            _runtime = runtime;
            _moduleName = moduleName;
            _bindings = bindings;
        }

        /// <inheritdoc/>
        public void Dispose() {
            var runtime = _runtime;
            if (runtime == null) {
                return;
            }

            _runtime = null;
            runtime.UnregisterModule(_moduleName, _bindings);
        }
    }
}

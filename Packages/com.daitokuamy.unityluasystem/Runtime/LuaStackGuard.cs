using System;

namespace UnityLuaSystem {
    /// <summary>
    /// Luaスタックを処理開始時の位置へ復元する構造体
    /// </summary>
    internal readonly ref struct LuaStackGuard {
        private readonly IntPtr _state;
        private readonly int _top;

        /// <summary>
        /// 現在のLuaスタック位置を保存
        /// </summary>
        /// <param name="state">復元対象のLua state</param>
        internal LuaStackGuard(IntPtr state) {
            _state = state;
            _top = LuaNative.GetTop(state);
        }

        /// <summary>
        /// Luaスタックを保存した位置へ復元
        /// </summary>
        internal void Dispose() {
            LuaNative.SetTop(_state, _top);
        }
    }
}

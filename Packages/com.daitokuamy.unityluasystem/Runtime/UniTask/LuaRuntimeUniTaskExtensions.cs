using System;

namespace UnityLuaSystem.UniTask {
    /// <summary>
    /// LuaRuntimeへUniTask連携を追加する拡張メソッド
    /// </summary>
    public static class LuaRuntimeUniTaskExtensions {
        /// <summary>
        /// LuaRuntimeでUniTaskを待機できるように設定
        /// </summary>
        /// <param name="runtime">設定対象のLuaRuntime</param>
        public static void UseUniTask(this LuaRuntime runtime) {
            if (runtime == null) {
                throw new ArgumentNullException(nameof(runtime));
            }

            runtime.RegisterAwaitableAdapter(UniTaskAwaitableAdapter.Instance);
        }
    }
}

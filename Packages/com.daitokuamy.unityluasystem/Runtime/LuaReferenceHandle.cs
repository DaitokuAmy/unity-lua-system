using System;

namespace UnityLuaSystem {
    /// <summary>
    /// Lua Registry上の参照を所有するクラス
    /// </summary>
    internal sealed class LuaReferenceHandle : IDisposable {
        private LuaRuntime _runtime;
        private int _reference;

        /// <summary>参照が属するLuaRuntime</summary>
        internal LuaRuntime Runtime => _runtime ?? throw new ObjectDisposedException(nameof(LuaReferenceHandle));
        /// <summary>Lua Registry上の参照番号</summary>
        internal int Reference => _runtime != null ? _reference : throw new ObjectDisposedException(nameof(LuaReferenceHandle));

        /// <summary>
        /// Lua Registry上の参照を生成
        /// </summary>
        /// <param name="runtime">参照が属するLuaRuntime</param>
        /// <param name="reference">Lua Registry上の参照番号</param>
        internal LuaReferenceHandle(LuaRuntime runtime, int reference) {
            _runtime = runtime;
            _reference = reference;
        }

        /// <inheritdoc/>
        public void Dispose() {
            if (_runtime == null) {
                return;
            }

            _runtime.ReleaseReference(_reference);
            _runtime = null;
            _reference = 0;
        }
    }
}

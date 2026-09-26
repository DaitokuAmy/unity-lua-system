namespace UnityLuaSystem {
    /// <summary>
    /// LuaRuntimeの生成設定
    /// </summary>
    public sealed class LuaRuntimeOptions {
        /// <summary>Lua標準ライブラリを有効にするかどうか</summary>
        public bool OpenStandardLibraries { get; set; } = true;
    }
}

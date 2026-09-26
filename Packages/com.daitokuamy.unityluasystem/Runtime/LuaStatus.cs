namespace UnityLuaSystem {
    /// <summary>
    /// Lua APIの実行結果
    /// </summary>
    public enum LuaStatus {
        /// <summary>成功</summary>
        Ok = 0,
        /// <summary>yieldによる中断</summary>
        Yield = 1,
        /// <summary>実行時エラー</summary>
        RuntimeError = 2,
        /// <summary>構文エラー</summary>
        SyntaxError = 3,
        /// <summary>メモリ割り当てエラー</summary>
        MemoryError = 4,
        /// <summary>エラーハンドラー実行中のエラー</summary>
        ErrorHandlerError = 5,
    }
}

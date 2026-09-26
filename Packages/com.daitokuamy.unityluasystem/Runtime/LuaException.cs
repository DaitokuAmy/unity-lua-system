using System;

namespace UnityLuaSystem {
    /// <summary>
    /// Luaコードの読み込みまたは実行に失敗した場合の例外
    /// </summary>
    public sealed class LuaException : Exception {
        /// <summary>Luaの実行結果</summary>
        public LuaStatus Status { get; }
        /// <summary>エラーが発生したチャンク名</summary>
        public string ChunkName { get; }

        /// <summary>
        /// エラー情報を指定して例外を生成
        /// </summary>
        /// <param name="status">Luaの実行結果</param>
        /// <param name="message">Luaから取得したエラーメッセージ</param>
        /// <param name="chunkName">エラーが発生したチャンク名</param>
        public LuaException(LuaStatus status, string message, string chunkName = null) : base(message) {
            Status = status;
            ChunkName = chunkName;
        }
    }
}

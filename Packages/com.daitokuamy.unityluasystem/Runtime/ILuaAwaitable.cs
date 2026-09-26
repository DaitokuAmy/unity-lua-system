using System;

namespace UnityLuaSystem {
    /// <summary>
    /// Lua Coroutineが待機できるC#非同期処理
    /// </summary>
    public interface ILuaAwaitable {
        /// <summary>処理が完了したかどうか</summary>
        bool IsCompleted { get; }
        /// <summary>処理完了時にLuaへ返す型</summary>
        Type ResultType { get; }

        /// <summary>
        /// 完了した処理の結果を取得
        /// </summary>
        /// <returns>Luaへ返す結果</returns>
        object GetResult();
    }
}

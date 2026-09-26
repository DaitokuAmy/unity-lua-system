using System;

namespace UnityLuaSystem {
    /// <summary>
    /// C#メソッドの戻り値をLuaが待機できる処理へ変換するアダプター
    /// </summary>
    public interface ILuaAwaitableAdapter {
        /// <summary>
        /// C#メソッドの戻り値から待機対象を生成
        /// </summary>
        /// <param name="returnType">C#メソッドの宣言上の戻り値型</param>
        /// <param name="returnValue">C#メソッドが返した値</param>
        /// <param name="awaitable">生成した待機対象</param>
        /// <returns>戻り値を処理できた場合はtrue</returns>
        bool TryCreate(Type returnType, object returnValue, out ILuaAwaitable awaitable);
    }
}

using System;

namespace UnityLuaSystem {
    /// <summary>
    /// Source Generatorが生成したC#モジュールバインディング
    /// </summary>
    public interface ILuaGeneratedModuleBinding {
        /// <summary>生成対象のC#モジュール型</summary>
        Type ModuleType { get; }

        /// <summary>
        /// C#メソッドに対応する生成済み関数を取得
        /// </summary>
        /// <param name="methodName">C#メソッド名</param>
        /// <param name="functionName">Luaへ公開する関数名</param>
        /// <param name="callback">生成済み呼び出し処理</param>
        /// <returns>生成済み関数が存在する場合はtrue</returns>
        bool TryGetFunction(string methodName, out string functionName, out LuaGeneratedFunctionCallback callback);
    }
}

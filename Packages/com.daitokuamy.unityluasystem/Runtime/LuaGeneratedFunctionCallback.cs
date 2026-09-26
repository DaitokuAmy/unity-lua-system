using System;

namespace UnityLuaSystem {
    /// <summary>
    /// Source Generatorが生成するLuaからC#への呼び出し処理
    /// </summary>
    /// <param name="runtime">呼び出し元のLuaRuntime</param>
    /// <param name="state">呼び出し元のLua state</param>
    /// <returns>Luaスタックへ積んだ戻り値数</returns>
    public delegate int LuaGeneratedFunctionCallback(LuaRuntime runtime, IntPtr state);
}

using System;

namespace UnityLuaSystem {
    /// <summary>
    /// C#型をLuaの名前付きモジュールとして公開することを示すAttribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class LuaModuleAttribute : Attribute {
        /// <summary>Luaへ公開するモジュール名</summary>
        public string Name { get; }

        /// <summary>
        /// 型名をモジュール名としてAttributeを生成
        /// </summary>
        public LuaModuleAttribute() {
        }

        /// <summary>
        /// モジュール名を指定してAttributeを生成
        /// </summary>
        /// <param name="name">Luaへ公開するモジュール名</param>
        public LuaModuleAttribute(string name) {
            Name = name;
        }
    }

    /// <summary>
    /// C#インスタンスをLua userdataとして公開できることを示すAttribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class LuaObjectAttribute : Attribute {
    }

    /// <summary>
    /// C#メソッドをLua関数として公開することを示すAttribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class LuaFunctionAttribute : Attribute {
        /// <summary>Luaへ公開する関数名</summary>
        public string Name { get; }

        /// <summary>
        /// メソッド名を関数名としてAttributeを生成
        /// </summary>
        public LuaFunctionAttribute() {
        }

        /// <summary>
        /// 関数名を指定してAttributeを生成
        /// </summary>
        /// <param name="name">Luaへ公開する関数名</param>
        public LuaFunctionAttribute(string name) {
            Name = name;
        }
    }
}

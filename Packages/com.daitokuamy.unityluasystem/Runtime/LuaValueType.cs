namespace UnityLuaSystem {
    /// <summary>
    /// Luaスタック上の値型
    /// </summary>
    internal enum LuaValueType {
        /// <summary>値が存在しない状態</summary>
        None = -1,
        /// <summary>nil値</summary>
        Nil = 0,
        /// <summary>bool値</summary>
        Boolean = 1,
        /// <summary>light userdata値</summary>
        LightUserData = 2,
        /// <summary>数値</summary>
        Number = 3,
        /// <summary>文字列</summary>
        String = 4,
        /// <summary>table値</summary>
        Table = 5,
        /// <summary>関数値</summary>
        Function = 6,
        /// <summary>userdata値</summary>
        UserData = 7,
        /// <summary>Coroutine値</summary>
        Thread = 8,
    }
}

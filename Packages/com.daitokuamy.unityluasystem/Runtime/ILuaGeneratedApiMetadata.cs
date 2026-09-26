namespace UnityLuaSystem {
    /// <summary>
    /// Source Generatorが生成したLua API定義を公開するinterface
    /// </summary>
    public interface ILuaGeneratedApiMetadata {
        /// <summary>LuaCATS形式のAPI定義</summary>
        string Definition { get; }
    }
}

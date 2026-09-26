using UnityEditor;
using UnityEngine;

namespace UnityLuaSystem.Editor {
    /// <summary>
    /// Lua Language Server向け定義の生成メニューを提供するクラス
    /// </summary>
    internal static class LuaDefinitionMenu {
        /// <summary>
        /// プロジェクト内のLua公開型から定義ファイルを生成
        /// </summary>
        [MenuItem("Tools/Unity Lua System/Generate Lua Definitions")]
        private static void Generate() {
            var absolutePath = LuaDefinitionExporter.Generate();
            Debug.Log($"Generated Lua definitions: {absolutePath}");
        }

        /// <summary>
        /// 生成済み定義ファイルをProject Browserで表示
        /// </summary>
        [MenuItem("Tools/Unity Lua System/Open Lua Definitions Folder")]
        private static void OpenDirectory() {
            var absolutePath = System.IO.Path.GetFullPath(LuaDefinitionExporter.DefaultOutputPath);
            var absoluteDirectory = System.IO.Path.GetDirectoryName(absolutePath);
            System.IO.Directory.CreateDirectory(absoluteDirectory);
            EditorUtility.RevealInFinder(absoluteDirectory);
        }
    }
}

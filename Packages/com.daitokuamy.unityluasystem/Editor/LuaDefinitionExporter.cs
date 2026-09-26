using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;

namespace UnityLuaSystem.Editor {
    /// <summary>
    /// プロジェクト内のLua公開APIをLuaCATS定義として出力するクラス
    /// </summary>
    public static class LuaDefinitionExporter {
        /// <summary>既定の定義ファイル出力先</summary>
        public const string DefaultOutputPath = "Library/UnityLuaSystem/LuaDefinitions/UnityLuaSystem.lua";

        /// <summary>
        /// プロジェクト内のLua公開APIを既定の出力先へ保存
        /// </summary>
        /// <returns>生成した定義ファイルの絶対パス</returns>
        public static string Generate() {
            return Generate(DefaultOutputPath);
        }

        /// <summary>
        /// プロジェクト内のLua公開APIを指定した出力先へ保存
        /// </summary>
        /// <param name="outputPath">プロジェクトルートからの相対パス、または絶対パス</param>
        /// <returns>生成した定義ファイルの絶対パス</returns>
        public static string Generate(string outputPath) {
            if (string.IsNullOrWhiteSpace(outputPath)) {
                throw new System.ArgumentException("The output path must not be empty.", nameof(outputPath));
            }

            var metadata = TypeCache.GetTypesDerivedFrom<ILuaGeneratedApiMetadata>()
                .Where(type => !type.IsAbstract && !type.IsInterface)
                .Select(type => (ILuaGeneratedApiMetadata)System.Activator.CreateInstance(type));
            var definition = LuaDefinitionGenerator.Generate(metadata);
            var absolutePath = Path.GetFullPath(outputPath);
            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory)) {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(absolutePath) || File.ReadAllText(absolutePath, Encoding.UTF8) != definition) {
                File.WriteAllText(absolutePath, definition, new UTF8Encoding(false));
            }
            return absolutePath;
        }
    }
}

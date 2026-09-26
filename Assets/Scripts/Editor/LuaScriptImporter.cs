using System.IO;
using System.Text;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace UnityLuaSystemSamples.Editor {
    /// <summary>
    /// LuaソースファイルをTextAssetとしてインポートするクラス
    /// </summary>
    [ScriptedImporter(1, "lua")]
    public sealed class LuaScriptImporter : ScriptedImporter {
        /// <summary>
        /// LuaソースファイルをTextAssetへ変換
        /// </summary>
        /// <param name="context">アセットのインポートコンテキスト</param>
        public override void OnImportAsset(AssetImportContext context) {
            var source = File.ReadAllText(context.assetPath, new UTF8Encoding(false));
            var textAsset = new TextAsset(source) {
                name = Path.GetFileNameWithoutExtension(context.assetPath),
            };

            context.AddObjectToAsset("Lua Script", textAsset);
            context.SetMainObject(textAsset);
        }
    }
}

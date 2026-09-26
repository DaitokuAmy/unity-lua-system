using NUnit.Framework;
using UnityLuaSystem.Editor;

namespace UnityLuaSystem.Tests {
    /// <summary>
    /// LuaDefinitionGeneratorのテスト
    /// </summary>
    public sealed class LuaDefinitionGeneratorTests {
        /// <summary>
        /// Source GeneratorのメタデータをLuaCATS定義へ統合できることを確認
        /// </summary>
        [Test]
        public void GenerateCombinesGeneratedMetadata() {
            var metadata = new ILuaGeneratedApiMetadata[] {
                new TestMetadata("---@class TestPlayer"),
                new TestMetadata("---@class TestModule"),
            };

            var definition = LuaDefinitionGenerator.Generate(metadata);

            StringAssert.StartsWith("---@meta", definition);
            StringAssert.Contains("---@class TestPlayer", definition);
            StringAssert.Contains("---@class TestModule", definition);
        }

        private sealed class TestMetadata : ILuaGeneratedApiMetadata {
            /// <inheritdoc/>
            public string Definition { get; }

            internal TestMetadata(string definition) {
                Definition = definition;
            }
        }
    }
}

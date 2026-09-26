using NUnit.Framework;

namespace UnityLuaSystem.Tests {
    /// <summary>
    /// C#値とLua値の変換を確認するテスト
    /// </summary>
    public sealed class LuaValueConversionTests {
        /// <summary>
        /// 対応する値型をLua関数との間で往復変換できることを確認
        /// </summary>
        [Test]
        public void LuaFunction_RoundTripsSupportedValueTypes() {
            using var runtime = new LuaRuntime();
            runtime.Execute("function identity(value) return value end");
            using var booleanIdentity = runtime.GetFunction<bool, bool>("identity");
            using var integerIdentity = runtime.GetFunction<long, long>("identity");
            using var singleIdentity = runtime.GetFunction<float, float>("identity");
            using var doubleIdentity = runtime.GetFunction<double, double>("identity");
            using var stringIdentity = runtime.GetFunction<string, string>("identity");

            Assert.That(booleanIdentity.Invoke(true), Is.True);
            Assert.That(integerIdentity.Invoke(long.MaxValue), Is.EqualTo(long.MaxValue));
            Assert.That(singleIdentity.Invoke(1.25f), Is.EqualTo(1.25f));
            Assert.That(doubleIdentity.Invoke(1.25d), Is.EqualTo(1.25d));
            Assert.That(stringIdentity.Invoke("日本語"), Is.EqualTo("日本語"));
        }

        /// <summary>
        /// Lua値の型が要求されたC#型と異なる場合に失敗することを確認
        /// </summary>
        [Test]
        public void Execute_WhenResultTypeDoesNotMatch_ThrowsLuaException() {
            using var runtime = new LuaRuntime();

            var exception = Assert.Throws<LuaException>(() => runtime.Execute<bool>("return 1"));

            Assert.That(exception.Status, Is.EqualTo(LuaStatus.RuntimeError));
            Assert.That(exception.Message, Does.Contain("System.Boolean"));
        }

        /// <summary>
        /// C#配列をLua tableとして関数へ渡せることを確認
        /// </summary>
        [Test]
        public void LuaFunction_WithArrayArgument_ReceivesLuaTable() {
            using var runtime = new LuaRuntime();
            runtime.Execute("function sum(values) local result = 0; for index = 1, #values do result = result + values[index] end; return result end");
            using var sum = runtime.GetFunction<int[], int>("sum");

            var result = sum.Invoke(new[] { 10, 20, 30 });

            Assert.That(result, Is.EqualTo(60));
        }

        /// <summary>
        /// Lua tableをC#配列として取得できることを確認
        /// </summary>
        [Test]
        public void Execute_WithTableResult_ReturnsArray() {
            using var runtime = new LuaRuntime();

            var result = runtime.Execute<string[]>("return { 'first', 'second', 'third' }");

            Assert.That(result, Is.EqualTo(new[] { "first", "second", "third" }));
        }

        /// <summary>
        /// 空のLua tableを空配列として取得できることを確認
        /// </summary>
        [Test]
        public void Execute_WithEmptyTableResult_ReturnsEmptyArray() {
            using var runtime = new LuaRuntime();

            var result = runtime.Execute<int[]>("return {}");

            Assert.That(result, Is.Empty);
        }

        /// <summary>
        /// Luaのnilをnull配列として取得できることを確認
        /// </summary>
        [Test]
        public void Execute_WithNilResult_ReturnsNullArray() {
            using var runtime = new LuaRuntime();

            var result = runtime.Execute<int[]>("return nil");

            Assert.That(result, Is.Null);
        }

        /// <summary>
        /// 配列要素の型が異なる場合に変換が失敗することを確認
        /// </summary>
        [Test]
        public void Execute_WhenArrayElementTypeDoesNotMatch_ThrowsLuaException() {
            using var runtime = new LuaRuntime();

            Assert.Throws<LuaException>(() => runtime.Execute<int[]>("return { 1, 'invalid', 3 }"));
        }

    }
}

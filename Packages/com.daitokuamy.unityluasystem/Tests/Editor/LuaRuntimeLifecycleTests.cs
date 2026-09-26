using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityLuaSystem.Tests {
    /// <summary>
    /// LuaRuntimeの入力検証とライフサイクルを確認するテスト
    /// </summary>
    public sealed class LuaRuntimeLifecycleTests {
        /// <summary>
        /// nullの設定でRuntimeを生成できないことを確認
        /// </summary>
        [Test]
        public void Constructor_WhenOptionsIsNull_ThrowsArgumentNullException() {
            Assert.Throws<ArgumentNullException>(() => new LuaRuntime(null));
        }

        /// <summary>
        /// Runtimeを複数回安全に破棄できることを確認
        /// </summary>
        [Test]
        public void Dispose_WhenCalledMultipleTimes_RemainsDisposed() {
            var runtime = new LuaRuntime();

            runtime.Dispose();
            runtime.Dispose();

            Assert.That(runtime.IsDisposed, Is.True);
        }

        /// <summary>
        /// nullのLuaソースを検証できないことを確認
        /// </summary>
        [Test]
        public void Validate_WhenSourceIsNull_ThrowsArgumentNullException() {
            using var runtime = new LuaRuntime();

            Assert.Throws<ArgumentNullException>(() => runtime.Validate(null));
        }

        /// <summary>
        /// 空のチャンク名を使用できないことを確認
        /// </summary>
        [TestCase(null)]
        [TestCase("")]
        public void Validate_WhenChunkNameIsEmpty_ThrowsArgumentException(string chunkName) {
            using var runtime = new LuaRuntime();

            Assert.Throws<ArgumentException>(() => runtime.Validate("return 1", chunkName));
        }

        /// <summary>
        /// 構文エラー後もRuntimeを継続利用できることを確認
        /// </summary>
        [Test]
        public void Validate_AfterSyntaxError_RuntimeRemainsUsable() {
            using var runtime = new LuaRuntime();

            Assert.Throws<LuaException>(() => runtime.Validate("function broken("));

            Assert.That(runtime.Execute<int>("return 42"), Is.EqualTo(42));
        }

        /// <summary>
        /// 実行時エラー後もRuntimeを継続利用できることを確認
        /// </summary>
        [Test]
        public void Execute_AfterRuntimeError_RuntimeRemainsUsable() {
            using var runtime = new LuaRuntime();

            Assert.Throws<LuaException>(() => runtime.Execute("error('expected failure')"));

            Assert.That(runtime.Execute<int>("return 42"), Is.EqualTo(42));
        }

        /// <summary>
        /// Runtimeを生成したスレッド以外から操作できないことを確認
        /// </summary>
        [Test]
        public void Execute_FromAnotherThread_ThrowsInvalidOperationException() {
            using var runtime = new LuaRuntime();

            var exception = Task.Run(() => {
                try {
                    runtime.Execute("return 1");
                    return null;
                }
                catch (Exception caughtException) {
                    return caughtException;
                }
            }).Result;

            Assert.That(exception, Is.TypeOf<InvalidOperationException>());
        }

        /// <summary>
        /// Runtime破棄時に待機中の非同期呼び出しが失敗として完了することを確認
        /// </summary>
        [Test]
        public void Dispose_WithPendingAsyncCall_CompletesCallWithException() {
            var runtime = new LuaRuntime();
            var module = new TestInstanceModule();
            using var registration = runtime.RegisterModule(module);
            runtime.Execute("function async_test() return test_instance.load_async() end");
            using var function = runtime.GetFunction<int>("async_test");
            var pendingResult = function.InvokeAsync();

            runtime.Dispose();

            Assert.That(pendingResult.IsCompleted, Is.True);
            Assert.ThrowsAsync<ObjectDisposedException>(async () => await pendingResult.AsTask());
        }

        /// <summary>
        /// 破棄したLua関数参照を呼び出せないことを確認
        /// </summary>
        [Test]
        public void LuaFunction_AfterDispose_ThrowsObjectDisposedException() {
            using var runtime = new LuaRuntime();
            runtime.Execute("function value() return 42 end");
            var function = runtime.GetFunction<int>("value");
            function.Dispose();

            Assert.Throws<ObjectDisposedException>(() => function.Invoke());
        }

        /// <summary>
        /// モジュール登録の破棄時にLuaグローバルから削除されることを確認
        /// </summary>
        [Test]
        public void ModuleRegistration_AfterDispose_RemovesLuaGlobal() {
            using var runtime = new LuaRuntime();
            var registration = runtime.RegisterModule<TestStaticModule>();

            registration.Dispose();

            Assert.That(runtime.Execute<bool>("return test_static == nil"), Is.True);
        }

        /// <summary>
        /// 標準ライブラリを開かずにRuntimeを生成できることを確認
        /// </summary>
        [Test]
        public void Constructor_WhenStandardLibrariesAreDisabled_DoesNotExposeBaseLibrary() {
            var options = new LuaRuntimeOptions {
                OpenStandardLibraries = false,
            };
            using var runtime = new LuaRuntime(options);

            Assert.That(runtime.Execute<bool>("return print == nil"), Is.True);
        }
    }
}

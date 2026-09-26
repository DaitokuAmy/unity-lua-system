using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace UnityLuaSystem.UniTask.Tests {
    /// <summary>
    /// LuaへUniTaskを返すテスト用モジュール
    /// </summary>
    [LuaModule("unitask_test")]
    public sealed class UniTaskTestModule {
        private readonly UniTaskCompletionSource<int> _completion = new UniTaskCompletionSource<int>();

        /// <summary>
        /// テストで完了を制御するUniTaskを返却
        /// </summary>
        [LuaFunction("load_async")]
        public global::Cysharp.Threading.Tasks.UniTask<int> LoadAsync() {
            return _completion.Task;
        }

        /// <summary>
        /// 待機中のUniTaskを完了
        /// </summary>
        /// <param name="value">完了時に返す値</param>
        public void Complete(int value) {
            _completion.TrySetResult(value);
        }
    }

    /// <summary>
    /// LuaRuntimeのUniTask連携を確認するテスト
    /// </summary>
    public sealed class LuaRuntimeUniTaskTests {
        /// <summary>
        /// UniTask完了後のTickでLua Coroutineが再開することを確認
        /// </summary>
        [Test]
        public void InvokeAsync_ResumesLuaAfterUniTaskCompletes() {
            using var runtime = new LuaRuntime();
            runtime.UseUniTask();
            var module = new UniTaskTestModule();
            using var registration = runtime.RegisterModule(module);
            runtime.Execute("function unitask_sample() local value = unitask_test.load_async(); return value + 1 end");
            using var function = runtime.GetFunction<int>("unitask_sample");

            var pendingResult = function.InvokeAsync();
            Assert.That(pendingResult.IsCompleted, Is.False);

            module.Complete(42);
            runtime.Tick();

            Assert.That(pendingResult.IsCompletedSuccessfully, Is.True);
            Assert.That(pendingResult.Result, Is.EqualTo(43));
        }
    }
}

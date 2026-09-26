using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityLuaSystem.Tests {
    [LuaObject]
    public sealed class TestPlayer {
        [LuaFunction("heal")]
        public int Heal(int amount) {
            return 80 + amount;
        }
    }

    [LuaModule("test_instance")]
    public sealed class TestInstanceModule {
        private readonly TaskCompletionSource<int> _completion = new TaskCompletionSource<int>();

        [LuaFunction("get_player")]
        public TestPlayer GetPlayer() {
            return new TestPlayer();
        }

        [LuaFunction("get_players")]
        public TestPlayer[] GetPlayers() {
            return new[] { new TestPlayer(), new TestPlayer() };
        }

        [LuaFunction("heal_all")]
        public int HealAll(TestPlayer[] players, int amount) {
            var result = 0;
            foreach (var player in players) {
                result += player.Heal(amount);
            }
            return result;
        }

        [LuaFunction("load_async")]
        public Task<int> LoadAsync() {
            return _completion.Task;
        }

        public void Complete(int value) {
            _completion.SetResult(value);
        }

        [LuaFunction("run_enumerator")]
        public IEnumerator RunEnumerator() {
            yield return null;
            yield return null;
        }
    }

    [LuaModule("test_static")]
    public sealed class TestStaticModule {
        [LuaFunction("add")]
        public static int Add(int left, int right) {
            return left + right;
        }

        [LuaFunction("sum")]
        public static int Sum(int[] values) {
            var result = 0;
            foreach (var value in values) {
                result += value;
            }
            return result;
        }

        [LuaFunction("create_values")]
        public static int[] CreateValues() {
            return new[] { 10, 20, 30 };
        }
    }

    /// <summary>
    /// LuaRuntimeの動作を確認するテスト
    /// </summary>
    public sealed class LuaRuntimeTests {
        [Test]
        public void RegisteredModulesAndLuaObjectCanBeCalledFromLua() {
            using var runtime = new LuaRuntime();
            using var staticRegistration = runtime.RegisterModule<TestStaticModule>();
            using var instanceRegistration = runtime.RegisterModule(new TestInstanceModule());

            var result = runtime.Execute<int>(
                "local player = test_instance.get_player(); return test_static.add(7, 8) + player:heal(15)",
                "binding-test");

            Assert.That(result, Is.EqualTo(110));
        }

        /// <summary>
        /// Lua tableとC#配列をモジュール呼び出しで相互変換できることを確認
        /// </summary>
        [Test]
        public void RegisteredModules_ConvertArraysInBothDirections() {
            using var runtime = new LuaRuntime();
            using var staticRegistration = runtime.RegisterModule<TestStaticModule>();
            using var instanceRegistration = runtime.RegisterModule(new TestInstanceModule());

            var result = runtime.Execute<int>(@"
                local values = test_static.create_values()
                local players = test_instance.get_players()
                return test_static.sum(values) + test_instance.heal_all(players, 5)
            ");

            Assert.That(result, Is.EqualTo(230));
        }

        [Test]
        public void InvokeAsync_ResumesLuaAfterCSharpTaskCompletes() {
            using var runtime = new LuaRuntime();
            var module = new TestInstanceModule();
            using var registration = runtime.RegisterModule(module);
            runtime.Execute("function async_test() local value = test_instance.load_async(); return value + 1 end");
            using var function = runtime.GetFunction<int>("async_test");

            var pendingResult = function.InvokeAsync();
            Assert.That(pendingResult.IsCompleted, Is.False);

            module.Complete(42);
            runtime.Tick();

            Assert.That(pendingResult.IsCompletedSuccessfully, Is.True);
            Assert.That(pendingResult.Result, Is.EqualTo(43));
        }

        [Test]
        public void InvokeAsync_AdvancesCSharpEnumeratorOncePerTick() {
            using var runtime = new LuaRuntime();
            using var registration = runtime.RegisterModule(new TestInstanceModule());
            runtime.Execute("function enumerator_test() test_instance.run_enumerator(); return 42 end");
            using var function = runtime.GetFunction<int>("enumerator_test");

            var pendingResult = function.InvokeAsync();
            Assert.That(pendingResult.IsCompleted, Is.False);

            runtime.Tick();
            Assert.That(pendingResult.IsCompleted, Is.False);

            runtime.Tick();
            Assert.That(pendingResult.IsCompletedSuccessfully, Is.True);
            Assert.That(pendingResult.Result, Is.EqualTo(42));
        }

        /// <summary>
        /// Luaコードの整数戻り値を取得できることを確認
        /// </summary>
        [Test]
        public void Execute_ReturnsCalculatedInteger() {
            using var runtime = new LuaRuntime();

            var result = runtime.Execute<int>("return 1 + 2", "calculation.lua");

            Assert.That(result, Is.EqualTo(3));
        }

        /// <summary>
        /// Luaコードから文字列を取得できることを確認
        /// </summary>
        [Test]
        public void Execute_ReturnsUtf8String() {
            using var runtime = new LuaRuntime();

            var result = runtime.Execute<string>("return '日本語'", "string.lua");

            Assert.That(result, Is.EqualTo("日本語"));
        }

        /// <summary>
        /// 構文検証時にLuaコードが実行されないことを確認
        /// </summary>
        [Test]
        public void Validate_WhenSourceIsValid_DoesNotExecuteSource() {
            using var runtime = new LuaRuntime();

            runtime.Validate("value = 42", "validation.lua");

            Assert.That(runtime.Execute<bool>("return value == nil"), Is.True);
        }

        /// <summary>
        /// 構文エラーをLuaExceptionとして取得できることを確認
        /// </summary>
        [Test]
        public void Validate_WhenSourceHasSyntaxError_ThrowsLuaException() {
            using var runtime = new LuaRuntime();

            var exception = Assert.Throws<LuaException>(() => runtime.Validate("function broken(", "broken.lua"));

            Assert.That(exception.ChunkName, Is.EqualTo("broken.lua"));
            Assert.That(exception.Status, Is.EqualTo(LuaStatus.SyntaxError));
        }

        /// <summary>
        /// Lua関数へ型付き引数を渡して戻り値を取得できることを確認
        /// </summary>
        [Test]
        public void GetFunction_InvokesTypedFunction() {
            using var runtime = new LuaRuntime();
            runtime.Execute("function add(left, right) return left + right end", "functions.lua");
            using var add = runtime.GetFunction<int, int, int>("add");

            var result = add.Invoke(10, 20);

            Assert.That(result, Is.EqualTo(30));
        }

        /// <summary>
        /// Lua関数参照を取得後のグローバル変更に影響されないことを確認
        /// </summary>
        [Test]
        public void GetFunction_KeepsRegistryReference() {
            using var runtime = new LuaRuntime();
            runtime.Execute("function value() return 10 end", "first.lua");
            using var function = runtime.GetFunction<int>("value");
            runtime.Execute("function value() return 20 end", "second.lua");

            var result = function.Invoke();

            Assert.That(result, Is.EqualTo(10));
        }

        /// <summary>
        /// 複数Runtimeのグローバル環境が独立していることを確認
        /// </summary>
        [Test]
        public void MultipleRuntimes_DoNotShareGlobals() {
            using var firstRuntime = new LuaRuntime();
            using var secondRuntime = new LuaRuntime();
            firstRuntime.Execute("value = 10");
            secondRuntime.Execute("value = 20");

            var first = firstRuntime.Execute<int>("return value");
            var second = secondRuntime.Execute<int>("return value");

            Assert.That(first, Is.EqualTo(10));
            Assert.That(second, Is.EqualTo(20));
        }

        /// <summary>
        /// Luaの実行エラーをLuaExceptionとして取得できることを確認
        /// </summary>
        [Test]
        public void Execute_WhenLuaFails_ThrowsLuaException() {
            using var runtime = new LuaRuntime();

            var exception = Assert.Throws<LuaException>(() => runtime.Execute("error('expected failure')", "error.lua"));

            Assert.That(exception.Message, Does.Contain("expected failure"));
            Assert.That(exception.ChunkName, Is.EqualTo("error.lua"));
            Assert.That(exception.Status, Is.EqualTo(LuaStatus.RuntimeError));
        }

        /// <summary>
        /// 関数ではないグローバル値を取得できないことを確認
        /// </summary>
        [Test]
        public void GetFunction_WhenGlobalIsNotFunction_ThrowsLuaException() {
            using var runtime = new LuaRuntime();
            runtime.Execute("value = 10");

            var exception = Assert.Throws<LuaException>(() => runtime.GetFunction<int>("value"));

            Assert.That(exception.Message, Does.Contain("not a function"));
        }

        /// <summary>
        /// 破棄済みRuntimeを操作できないことを確認
        /// </summary>
        [Test]
        public void Execute_AfterDispose_ThrowsObjectDisposedException() {
            var runtime = new LuaRuntime();
            runtime.Dispose();

            Assert.Throws<ObjectDisposedException>(() => runtime.Execute("return 1"));
        }
    }
}

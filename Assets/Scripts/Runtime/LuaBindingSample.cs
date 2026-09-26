using System.Collections;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityLuaSystem;

namespace UnityLuaSystemSamples {
    /// <summary>
    /// Luaへuserdataとして公開するプレイヤーのサンプル
    /// </summary>
    [LuaObject]
    public sealed class SamplePlayer {
        private int _health;

        /// <summary>プレイヤー名</summary>
        public string Name { get; }

        /// <summary>
        /// プレイヤー情報を生成
        /// </summary>
        public SamplePlayer(string name, int health) {
            Name = name;
            _health = health;
        }

        /// <summary>
        /// 名前をLuaへ返却
        /// </summary>
        [LuaFunction("get_name")]
        public string GetName() {
            return Name;
        }

        /// <summary>
        /// 現在の体力をLuaへ返却
        /// </summary>
        [LuaFunction("get_health")]
        public int GetHealth() {
            return _health;
        }

        /// <summary>
        /// Luaから指定された量だけ体力を回復
        /// </summary>
        [LuaFunction("heal")]
        public int Heal(int amount) {
            _health += amount;
            return _health;
        }
    }

    /// <summary>
    /// インスタンスを登録するLuaモジュールのサンプル
    /// </summary>
    [LuaModule("sample_instance")]
    public sealed class SampleInstanceModule {
        private readonly SamplePlayer _player = new SamplePlayer("Lua Player", 80);

        private int _operationStep;

        /// <summary>
        /// 登録済みインスタンスが保持するメッセージを生成
        /// </summary>
        [LuaFunction("create_message")]
        public string CreateMessage(string value) {
            return $"Instance module received: {value}";
        }

        /// <summary>
        /// LuaObjectとして公開するプレイヤーを返却
        /// </summary>
        [LuaFunction("get_player")]
        public SamplePlayer GetPlayer() {
            return _player;
        }

        /// <summary>
        /// 非同期処理の完了後にLuaへ文字列を返却
        /// </summary>
        [LuaFunction("load_message_async")]
        public async Task<string> LoadMessageAsync(string value) {
            await Task.Delay(3000);
            return $"Async C# completed: {value}";
        }

        /// <summary>
        /// UniTask処理の完了後にLuaへ文字列を返却
        /// </summary>
        [LuaFunction("load_unitask_message_async")]
        public async UniTask<string> LoadUniTaskMessageAsync(string value) {
            await UniTask.Delay(3000);
            return $"UniTask C# completed: {value}";
        }

        /// <summary>
        /// RuntimeのTickに合わせて利用者管理の処理を進行
        /// </summary>
        [LuaFunction("run_controlled_operation")]
        public IEnumerator RunControlledOperation() {
            _operationStep = 0;
            while (!AdvanceOperation()) {
                yield return null;
            }
        }

        /// <summary>
        /// 利用者管理の処理を1ステップ進行
        /// </summary>
        /// <returns>処理が完了した場合はtrue</returns>
        private bool AdvanceOperation() {
            _operationStep++;
            return _operationStep >= 3;
        }
    }

    /// <summary>
    /// static関数を登録するLuaモジュールのサンプル
    /// </summary>
    [LuaModule("sample_static")]
    public sealed class SampleStaticModule {
        /// <summary>
        /// 2つの整数を加算
        /// </summary>
        [LuaFunction("add")]
        public static int Add(int left, int right) {
            return left + right;
        }

        /// <summary>
        /// 文字列へ接頭辞を付与
        /// </summary>
        [LuaFunction("decorate")]
        public static string Decorate(string value) {
            return $"[C# static] {value}";
        }

        /// <summary>
        /// Lua tableから受け取った整数配列を合計
        /// </summary>
        [LuaFunction("sum")]
        public static int Sum(int[] values) {
            var result = 0;
            foreach (var value in values) {
                result += value;
            }
            return result;
        }

        /// <summary>
        /// Lua tableへ変換される整数配列を生成
        /// </summary>
        [LuaFunction("create_values")]
        public static int[] CreateValues() {
            return new[] { 10, 20, 30 };
        }
    }
}

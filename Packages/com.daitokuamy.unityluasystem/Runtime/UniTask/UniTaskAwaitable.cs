using System;

namespace UnityLuaSystem.UniTask {
    /// <summary>
    /// 戻り値がないUniTaskの待機状態
    /// </summary>
    internal sealed class UniTaskAwaitable : ILuaAwaitable {
        private readonly global::Cysharp.Threading.Tasks.UniTask.Awaiter _awaiter;

        /// <inheritdoc/>
        public bool IsCompleted => _awaiter.IsCompleted;
        /// <inheritdoc/>
        public Type ResultType => typeof(void);

        /// <summary>
        /// UniTaskの待機状態を生成
        /// </summary>
        /// <param name="task">待機するUniTask</param>
        internal UniTaskAwaitable(global::Cysharp.Threading.Tasks.UniTask task) {
            _awaiter = task.GetAwaiter();
        }

        /// <inheritdoc/>
        public object GetResult() {
            _awaiter.GetResult();
            return null;
        }
    }
}

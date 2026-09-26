using System;

namespace UnityLuaSystem.UniTask {
    /// <summary>
    /// 戻り値があるUniTaskの待機状態
    /// </summary>
    /// <typeparam name="TResult">UniTaskが返す型</typeparam>
    internal sealed class UniTaskAwaitable<TResult> : ILuaAwaitable {
        private readonly global::Cysharp.Threading.Tasks.UniTask<TResult>.Awaiter _awaiter;

        /// <inheritdoc/>
        public bool IsCompleted => _awaiter.IsCompleted;
        /// <inheritdoc/>
        public Type ResultType => typeof(TResult);

        /// <summary>
        /// UniTaskの待機状態を生成
        /// </summary>
        /// <param name="task">待機するUniTask</param>
        public UniTaskAwaitable(global::Cysharp.Threading.Tasks.UniTask<TResult> task) {
            _awaiter = task.GetAwaiter();
        }

        /// <inheritdoc/>
        public object GetResult() {
            return _awaiter.GetResult();
        }
    }
}

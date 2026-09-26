using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnityLuaSystem {
    /// <summary>
    /// Lua関数の非同期実行を管理するLuaRuntime実装
    /// </summary>
    public sealed partial class LuaRuntime {
        /// <summary>待機中のLua呼び出しに共通する操作</summary>
        private interface IPendingLuaCall {
            /// <summary>Lua Coroutineを再開できるかどうか</summary>
            bool CanResume { get; }

            /// <summary>
            /// Lua Coroutineを再開
            /// </summary>
            /// <returns>呼び出しが完了した場合はtrue</returns>
            bool Resume();

            /// <summary>
            /// Lua呼び出しを失敗として完了
            /// </summary>
            /// <param name="exception">完了時に通知する例外</param>
            void Fail(Exception exception);
        }

        /// <summary>待機中のLua呼び出しに共通する基底クラス</summary>
        private abstract class PendingLuaCallBase : IPendingLuaCall {
            private readonly LuaRuntime _runtime;
            private readonly IntPtr _thread;
            private readonly int _threadReference;

            /// <summary>実行中のLua Coroutine</summary>
            protected IntPtr Thread => _thread;

            /// <inheritdoc/>
            public bool CanResume => _runtime._pendingManagedCalls.TryGetValue(_thread, out var call) && call.Advance();

            /// <summary>
            /// 待機中のLua呼び出しを生成
            /// </summary>
            /// <param name="runtime">呼び出し元のLuaRuntime</param>
            /// <param name="thread">実行中のLua Coroutine</param>
            /// <param name="threadReference">Lua Registry上のCoroutine参照</param>
            protected PendingLuaCallBase(LuaRuntime runtime, IntPtr thread, int threadReference) {
                _runtime = runtime;
                _thread = thread;
                _threadReference = threadReference;
            }

            /// <inheritdoc/>
            public bool Resume() {
                var status = (LuaStatus)LuaNative.Resume(_thread, _runtime._state, 0, out var resultCount);
                if (status == LuaStatus.Yield) {
                    if (!_runtime._pendingManagedCalls.ContainsKey(_thread)) {
                        CompleteFailure(new LuaException(LuaStatus.RuntimeError, "The Lua coroutine yielded without a pending C# operation."));
                        ReleaseReference();
                        return true;
                    }
                    return false;
                }
                if (status == LuaStatus.Ok) {
                    CompleteSuccess(resultCount);
                }
                else {
                    CompleteFailure(_runtime.CreateCoroutineException(_thread, status));
                }
                ReleaseReference();
                return true;
            }

            /// <inheritdoc/>
            public void Fail(Exception exception) {
                CompleteFailure(exception);
                ReleaseReference();
            }

            /// <summary>
            /// Lua呼び出しの成功結果を反映
            /// </summary>
            /// <param name="resultCount">Lua Coroutineが返した値の数</param>
            protected abstract void CompleteSuccess(int resultCount);

            /// <summary>
            /// Lua呼び出しの失敗結果を反映
            /// </summary>
            /// <param name="exception">通知する例外</param>
            protected abstract void CompleteFailure(Exception exception);

            /// <summary>
            /// Lua Registry上のCoroutine参照を解放
            /// </summary>
            private void ReleaseReference() {
                if (_runtime._state != IntPtr.Zero) {
                    LuaNative.ReleaseReference(_runtime._state, LuaNative.RegistryIndex, _threadReference);
                }
            }
        }

        /// <summary>戻り値がある待機中のLua呼び出し</summary>
        /// <typeparam name="TResult">戻り値のC#型</typeparam>
        private sealed class PendingLuaCall<TResult> : PendingLuaCallBase {
            private readonly TaskCompletionSource<TResult> _completion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>呼び出し完了を通知するTask</summary>
            internal Task<TResult> Task => _completion.Task;

            /// <summary>
            /// 戻り値がある待機中のLua呼び出しを生成
            /// </summary>
            /// <param name="runtime">呼び出し元のLuaRuntime</param>
            /// <param name="thread">実行中のLua Coroutine</param>
            /// <param name="threadReference">Lua Registry上のCoroutine参照</param>
            internal PendingLuaCall(LuaRuntime runtime, IntPtr thread, int threadReference) : base(runtime, thread, threadReference) {
            }

            /// <inheritdoc/>
            protected override void CompleteSuccess(int resultCount) {
                try {
                    RequireResultCount(resultCount, 1);
                    _completion.TrySetResult(LuaValueConverter<TResult>.Read(Thread, -1));
                }
                catch (Exception exception) {
                    _completion.TrySetException(exception);
                }
            }

            /// <inheritdoc/>
            protected override void CompleteFailure(Exception exception) {
                _completion.TrySetException(exception);
            }
        }

        /// <summary>戻り値がない待機中のLua呼び出し</summary>
        private sealed class PendingLuaAction : PendingLuaCallBase {
            private readonly TaskCompletionSource<bool> _completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>呼び出し完了を通知するTask</summary>
            internal Task Task => _completion.Task;

            /// <summary>
            /// 戻り値がない待機中のLua呼び出しを生成
            /// </summary>
            /// <param name="runtime">呼び出し元のLuaRuntime</param>
            /// <param name="thread">実行中のLua Coroutine</param>
            /// <param name="threadReference">Lua Registry上のCoroutine参照</param>
            internal PendingLuaAction(LuaRuntime runtime, IntPtr thread, int threadReference) : base(runtime, thread, threadReference) {
            }

            /// <inheritdoc/>
            protected override void CompleteSuccess(int resultCount) {
                try {
                    RequireResultCount(resultCount, 0);
                    _completion.TrySetResult(true);
                }
                catch (Exception exception) {
                    _completion.TrySetException(exception);
                }
            }

            /// <inheritdoc/>
            protected override void CompleteFailure(Exception exception) {
                _completion.TrySetException(exception);
            }
        }

        private readonly List<IPendingLuaCall> _pendingLuaCalls = new List<IPendingLuaCall>();

        /// <summary>
        /// 完了したC#非同期処理を反映し、待機中のLua Coroutineを再開
        /// </summary>
        public void Tick() {
            ValidateAccess();
            for (var index = _pendingLuaCalls.Count - 1; index >= 0; index--) {
                var call = _pendingLuaCalls[index];
                if (!call.CanResume) {
                    continue;
                }
                if (call.Resume()) {
                    _pendingLuaCalls.RemoveAt(index);
                }
            }
        }

        /// <summary>
        /// 引数なしで戻り値があるLua関数を非同期実行
        /// </summary>
        /// <typeparam name="TResult">戻り値のC#型</typeparam>
        /// <param name="reference">Lua Registry上の関数参照</param>
        /// <returns>Lua関数の完了を表すValueTask</returns>
        internal ValueTask<TResult> InvokeAsync<TResult>(int reference) {
            ValidateAccess();
            var thread = CreateCallThread(reference, out var threadReference);
            return ResumeAsync<TResult>(thread, threadReference, 0);
        }

        /// <summary>
        /// 引数が1つで戻り値があるLua関数を非同期実行
        /// </summary>
        /// <typeparam name="T1">第1引数のC#型</typeparam>
        /// <typeparam name="TResult">戻り値のC#型</typeparam>
        /// <param name="reference">Lua Registry上の関数参照</param>
        /// <param name="argument1">第1引数</param>
        /// <returns>Lua関数の完了を表すValueTask</returns>
        internal ValueTask<TResult> InvokeAsync<T1, TResult>(int reference, T1 argument1) {
            ValidateAccess();
            var thread = CreateCallThread(reference, out var threadReference);
            LuaValueConverter<T1>.Push(thread, argument1);
            return ResumeAsync<TResult>(thread, threadReference, 1);
        }

        /// <summary>
        /// 引数が2つで戻り値があるLua関数を非同期実行
        /// </summary>
        /// <typeparam name="T1">第1引数のC#型</typeparam>
        /// <typeparam name="T2">第2引数のC#型</typeparam>
        /// <typeparam name="TResult">戻り値のC#型</typeparam>
        /// <param name="reference">Lua Registry上の関数参照</param>
        /// <param name="argument1">第1引数</param>
        /// <param name="argument2">第2引数</param>
        /// <returns>Lua関数の完了を表すValueTask</returns>
        internal ValueTask<TResult> InvokeAsync<T1, T2, TResult>(int reference, T1 argument1, T2 argument2) {
            ValidateAccess();
            var thread = CreateCallThread(reference, out var threadReference);
            LuaValueConverter<T1>.Push(thread, argument1);
            LuaValueConverter<T2>.Push(thread, argument2);
            return ResumeAsync<TResult>(thread, threadReference, 2);
        }

        /// <summary>
        /// 引数なしで戻り値がないLua関数を非同期実行
        /// </summary>
        /// <param name="reference">Lua Registry上の関数参照</param>
        /// <returns>Lua関数の完了を表すValueTask</returns>
        internal ValueTask InvokeActionAsync(int reference) {
            ValidateAccess();
            var thread = CreateCallThread(reference, out var threadReference);
            return ResumeActionAsync(thread, threadReference, 0);
        }

        /// <summary>
        /// 引数が1つで戻り値がないLua関数を非同期実行
        /// </summary>
        /// <typeparam name="T1">第1引数のC#型</typeparam>
        /// <param name="reference">Lua Registry上の関数参照</param>
        /// <param name="argument1">第1引数</param>
        /// <returns>Lua関数の完了を表すValueTask</returns>
        internal ValueTask InvokeActionAsync<T1>(int reference, T1 argument1) {
            ValidateAccess();
            var thread = CreateCallThread(reference, out var threadReference);
            LuaValueConverter<T1>.Push(thread, argument1);
            return ResumeActionAsync(thread, threadReference, 1);
        }

        /// <summary>
        /// 引数が2つで戻り値がないLua関数を非同期実行
        /// </summary>
        /// <typeparam name="T1">第1引数のC#型</typeparam>
        /// <typeparam name="T2">第2引数のC#型</typeparam>
        /// <param name="reference">Lua Registry上の関数参照</param>
        /// <param name="argument1">第1引数</param>
        /// <param name="argument2">第2引数</param>
        /// <returns>Lua関数の完了を表すValueTask</returns>
        internal ValueTask InvokeActionAsync<T1, T2>(int reference, T1 argument1, T2 argument2) {
            ValidateAccess();
            var thread = CreateCallThread(reference, out var threadReference);
            LuaValueConverter<T1>.Push(thread, argument1);
            LuaValueConverter<T2>.Push(thread, argument2);
            return ResumeActionAsync(thread, threadReference, 2);
        }

        /// <summary>
        /// Lua Coroutineが返した値の数を検証
        /// </summary>
        /// <param name="actualCount">実際の戻り値数</param>
        /// <param name="expectedCount">期待する戻り値数</param>
        private static void RequireResultCount(int actualCount, int expectedCount) {
            if (actualCount != expectedCount) {
                throw new LuaException(LuaStatus.RuntimeError, $"The Lua coroutine returned {actualCount} value(s); expected {expectedCount}.");
            }
        }

        /// <summary>
        /// 戻り値があるLua Coroutineの実行を開始
        /// </summary>
        /// <typeparam name="TResult">戻り値のC#型</typeparam>
        /// <param name="thread">実行するLua Coroutine</param>
        /// <param name="threadReference">Lua Registry上のCoroutine参照</param>
        /// <param name="argumentCount">引数の数</param>
        /// <returns>Lua関数の完了を表すValueTask</returns>
        private ValueTask<TResult> ResumeAsync<TResult>(IntPtr thread, int threadReference, int argumentCount) {
            var status = (LuaStatus)LuaNative.Resume(thread, _state, argumentCount, out var resultCount);
            if (status == LuaStatus.Ok) {
                try {
                    RequireResultCount(resultCount, 1);
                    return new ValueTask<TResult>(LuaValueConverter<TResult>.Read(thread, -1));
                }
                finally {
                    LuaNative.ReleaseReference(_state, LuaNative.RegistryIndex, threadReference);
                }
            }
            if (status != LuaStatus.Yield) {
                var exception = CreateCoroutineException(thread, status);
                LuaNative.ReleaseReference(_state, LuaNative.RegistryIndex, threadReference);
                throw exception;
            }

            var pendingCall = new PendingLuaCall<TResult>(this, thread, threadReference);
            _pendingLuaCalls.Add(pendingCall);
            return new ValueTask<TResult>(pendingCall.Task);
        }

        /// <summary>
        /// 戻り値がないLua Coroutineの実行を開始
        /// </summary>
        /// <param name="thread">実行するLua Coroutine</param>
        /// <param name="threadReference">Lua Registry上のCoroutine参照</param>
        /// <param name="argumentCount">引数の数</param>
        /// <returns>Lua関数の完了を表すValueTask</returns>
        private ValueTask ResumeActionAsync(IntPtr thread, int threadReference, int argumentCount) {
            var status = (LuaStatus)LuaNative.Resume(thread, _state, argumentCount, out var resultCount);
            if (status == LuaStatus.Ok) {
                LuaNative.ReleaseReference(_state, LuaNative.RegistryIndex, threadReference);
                RequireResultCount(resultCount, 0);
                return default;
            }
            if (status != LuaStatus.Yield) {
                var exception = CreateCoroutineException(thread, status);
                LuaNative.ReleaseReference(_state, LuaNative.RegistryIndex, threadReference);
                throw exception;
            }

            var pendingCall = new PendingLuaAction(this, thread, threadReference);
            _pendingLuaCalls.Add(pendingCall);
            return new ValueTask(pendingCall.Task);
        }

        /// <summary>
        /// Lua関数を実行するCoroutineを生成
        /// </summary>
        /// <param name="reference">Lua Registry上の関数参照</param>
        /// <param name="threadReference">生成したCoroutineのRegistry参照</param>
        /// <returns>生成したLua Coroutine</returns>
        private IntPtr CreateCallThread(int reference, out int threadReference) {
            var thread = LuaNative.NewThread(_state);
            threadReference = LuaNative.CreateReference(_state, LuaNative.RegistryIndex);
            PushFunction(reference);
            LuaNative.Move(_state, thread, 1);
            return thread;
        }

        /// <summary>
        /// Lua Coroutineのエラーから例外を生成
        /// </summary>
        /// <param name="thread">エラーが発生したLua Coroutine</param>
        /// <param name="status">Luaの実行結果</param>
        /// <returns>エラー内容を保持する例外</returns>
        private LuaException CreateCoroutineException(IntPtr thread, LuaStatus status) {
            var message = GetString(thread, -1);
            return new LuaException(status, string.IsNullOrEmpty(message) ? $"Lua failed with status {status}." : message);
        }

        /// <summary>
        /// 待機中の非同期呼び出しをすべて失敗として完了
        /// </summary>
        /// <param name="exception">完了時に通知する例外</param>
        private void FailAllAsyncCalls(Exception exception) {
            foreach (var call in _pendingLuaCalls) {
                call.Fail(exception);
            }
            _pendingLuaCalls.Clear();
            foreach (var call in _pendingManagedCalls.Values) {
                call.Dispose();
            }
            _pendingManagedCalls.Clear();
        }
    }
}

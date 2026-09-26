using System;
using System.Collections.Concurrent;

namespace UnityLuaSystem.UniTask {
    /// <summary>
    /// UniTaskをLuaが待機できる処理へ変換するアダプター
    /// </summary>
    internal sealed class UniTaskAwaitableAdapter : ILuaAwaitableAdapter {
        private readonly ConcurrentDictionary<Type, Func<object, ILuaAwaitable>> _factories = new ConcurrentDictionary<Type, Func<object, ILuaAwaitable>>();

        /// <summary>共有インスタンス</summary>
        internal static UniTaskAwaitableAdapter Instance { get; } = new UniTaskAwaitableAdapter();

        /// <inheritdoc/>
        public bool TryCreate(Type returnType, object returnValue, out ILuaAwaitable awaitable) {
            if (returnType == typeof(global::Cysharp.Threading.Tasks.UniTask)) {
                awaitable = new UniTaskAwaitable((global::Cysharp.Threading.Tasks.UniTask)returnValue);
                return true;
            }
            if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(global::Cysharp.Threading.Tasks.UniTask<>)) {
                awaitable = null;
                return false;
            }

            var factory = _factories.GetOrAdd(returnType, type => {
                var resultType = type.GetGenericArguments()[0];
                var wrapperType = typeof(UniTaskAwaitable<>).MakeGenericType(resultType);
                return value => (ILuaAwaitable)Activator.CreateInstance(wrapperType, value);
            });

            awaitable = factory(returnValue);
            return true;
        }
    }
}

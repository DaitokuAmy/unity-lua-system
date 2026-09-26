using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace UnityLuaSystem {
    /// <summary>
    /// C#モジュールとオブジェクトのバインディングを管理するLuaRuntime実装
    /// </summary>
    public sealed partial class LuaRuntime {
        private static readonly LuaNative.ManagedCallback ManagedCallback = DispatchManagedCallback;

        private readonly List<GCHandle> _bindingHandles = new List<GCHandle>();
        private readonly Dictionary<long, object> _objects = new Dictionary<long, object>();
        private readonly Dictionary<object, long> _objectIds = new Dictionary<object, long>(ReferenceComparer.Instance);
        private readonly Dictionary<Type, string> _objectMetatables = new Dictionary<Type, string>();
        private readonly Dictionary<IntPtr, PendingManagedCall> _pendingManagedCalls = new Dictionary<IntPtr, PendingManagedCall>();
        private readonly List<ILuaAwaitableAdapter> _awaitableAdapters = new List<ILuaAwaitableAdapter>();

        private long _nextObjectId = 1;

        /// <summary>
        /// Attributeで公開されたインスタンスメソッドをLuaモジュールとして登録
        /// </summary>
        /// <typeparam name="TModule">登録するモジュール型</typeparam>
        /// <param name="module">登録するモジュールインスタンス</param>
        /// <returns>モジュール登録の有効期間を表すオブジェクト</returns>
        public LuaModuleRegistration RegisterModule<TModule>(TModule module) where TModule : class {
            if (module == null) {
                throw new ArgumentNullException(nameof(module));
            }

            return RegisterModule(module.GetType(), module, false);
        }

        /// <summary>
        /// Attributeで公開されたstaticメソッドをLuaモジュールとして登録
        /// </summary>
        /// <typeparam name="TModule">登録するstaticモジュール型</typeparam>
        /// <returns>モジュール登録の有効期間を表すオブジェクト</returns>
        public LuaModuleRegistration RegisterModule<TModule>() {
            return RegisterModule(typeof(TModule), null, true);
        }

        /// <summary>
        /// C#メソッドが返す非同期型のアダプターを登録
        /// </summary>
        /// <param name="adapter">登録する非同期型アダプター</param>
        public void RegisterAwaitableAdapter(ILuaAwaitableAdapter adapter) {
            ValidateAccess();
            if (adapter == null) {
                throw new ArgumentNullException(nameof(adapter));
            }
            if (_awaitableAdapters.Contains(adapter)) {
                return;
            }

            _awaitableAdapters.Add(adapter);
        }

        /// <summary>
        /// C#モジュールの登録を解除
        /// </summary>
        /// <param name="moduleName">Luaへ公開したモジュール名</param>
        /// <param name="bindings">無効化するバインディング</param>
        internal void UnregisterModule(string moduleName, BindingContext[] bindings) {
            ValidateThread();
            foreach (var binding in bindings) {
                binding.Deactivate();
            }

            if (_state == IntPtr.Zero) {
                return;
            }

            LuaNative.PushNil(_state);
            LuaNative.SetGlobal(_state, ToNullTerminatedUtf8(moduleName));
        }

        /// <summary>
        /// ネイティブブリッジからC#メソッド呼び出しを振り分け
        /// </summary>
        /// <param name="state">呼び出し元のLua state</param>
        /// <param name="contextPointer">バインディングを保持するGCHandle</param>
        /// <returns>Luaスタックへ積んだ戻り値数、または制御コード</returns>
        [AOT.MonoPInvokeCallback(typeof(LuaNative.ManagedCallback))]
        private static int DispatchManagedCallback(IntPtr state, IntPtr contextPointer) {
            try {
                var handle = GCHandle.FromIntPtr(contextPointer);
                var context = (BindingContext)handle.Target;
                return context.Invoke(state);
            }
            catch (Exception exception) {
                var message = Encoding.UTF8.GetBytes(exception.GetBaseException().Message);
                LuaNative.PushString(state, message, new UIntPtr((uint)message.Length));
                return -1;
            }
        }

        /// <summary>
        /// 指定した型のC#モジュールをLuaへ登録
        /// </summary>
        /// <param name="moduleType">登録するモジュール型</param>
        /// <param name="instance">登録するインスタンス</param>
        /// <param name="staticOnly">staticメソッドのみ登録するかどうか</param>
        /// <returns>モジュール登録の有効期間を表すオブジェクト</returns>
        private LuaModuleRegistration RegisterModule(Type moduleType, object instance, bool staticOnly) {
            ValidateAccess();
            var moduleAttribute = moduleType.GetCustomAttribute<LuaModuleAttribute>();
            if (moduleAttribute == null) {
                throw new ArgumentException($"The type '{moduleType.FullName}' must have LuaModuleAttribute.", nameof(moduleType));
            }

            var moduleName = string.IsNullOrEmpty(moduleAttribute.Name) ? moduleType.Name : moduleAttribute.Name;
            var methodFlags = BindingFlags.Public | (staticOnly ? BindingFlags.Static : BindingFlags.Instance);
            var methods = moduleType.GetMethods(methodFlags);
            var bindings = new List<BindingContext>();
            var generatedBinding = CreateGeneratedModuleBinding(moduleType, instance);

            using var stack = new LuaStackGuard(_state);
            LuaNative.CreateTable(_state, 0, methods.Length);
            foreach (var method in methods) {
                var functionAttribute = method.GetCustomAttribute<LuaFunctionAttribute>();
                if (functionAttribute == null) {
                    continue;
                }

                ValidateMethod(method);
                var functionName = string.IsNullOrEmpty(functionAttribute.Name) ? method.Name : functionAttribute.Name;
                BindingContext binding;
                if (generatedBinding != null && generatedBinding.TryGetFunction(method.Name, out var generatedName, out var callback)) {
                    functionName = generatedName;
                    binding = CreateBinding(callback);
                }
                else {
                    binding = CreateBinding(method, instance, false);
                }
                bindings.Add(binding);
                PushBinding(binding);
                LuaNative.SetField(_state, -2, ToNullTerminatedUtf8(functionName));
            }

            if (bindings.Count == 0) {
                throw new InvalidOperationException($"The module type '{moduleType.FullName}' has no matching LuaFunction methods.");
            }

            LuaNative.SetGlobal(_state, ToNullTerminatedUtf8(moduleName));
            return new LuaModuleRegistration(this, moduleName, bindings.ToArray());
        }

        /// <summary>
        /// C#メソッドのバインディングを生成
        /// </summary>
        /// <param name="method">公開するC#メソッド</param>
        /// <param name="target">呼び出し対象のインスタンス</param>
        /// <param name="objectMethod">LuaObjectのメソッドかどうか</param>
        /// <returns>生成したバインディング</returns>
        private BindingContext CreateBinding(MethodInfo method, object target, bool objectMethod) {
            var binding = new BindingContext(this, method, target, objectMethod);
            var handle = GCHandle.Alloc(binding);
            binding.SetHandle(handle);
            _bindingHandles.Add(handle);
            return binding;
        }

        /// <summary>
        /// 生成済みC#メソッドのバインディングを生成
        /// </summary>
        /// <param name="callback">生成済み呼び出し処理</param>
        /// <returns>生成したバインディング</returns>
        private BindingContext CreateBinding(LuaGeneratedFunctionCallback callback) {
            var binding = new BindingContext(this, callback);
            var handle = GCHandle.Alloc(binding);
            binding.SetHandle(handle);
            _bindingHandles.Add(handle);
            return binding;
        }

        /// <summary>
        /// C#モジュール型に対応する生成済みバインディングを生成
        /// </summary>
        /// <param name="moduleType">登録するC#モジュール型</param>
        /// <param name="instance">登録するモジュールインスタンス</param>
        /// <returns>生成済みバインディング、またはnull</returns>
        private ILuaGeneratedModuleBinding CreateGeneratedModuleBinding(Type moduleType, object instance) {
            if (moduleType.IsNested || moduleType.IsGenericType) {
                return null;
            }

            var generatedTypeName = string.IsNullOrEmpty(moduleType.Namespace)
                ? $"__UnityLuaSystem_{moduleType.Name}Binding"
                : $"{moduleType.Namespace}.__UnityLuaSystem_{moduleType.Name}Binding";
            var generatedType = moduleType.Assembly.GetType(generatedTypeName, false);
            if (generatedType == null || !typeof(ILuaGeneratedModuleBinding).IsAssignableFrom(generatedType)) {
                return null;
            }

            return (ILuaGeneratedModuleBinding)Activator.CreateInstance(generatedType, instance);
        }

        /// <summary>
        /// 生成済みバインディングからLua引数を読み取り
        /// </summary>
        /// <typeparam name="T">読み取るC#型</typeparam>
        /// <param name="state">対象のLua state</param>
        /// <param name="index">Luaスタック上の位置</param>
        /// <returns>変換したC#値</returns>
        public T ReadGeneratedValue<T>(IntPtr state, int index) {
            if (GeneratedValueType<T>.IsLuaObjectArray) {
                return (T)(object)ReadManagedArray(state, index, typeof(T).GetElementType());
            }
            if (GeneratedValueType<T>.IsLuaObject) {
                return (T)ReadObject(state, index, typeof(T));
            }

            return LuaValueConverter<T>.Read(state, index);
        }

        /// <summary>
        /// 生成済みバインディングからC#値をLuaスタックへ積む
        /// </summary>
        /// <typeparam name="T">積むC#型</typeparam>
        /// <param name="state">対象のLua state</param>
        /// <param name="value">積むC#値</param>
        public void PushGeneratedValue<T>(IntPtr state, T value) {
            if (GeneratedValueType<T>.IsLuaObjectArray) {
                if (value == null) {
                    LuaNative.PushNil(state);
                }
                else {
                    PushManagedArray(state, (Array)(object)value, typeof(T).GetElementType());
                }
                return;
            }
            if (GeneratedValueType<T>.IsLuaObject) {
                if (value == null) {
                    LuaNative.PushNil(state);
                }
                else {
                    PushObject(state, value, typeof(T));
                }
                return;
            }

            LuaValueConverter<T>.Push(state, value);
        }

        /// <summary>
        /// 生成済みバインディングへ渡されたLua引数の数を検証
        /// </summary>
        /// <param name="state">対象のLua state</param>
        /// <param name="expectedCount">期待する引数の数</param>
        public void ValidateGeneratedArgumentCount(IntPtr state, int expectedCount) {
            var actualCount = LuaNative.GetTop(state);
            if (actualCount != expectedCount) {
                throw new ArgumentException($"The generated C# binding expects {expectedCount} argument(s), but received {actualCount}.");
            }
        }

        /// <summary>
        /// C#メソッドのコールバックをLuaスタックへ積む
        /// </summary>
        /// <param name="binding">コールバックへ関連付けるバインディング</param>
        private void PushBinding(BindingContext binding) {
            LuaNative.PushManagedCallback(_state, ManagedCallback, GCHandle.ToIntPtr(binding.Handle));
        }

        /// <summary>
        /// C#値をLuaスタックへ積む
        /// </summary>
        /// <param name="state">対象のLua state</param>
        /// <param name="value">積むC#値</param>
        /// <param name="declaredType">C#メソッド上の宣言型</param>
        private void PushManagedValue(IntPtr state, object value, Type declaredType) {
            if (declaredType == typeof(void)) {
                return;
            }
            if (value == null) {
                LuaNative.PushNil(state);
                return;
            }
            if (declaredType == typeof(bool)) {
                LuaNative.PushBoolean(state, (bool)value ? 1 : 0);
                return;
            }
            if (declaredType == typeof(int)) {
                LuaNative.PushInteger(state, (int)value);
                return;
            }
            if (declaredType == typeof(long)) {
                LuaNative.PushInteger(state, (long)value);
                return;
            }
            if (declaredType == typeof(float)) {
                LuaNative.PushNumber(state, (float)value);
                return;
            }
            if (declaredType == typeof(double)) {
                LuaNative.PushNumber(state, (double)value);
                return;
            }
            if (declaredType == typeof(string)) {
                var bytes = Encoding.UTF8.GetBytes((string)value);
                LuaNative.PushString(state, bytes, new UIntPtr((uint)bytes.Length));
                return;
            }
            if (declaredType.IsArray && declaredType.GetArrayRank() == 1) {
                PushManagedArray(state, (Array)value, declaredType.GetElementType());
                return;
            }
            if (declaredType.GetCustomAttribute<LuaObjectAttribute>() != null) {
                PushObject(state, value, declaredType);
                return;
            }

            throw new NotSupportedException($"The type '{declaredType.FullName}' cannot be returned to Lua.");
        }

        /// <summary>
        /// Luaスタックの値をC#値として読み取り
        /// </summary>
        /// <param name="state">対象のLua state</param>
        /// <param name="index">Luaスタック上の位置</param>
        /// <param name="targetType">変換先のC#型</param>
        /// <returns>変換したC#値</returns>
        private object ReadManagedValue(IntPtr state, int index, Type targetType) {
            if (targetType == typeof(bool)) {
                return LuaValueConverter<bool>.Read(state, index);
            }
            if (targetType == typeof(int)) {
                return LuaValueConverter<int>.Read(state, index);
            }
            if (targetType == typeof(long)) {
                return LuaValueConverter<long>.Read(state, index);
            }
            if (targetType == typeof(float)) {
                return LuaValueConverter<float>.Read(state, index);
            }
            if (targetType == typeof(double)) {
                return LuaValueConverter<double>.Read(state, index);
            }
            if (targetType == typeof(string)) {
                return LuaValueConverter<string>.Read(state, index);
            }
            if (targetType.IsArray && targetType.GetArrayRank() == 1) {
                return ReadManagedArray(state, index, targetType.GetElementType());
            }
            if (targetType.GetCustomAttribute<LuaObjectAttribute>() != null) {
                return ReadObject(state, index, targetType);
            }

            throw new NotSupportedException($"The type '{targetType.FullName}' cannot be read from Lua.");
        }

        /// <summary>
        /// C#配列をLua tableとして積む
        /// </summary>
        /// <param name="state">対象のLua state</param>
        /// <param name="values">積む配列</param>
        /// <param name="elementType">配列要素の型</param>
        private void PushManagedArray(IntPtr state, Array values, Type elementType) {
            LuaNative.CreateTable(state, values.Length, 0);
            for (var index = 0; index < values.Length; index++) {
                PushManagedValue(state, values.GetValue(index), elementType);
                LuaNative.RawSetInteger(state, -2, index + 1);
            }
        }

        /// <summary>
        /// Lua tableをC#配列として読み取り
        /// </summary>
        /// <param name="state">対象のLua state</param>
        /// <param name="index">Luaスタック上の位置</param>
        /// <param name="elementType">配列要素の型</param>
        /// <returns>変換した一次元配列</returns>
        private Array ReadManagedArray(IntPtr state, int index, Type elementType) {
            if (LuaNative.GetType(state, index) == LuaValueType.Nil) {
                return null;
            }
            if (LuaNative.GetType(state, index) != LuaValueType.Table) {
                throw new LuaException(LuaStatus.RuntimeError, $"Expected a Lua table at stack index {index}.");
            }

            var tableIndex = index > 0 || index <= LuaNative.RegistryIndex ? index : LuaNative.GetTop(state) + index + 1;
            var length = checked((int)LuaNative.RawLength(state, tableIndex).ToUInt64());
            var values = Array.CreateInstance(elementType, length);
            for (var arrayIndex = 0; arrayIndex < length; arrayIndex++) {
                LuaNative.RawGetInteger(state, tableIndex, arrayIndex + 1);
                try {
                    values.SetValue(ReadManagedValue(state, -1, elementType), arrayIndex);
                }
                finally {
                    LuaNative.SetTop(state, LuaNative.GetTop(state) - 1);
                }
            }
            return values;
        }

        /// <summary>
        /// C#オブジェクトをLua userdataとして積む
        /// </summary>
        /// <param name="state">対象のLua state</param>
        /// <param name="value">公開するC#オブジェクト</param>
        /// <param name="objectType">公開するオブジェクト型</param>
        private void PushObject(IntPtr state, object value, Type objectType) {
            if (!_objectIds.TryGetValue(value, out var objectId)) {
                objectId = _nextObjectId++;
                _objectIds.Add(value, objectId);
                _objects.Add(objectId, value);
            }

            var memory = LuaNative.NewUserData(state, new UIntPtr(sizeof(long)), 0);
            Marshal.WriteInt64(memory, objectId);
            var metatableName = EnsureObjectMetatable(objectType);
            LuaNative.NewMetatable(state, ToNullTerminatedUtf8(metatableName));
            LuaNative.SetMetatable(state, -2);
        }

        /// <summary>
        /// Lua userdataに対応するC#オブジェクトを取得
        /// </summary>
        /// <param name="state">対象のLua state</param>
        /// <param name="index">Luaスタック上の位置</param>
        /// <param name="expectedType">期待するC#型</param>
        /// <returns>userdataに対応するC#オブジェクト</returns>
        private object ReadObject(IntPtr state, int index, Type expectedType) {
            var metatableName = EnsureObjectMetatable(expectedType);
            var memory = LuaNative.TestUserData(state, index, ToNullTerminatedUtf8(metatableName));
            if (memory == IntPtr.Zero) {
                throw new LuaException(LuaStatus.RuntimeError, $"Expected '{expectedType.FullName}' userdata at stack index {index}.");
            }

            var objectId = Marshal.ReadInt64(memory);
            if (!_objects.TryGetValue(objectId, out var value) || !expectedType.IsInstanceOfType(value)) {
                throw new LuaException(LuaStatus.RuntimeError, $"The userdata at stack index {index} is not a valid '{expectedType.FullName}'.");
            }
            return value;
        }

        /// <summary>
        /// LuaObject用metatableを登録して名前を取得
        /// </summary>
        /// <param name="objectType">登録するLuaObject型</param>
        /// <returns>登録したmetatable名</returns>
        private string EnsureObjectMetatable(Type objectType) {
            if (_objectMetatables.TryGetValue(objectType, out var existingName)) {
                return existingName;
            }

            var metatableName = $"UnityLuaSystem.{objectType.AssemblyQualifiedName}";
            LuaNative.NewMetatable(_state, ToNullTerminatedUtf8(metatableName));
            LuaNative.PushValue(_state, -1);
            LuaNative.SetField(_state, -2, ToNullTerminatedUtf8("__index"));

            foreach (var method in objectType.GetMethods(BindingFlags.Public | BindingFlags.Instance)) {
                var functionAttribute = method.GetCustomAttribute<LuaFunctionAttribute>();
                if (functionAttribute == null) {
                    continue;
                }

                ValidateMethod(method);
                var functionName = string.IsNullOrEmpty(functionAttribute.Name) ? method.Name : functionAttribute.Name;
                var binding = CreateBinding(method, null, true);
                PushBinding(binding);
                LuaNative.SetField(_state, -2, ToNullTerminatedUtf8(functionName));
            }

            LuaNative.SetTop(_state, LuaNative.GetTop(_state) - 1);
            _objectMetatables.Add(objectType, metatableName);
            return metatableName;
        }

        /// <summary>
        /// Luaへ公開するC#メソッドのシグネチャを検証
        /// </summary>
        /// <param name="method">検証するC#メソッド</param>
        private static void ValidateMethod(MethodInfo method) {
            if (method.ContainsGenericParameters || method.ReturnType.IsByRef) {
                throw new NotSupportedException($"The method '{method.DeclaringType?.FullName}.{method.Name}' has an unsupported signature.");
            }
            foreach (var parameter in method.GetParameters()) {
                if (parameter.ParameterType.IsByRef || parameter.IsOut || parameter.IsOptional) {
                    throw new NotSupportedException($"The method '{method.DeclaringType?.FullName}.{method.Name}' has an unsupported parameter.");
                }
            }
        }

        /// <summary>
        /// Runtimeが所有するバインディングとオブジェクト参照を解放
        /// </summary>
        private void ReleaseBindingHandles() {
            foreach (var handle in _bindingHandles) {
                if (handle.IsAllocated) {
                    handle.Free();
                }
            }
            _bindingHandles.Clear();
            _objects.Clear();
            _objectIds.Clear();
            _objectMetatables.Clear();
            _awaitableAdapters.Clear();
            foreach (var call in _pendingManagedCalls.Values) {
                call.Dispose();
            }
            _pendingManagedCalls.Clear();
        }

        /// <summary>C#メソッド1件分の呼び出し情報</summary>
        internal sealed class BindingContext {
            private readonly LuaRuntime _runtime;
            private readonly MethodInfo _method;
            private readonly bool _objectMethod;
            private object _target;
            private LuaGeneratedFunctionCallback _generatedCallback;
            private bool _active = true;

            /// <summary>ネイティブコールバックへ渡すGCHandle</summary>
            internal GCHandle Handle { get; private set; }

            /// <summary>
            /// C#メソッドの呼び出し情報を生成
            /// </summary>
            /// <param name="runtime">呼び出し元のLuaRuntime</param>
            /// <param name="method">呼び出すC#メソッド</param>
            /// <param name="target">呼び出し対象のインスタンス</param>
            /// <param name="objectMethod">LuaObjectのメソッドかどうか</param>
            internal BindingContext(LuaRuntime runtime, MethodInfo method, object target, bool objectMethod) {
                _runtime = runtime;
                _method = method;
                _target = target;
                _objectMethod = objectMethod;
            }

            /// <summary>
            /// 生成済みC#メソッドの呼び出し情報を生成
            /// </summary>
            /// <param name="runtime">呼び出し元のLuaRuntime</param>
            /// <param name="generatedCallback">生成済み呼び出し処理</param>
            internal BindingContext(LuaRuntime runtime, LuaGeneratedFunctionCallback generatedCallback) {
                _runtime = runtime;
                _generatedCallback = generatedCallback;
            }

            /// <summary>
            /// ネイティブコールバックへ渡すGCHandleを設定
            /// </summary>
            /// <param name="handle">バインディング自身を保持するGCHandle</param>
            internal void SetHandle(GCHandle handle) {
                Handle = handle;
            }

            /// <summary>
            /// Luaスタックから引数を取得してC#メソッドを実行
            /// </summary>
            /// <param name="state">呼び出し元のLua state</param>
            /// <returns>Luaスタックへ積んだ戻り値数、または制御コード</returns>
            internal int Invoke(IntPtr state) {
                if (!_active) {
                    throw new InvalidOperationException("The Lua binding is no longer registered.");
                }

                if (_generatedCallback != null) {
                    return _generatedCallback(_runtime, state);
                }

                if (_runtime._pendingManagedCalls.TryGetValue(state, out var pendingCall)) {
                    if (!ReferenceEquals(pendingCall.Binding, this)) {
                        throw new InvalidOperationException("The pending C# call does not match the resumed Lua callback.");
                    }
                    if (!pendingCall.IsCompleted) {
                        return -2;
                    }

                    _runtime._pendingManagedCalls.Remove(state);
                    try {
                        var pendingResult = pendingCall.GetResult();
                        if (pendingCall.ResultType != typeof(void)) {
                            _runtime.PushManagedValue(state, pendingResult, pendingCall.ResultType);
                            return 1;
                        }
                        return 0;
                    }
                    finally {
                        pendingCall.Dispose();
                    }
                }

                var target = _objectMethod ? _runtime.ReadObject(state, 1, _method.DeclaringType) : _target;
                var parameters = _method.GetParameters();
                var firstArgumentIndex = _objectMethod ? 2 : 1;
                var actualArgumentCount = LuaNative.GetTop(state) - firstArgumentIndex + 1;
                if (actualArgumentCount != parameters.Length) {
                    throw new ArgumentException($"'{_method.Name}' expects {parameters.Length} argument(s), but received {actualArgumentCount}.");
                }

                var arguments = new object[parameters.Length];
                for (var index = 0; index < parameters.Length; index++) {
                    arguments[index] = _runtime.ReadManagedValue(state, firstArgumentIndex + index, parameters[index].ParameterType);
                }

                object result;
                try {
                    result = _method.Invoke(target, arguments);
                }
                catch (TargetInvocationException exception) when (exception.InnerException != null) {
                    throw exception.InnerException;
                }

                if (result is Task task) {
                    var resultType = GetTaskResultType(_method.ReturnType);
                    if (!task.IsCompleted) {
                        _runtime._pendingManagedCalls.Add(state, new PendingManagedCall(this, task, resultType));
                        return -2;
                    }

                    task.GetAwaiter().GetResult();
                    if (resultType != typeof(void)) {
                        _runtime.PushManagedValue(state, GetTaskResult(task), resultType);
                        return 1;
                    }
                    return 0;
                }
                if (result is IEnumerator enumerator) {
                    var pendingEnumerator = new PendingManagedCall(this, enumerator);
                    if (pendingEnumerator.Advance()) {
                        try {
                            pendingEnumerator.GetResult();
                            return 0;
                        }
                        finally {
                            pendingEnumerator.Dispose();
                        }
                    }

                    _runtime._pendingManagedCalls.Add(state, pendingEnumerator);
                    return -2;
                }
                foreach (var adapter in _runtime._awaitableAdapters) {
                    if (!adapter.TryCreate(_method.ReturnType, result, out var awaitable)) {
                        continue;
                    }

                    var pendingAwaitable = new PendingManagedCall(this, awaitable);
                    if (!pendingAwaitable.IsCompleted) {
                        _runtime._pendingManagedCalls.Add(state, pendingAwaitable);
                        return -2;
                    }

                    try {
                        var awaitableResult = pendingAwaitable.GetResult();
                        if (pendingAwaitable.ResultType != typeof(void)) {
                            _runtime.PushManagedValue(state, awaitableResult, pendingAwaitable.ResultType);
                            return 1;
                        }
                        return 0;
                    }
                    finally {
                        pendingAwaitable.Dispose();
                    }
                }

                _runtime.PushManagedValue(state, result, _method.ReturnType);
                return _method.ReturnType == typeof(void) ? 0 : 1;
            }

            /// <summary>
            /// バインディングを無効化して対象インスタンスを解放
            /// </summary>
            internal void Deactivate() {
                _active = false;
                _target = null;
                _generatedCallback = null;
            }

            /// <summary>
            /// Task型から非同期結果の型を取得
            /// </summary>
            /// <param name="taskType">確認するTask型</param>
            /// <returns>Taskの結果型、またはvoid</returns>
            private static Type GetTaskResultType(Type taskType) {
                return taskType.IsGenericType && taskType.GetGenericTypeDefinition() == typeof(Task<>)
                    ? taskType.GetGenericArguments()[0]
                    : typeof(void);
            }

            /// <summary>
            /// 完了したTaskから結果を取得
            /// </summary>
            /// <param name="task">結果を保持するTask</param>
            /// <returns>Taskの結果</returns>
            private static object GetTaskResult(Task task) {
                return task.GetType().GetProperty("Result")?.GetValue(task);
            }
        }

        /// <summary>Lua Coroutineが待機しているC#処理</summary>
        private sealed class PendingManagedCall : IDisposable {
            private readonly IEnumerator _enumerator;
            private readonly ILuaAwaitable _awaitable;
            private Exception _exception;

            /// <summary>処理を開始したバインディング</summary>
            internal BindingContext Binding { get; }
            /// <summary>待機対象のTask</summary>
            internal Task Task { get; }
            /// <summary>処理完了時にLuaへ返す型</summary>
            internal Type ResultType { get; }
            /// <summary>C#処理が完了したかどうか</summary>
            internal bool IsCompleted { get; private set; }

            /// <summary>
            /// Taskを待機するC#処理を生成
            /// </summary>
            /// <param name="binding">処理を開始したバインディング</param>
            /// <param name="task">待機対象のTask</param>
            /// <param name="resultType">処理完了時にLuaへ返す型</param>
            internal PendingManagedCall(BindingContext binding, Task task, Type resultType) {
                Binding = binding;
                Task = task;
                ResultType = resultType;
                IsCompleted = task.IsCompleted;
            }

            /// <summary>
            /// IEnumeratorを進行するC#処理を生成
            /// </summary>
            /// <param name="binding">処理を開始したバインディング</param>
            /// <param name="enumerator">Tickごとに進行するIEnumerator</param>
            internal PendingManagedCall(BindingContext binding, IEnumerator enumerator) {
                Binding = binding;
                _enumerator = enumerator;
                ResultType = typeof(void);
            }

            /// <summary>
            /// アダプターが生成した非同期処理を待機するC#処理を生成
            /// </summary>
            /// <param name="binding">処理を開始したバインディング</param>
            /// <param name="awaitable">待機対象の非同期処理</param>
            internal PendingManagedCall(BindingContext binding, ILuaAwaitable awaitable) {
                Binding = binding;
                _awaitable = awaitable;
                ResultType = awaitable.ResultType;
                IsCompleted = awaitable.IsCompleted;
            }

            /// <summary>
            /// 完了したC#処理の結果を取得
            /// </summary>
            /// <returns>C#処理の結果</returns>
            internal object GetResult() {
                if (_exception != null) {
                    throw _exception;
                }
                if (Task == null) {
                    return _awaitable?.GetResult();
                }

                Task.GetAwaiter().GetResult();
                return ResultType == typeof(void) ? null : Task.GetType().GetProperty("Result")?.GetValue(Task);
            }

            /// <summary>
            /// C#処理を確認または1ステップ進行
            /// </summary>
            /// <returns>C#処理が完了した場合はtrue</returns>
            internal bool Advance() {
                if (Task != null) {
                    IsCompleted = Task.IsCompleted;
                    return IsCompleted;
                }
                if (_awaitable != null) {
                    IsCompleted = _awaitable.IsCompleted;
                    return IsCompleted;
                }
                if (IsCompleted) {
                    return true;
                }

                try {
                    IsCompleted = !_enumerator.MoveNext();
                }
                catch (Exception exception) {
                    _exception = exception;
                    IsCompleted = true;
                }
                return IsCompleted;
            }

            /// <inheritdoc/>
            public void Dispose() {
                (_enumerator as IDisposable)?.Dispose();
                (_awaitable as IDisposable)?.Dispose();
            }
        }

        /// <summary>オブジェクトを参照同一性で比較するクラス</summary>
        private sealed class ReferenceComparer : IEqualityComparer<object> {
            /// <summary>共有インスタンス</summary>
            internal static ReferenceComparer Instance { get; } = new ReferenceComparer();

            /// <inheritdoc/>
            public new bool Equals(object left, object right) {
                return ReferenceEquals(left, right);
            }

            /// <inheritdoc/>
            public int GetHashCode(object value) {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
            }
        }

        /// <summary>
        /// 生成済み値変換で使用する型情報
        /// </summary>
        /// <typeparam name="T">確認するC#型</typeparam>
        private static class GeneratedValueType<T> {
            /// <summary>LuaObjectとして扱う型かどうか</summary>
            internal static bool IsLuaObject { get; } = typeof(T).GetCustomAttribute<LuaObjectAttribute>() != null;
            /// <summary>LuaObjectの一次元配列として扱う型かどうか</summary>
            internal static bool IsLuaObjectArray { get; } = typeof(T).IsArray
                && typeof(T).GetArrayRank() == 1
                && typeof(T).GetElementType().GetCustomAttribute<LuaObjectAttribute>() != null;
        }
    }
}

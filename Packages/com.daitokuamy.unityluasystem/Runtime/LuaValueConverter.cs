using System;
using System.Runtime.InteropServices;
using System.Text;

namespace UnityLuaSystem {
    /// <summary>
    /// C#値とLua値の相互変換を提供するクラス
    /// </summary>
    internal static class LuaValueConverter<T> {
        private static readonly ILuaValueConverter<T> Converter = CreateConverter();

        /// <summary>
        /// C#値をLuaスタックへ積む
        /// </summary>
        internal static void Push(IntPtr state, T value) {
            Converter.Push(state, value);
        }

        /// <summary>
        /// LuaスタックからC#値を読み取り
        /// </summary>
        internal static T Read(IntPtr state, int index) {
            return Converter.Read(state, index);
        }

        /// <summary>
        /// 対象型に対応する変換処理を生成
        /// </summary>
        private static ILuaValueConverter<T> CreateConverter() {
            var type = typeof(T);
            if (type == typeof(bool)) {
                return (ILuaValueConverter<T>)(object)new BooleanConverter();
            }
            if (type == typeof(int)) {
                return (ILuaValueConverter<T>)(object)new Int32Converter();
            }
            if (type == typeof(long)) {
                return (ILuaValueConverter<T>)(object)new Int64Converter();
            }
            if (type == typeof(float)) {
                return (ILuaValueConverter<T>)(object)new SingleConverter();
            }
            if (type == typeof(double)) {
                return (ILuaValueConverter<T>)(object)new DoubleConverter();
            }
            if (type == typeof(string)) {
                return (ILuaValueConverter<T>)(object)new StringConverter();
            }
            if (type == typeof(bool[])) {
                return (ILuaValueConverter<T>)(object)new ArrayConverter<bool>();
            }
            if (type == typeof(int[])) {
                return (ILuaValueConverter<T>)(object)new ArrayConverter<int>();
            }
            if (type == typeof(long[])) {
                return (ILuaValueConverter<T>)(object)new ArrayConverter<long>();
            }
            if (type == typeof(float[])) {
                return (ILuaValueConverter<T>)(object)new ArrayConverter<float>();
            }
            if (type == typeof(double[])) {
                return (ILuaValueConverter<T>)(object)new ArrayConverter<double>();
            }
            if (type == typeof(string[])) {
                return (ILuaValueConverter<T>)(object)new ArrayConverter<string>();
            }

            throw new NotSupportedException($"The type '{type.FullName}' is not supported by the Lua value converter.");
        }

        /// <summary>
        /// C#値とLua値の変換処理
        /// </summary>
        private interface ILuaValueConverter<TValue> {
            /// <summary>
            /// C#値をLuaスタックへ積む
            /// </summary>
            void Push(IntPtr state, TValue value);
            /// <summary>
            /// LuaスタックからC#値を読み取り
            /// </summary>
            TValue Read(IntPtr state, int index);
        }

        /// <summary>
        /// bool値の変換処理
        /// </summary>
        private sealed class BooleanConverter : ILuaValueConverter<bool> {
            /// <inheritdoc/>
            public void Push(IntPtr state, bool value) {
                LuaNative.PushBoolean(state, value ? 1 : 0);
            }

            /// <inheritdoc/>
            public bool Read(IntPtr state, int index) {
                RequireType(state, index, LuaValueType.Boolean, typeof(bool));
                return LuaNative.ToBoolean(state, index) != 0;
            }
        }

        /// <summary>
        /// int値の変換処理
        /// </summary>
        private sealed class Int32Converter : ILuaValueConverter<int> {
            /// <inheritdoc/>
            public void Push(IntPtr state, int value) {
                LuaNative.PushInteger(state, value);
            }

            /// <inheritdoc/>
            public int Read(IntPtr state, int index) {
                return checked((int)ReadInteger(state, index, typeof(int)));
            }
        }

        /// <summary>
        /// long値の変換処理
        /// </summary>
        private sealed class Int64Converter : ILuaValueConverter<long> {
            /// <inheritdoc/>
            public void Push(IntPtr state, long value) {
                LuaNative.PushInteger(state, value);
            }

            /// <inheritdoc/>
            public long Read(IntPtr state, int index) {
                return ReadInteger(state, index, typeof(long));
            }
        }

        /// <summary>
        /// float値の変換処理
        /// </summary>
        private sealed class SingleConverter : ILuaValueConverter<float> {
            /// <inheritdoc/>
            public void Push(IntPtr state, float value) {
                LuaNative.PushNumber(state, value);
            }

            /// <inheritdoc/>
            public float Read(IntPtr state, int index) {
                return (float)ReadNumber(state, index, typeof(float));
            }
        }

        /// <summary>
        /// double値の変換処理
        /// </summary>
        private sealed class DoubleConverter : ILuaValueConverter<double> {
            /// <inheritdoc/>
            public void Push(IntPtr state, double value) {
                LuaNative.PushNumber(state, value);
            }

            /// <inheritdoc/>
            public double Read(IntPtr state, int index) {
                return ReadNumber(state, index, typeof(double));
            }
        }

        /// <summary>
        /// string値の変換処理
        /// </summary>
        private sealed class StringConverter : ILuaValueConverter<string> {
            /// <inheritdoc/>
            public void Push(IntPtr state, string value) {
                if (value == null) {
                    throw new ArgumentNullException(nameof(value), "Null string conversion is not supported yet.");
                }

                var bytes = Encoding.UTF8.GetBytes(value);
                LuaNative.PushString(state, bytes, new UIntPtr((uint)bytes.Length));
            }

            /// <inheritdoc/>
            public string Read(IntPtr state, int index) {
                RequireType(state, index, LuaValueType.String, typeof(string));
                return ReadString(state, index);
            }
        }

        /// <summary>
        /// 一次元配列とLua tableの変換処理
        /// </summary>
        /// <typeparam name="TElement">配列要素の型</typeparam>
        private sealed class ArrayConverter<TElement> : ILuaValueConverter<TElement[]> {
            /// <inheritdoc/>
            public void Push(IntPtr state, TElement[] value) {
                if (value == null) {
                    LuaNative.PushNil(state);
                    return;
                }

                LuaNative.CreateTable(state, value.Length, 0);
                for (var index = 0; index < value.Length; index++) {
                    LuaValueConverter<TElement>.Push(state, value[index]);
                    LuaNative.RawSetInteger(state, -2, index + 1);
                }
            }

            /// <inheritdoc/>
            public TElement[] Read(IntPtr state, int index) {
                var valueType = LuaNative.GetType(state, index);
                if (valueType == LuaValueType.Nil) {
                    return null;
                }
                if (valueType != LuaValueType.Table) {
                    ThrowTypeError(state, index, typeof(TElement[]));
                }

                var tableIndex = GetAbsoluteIndex(state, index);
                var length = checked((int)LuaNative.RawLength(state, tableIndex).ToUInt64());
                var values = new TElement[length];
                for (var arrayIndex = 0; arrayIndex < length; arrayIndex++) {
                    LuaNative.RawGetInteger(state, tableIndex, arrayIndex + 1);
                    try {
                        values[arrayIndex] = LuaValueConverter<TElement>.Read(state, -1);
                    }
                    finally {
                        LuaNative.SetTop(state, LuaNative.GetTop(state) - 1);
                    }
                }
                return values;
            }
        }

        /// <summary>
        /// Luaスタックの相対インデックスを絶対インデックスへ変換
        /// </summary>
        private static int GetAbsoluteIndex(IntPtr state, int index) {
            return index > 0 || index <= LuaNative.RegistryIndex ? index : LuaNative.GetTop(state) + index + 1;
        }

        /// <summary>
        /// Luaスタックから整数値を読み取り
        /// </summary>
        private static long ReadInteger(IntPtr state, int index, Type targetType) {
            var value = LuaNative.ToInteger(state, index, out var isNumber);
            if (isNumber == 0) {
                ThrowTypeError(state, index, targetType);
            }
            return value;
        }

        /// <summary>
        /// Luaスタックから浮動小数点値を読み取り
        /// </summary>
        private static double ReadNumber(IntPtr state, int index, Type targetType) {
            var value = LuaNative.ToNumber(state, index, out var isNumber);
            if (isNumber == 0) {
                ThrowTypeError(state, index, targetType);
            }
            return value;
        }

        /// <summary>
        /// Luaスタックから文字列を読み取り
        /// </summary>
        private static string ReadString(IntPtr state, int index) {
            var pointer = LuaNative.ToStringPointer(state, index, out var nativeLength);
            if (pointer == IntPtr.Zero) {
                return null;
            }

            var length = checked((int)nativeLength.ToUInt64());
            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Luaスタック上の値型を検証
        /// </summary>
        private static void RequireType(IntPtr state, int index, LuaValueType expectedType, Type targetType) {
            if (LuaNative.GetType(state, index) != expectedType) {
                ThrowTypeError(state, index, targetType);
            }
        }

        /// <summary>
        /// Lua値の型変換例外を送出
        /// </summary>
        private static void ThrowTypeError(IntPtr state, int index, Type targetType) {
            var actualType = LuaNative.GetType(state, index);
            throw new LuaException(LuaStatus.RuntimeError, $"Cannot convert Lua value '{actualType}' at stack index {index} to '{targetType.FullName}'.");
        }
    }
}

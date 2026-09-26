using System;
using System.Runtime.InteropServices;

namespace UnityLuaSystem {
    /// <summary>
    /// Lua 5.4 C APIのネイティブ関数を提供するクラス
    /// </summary>
    internal static class LuaNative {
        /// <summary>Lua Registryを示す疑似インデックス</summary>
        internal const int RegistryIndex = -1001000;

        /// <summary>ネイティブライブラリ名</summary>
#if UNITY_IOS && !UNITY_EDITOR
        private const string LibraryName = "__Internal";
#else
        private const string LibraryName = "unitylua54";
#endif

        /// <summary>
        /// ネイティブブリッジから呼び出すC#コールバック
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int ManagedCallback(IntPtr state, IntPtr context);

        /// <summary>
        /// Lua stateを生成
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "luaL_newstate")]
        internal static extern IntPtr NewState();
        /// <summary>
        /// Lua標準ライブラリを開く
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "luaL_openlibs")]
        internal static extern void OpenLibraries(IntPtr state);
        /// <summary>
        /// Luaソースをチャンクとしてロード
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "luaL_loadbufferx")]
        internal static extern int LoadBuffer(IntPtr state, byte[] buffer, UIntPtr size, byte[] name, IntPtr mode);
        /// <summary>
        /// Lua Registryへ参照を生成
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "luaL_ref")]
        internal static extern int CreateReference(IntPtr state, int tableIndex);
        /// <summary>
        /// Lua Registry上の参照を解放
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "luaL_unref")]
        internal static extern void ReleaseReference(IntPtr state, int tableIndex, int reference);
        /// <summary>
        /// Lua関数を保護呼び出し
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_pcallk")]
        internal static extern int ProtectedCall(IntPtr state, int argumentCount, int resultCount, int errorFunctionIndex, IntPtr context, IntPtr continuation);
        /// <summary>
        /// Luaグローバル値を取得
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_getglobal")]
        internal static extern LuaValueType GetGlobal(IntPtr state, byte[] name);
        /// <summary>
        /// 整数キーを使用してLua tableから値を取得
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_rawgeti")]
        internal static extern LuaValueType RawGetInteger(IntPtr state, int tableIndex, long index);
        /// <summary>
        /// Lua tableの配列部分の長さを取得
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_rawlen")]
        internal static extern UIntPtr RawLength(IntPtr state, int index);
        /// <summary>
        /// 整数キーを使用してLua tableへ値を設定
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_rawseti")]
        internal static extern void RawSetInteger(IntPtr state, int tableIndex, long index);
        /// <summary>
        /// Lua Coroutineを生成
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_newthread")]
        internal static extern IntPtr NewThread(IntPtr state);
        /// <summary>
        /// Lua Coroutineを再開
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_resume")]
        internal static extern int Resume(IntPtr state, IntPtr fromState, int argumentCount, out int resultCount);
        /// <summary>
        /// Lua state間でスタック値を移動
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_xmove")]
        internal static extern void Move(IntPtr fromState, IntPtr toState, int valueCount);
        /// <summary>
        /// Lua tableを生成
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_createtable")]
        internal static extern void CreateTable(IntPtr state, int arrayCount, int recordCount);
        /// <summary>
        /// Lua tableへ文字列キーで値を設定
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_setfield")]
        internal static extern void SetField(IntPtr state, int index, byte[] name);
        /// <summary>
        /// Luaグローバル値を設定
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_setglobal")]
        internal static extern void SetGlobal(IntPtr state, byte[] name);
        /// <summary>
        /// nilをLuaスタックへ積む
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_pushnil")]
        internal static extern void PushNil(IntPtr state);
        /// <summary>
        /// Luaスタック上の値を複製
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_pushvalue")]
        internal static extern void PushValue(IntPtr state, int index);
        /// <summary>
        /// Lua userdataを生成
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_newuserdatauv")]
        internal static extern IntPtr NewUserData(IntPtr state, UIntPtr size, int userValueCount);
        /// <summary>
        /// 名前付きmetatableを生成または取得
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "luaL_newmetatable")]
        internal static extern int NewMetatable(IntPtr state, byte[] name);
        /// <summary>
        /// 指定したmetatableを持つuserdataを取得
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "luaL_testudata")]
        internal static extern IntPtr TestUserData(IntPtr state, int index, byte[] name);
        /// <summary>
        /// Lua値へmetatableを設定
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_setmetatable")]
        internal static extern int SetMetatable(IntPtr state, int objectIndex);
        /// <summary>
        /// Lua userdataのメモリを取得
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_touserdata")]
        internal static extern IntPtr ToUserData(IntPtr state, int index);
        /// <summary>
        /// C#コールバックを呼び出すLua関数をスタックへ積む
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "unity_lua_push_managed_callback")]
        internal static extern void PushManagedCallback(IntPtr state, ManagedCallback callback, IntPtr context);
        /// <summary>
        /// Luaスタック上の値型を取得
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_type")]
        internal static extern LuaValueType GetType(IntPtr state, int index);
        /// <summary>
        /// bool値をLuaスタックへ積む
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_pushboolean")]
        internal static extern void PushBoolean(IntPtr state, int value);
        /// <summary>
        /// 整数値をLuaスタックへ積む
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_pushinteger")]
        internal static extern void PushInteger(IntPtr state, long value);
        /// <summary>
        /// 浮動小数点値をLuaスタックへ積む
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_pushnumber")]
        internal static extern void PushNumber(IntPtr state, double value);
        /// <summary>
        /// 文字列をLuaスタックへ積む
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_pushlstring")]
        internal static extern IntPtr PushString(IntPtr state, byte[] value, UIntPtr length);
        /// <summary>
        /// Lua値をbool値として取得
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_toboolean")]
        internal static extern int ToBoolean(IntPtr state, int index);
        /// <summary>
        /// Lua値を整数値として取得
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_tointegerx")]
        internal static extern long ToInteger(IntPtr state, int index, out int isNumber);
        /// <summary>
        /// Lua値を浮動小数点値として取得
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_tonumberx")]
        internal static extern double ToNumber(IntPtr state, int index, out int isNumber);
        /// <summary>
        /// Lua値を文字列として取得
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_tolstring")]
        internal static extern IntPtr ToStringPointer(IntPtr state, int index, out UIntPtr length);
        /// <summary>
        /// Luaスタック上の値数を取得
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_gettop")]
        internal static extern int GetTop(IntPtr state);
        /// <summary>
        /// Luaスタック上の値数を変更
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_settop")]
        internal static extern void SetTop(IntPtr state, int index);
        /// <summary>
        /// Lua stateを破棄
        /// </summary>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_close")]
        internal static extern void Close(IntPtr state);
    }
}

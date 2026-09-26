#include <stdint.h>

#define LUA_CORE
#include "lua.h"

#if defined(_WIN32)
#define UNITY_LUA_EXPORT __declspec(dllexport)
#else
#define UNITY_LUA_EXPORT __attribute__((visibility("default")))
#endif

typedef int (*unity_lua_managed_callback)(lua_State *state, intptr_t context);

enum {
    UNITY_LUA_CALLBACK_ERROR = -1,
    UNITY_LUA_CALLBACK_YIELD = -2
};

static int unity_lua_invoke_managed(lua_State *state) {
    unity_lua_managed_callback callback = (unity_lua_managed_callback)lua_touserdata(state, lua_upvalueindex(1));
    intptr_t context = (intptr_t)lua_touserdata(state, lua_upvalueindex(2));
    return callback(state, context);
}

static int unity_lua_continue(lua_State *state, int status, lua_KContext context) {
    int result_count = unity_lua_invoke_managed(state);
    (void)status;
    (void)context;
    if (result_count == UNITY_LUA_CALLBACK_ERROR) {
        return lua_error(state);
    }
    if (result_count == UNITY_LUA_CALLBACK_YIELD) {
        return lua_yieldk(state, 0, 0, unity_lua_continue);
    }
    return result_count;
}

static int unity_lua_dispatch(lua_State *state) {
    int result_count = unity_lua_invoke_managed(state);
    if (result_count == UNITY_LUA_CALLBACK_ERROR) {
        return lua_error(state);
    }
    if (result_count == UNITY_LUA_CALLBACK_YIELD) {
        return lua_yieldk(state, 0, 0, unity_lua_continue);
    }
    return result_count;
}

UNITY_LUA_EXPORT void unity_lua_push_managed_callback(
    lua_State *state,
    unity_lua_managed_callback callback,
    intptr_t context) {
    lua_pushlightuserdata(state, (void *)callback);
    lua_pushlightuserdata(state, (void *)context);
    lua_pushcclosure(state, unity_lua_dispatch, 2);
}

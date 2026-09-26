# Native Lua

This directory builds the official Lua 5.4.9 sources as Windows x64 and Android arm64 native plugins for Unity.

## Requirements

- Visual Studio or Visual Studio Build Tools with Desktop development with C++
- CMake 3.20 or later
- Unity Android Build Support with the Android SDK and NDK, for Android builds

## Build

Run the following commands from the repository root in a Developer PowerShell for Visual Studio:

```powershell
cmake -S Native/Lua -B Native/Lua/build/windows-x64 -A x64
cmake --build Native/Lua/build/windows-x64 --config Release
```

The normal build writes `unitylua54.dll` to a staging directory so Unity can keep using the installed DLL without locking the linker output:

```text
Native/Lua/build/windows-x64/plugin/unitylua54.dll
```

Close the Unity Editor before installing the rebuilt DLL into the package, then run:

```powershell
cmake --install Native/Lua/build/windows-x64 --config Release
```

The install step copies the DLL to:

```text
Packages/com.daitokuamy.unityluasystem/Runtime/Plugins/x86_64/unitylua54.dll
```

Unity native plugins remain loaded until the Editor process exits. Domain Reload and asset reimport do not release the Windows file lock.

The library includes the Lua core and standard libraries. The standalone interpreter and compiler entry points in `lua.c` and `luac.c` are excluded.

## Android arm64 build

Use the CMake and Android NDK installed with the project's Unity Editor. The following variables are placeholders for their absolute paths:

```powershell
$cmake = 'path/to/Unity/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe'
$ninja = 'path/to/Unity/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/ninja.exe'
$toolchain = 'path/to/Unity/Editor/Data/PlaybackEngines/AndroidPlayer/NDK/build/cmake/android.toolchain.cmake'
$pluginDirectory = 'path/to/repository/Packages/com.daitokuamy.unityluasystem/Runtime/Plugins/Android/arm64-v8a'

& $cmake -S Native/Lua -B Native/Lua/build/android-arm64 -G Ninja `
  "-DCMAKE_MAKE_PROGRAM=$ninja" `
  "-DCMAKE_TOOLCHAIN_FILE=$toolchain" `
  -DANDROID_ABI=arm64-v8a `
  -DANDROID_PLATFORM=android-25 `
  -DCMAKE_BUILD_TYPE=Release `
  "-DUNITY_PLUGIN_DIRECTORY=$pluginDirectory"
& $cmake --build Native/Lua/build/android-arm64
& $cmake --install Native/Lua/build/android-arm64
```

The install step copies the shared library to:

```text
Packages/com.daitokuamy.unityluasystem/Runtime/Plugins/Android/arm64-v8a/libunitylua54.so
```

## Apple builds

Run the `Build Apple native plugins` workflow manually from the branch that should receive the generated plugins. It builds:

- `unitylua54.bundle` as a macOS Universal plugin containing arm64 and x86_64
- `libunitylua54.a` as an iOS device plugin containing arm64

The workflow always uploads both files as a workflow artifact. By default, it also creates a pull request that commits the generated files. Merge and validate that pull request before creating a package release tag so that the tag points to a commit that already contains the Apple plugins.

The workflow requires the repository setting that allows GitHub Actions to create and approve pull requests. If that setting is disabled, run the workflow with `create_pull_request` cleared, download the artifact, and commit the two plugins manually.

## Source

- Version: Lua 5.4.9
- Official download: https://www.lua.org/ftp/lua-5.4.9.tar.gz
- License: https://www.lua.org/license.html

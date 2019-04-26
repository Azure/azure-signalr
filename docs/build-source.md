Build Azure SignalR Service SDK from Source
==============================

Building Azure SignalR Service SDK from source allows you tweak and customize the SDK, and to contribute your improvements back to the project.

## Install pre-requistes

Building Azure SignalR Service SDK requires:

* Visual Studio **2019 Preview**. <https://visualstudio.com>
    * To install the exact required components, run [eng/scripts/InstallVisualStudio.ps1](/eng/scripts/InstallVisualStudio.ps1).
        ```ps1
        PS> ./eng/scripts/InstallVisualStudio.ps1
        ```
* Git. <https://git-scm.org>
* AspNetCore 3.0 Preview Runtime (Version >= 3.0.0-preview3-27503-5) <https://dotnet.microsoft.com/download/dotnet-core/3.0>
* AspNetCore 3.0 SDK (Version >= 3.0.100-preview4-011136). Install from AspNetCore code repo before release <https://github.com/dotnet/core-sdk#installers-and-binaries>.

## Clone the source code

For a new copy of the project, run:
```
git clone https://github.com/Azure/azure-signalr
```

## Building on command-line

You can build the entire project on command line with the `build.cmd`/`.sh` scripts.

On Windows:
```
.\build.cmd
```

On macOS/Linux:
```
./build.sh
```

## Building in Visual Studio

Before opening our .sln files in Visual Studio or VS Code, it is suggested to run `.\build.cmd` to make sure all the dependencies are restored correctly.

The solution file is **AzureSignalR.sln** in the root.

## Build properties

Additional properties can be added as an argument in the form `/property:$name=$value`, or `/p:$name=$value` for short. For example:
```
.\build.cmd /p:Configuration=Release
```

Common properties include:

Property                 | Description
-------------------------|-------------------------------------------------------------------------------------------------------------
BuildNumber              | (string). A specific build number, typically from a CI counter, which is appended to the preview1 label.
Configuration            | `Debug` or `Release`. Default = `Debug`.
IsTargetMultiFramework   | `true` or `false`. Default = `true`. Configure whether to build projects targeting ASP.NET Core 3.0.

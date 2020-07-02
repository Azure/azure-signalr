Build Azure SignalR Service SDK from Source
==============================

Building Azure SignalR Service SDK from source allows you tweak and customize the SDK, and to contribute your improvements back to the project.

## Install pre-requistes

Building Azure SignalR Service SDK requires:

* Latest Visual Studio. <https://visualstudio.com>
* Git. <https://git-scm.org>

## Clone the source code

For a new copy of the project, run:
```
git clone --recursive https://github.com/Azure/azure-signalr
```
or if you have already cloned the repository :
```
git clone https://github.com/Azure/azure-signalr
git submodule update --init --recursive
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

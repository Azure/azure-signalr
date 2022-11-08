Build Azure SignalR Service SDK from Source
==============================

Building Azure SignalR Service SDK from source allows you tweak and customize the SDK, and to contribute your improvements back to the project.

## Install pre-requistes

Building Azure SignalR Service SDK requires:

* Latest Visual Studio (include pre-release). <https://visualstudio.com>
* Git. <https://git-scm.org>
* .NET SDK (Version >= 7.0.0-preview.7). <https://dotnet.microsoft.com/download/dotnet>	

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

You can build the entire project on command line with the [`dotnet` command](https://docs.microsoft.com/dotnet/core/tools/dotnet). Run command below in the repo's root folder.

```
dotnet build
```

## Building in Visual Studio

Before opening our .sln files in Visual Studio or VS Code, it is suggested to run `dotnet restore` to make sure all the dependencies are restored correctly.

The solution file is **AzureSignalR.sln** in the root.
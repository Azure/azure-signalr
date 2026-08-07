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

## Building the emulator container

The container installs an exact, already released version of the `Microsoft.Azure.SignalR.Emulator` NuGet dotnet tool. To build the image with the current released version:

```
docker build --build-arg EMULATOR_VERSION=1.6.1 -f src/Microsoft.Azure.SignalR.Emulator/Dockerfile -t signalr-emulator:dev src/Microsoft.Azure.SignalR.Emulator
docker run --rm -p 8888:8888 signalr-emulator:dev
```

### Releasing the emulator container

After the emulator package is publicly available on NuGet.org, queue the existing `.azure/pipelines/release.yml` pipeline with `releaseEmulatorContainer=true` and its exact stable `emulatorPackageVersion` (for example, `1.6.1`). This runs only the emulator-container stage; the normal SDK/NuGet package and partner-release jobs are skipped.

The container release does not poll or resolve the latest package. It immediately downloads the requested exact package, refuses to overwrite an existing version tag, builds with ACR Tasks, publishes `<version>` and `latest`, and locks the version tag. MAR must map `signalr/signalr-emulator` to `mcr.microsoft.com/signalr/signalr-emulator`.

Configure `EmulatorContainerRegistry` and `EmulatorContainerRegistryServiceConnection` in Azure Pipelines. Add an exclusive lock check to the service connection and grant it permission to query and lock tags and run ACR Tasks. The Dockerfile installs the released NuGet tool; it does not build emulator source.

## Public API changes

If you make a public API change `eng\Export-API.ps1` script has to be run to update public API listings.

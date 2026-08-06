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

The NuGet partner-drop job does not make a package immediately available on NuGet.org, so container publication is deliberately separate from `releaseEmulator`. Every Monday, the official pipeline in `.azure/pipelines/release.yml` reads the latest stable `Microsoft.Azure.SignalR.Emulator` version from NuGet.org, verifies that its package is downloadable, and builds the Linux/amd64 image with that exact version. To publish immediately after a package reaches NuGet.org, queue the pipeline with `releaseEmulatorContainer` set to `true` and optionally set `emulatorPackageVersion` to the exact released version. The package-release stage is skipped during container-only runs.

The container stage pushes both the package version tag and `latest` to the `signalr/signalr-emulator` repository in the team's ACR. The existing Microsoft Artifact Registry (MAR) onboarding must syndicate that repository to `mcr.microsoft.com/signalr/signalr-emulator`; Aspire consumes its `latest` tag on port 8888.

The release pipeline requires the `EmulatorContainerRegistry` variable and an `EmulatorContainerRegistryServiceConnection` Azure Resource Manager service connection. Its identity needs `Contributor` on the registry, or a custom role that can read the registry and call `scheduleRun` and `listBuildSourceUploadUrl`; `AcrPush` alone cannot queue an ACR build. The registry must also allow ACR Tasks build compute through its network configuration. Configure these protected values in Azure Pipelines rather than in the repository.

## Public API changes

If you make a public API change `eng\Export-API.ps1` script has to be run to update public API listings.

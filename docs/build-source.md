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

Build the emulator image from the repository root so the Docker build uses the same source and version metadata as the .NET tool:

```
docker build -f src/Microsoft.Azure.SignalR.Emulator/Dockerfile -t signalr-emulator:dev .
docker run --rm -p 8888:8888 signalr-emulator:dev
```

### Releasing the emulator container

The official pipeline in `.azure/pipelines/release.yml` publishes the emulator container whenever a final emulator release is queued with both `isFinalBuild` and `releaseEmulator` set to `true`. The pipeline reads the emulator version from `version.props`, builds the Linux/amd64 image from that release's source, and pushes both the version tag and `latest` to the `signalr/signalr-emulator` repository in the team's ACR. The existing Microsoft Artifact Registry (MAR) onboarding must syndicate that ACR repository to `mcr.microsoft.com/signalr/signalr-emulator`; Aspire consumes its `latest` tag on port 8888.

The release pipeline requires the `EmulatorContainerRegistry` variable and an `EmulatorContainerRegistryServiceConnection` Azure Resource Manager service connection. Its identity needs `Contributor` on the registry, or a custom role that can read the registry and call `scheduleRun` and `listBuildSourceUploadUrl`; `AcrPush` alone cannot queue an ACR build. The registry must also allow ACR Tasks build compute through its network configuration. Configure these protected values in Azure Pipelines rather than in the repository.

## Public API changes

If you make a public API change `eng\Export-API.ps1` script has to be run to update public API listings.

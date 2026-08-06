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

When a final emulator release is queued with both `isFinalBuild` and `releaseEmulator` set to `true`, the official pipeline in `.azure/pipelines/release.yml` packs the emulator once. The container job downloads that exact pipeline artifact, reads its version from the embedded nuspec, and installs the nupkg from a local feed. The same artifact is uploaded through the existing NuGet partner-release path, so container publication does not depend on NuGet.org indexing or propagation.

The container job runs only after the partner-drop job succeeds, then pushes both the package version tag and `latest` to the `signalr/signalr-emulator` repository in the team's ACR. The existing Microsoft Artifact Registry (MAR) onboarding must syndicate that repository to `mcr.microsoft.com/signalr/signalr-emulator`; Aspire consumes its `latest` tag on port 8888.

The current pipeline does not produce a prebuilt OCI image artifact, so the container job creates a new image digest from the exact release nupkg; it never compiles emulator source in Docker. If a future release pipeline produces an immutable image artifact before publication, publish that artifact by retagging and pushing it instead of rebuilding it.

For a previously released version whose pipeline artifact is no longer retained, queue the same pipeline with `emulatorContainerPackageVersion` set to the exact version. This backfill mode skips package production, waits up to 30 minutes for only that version to become downloadable from NuGet.org, and never resolves an unpinned or `latest` package. Use `1.6.1` for the initial image correction.

The release pipeline requires the `EmulatorContainerRegistry` variable and an `EmulatorContainerRegistryServiceConnection` Azure Resource Manager service connection. Its identity needs `Contributor` on the registry, or a custom role that can read the registry and call `scheduleRun` and `listBuildSourceUploadUrl`; `AcrPush` alone cannot queue an ACR build. The registry must also allow ACR Tasks build compute through its network configuration. Configure these protected values in Azure Pipelines rather than in the repository.

## Public API changes

If you make a public API change `eng\Export-API.ps1` script has to be run to update public API listings.

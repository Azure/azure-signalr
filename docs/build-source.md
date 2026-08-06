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

The repository previously contained the Dockerfile but no image build or publication automation. Repository history does not establish whether the 2024 MCR image was published manually or by an external pipeline. The pipeline described here is the first explicit, repository-owned image publication path.

Image publication is a separate, explicit post-release action. After `Microsoft.Azure.SignalR.Emulator` is publicly downloadable from NuGet.org, queue `.azure/pipelines/release-emulator-container.yml` with `emulatorPackageVersion` set to that exact stable version. Use `1.6.1` for the initial image correction.

The pipeline has no trigger, pull request validation, schedule, or latest-version lookup. It validates and downloads only the requested package, refuses to overwrite an existing versioned image tag, then uses ACR Tasks to build the Dockerfile with that exact version. A successful run publishes both `<version>` and `latest` tags to the `signalr/signalr-emulator` repository in the configured ACR, then locks the versioned tag against updates and deletion while leaving `latest` mutable.

A Microsoft Artifact Registry (MAR) mapping must be configured or confirmed to syndicate the ACR repository to `mcr.microsoft.com/signalr/signalr-emulator`; Aspire consumes its `latest` tag on port 8888. The Dockerfile installs the exact released NuGet tool and never compiles or packs emulator source.

The image pipeline requires the `EmulatorContainerRegistry` variable and an `EmulatorContainerRegistryServiceConnection` Azure Resource Manager service connection. Configure an exclusive lock check on that protected service connection; the pipeline's sequential lock behavior prevents concurrent publication runs from racing on the same version tag. Its identity needs permission to list and update repository tag attributes and queue ACR Tasks builds, including `scheduleRun` and `listBuildSourceUploadUrl`; `AcrPush` alone cannot queue a build. The registry must also allow ACR Tasks build compute through its network configuration. No registry name, service connection identifier, or MAR configuration is checked in; configure or confirm those protected values in Azure Pipelines and the publishing environment.

## Public API changes

If you make a public API change `eng\Export-API.ps1` script has to be run to update public API listings.

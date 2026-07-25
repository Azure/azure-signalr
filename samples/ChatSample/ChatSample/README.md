# Azure SignalR Service Chat Sample

This sample demonstrates how to use Azure SignalR Service with ASP.NET Core SignalR.

## Prerequisites

- .NET 8.0 SDK or later
- An Azure SignalR Service instance
- Git (for submodule dependencies)
- Docker (optional, for containerized deployment)

## Running the Sample

### Option 1: Run with .NET Aspire (Recommended)

[.NET Aspire](https://learn.microsoft.com/dotnet/aspire/get-started/aspire-overview#orchestration) is used to orchestrate the samples.

To work with .NET Aspire, you need the following installed locally:
- [.NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
- .NET Aspire workload:
  - Installed with the [Visual Studio installer](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling?tabs=visual-studio#install-net-aspire) or [the .NET CLI workload](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling?tabs=dotnet-cli#install-net-aspire).
- An OCI compliant container runtime, such as:
  - [Docker Desktop](https://www.docker.com/products/docker-desktop) or [Podman](https://podman.io/).
 
In Visual Studio, set **samples/Samples.AppHost** project as the Startup Project. Right click **Connected Services** and select **Azure Resource Provisioning Settings** and select your Azure subscription, region and resource group to use.

Alternatively, you could add Azure related configurations in the appsettings.json file:
  ```json
  {
    "Azure": {
      "SubscriptionId": "your subscription",
      "Location": "your location"
    }
  }
  ```

Run the project and use Aspire dashboard to navigate to different samples.

### Option 2: Run without Aspire

Aspire helps you to automatically provision a new Azure SignalR resource and set the connection strings for the sample to use automatically. You could still use the traditional way to set the connection strings by yourself and run the sample directly. Samples now use named connection string `AddNamedAzureSignalR("signalr1")`. Set your connection string to `Azure:SignalR:signalr1:ConnectionString`, or `ConnectionStrings:signalr1`:

```bash
dotnet user-secrets set Azure:SignalR:signalr1:ConnectionString "<Your connection string>"
```

Or:

```bash
dotnet user-secrets set ConnectionStrings:signalr1 "<Your connection string>"
```

Then build and run:

```bash
dotnet build
dotnet run
```

You can also specify a custom port:

```bash
dotnet run --urls="http://localhost:5050"
```

### Option 3: Running with Docker

1. Initialize the required submodules:

```bash
git submodule update --init --recursive
```

2. Build the Docker image:
```bash
docker build -t chat-app -f samples/ChatSample/ChatSample/Dockerfile .
```

3. Run the container:
```bash
docker run -d -p 5050:5050 -e "ConnectionStrings__signalr1=<your-connection-string>" chat-app
```

Additional Docker commands:
```bash
# View running containers
docker ps

# View container logs
docker logs <container_id>

# Stop the container
docker stop <container_id>
```

## Accessing the Application

To access the chat application, open your web browser and navigate to:
- `http://localhost:5050` (or the custom port you specified)

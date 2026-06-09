# Azure SignalR Service Chat Sample

This sample demonstrates how to use Azure SignalR Service with ASP.NET Core SignalR.

## Prerequisites

- .NET 8.0 SDK or later
- An Azure SignalR Service instance
- Git (for submodule dependencies)
- Docker (optional, for containerized deployment)

## Setup

1. Initialize the required submodules:

```bash
git submodule update --init --recursive
```

2. Configure your Azure SignalR Service connection string:

   - Update `appsettings.json` by replacing the empty connection string:

    ```json
    {
      "Azure": {
        "SignalR": {
          "ConnectionString": "<your-connection-string>"
        }
      }
    }
    ```

   ⚠️ **Important**: Make sure to set your connection string before building the Docker image or running the application.

## Running the Sample

### Option 1: Running Locally

1. Build and run the project:

```bash
dotnet build
dotnet run
```

You can also specify a custom port:

```bash
dotnet run --urls="http://localhost:5050"
```

### Option 2: Running with Docker

1. Build the Docker image:
```bash
docker build -t chat-app -f samples/ChatSample/ChatSample/Dockerfile .
```

2. Run the container:
```bash
docker run -d -p 5050:5050 chat-app
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

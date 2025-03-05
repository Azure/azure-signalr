# Azure SignalR Service Chat Sample

This sample demonstrates how to use Azure SignalR Service with ASP.NET Core SignalR.

## Prerequisites

- .NET 8.0 SDK or later
- An Azure SignalR Service instance
- Git (for submodule dependencies)

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

### Running Locally

We use [dotnet dev-certs](https://learn.microsoft.com/dotnet/core/tools/dotnet-dev-certs) to generate a self-signed cert to enable HTTPS use in development.

Build and run the project:

```bash
dotnet dev-certs https --trust
dotnet build
dotnet run
```

You can also specify a custom port:

```bash
dotnet run --urls="https://localhost:5050"
```

## Accessing the Application

To access the chat application, open your web browser and navigate to:
- `https://localhost:7113` (or the custom port you specified)


#### Using broadcast method

1. Browse to the site with your favorite browser and it will connect with the SignalR Javascript client.
2. It creates 2 clients by default.
3. Enter some message in the text box above 'Broadcast'.
4. Press 'Broadcast' to send message to all connected clients.

#### Using client results

The sample also shows the usage of [SignalR client results](https://learn.microsoft.com/aspnet/core/signalr/hubs#client-results) which is the new feature supported since NET7.0.

1. Browse to the site with your favorite browser and it will connect with the SignalR Javascript client.
2. It creates 2 clients by default. Grab an ID from the connected connections and paste it in the ID text box.
3. Press 'Invoke' to invoke a Hub method which will ask the specified ID for a result.
4. The client invoked will unlock 'Reply' button and you can type something in the text box above.
5. Press 'Reply' to return the message to the server which will return it to the original client that asked for a result.

#### Using client results from anywhere with `IHubContext`

1. Browse to the site with your favorite browser and it will connect with the SignalR Javascript client.
2. Copy the ID for a connected connection.
3. Navigate to `/get/<ID>` in a new tab. Replace `<ID>` with the copied connection ID.
5. Go to the browser tab for the chosen ID and write a message in the Message text box.
6. Press 'Send Message' to return the message to the server which will return it to the `/get/<ID>` request.

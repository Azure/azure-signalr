# Use Azure SignalR Service

- [Use Azure SignalR Service](#use-azure-signalr-service)
  - [Provision an Azure SignalR Service instance](#provision-an-azure-signalr-service-instance)
  - [For ASP&#46;NET Core SignalR](#for-aspnet-core-signalr)
  - [For ASP&#46;NET SignalR](#for-aspnet-signalr)
  - [Scale Out Application Server](#scale-out-application-server)

## Provision an Azure SignalR Service instance

Go to [Azure Portal](https://portal.azure.com) to provision a SignalR service instance.

## For ASP&#46;NET Core SignalR

[See document](run-asp-net-core.md)

## For ASP&#46;NET SignalR

[See document](run-asp-net.md)

## Scale Out Application Server

With Azure SignalR Service, persistent connections are offloaded from application server.
It only has to take care of business logics in your hub classes.
But you still need to scale out application servers for better performance when handling massive client connections.
Below are a few tips for scaling out application servers.

- Multiple application servers can connect to the same Azure SignalR Service instance.
- As long as name of the hub class is the same, connections from different application servers are grouped in the same hub.
- Each client connection will only be created in **one** of the application servers, and messages from that client will only be sent to the same application server.
- If you want to access client information globally (from all application servers), you have to use some storage to save client information from all application servers.
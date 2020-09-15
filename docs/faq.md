# Frequently Asked Questions

- [Is Azure SignalR Service ready for production use?](#production-use)
- [Client connection is closed with error message "No server available". What does that mean?](#no-server-available)
- [When there are multiple application servers, are messages from clients sent to all of them or just one of them?](#client-server-mapping)
- [If one of my application servers is down, will SignalR Service know about it? Will clients be notified too?](#server-down)
- [Why is exception thrown from my custom `IUserIdProvider` when switching from ASP.NET Core SignalR to Azure SignalR Service SDK?](#limited-context)
- [Can I configure available transports at server side in Azure SignalR Service like ASP.NET Core SignalR?](#configure-transports)
- [What is the meaning of metrics like message count or connection count showed in Azure portal? Which kind of aggregation type should I choose?](#metrics-meaning)
- [What is the meaning of service mode `Default`/`Serverless`/`Classic`? How can I choose?](#service-mode)
- [Any feature differences when using Azure SignalR for ASP.NET SignalR?](#diff-aspnet-signalr)

<a name="production-use"></a>
## Is Azure SignalR Service ready for production use?

Yes, both the support for [ASP.NET Core SignalR](https://github.com/aspnet/SignalR) and [ASP.NET SignalR](https://github.com/SignalR/SignalR) are all generally available.

<a name="no-server-available"></a>
## Client connection is closed with error message "No server available". What does that mean?

This error occurs only when clients are sending messages to SignalR Service.

If you don't have any application server and only use SignalR Service's REST APIs, this is **by design**.
In serverless architecture, client connections are in **LISTEN** mode and should not send any messages to SignalR Service.
Read more about [REST API](./rest-api.md) in SignalR Service.

If you have application servers, this means there is no application server connected with your SignalR Service instance.
Possible root causes are:
- None of your application servers are connected to SignalR Service.
Check application server logs to see whether there are connection errors.
This should be very rare when you have multiple application servers for high availability.
- There are some connectivity issues inside SignalR Service instances.
It should be transient and recovered automatically within a couple of minutes.
If it lasts for over an hour, please [open an issue in GitHub](https://github.com/Azure/azure-signalr/issues/new) or [create an Azure support request](https://docs.microsoft.com/en-us/azure/azure-supportability/how-to-create-azure-support-request).
Then we can work with you to troubleshoot.

<a name="client-server-mapping"></a>
## When there are multiple application servers, are messages from clients sent to all of them or just one of them?

It is one-to-one mapped between clients and application servers ([server connections](./internal.md#server-connections) accurately).
So messages from one client will only be sent to the same one application server.

The mapping between a client and an application server is valid in SignalR Service until the client or the application server disconnects.

<a name="server-down"></a>
## If one of my application servers is down, will SignalR Service know about it? Will clients be notified too?

Yes.

SignalR Service monitors heartbeats from application servers.
If there is no heartbeat for some time, the application server will be regarded as offline.
All client connections which are served by this application server will be disconnected too.

<a name="limited-context"></a>
## Why is exception thrown from my custom `IUserIdProvider` when switching from ASP.NET Core SignalR to Azure SignalR Service SDK?

Parameter `HubConnectionContext context` is different in ASP.NET Core SignalR and Azure SignalR Service SDK when `IUserIdProvider` is called.

In ASP.NET Core SignalR, `HubConnectionContext context` is the context from the physical client connection and have valid values for all properties.

In Azure SignalR Service SDK, `HubConnectionContext context` is a logical client connection context.
The physical client connection is connected to SignalR Service instance.
So only limited properties are provided.
For now, only `HubConnectionContext.GetHttpContext()` and `HubConnectionContext.User` is available for access.
You can find the source code at [here](https://github.com/Azure/azure-signalr/blob/kevinzha/faq/src/Microsoft.Azure.SignalR/ServiceHubConnectionContext.cs).

<a name="configure-transports"></a>
## Can I configure available transports at server side in Azure SignalR Service like ASP.NET Core SignalR? Say WebSocket transport is disabled.

No.

Azure SignalR Service provides all 3 transports by default and you can't configure it.
As a matter of fact, you don't have to worry about transports because clients are all connected to Azure SignalR Service and service will handle connection management.

But you can still configure allowed transports at client side as documented [here](https://docs.microsoft.com/en-us/aspnet/core/signalr/configuration?view=aspnetcore-2.1#configure-allowed-transports).

<a name="metrics-meaning"></a>
## What is the meaning of metrics like message count or connection count showed in Azure portal? Which kind of aggregation type should I choose?

You can find the details about how do we calculate these metrics [here](https://docs.microsoft.com/en-us/azure/azure-signalr/signalr-messages).

In the overview blade of Azure SignalR Service resources, we have already chosen the appropriate aggregation type for you. And if you go to the Metrics blade, you can
take the aggregation type [here](https://docs.microsoft.com/en-us/azure/azure-monitor/platform/metrics-supported#microsoftsignalrservicesignalr) as a reference.

<a name="service-mode"></a>
## What is the meaning of service mode `Default`/`Serverless`/`Classic`? How can I choose?

Modes:
* `Default` mode **requires** hub server. When in this mode, Azure SignalR routes the client traffic to its connected hub server connections. Azure SignalR checks if there is any hub server connected, if not, Azure SignalR rejects the incoming client connections. One thing to mention is that **Management Api** can also be used in this mode to manage the connected clients directly through Azure SignalR.
* `Serverless` mode does **NOT** allow any server connection, i.e. it will reject all server connections, all clients must in serverless mode.	* `Serverless` mode does **NOT** allow any server connection. Clients connect to Azure SignalR, and users usually use serverless technologies such as **Azure Function** to handle hub logics. [Here](https://docs.microsoft.com/azure/azure-signalr/signalr-quickstart-azure-functions-javascript?WT.mc_id=signalrquickstart-github-antchu) is a simple example using Azure SignalR's Serverless mode.
* `Classic` mode is a mixed status. When a hub has server connection, the new client will be routed to hub server, if not, client will enter serverless mode.

  This may cause some problem, for example, all of server connections are lost for a moment, some clients will enter serverless mode, instead of route to hub server.

Choosing:
1. No hub server, choose `Serverless`.
1. All of hubs have hub servers, choose `Default`.
1. Some of hubs have hub servers, others not, choose `Classic`, but this may cause some problem, the better way is create two instances, one is `Serverless`, another is `Default`.

<a name="diff-aspnet-signalr"></a>
## Any feature differences when using Azure SignalR for ASP.NET SignalR?

When using Azure SignalR, some APIs and features of ASP.NET SignalR are no longer supported:

- The ability to pass arbitrary state between clients and the hub (often called `HubState`) is not supported when using Azure SignalR.
- `PersistentConnection` class is not yet supported when using Azure SignalR.
- **Forever Frame transport** is not supported  when using Azure SignalR.
- Azure SignalR no longer replays messages sent to client when client is offline.
- When using Azure SignalR, the traffic for one client connection is always routed (aka. **sticky**) to one app server instance for the duration of the connection.
- Clients using long polling or server sent events cannot send a message large than 1MB.

The support for ASP.NET SignalR is focused on compatibility, so not all new features from ASP.NET Core SignalR are supported. For example, **MessagePack**, **Streaming**, etc., are only available for ASP.NET Core SignalR applications.

SignalR Service can be configured for different service mode: `Classic`/`Default`/`Serverless`.
The current implementation does not support `serverless` mode and REST API for ASP.NET Signalr.

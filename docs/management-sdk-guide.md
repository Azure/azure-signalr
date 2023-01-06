# Azure SignalR Service Management SDK

This SDK is for ASP.NET **Core** SignalR. For differences between ASP.NET SignalR and ASP.NET Core SignalR, see [here](https://learn.microsoft.com/aspnet/core/signalr/version-differences?view=aspnetcore-6.0).

## Build Status

[![Windows](https://img.shields.io/github/workflow/status/Azure/azure-signalr/Gated-Windows/dev?label=Windows)](https://github.com/Azure/azure-signalr/actions?query=workflow%3AGated-Windowns)
[![Ubuntu](https://img.shields.io/github/workflow/status/Azure/azure-signalr/Gated-Ubuntu/dev?label=Ubuntu)](https://github.com/Azure/azure-signalr/actions?query=workflow%3AGated-Ubuntu)
[![OSX](https://img.shields.io/github/workflow/status/Azure/azure-signalr/Gated-OSX/dev?label=OSX)](https://github.com/Azure/azure-signalr/actions?query=workflow%3AGated-OSX)

## Nuget Packages

|Package Name | Target Framework | NuGet | MyGet|
|---|---|---|---|
| Microsoft.Azure.SignalR.Management | .NET Standard 2.0 <br/> .NET Core App 3.0 <br/> .NET 5.0 | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Azure.SignalR.Management.svg)](https://www.nuget.org/packages/Microsoft.Azure.SignalR.Management) | [![MyGet](https://img.shields.io/myget/azure-signalr-dev/vpre/Microsoft.Azure.SignalR.Management.svg)](https://www.myget.org/feed/azure-signalr-dev/package/nuget/Microsoft.Azure.SignalR.Management) |


## Getting Started

Azure SignalR Service Management SDK helps you to manage SignalR clients through Azure SignalR Service directly such as broadcast messages. Therefore, this SDK can be but not limited to be used in [serverless](https://azure.microsoft.com/solutions/serverless/) environments. You can use this SDK to manage SignalR clients connected to your Azure SignalR Service in any environment, such as in a console app, in an Azure function or in a web server.

**To see guides for SDK version 1.9.x and before, go to [Azure SignalR Service Management SDK (Legacy)](./management-sdk-guide-legacy.md). You might also want to read [Migration guidance](./management-sdk-migration.md).**

<!--Add migration guide-->

### Features

|                                            | Transient          | Persistent         |
| ------------------------------------------ | ------------------ | ------------------ |
| Broadcast                                  | :heavy_check_mark: | :heavy_check_mark: |
| Broadcast except some clients              | :heavy_check_mark: | :heavy_check_mark: |
| Send to a client                           | :heavy_check_mark: | :heavy_check_mark: |
| Send to clients                            | :heavy_check_mark: | :heavy_check_mark: |
| Send to a user                             | :heavy_check_mark: | :heavy_check_mark: |
| Send to users                              | :heavy_check_mark: | :heavy_check_mark: |
| Send to a group                            | :heavy_check_mark: | :heavy_check_mark: |
| Send to groups                             | :heavy_check_mark: | :heavy_check_mark: |
| Send to a group except some clients        | :heavy_check_mark: | :heavy_check_mark: |
| Add a user to a group                      | :heavy_check_mark: | :heavy_check_mark: |
| Remove a user from a group                 | :heavy_check_mark: | :heavy_check_mark: |
| Check if a user in a group                 | :heavy_check_mark: | :heavy_check_mark: |
|                                            |                    |                    |
| Multiple SignalR service instances support | :x:                | :heavy_check_mark: |
| [MessaegPack clients support](#message-pack-serialization)                |  limited support   |  since v1.20.0     |

**Features only come with new API**
|                              | Transient          | Persistent  |
| ---------------------------- | ------------------ | ----------- |
| Check if a connection exists | :heavy_check_mark: | Since v1.11 |
| Check if a group exists      | :heavy_check_mark: | Since v1.11 |
| Check if a user exists       | :heavy_check_mark: | Since v1.11 |
| Close a client connection    | :heavy_check_mark: | Since v1.11 |


> More details about different modes can be found [here](#Transport-Type).

> [For a full sample on management SDK, please go here](https://github.com/aspnet/AzureSignalR-samples/tree/master/samples/Management).
## Create Service Manager

Build your instance of `ServiceManager` from a `ServiceManagerBuilder`

``` C#

var serviceManager = new ServiceManagerBuilder()
                    .WithOptions(option =>
                    {
                        option.ConnectionString = "<Your Azure SignalR Service Connection String>";
                    })
                    .WithLoggerFactory(loggerFactory)
                    .BuildServiceManager();

```

You can use `ServiceManager` to check the Azure SignalR endpoint health and create service hub context. The following [section](#create-service-hub-context) provides details about creating service hub context.

To check the Azure SignalR endpoint health, you can use `ServiceManager.IsServiceHealthy` method. Note that if you have multiple Azure SignalR endpoints, only the first endpoint will be checked.

```cs
var health = await serviceManager.IsServiceHealthy(cancellationToken);
```
<!--Todo: Add multiple endpoint guide-->
## Create Service Hub Context

Create your instance of `ServiceHubContext` from a `ServiceManager`:

``` C#
var serviceHubContext = await serviceManager.CreateHubContextAsync("<Your Hub Name>",cancellationToken);
```

## Negotiation

> In server mode, an endpoint `/<Your Hub Name>/negotiate` is exposed for negotiation by Azure SignalR Service SDK. SignalR clients will reach this endpoint and then redirect to Azure SignalR Service later.
>
> Unlike server scenario, there is no web server accepts SignalR clients in serverless scenario. To protect your connection string, you need to redirect SignalR clients from the negotiation endpoint to Azure SignalR Service instead of giving your connection string to all the SignalR clients.
>
> The best practice is to host a negotiation endpoint and then you can use SignalR clients to connect your hub: `/<Your Hub Name>`.
>
> Read more details about the redirection at SignalR's [Negotiation Protocol](https://github.com/aspnet/SignalR/blob/master/specs/TransportProtocols.md#post-endpoint-basenegotiate-request).

Both of endpoint and access token are useful when you want to redirect SignalR clients to your Azure SignalR Service.

You can use the instance of `ServiceHubContext` to generate the endpoint url and corresponding access token for SignalR clients to connect to your Azure SignalR Service.

```C#
var negotiationResponse = await serviceHubContext.NegotiateAsync(new (){UserId = "<Your User Id>"});
```

Suppose your hub endpoint is `http://<Your Host Name>/<Your Hub Name>`, then your negotiation endpoint will be `http://<Your Host Name>/<Your Hub Name>/negotiate`. Once you host the negotiation endpoint, you can use the SignalR clients to connect to your hub like this:

``` c#
var connection = new HubConnectionBuilder().WithUrl("http://<Your Host Name>/<Your Hub Name>").Build();
await connection.StartAsync();
```

<!-- Please note that by default we have a mechanism to check if your SignalR Service is healthy. If none of your SignalR Service is healthy during negotiation, then an `AzureSignalRNotConnectedException` is thrown. -->
<!--TODO: After sharding document is ready, add link to sharding doc.-->

The sample on how to use Management SDK to redirect SignalR clients to Azure SignalR Service can be found [here](https://github.com/aspnet/AzureSignalR-samples/tree/master/samples/Management).

## Send Messages and Manage Groups

The `ServiceHubContext` we build from `ServiceHubContextBuilder` is a class that implements and extends `IServiceHubContext`. You can use it to send messages to your clients as well as managing your groups.

```C#
try
{
    // Broadcast
    await hubContext.Clients.All.SendAsync(callbackName, obj1, obj2, ...);

    // Send to user
    await hubContext.Clients.User(userId).SendAsync(callbackName, obj1, obj2, ...);

    // Send to group
    await hubContext.Clients.Group(groupId).SendAsync(callbackName, obj1, obj2, ...);

    // add user to group
    await hubContext.UserGroups.AddToGroupAsync(userId, groupName);

    // remove user from group
    await hubContext.UserGroups.RemoveFromGroupAsync(userId, groupName);
}
finally
{
    await hubContext.DisposeAsync();
}
```

## Strongly typed hub

A strongly typed hub is a programming model that you can extract your client methods into an interface, so that avoid errors like misspelling the method name or passing the wrong parameter types.

Let's say we have a client method called `ReceivedMessage` with two string parameters. Without strongly typed hubs, you broadcast to clients through `hubContext.Clients.All.SendAsync("ReceivedMessage", user, message)`. With strongly typed hubs, you first define an interface like this:
```cs
public interface IChatClient
{
    Task ReceiveMessage(string user, string message);
}
```

And then you create a strongly typed hub context which implements `IHubContext<Hub<T>, T>`, `T` is your client method interface:
```cs
ServiceHubContext<IChatClient> serviceHubContext = await serviceManager.CreateHubContextAsync<IChatClient>(hubName, cancellationToken);
```

Finally, you could directly invoke the method:
```cs
await Clients.All.ReceiveMessage(user, message);
```

Except for the difference of sending messages, you could negotiate or manage groups with `ServiceHubContext<T>` just like `ServiceHubContext`.

[To read more on strongly typed hubs in the ASP.NET Core docs, go here.](https://docs.microsoft.com/aspnet/core/signalr/hubs?#strongly-typed-hubs)

<!--Add sample link here-->

## Transport Type

This SDK can communicates to Azure SignalR Service with two transport types:
* Transient: Create a Http request Azure SignalR Service for each message sent. The SDK simply wrap up [Azure SignalR Service REST API](./rest-api.md) in Transient mode. It is useful when you are unable to establish a WebSockets connection.
* Persistent: Create a WebSockets connection first and then send all messages in this connection. It is useful when you send large amount of messages.

### Summary of Serialization behaviors of the Arguments in Messages

|                                | Transient          | Persistent                        |
| ------------------------------ | ------------------ | --------------------------------- |
| Default JSON library           | `Newtonsoft.Json`  | The same as Asp.Net Core SignalR: <br>`Newtonsoft.Json` for .NET Standard 2.0; <br>`System.Text.Json` for .NET Core App 3.1 and above  |
| MessaegPack clients support    |  limited support   |  since v1.20.0                    |

#### Json serialization
See [Customizing Json Serialization in Management SDK](./advanced-topics/json-object-serializer.md)

#### Message Pack serialization

##### Persistent mode

1. You need to install `Microsoft.AspNetCore.SignalR.Protocols.MessagePack` package.
1. To add a MessagePack protocol side-by-side with the default JSON protocol:
    ```csharp
    var serviceManagerBuilder = new ServiceManagerBuilder()
        .AddHubProtocol(new MessagePackHubProtocol());
    ```
1. To fully control the hub protocols, you can use
    ```csharp
        var serviceManagerBuilder = new ServiceManagerBuilder()
            .WithHubProtocols(new MessagePackHubProtocol(), new JsonHubProtocol());
    ```
    `WithHubProtocols` first clears the existing protocols, and then adds the new protocols. You can also use this method to remove the JSON protocol and use MessagePack only.
##### Transient mode (limited support)
The service side will first deserialize the JSON to object, then serialize the object to MessagePack. During the conversions, the type information might be lost.

A better implementation to allow users to send MessagePack payload directly in transient mode is to be done. See #1745 for tracking.


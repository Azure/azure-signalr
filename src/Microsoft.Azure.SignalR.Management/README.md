# Azure SignalR Service Management SDK 

## Build Status

[![Travis build status](https://img.shields.io/travis/Azure/azure-signalr.svg?label=travis-ci&branch=dev&style=flat-square)](https://travis-ci.org/Azure/azure-signalr/branches)
[![AppVeyor build status](https://img.shields.io/appveyor/ci/vicancy/azure-signalr/dev.svg?label=appveyor&style=flat-square)](https://ci.appveyor.com/project/vicancy/azure-signalr)

## Nuget Packages

Package Name | Target Framework | NuGet | MyGet
---|---|---|---
Microsoft.Azure.SignalR.Management | .NET Standard 2.0 | null | [![MyGet](https://img.shields.io/myget/azure-signalr-dev/vpre/Microsoft.Azure.SignalR.Management.svg)](https://www.myget.org/feed/azure-signalr-dev/package/nuget/Microsoft.Azure.SignalR.Management)

## Getting Started

Azure SignalR Service Management SDK helps you to manages SignalR clients through Azure SignalR Service directly. Therefore, this SDK can be but not limited to be used in [serverless](https://azure.microsoft.com/zh-cn/solutions/serverless/) environments. You can use this SDK to manage SignalR clients connected to your Azure SignalR Service in any environment, such as in a console app, in an Azure function or in an App Server.

### Features

#### Transient Mode:

Features are limited. The supported features are:

* Publishing messages:
  * Broadcast messages to all SignalR clients
  * Publish messages to a specific user(s)
  * Publish messages to a specific group(s)
* Group membership:
  * Add a specific user to a specific group
  * Remove a specific user from a specific group

#### Persistent Mode:

Persistent mode includes the aboves fetures and are extended to all features that .Net Core SignalR supports.

> More details about different modes can be found [here](#Transport-Type).

## Quick Start

### Create Service Manager

Build your instance of `IServiceManager` from a `ServiceManagerBuilder`

``` C#

var serviceManager = new ServiceManagerBuilder()
                    .WithOptions(option => 
                    {
                        option.ConnectionString = "<Your Azure SignalR Service Connection String>";
                    })
                    .Build();
    

```

You can generate the endpoint for SignalR clients connecting to your Azure SignalR Service.
You can also generate the access token for SignalR clients connecting to your Azure SignalR Service in serverless mode.

Both of endpoint and access token are useful when you want to redirect SignalR clients to your Azure SignalR Service. The sample on how to use Management SDK to redirect SignalR clients to Azure SignalR Service can be found [here](TODO).

> Usually you don't want to expose your connection string to SignalR clients. In this situation, the SignalR clients connect to a spacific endpoint which will return a special negotiation response and redirect clients to your Azure SignalR Service from the connection string. Read more details about the redirection at SignalR's [Negotiation Protocol](https://github.com/aspnet/SignalR/blob/master/specs/TransportProtocols.md#post-endpoint-basenegotiate-request).

``` C#
var clientEndpoint = serviceManager.GetClientEndpoint("<Your Hub Name>");
var accessToken = serviceManager.GenerateClientAccessToken("<Your Hub Name>", "<Your User ID>");
```


### Create and Use ServiceHubContext

You can create a instance of `IServiceHubContext` to publish messages or manage group membership. The sample on how to use Management SDK to publish messages to SignalR clients can be found [here](TODO).

``` C#
try
{
    var hubcontext = await serviceManager.CreateHubContextAsync(hubName);

    // Broadcast
    hubContext.Clients.All.SendAsync(callbackName, obj1, obj2, ...);

    // Send to user
    hubContext.Clients.User(userId).SendAsync(callbackName, obj1, obj2, ...);

    // Send to group
    hubContext.Clients.Group(groupId).SendAsync(callbackName, obj1, obj2, ...);

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

For full sample on how to use Management SDK can be found [here](TODO).

## Transport Type

This SDK can communicates to Azure SignalR Service with two transport types:
* Transient: Create a Http request Azure SignalR Service for each message sent. It is useful when you are unable to establish a WebSockets connection.
* Persistent: Create a WebSockets connection first and then sent all messages in this connection. It is useful when you send large amount of messages.

## Interface

### `ServiceManagerBuilder`

This class contains some utilities for managing SignalR services. For now, ServiceHubContext is the only utility provided. Other utilities will be added in the future.


#### `ServiceManagerBuilder WithOptions(Action<ServiceManagerOptions> configure)`

Set options for service manager.

> Parameters
>  * `configure`: A callback to configure the `IServiceManager`

#### `ServiceManager Build()`

Build service manager.

### `IServiceManager`

Interface for managing Azure SignalR service.

#### `ServiceHubContext CreateHubContextAsync(string hubName, ILoggerFactory loggerFactory = null, CancellationToken cancellationToken = default)`

Create service hub context to publish messages.
If in Persistent mode, IServiceManager also starts a WebSockets connection to Azure SignalR Service for each `IServiceHubContext`. All messages will be sent in this connection.

> Parameters
> * `hubName`: The hub name
> * `loggerFactory`: The logger factory
> * `cancellationToken`: Cancellation token for creating service hub context

#### `string GenerateClientAccessToken(string hubName, string userId = null, IList<Claim> claims = null, TimeSpan? lifeTime = null)`


Generate access token for client to connect to service directly.

> Parameters
> * `hubName`: The hub name
> * `userId`: The user ID
> * `claims`: The claim list to be put into access token
> * `lifeTime`: The lifetime of the token. The default value is one hour.

#### `string GetClientEndpoint(string hubName)`

Generate client endpoint for SignalR clients to connect to service directly.

> Parameters
> * `hubName`: The hub name

### `IServiceHubContext`

This interface manages SignalR clients connected to a specific hub in your Azure SignalR service and the interfaces follow the interface of `IHubContext` with extented interfaces. For example, it can broadcast messages to all connections, send messages to a specific user, send messages to a specific group, add or remove a specific user from a specific group.    


#### Properties

The properties are almost the same as [.Net Core SignalR](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.signalr.ihubcontext-1?view=aspnetcore-2.2).

In this SDK, `IUserGroupManager UserGroups` is newly added. It manages groups membership for users instead of connections. But the interface stays the same as `IGroupManager Groups`.

* `IHubClients Clients`
  * `ClientProxy Client (string connectionId)`
    Get proxy for client with connection ID
  * `IClientProxy Group (string groupName)`
    Get proxy for group with group name
  * `IClientProxy User (string userId)`
    Get proxy for user with user ID
  * `IClientProxy All`
    Get proxy for all clients
* `IGroupManager Groups`
  Get manager for groups, manage groups with conenction ID
  * `Task AddToGroupAsync (string connectionId, string groupName, CancellationToken cancellationToken = null)`
    Add client with connection ID to some group with group Name 
  * `Task RemoveFromGroupAsync (string connectionId, string groupName, CancellationToken cancellationToken = null)`
    Remove client with connection ID to some group with group Name
* `IUserGroupManager UserGroups`
  Get manager for groups, manage groups with user ID
  * `Task AddToGroupAsync (string userId, string groupName, CancellationToken cancellationToken = null)`
    Add user with user ID to some group with group Name 
  * `Task RemoveFromGroupAsync (string userId, string groupName, CancellationToken cancellationToken = null)`
    Remove user with user ID to some group with group Name 

#### Method
* `Task DisposeAsync()`
Stop connection if in the Persistent mode. And then dispose all unmanaged resources. 




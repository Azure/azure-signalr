# Azure SignalR Service Management SDK 

> **NOTE**
>
> Azure SignalR Service only supports this SDK for ASP.NET CORE SignalR clients.
<!-- TOC -->

- [Azure SignalR Service Management SDK](#azure-signalr-service-management-sdk)
  - [Build Status](#build-status)
  - [Nuget Packages](#nuget-packages)
  - [Getting Started](#getting-started)
    - [Features](#features)
  - [Create Service Hub Context](#create-service-hub-context)
  - [Negotiation](#negotiation)
  - [Send Messages and Manage Groups](#send-messages-and-manage-groups)
  - [Transport Type](#transport-type)

<!-- /TOC -->
## Build Status

[![Windows](https://img.shields.io/github/workflow/status/Azure/azure-signalr/Gated-Windows/dev?label=Windows)](https://github.com/Azure/azure-signalr/actions?query=workflow%3AGated-Windowns)
[![Ubuntu](https://img.shields.io/github/workflow/status/Azure/azure-signalr/Gated-Ubuntu/dev?label=Ubuntu)](https://github.com/Azure/azure-signalr/actions?query=workflow%3AGated-Ubuntu)
[![OSX](https://img.shields.io/github/workflow/status/Azure/azure-signalr/Gated-OSX/dev?label=OSX)](https://github.com/Azure/azure-signalr/actions?query=workflow%3AGated-OSX)

## Nuget Packages

Package Name | Target Framework | NuGet | MyGet
---|---|---|---
Microsoft.Azure.SignalR.Management | .NET Standard 2.0 <br/> .NET Core App 3.0 <br/> .NET 5.0 | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Azure.SignalR.Management.svg)](https://www.nuget.org/packages/Microsoft.Azure.SignalR.Management) | [![MyGet](https://img.shields.io/myget/azure-signalr-dev/vpre/Microsoft.Azure.SignalR.Management.svg)](https://www.myget.org/feed/azure-signalr-dev/package/nuget/Microsoft.Azure.SignalR.Management)


## Getting Started

Azure SignalR Service Management SDK helps you to manage SignalR clients through Azure SignalR Service directly such as broadcast messages. Therefore, this SDK can be but not limited to be used in [serverless](https://azure.microsoft.com/zh-cn/solutions/serverless/) environments. You can use this SDK to manage SignalR clients connected to your Azure SignalR Service in any environment, such as in a console app, in an Azure function or in a web server.

**To see guides for SDK version 1.9.x and before, go to [Azure SignalR Service Management SDK (Legacy)](./management-sdk-guide-legacy.md)**

<!--Add migration guide-->

### Features

|                                     | Transient          | Persistent         |
|-------------------------------------|--------------------|--------------------|
| Broadcast                           | :heavy_check_mark: | :heavy_check_mark: |
| Broadcast except some clients       | :heavy_check_mark: | :heavy_check_mark: |
| Send to a client                    | :heavy_check_mark: | :heavy_check_mark: |
| Send to clients                     | :heavy_check_mark: | :heavy_check_mark: |
| Send to a user                      | :heavy_check_mark: | :heavy_check_mark: |
| Send to users                       | :heavy_check_mark: | :heavy_check_mark: |
| Send to a group                     | :heavy_check_mark: | :heavy_check_mark: |
| Send to groups                      | :heavy_check_mark: | :heavy_check_mark: |
| Send to a group except some clients | :heavy_check_mark: | :heavy_check_mark: |
| Add a user to a group               | :heavy_check_mark: | :heavy_check_mark: |
| Remove a user from a group          | :heavy_check_mark: | :heavy_check_mark: |
| Check if a user in a group          | :heavy_check_mark: | :heavy_check_mark: |
| Check if a connection exists        | :heavy_check_mark: | `N/A`              |
| Check if a group exists             | :heavy_check_mark: | `N/A`              |
| Check if a user exists              | :heavy_check_mark: | `N/A`              |
| Close a client connection           | :heavy_check_mark: | :heavy_check_mark: |


> More details about different modes can be found [here](#Transport-Type).

## Create Service Hub Context

Build your instance of `ServiceHubContext` from a `ServiceHubContextBuilder`:

``` C#
var serviceHubContext = await new ServiceHubContextBuilder()
        .WithOptions(options => options.ConnectionString = "<Your Azure SignalR Service Connection String>")
        .CreateAsync("<Your hub name>", default);
```

Instead of setting the options directly, you can specify a `IConfiguration` instance via method `ServiceHubContextBuilder.WithConfiguration(IConfiguration configuration)` and the configuration section `Azure:SignalR` will be bound to [ServiceManagerOptions](https://github.com/Azure/azure-signalr/blob/dev/src/Microsoft.Azure.SignalR.Management/Configuration/ServiceManagerOptions.cs). This is useful when you want to change your SignalR Service instances dynamically. Here is a code sample and a configuration JSON file sample:

```C#
var serviceHubContext = await new ServiceHubContextBuilder()
        .WithConfiguration(configuration)
        .CreateAsync("<Your hub name>", default);
```
<!--Todo: Add sample link-->

```json
{
    "Azure": {
        "SignalR": {
            "ConnectionString": "<YourConnectionString>",
            "ApplicationName": "ManagementApp",
            "ServiceTransportType": "Transient"
        }
    }
}
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

The sample on how to use Management SDK to redirect SignalR clients to Azure SignalR Service can be found [here](https://github.com/aspnet/AzureSignalR-samples/tree/master/samples/Management).

## Send Messages and Manage Groups

The `ServiceHubContext` we build from `ServiceHubContextBuilder` is a class that implements and extends `IServiceHubContext`. You can use it to send messages to your clients as well as managing your groups.

```C#
try
{
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

<!--Add sample link here-->

## Transport Type

This SDK can communicates to Azure SignalR Service with two transport types:
* Transient: Create a Http request Azure SignalR Service for each message sent. The SDK simply wrap up [Azure SignalR Service REST API](./rest-api.md) in Transient mode. It is useful when you are unable to establish a WebSockets connection.
* Persistent: Create a WebSockets connection first and then sent all messages in this connection. It is useful when you send large amount of messages.


<!--Add API reference link here-->
<!--Intend to generate API reference by docfx-->
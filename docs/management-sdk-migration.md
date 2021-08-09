# `IServiceManager` to `ServiceManager` Migration Guidance

This guidance is intended to assist in the migration to `ServiceManager` from `IServiceManager`. It will focus on side-by-side comparisons for similar operations between the legacy and the new APIs. The new APIs will be released in version 10.0.0 .

We assume that you are familiar with the usage of `ServiceManagerBuilder` and the related APIs, otherwise, please refer the [Azure SignalR Service Management SDK ](management-sdk-guide.md) other than this guidance.
- [`IServiceManager` to `ServiceManager` Migration Guidance](#iservicemanager-to-servicemanager-migration-guidance)
  - [Migration Benefits](#migration-benefits)
  - [Change Overview](#change-overview)
  - [API comparisons](#api-comparisons)
    - [Build service manager](#build-service-manager)
    - [Create service hub context](#create-service-hub-context)
    - [Negotiation](#negotiation)
    - [Send Messages and Manage Groups](#send-messages-and-manage-groups)
    - [Health check](#health-check)



## Migration Benefits
* The new APIs provide more functionalities to manage your clients and groups, such as closing a connection by connection id, checking if a connection exists, if a user exists, if a group exists. 
* The new APIs provide more options for negotiation, such as whether the client is a diagnostic client.
* The new APIs are more friendly for negotiation with multiple SignalR Service instances. `IServiceManager.GetClientEndpoint` and `IServiceManager.GenerateClientAccessToken` are combined into one method to make sure the client endpoint and the access token come from the same SignalR Service endpoint. An `HttpContext` instance is passed into the endpoint router to provide more information for the routing. 
<!--Todo Add link about sharding doc-->

## Change Overview
Generally we use abstract classes to replace interfaces for better extensibility under different frameworks (.Net Standard 2.0, .Net Core 3.1, ...). `IServiceManager` is replaced by abstract class `ServiceManager`; `IServiceHubContext` is replaced by `ServiceHubContext`. What's more, negotiation is moved from `IServiceManager` to `ServiceHubContext`.
## API comparisons
### Build service manager

**Legacy APIs**
```cs
var serviceManager = new ServiceManagerBuilder()
    .WithOptions(o => o.ConnectionString = "<Your ConnectionString>")
    .Build(); //build method is changed
```

**New APIs**
```cs
var serviceManager = new ServiceManagerBuilder()
    .WithOptions(o => o.ConnectionString = "<Your ConnectionString>")
    .WithLoggerFactory(loggerFactory) // optional, specify a custom logger factory instance for the whole management SDK
    .BuildServiceManager(); // note this
```

<!--Add sharding link-->
### Create service hub context

**Legacy APIs**
```cs
var serviceHubContext = await serviceManager.CreateServiceHubContextAsync("<Your Hub Name>", loggerFactory, cancellationToken);
```

**New APIs**
```cs
var serviceHubContext = await serviceManager.CreateServiceHubContextAsync("<Your Hub Name>", cancellationToken);
```

If you have custom logger factory, you should specify it when you create the service manager with new APIs. [See sample in the previous section](#build-service-manager)
### Negotiation

**Legacy APIs**
```cs
var clientEndpoint = serviceManager.GetClientEndpoint("<Your Hub Name>");
var accessToken = serviceManager.GenerateClientAccessToken("<Your Hub Name>", "<Your User ID>");
```

**New APIs**
```cs
var negotiationResponse = await serviceHubContext.NegotiateAsync(new NegotiationOptions(){UserId = "<Your User Id>"});
var clientEndpoint = negotiationResponse.Url;
var accessToken = negotiationResponse.AccessToken;
```
The negotiation API is async now because there is an async operation behind if you use AAD connection string. In the old negotiation API, you just wait for the result synchronously.

Except for the API change, there is also an implementation change. In [Persistent mode](management-sdk-guide.md#transport-type), you will call negotiation API after trying to establish WebSocket connections to Azure SignalR Service, so that you know whether the SignalR Service is healthy/online through the connection status. Therefore, the negotiation API only returns an healthy SignalR endpoint, if there is no healthy SignalR endpoint, an `AzureSignalRNotConnectedException` is thrown. In [Transient mode](management-sdk-guide.md#transport-type), we also have a plan to implement an endpoint health checker.

### Send Messages and Manage Groups
**Legacy APIs**
```cs
try
{
    var hubcontext = await serviceManager.CreateHubContextAsync("<Your Hub Name>");

    // Broadcast
    await hubContext.Clients.All.SendAsync(callbackName, obj1, obj2, ...);
    
    //...
    
    // add user to group
    await hubContext.UserGroups.AddToGroupAsync(userId, groupName);
}
finally
{
    await hubContext.DisposeAsync();
}
```

**New APIs**
```cs
try
{
    // Broadcast
    await hubContext.Clients.All.SendAsync(callbackName, obj1, obj2, ...);

    // add user to group
    await hubContext.UserGroups.AddToGroupAsync(userId, groupName);
}
finally
{
    await hubContext.DisposeAsync();
}
```

With legacy APIs, you need to create a hub context from `IServiceManager`, but with new APIs, you have built a hub context from `ServiceHubContextBuilder` at the beginning. The usages of hub context are nearly the same, except that in the legacy APIs you get an interface `IServiceHubContext` while in the new APIs you get an abstract class `ServiceHubContext`.

### Health check
**Legacy APIs**
```cs
var isHealthy = await serviceManager.IsServiceHealthy(cancellationToken);
```

**New APIs**

N/A

Currently in new APIs, you can't check service health directly. But the health check is integrated into negotiation API partially (implemented in [Persistent mode](management-sdk-guide.md#transport-type) but not yet for [Transient mode](management-sdk-guide.md#transport-type). If you need to check the service health yourself, you could call our [health check REST API](https://docs.microsoft.com/en-us/azure/azure-signalr/signalr-quickstart-rest-api#service-health) yourself.
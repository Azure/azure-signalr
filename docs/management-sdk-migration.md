# `ServiceManagerBuilder` to `ServiceHubContextBuilder` Migration guidance

This guidance is intended to assist in the migration to `ServiceHubContextBuilder` from `ServiceManagerBuilder`. It will focus on side-by-side comparisons for similar operations between the legacy and the new APIs. The new APIs will be released in version 10.0.0 .

We assume that you are familiar with the usage of `ServiceManagerBuilder` and the related APIs, otherwise, please refer the [Azure SignalR Service Management SDK ](management-sdk-guide.md) other than this guidance.
- [`ServiceManagerBuilder` to `ServiceHubContextBuilder` Migration guidance](#servicemanagerbuilder-to-servicehubcontextbuilder-migration-guidance)
  - [Migration Benefits](#migration-benefits)
  - [Change Overview](#change-overview)
  - [API comparisons](#api-comparisons)
    - [Entry point](#entry-point)
    - [Negotiate](#negotiate)
    - [Send Messages and Manage Groups](#send-messages-and-manage-groups)
    - [Health check](#health-check)



## Migration Benefits
* The new APIs provide more functionalities to manage your clients and groups, such as closing a connection by connection id, checking if a connection exists, if a user exists, if a group exists. 
* The new APIs provide more options for negotiation, such as whether the client is a diagnostic client.
* The new APIs are more friendly for negotiation with multiple SignalR Service instances. `IServiceManager.GetClientEndpoint` and `IServiceManager.GenerateClientAccessToken` are combined into one method to make sure the client endpoint and the access token come from the same SignalR Service endpoint. An `HttpContext` instance is allowed to passed into the endpoint router to provide more information for the routing. <!--Todo Add link about sharding doc-->

## Change Overview
The following image shows the relation for the legacy APIs (green blocks) and the new APIs (blue blocks). Currently, to get a `ServiceHubContext` which implements `IServiceHubContext` interface, you just need to create it directly from `ServiceHubContextBuilder`. The `ServiceHubContext` abstract class combines the functionalities of `IServiceManager` and `IServiceHubContext`.

![image](./images/migration-class-diagram.png)

## API comparisons

### Entry point

**Legacy APIs**
```cs
var serviceManager = new ServiceManagerBuilder()
    .WithOptions(o => o.ConnectionString = "<Your ConnectionString>")
    .Build();
```

**New APIs**
```cs
var serviceHubContext = await new ServiceHubContextBuilder()
    .WithOptions(o => o.ConnectionString = "<Your ConnectionString>")
    .CreateAsync("<Your Hub Name>", cancellationToken);
```
<!--Add sharding link-->

Note that you need to specify a hub name at the beginning in the new APIs.

### Negotiate

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
    hubContext.Clients.All.SendAsync(callbackName, obj1, obj2, ...);
    
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
    hubContext.Clients.All.SendAsync(callbackName, obj1, obj2, ...);

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
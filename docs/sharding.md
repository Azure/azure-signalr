# Multiple SignalR service endpoint support
In latest SDK, we add support for configuring multiple SignalR service instances. This feature can be used to increase the scale of concurrent connections, and can also be used in cross-region scenarios.

* [For ASP.NET Core SignalR](#aspnetcore)
    * [How to add multiple endpoints from config](#aspnetcore-config)
    * [How to add multiple endpoints from code](#aspnetcore-code)
    * [How to customize endpoint router](#customize-router)

* [For ASP.NET SignalR](#aspnet)
    * [How to add multiple endpoints from config](#aspnet-config)
    * [How to add multiple endpoints from code](#aspnet-code)
    * [How to customize endpoint router](#aspnet-customize-router)
* [Configuration in cross-geo scenarios](#cross-geo)
* [Failover](#failover)

## For ASP.NET Core
<a name="aspnetcore"></a>

### How to add multiple endpoints from config
<a name="aspnetcore-config"></a>

Config with key `Azure:SignalR:ConnectionString` or starting with `Azure:SignalR:ConnectionString:` is considered as an SignalR Service connection string.

If the key starts with `Azure:SignalR:ConnectionString:`, it is in format `Azure:SignalR:ConnectionString:{Name}:{EndpointType}`, `Name` and `EndpointType` are properties of the `ServiceEndpoint` object, and is accessible from code.

You can add multiple instance connection strings using the following `dotnet` commands:

```batch
dotnet user-secrets set Azure:SignalR:ConnectionString:1 <ConnectionString1>
dotnet user-secrets set Azure:SignalR:ConnectionString:2 <ConnectionString2>
dotnet user-secrets set Azure:SignalR:ConnectionString:3 <ConnectionString3>
```

### How to add multiple endpoints from code
<a name="aspnetcore-code"></a>

A `ServicEndpoint` class is introduced in to describe the properties of an Azure SignalR Service endpoint.
You can configure multiple instance endpoints when using Azure SignalR Service SDK through:
```cs
services.AddSignalR()
        .AddAzureSignalR(options => {
            options.Endpoints = new ServiceEndpoint[]
            {
                // Note: this is just a demonstration of how to set options.Endpoints
                // Having ConnectionStrings explicitly set inside the code is not encouraged
                // You can fetch it from a safe place such as Azure KeyVault
                new ServiceEndpoint("<ConnectionString1>"),
                new ServiceEndpoint("<ConnectionString2>"),
                new ServiceEndpoint("<ConnectionString3>"),
            }
        });
```

### How to customize endpoint router
<a name="customize-router"></a>

By default, the SDK uses the [DefaultEndpointRouter](https://github.com/Azure/azure-signalr/blob/dev/src/Microsoft.Azure.SignalR.Common/Endpoints/DefaultEndpointRouter.cs) to pick up endpoints.

#### Default behavior 
1. Client request routing

    When client `/negotiate` with the app server. By default, SDK uses **round robin** algorithm to pick up the redirect Azure SignalR endpoint from the set of available service endpoints.

2. Server message routing

    When it is *sending message to some **connection***, and the target connection is routed to current server, the message goes back directly to that connected endpoint;
for other cases, the messages are broadcasted to every Azure SignalR endpoint.

#### Customize route algorithm
You can create your own router when you have special knowledge to identify which endpoints the messages should go to. 

Below defines a custom router when groups starting with `east-` always go to the endpoint named `east`:

```cs
private sealed class CustomRouter : IEndpointRouter
{
    private readonly IEndpointRouter _inner = new DefaultEndpointRouter();

    public IEnumerable<ServiceEndpoint> GetEndpointsForGroup(string groupName, IEnumerable<ServiceEndpoint> availableEndpoints)
    {
        if (groupName.StartsWith("east-"))
        {
            return availableEndpoints.Where(e => e.Name == "east");
        }
        
        return _inner.GetEndpointsForGroup(groupName, availableEndpoints);
    }
    ...
}
```

## For ASP.NET
<a name="aspnet"></a>

### How to add multiple endpoints from config
<a name="aspnet-config"></a>

Config with key `Azure:SignalR:ConnectionString` or starting with `Azure:SignalR:ConnectionString:` is considered as an SignalR Service connection string.

If the key starts with `Azure:SignalR:ConnectionString:`, it is in format `Azure:SignalR:ConnectionString:{Name}:{EndpointType}`, `Name` and `EndpointType` are properties of the `ServiceEndpoint` object, and is accessible from code.

You can add multiple instance connection strings to `web.config`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <connectionStrings>
    <add name="Azure:SignalR:ConnectionString" connectionString="<ConnectionString1>"/>
    <add name="Azure:SignalR:ConnectionString:1" connectionString="<ConnectionString2>"/>
    <add name="Azure:SignalR:ConnectionString:2" connectionString="<ConnectionString3>"/>
    <add name="Azure:SignalR:ConnectionString:3" connectionString="<ConnectionString4>"/>
  </connectionStrings>
  ...
</configuration>
```

### How to add multiple endpoints from code
<a name="aspnet-code"></a>

A `ServicEndpoint` class is introduced in to describe the properties of an Azure SignalR Service endpoint.
You can configure multiple instance endpoints when using Azure SignalR Service SDK through:

```cs
app.MapAzureSignalR(
    this.GetType().FullName, 
    options => {
            options.Endpoints = new ServiceEndpoint[]
            {
                // Note: this is just a demonstration of how to set options.Endpoints
                // Having ConnectionStrings explicitly set inside the code is not encouraged
                // You can fetch it from a safe place such as Azure KeyVault
                new ServiceEndpoint("<ConnectionString1>"),
                new ServiceEndpoint("<ConnectionString2>"),
                new ServiceEndpoint("<ConnectionString3>"),
            }
        });
```

### How to customize router
<a name="aspnet-customize-router"></a>

Please refer to [How to customize router](#customize-router) for detailed information about creating a custom router.

And don't forget to register the router to DI container using:

```cs
var hub = new HubConfiguration();
var router = new CustomRouter();
hub.Resolver.Register(typeof(IEndpointRouter), () => router);
app.MapAzureSignalR(GetType().FullName, hub,  => {
    options.Endpoints = new ServiceEndpoint[]
                {
                    new ServiceEndpoint(name: "east", connectionString: "<connectionString1>"),
                    new ServiceEndpoint(name: "west", connectionString: "<connectionString2>"),
                    new ServiceEndpoint("<connectionString3>")
                };
});
```

## Configuration in cross-geo scenarios
<a name="cross-geo"></a>

A `ServiceEndpoint` object has an `EndpointType` property with value `primary` or `secondary`, which can be helpful in cross-geo cases.

In general, `primary` endpoints are considered to have more reliable network connections and are taking client traffics in; `secondary` endpoints are considered to have less reliable network connections and are not taking client traffic in, but only taking server to client side traffic, for example, broadcast messages.

In cross-geo cases, cross-geo network can be comparatively unstable. For one app server located in *east us*, the endpoint located in east us can be marked as `primary` and endpoints in other regions marked as `secondary`. In this way, service endpoints in other regions can **receive** messages from this *east us* app server, but there will be no **cross-geo** clients routed to this app server on the cross-geo unstable server connections, the infrastructure is as below:

![Cross-Geo Infra](./images/cross_geo_infra.png)

## Failover
<a name="failover"></a>

When all `primary` endpoints are offline, client `/negotiate` returns one online `secondary` endpoint if any exists. In this way, the client is routed to an available `secondary` endpoint. This failover  mechanism relies on the settings that all the endpoints should be `primary` endpoints to some app server.

![Failover](./images/failover.png)

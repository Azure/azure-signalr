# Multiple SignalR service endpoint support in Management SDK
As a preview feature, we add support for multiple SignalR service instances in Management SDK for **persisent mode only** based on Server SDK, so most content in [Multiple SignalR service endpoint support](sharding.md) is applicable to management SDK. We'll focus on the difference in the following doc.

## How to add multiple endpoints from config

We assume you have basic knowledge about [Configuration in .NET](https://docs.microsoft.com/en-us/dotnet/core/extensions/configuration).

Each SignalR Service endpoint entry follows this format:
```
Azure:SignalR:Endpoints:{Name}:{EndpointType} = <ConnectionString> 
```
`Name` and `EndpointType` are properties of the `ServiceEndpoint` object. `Name` will be useful if you want to further customize the routing behavior among multiple endpoints. `EndpointType` is optional and has two valid value: (default)`Primary` and `Secondary`.

You can add multiple SignalR Service endpoint entries in your configuration, then add the `IConfiguration` instance to the `ServiceHubContextBuilder`.

```cs
// var builder = new ServiceHubContextBuilder();
builder.WithConfiguration(configuration);
```

Management SDK supports endpoint hot reload as long as your [Configuration providers](https://docs.microsoft.com/en-us/dotnet/core/extensions/configuration-providers) are enabled with `reloadOnChange`. So you don't have to restart your app when you need to add/remove an service endpoint.

[TODO]
Add configuration code example

## How to add multiple endpoints from code
You can configure multiple instance endpoints when using Management SDK through `ServiceHubContextBuilder`:
```cs
// var builder = new ServiceHubContextBuilder();
builder.WithOptions(o =>
{
    o.ServiceEndpoints = new ServiceEndpoint[]
    {
        // Note: this is just a demonstration of how to set options.Endpoints
        // Having ConnectionStrings explicitly set inside the code is not encouraged
        // You can fetch it from a safe place such as Azure KeyVault
        new ServiceEndpoint("<ConnectionString0>"),
        new ServiceEndpoint("<ConnectionString1>", type: EndpointType.Primary, name: "east-region-a"),
        new ServiceEndpoint("<ConnectionString2>", type: EndpointType.Primary, name: "east-region-b"),
        new ServiceEndpoint("<ConnectionString3>", type: EndpointType.Secondary, name: "backup"),
    };
});
```

## How to customize endpoint router
The way to implement a customized router in Management SDK is almost the same as in Server SDK, see [How to customize endpoint router](sharding.md#How-to-customize-endpoint-router), except that you should register the cusomized router through `ServiceHubContextBuilder`:
```cs
// var builder = new ServiceHubContextBuilder();
var builder.WithRouter(new CustomRouter());
```
## How to negotiate
The generation of endpoint and corresponding access token for SignalR clients is combined into one method and moved into `ServiceHubContext` now.

To get the endpoint url and access token, you should use:
```cs
var response = await hubContext.NegotiateAsync(new NegotiationOptions{ HttpContext = httpContext });
var endpoint = response.Url;
var accessToken = response.AccessToken;
```
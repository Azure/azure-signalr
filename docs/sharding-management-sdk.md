# Multiple SignalR service endpoint support in Management SDK
As a preview feature, we add support for multiple SignalR service instances in Management SDK for **persisent mode only** based on Server SDK, so most content in [Multiple SignalR service endpoint support](sharding.md) is applicable to management SDK. We'll focus on the difference in the following doc.

## How to add multiple endpoints from config
See [How to add multiple endpoints from config](sharding.md#How-to-add-multiple-endpoints-from-config)

Then don't forget to add the `IConfiguration` instance to the `ServiceHubContextBuilder`.

```cs
// var builder = new ServiceHubContextBuilder();
builder.WithConfiguration(configuration);
```

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
# Multiple SignalR service endpoint support in Management SDK
As a preview feature, we add support for multiple SignalR service instances in Management SDK for **persisent mode only** based on Server SDK, so most content in [Multiple SignalR service endpoint support](sharding.md) is applicable to management SDK. We'll focus on the difference in the following doc.

## How to add multiple endpoints from config
See [How to add multiple endpoints from config](sharding.md#How-to-add-multiple-endpoints-from-config)

## How to add multiple endpoints from code
You can configure multiple instance endpoints when using Management SDK through `ServiceManagerBuilder`:
```cs
var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
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
}).Build();
```

## How to customize endpoint router
The way to implement a customized router in Management SDK is almost the same as in Server SDK, see [How to customize endpoint router](sharding.md#How-to-customize-endpoint-router), except that you should register the cusomized router through `ServiceManagerBuilder`:
```cs
var builder = new ServiceManagerBuilder().WithRouter(new CustomRouter());
```

## How to negotiate
The generation of endpoint and corresponding access token for SignalR clients is combined into one method and moved into `ServiceHubContext` now.
Here is the method signature.
```cs
/// <summary>
/// Gets client endpoint access information object for SignalR hub connections to connect to Azure SignalR Service
/// </summary>
/// <param name="httpContext">The HTTP context which might provide information for routing and generating access token.</param>
/// <param name="userId">The user ID. If null, the identity name in <see cref="HttpContext.User" /> of <paramref name="httpContext"/> will be used.</param>
/// <param name="claims">The claim list to be put into access token. If null, the claims in <see cref="HttpContext.User"/> of <paramref name="httpContext"/> will be used.</param>
/// <param name="lifetime">The lifetime of the token. The default value is one hour.</param>
/// <param name="isDiagnosticClient">The flag whether the client to be connected is a diagnostic client.</param>
/// <param name="cancellationToken">Cancellation token for aborting the operation. If null, the <see cref="HttpContext.RequestAborted"/> of <paramref name="httpContext"/> will be used. </param>
/// <returns>Client endpoint and access token to Azure SignalR Service.</returns>
Task<NegotiationResponse> NegotiateAsync(HttpContext httpContext = null, string userId = null, IList<Claim> claims = null, TimeSpan? lifetime = null, bool isDiagnosticClient = false, CancellationToken cancellationToken = default);
```
To get the endpoint url and access token, you should use:
```cs
var hubContext = await serviceManager.CreateHubContextAsync(hubName);
var response = await hubContext.NegotiateAsync(userId);
var endpoint = response.Url;
var accessToken = response.AccessToken;
```
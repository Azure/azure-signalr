# Transport configuration

Currently, Azure SignalR Service supports configuring the transport type between clients and service. You can specify the transport type for each client by setting `ServiceOptions.TransportTypeDetector`.
The `TransportTypeDetector` is a function which takes the `HttpContext` during negotiation as parameter and returns a bit mask combining one or more transport type values.

For example, if you want to disable Server-Sent Event (enable only WebSocket and Long Polling) for all clients, you can set a transport type detector when you add Azure SignalR Service as a dependency:

```cs
public void ConfigureServices(IServiceCollection services)
{
    services.AddSignalR().AddAzureSignalR(o =>
    {
        o.TransportTypeDetector = (httpContext) => Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets | Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
    });
}
```

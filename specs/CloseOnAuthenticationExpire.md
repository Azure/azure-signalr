Feature request: #1690

When server sets `HttpConnectionDispatcherOptions.CloseOnAuthenticationExpiration` to true, SignalR service should track the client authentication expriation and close a client when its token expires. See [here](https://learn.microsoft.com/aspnet/core/signalr/configuration?view=aspnetcore-6.0&tabs=dotnet#advanced-http-configuration-options) for related ASP.NET Core SignalR doc.

The feature works since .NET 6 as ASP.NET Core SignalR adds it in ASP.NET Core 6.

* SDK
    * In the negotiation response, when `HttpConnectionDispatcherOptions.CloseOnAuthenticationExpiration` is true, sets the claim "asrs.s.coae" to "true" to indicate the client should be closed on auth expires, sets the claim "asrs.s.aeo" as the unix time of when the authentication expires.
    * TODO: Provides hooks for server to be notified when client is closed on auth expires in the same way as [dotnet/aspnetcore/pull/32431](https://github.com/dotnet/aspnetcore/pull/32431) does.

* Service runtime:
    * Responsible for closing the expired connections so that we can also support the feature in serverless mode.
    * Like [dotnet/aspnetcore/pull/32431](https://github.com/dotnet/aspnetcore/pull/32431) does, runtime will scan all the client connections every seconds and close the expired connections in a way that allow reconnect.

Currently limitation: the feature doesn't work on diagnostic clients, as diagnostic clients always receive a close message which disallows reconnect.



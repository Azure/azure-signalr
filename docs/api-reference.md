# Azure SignalR SDK for .NET - API Reference

## Namespaces

Namespace | Description
---|---
[Microsoft.Extensions.DependencyInjection](#microsoftexteionsdependencyinjection-namespace) | Contains extension methods for Azure SignalR dependency injection in ASP.NET Core 2.x
[Microsoft.AspNetCore.Builder](#microsoftaspnetcorebuilder-namespace) | Contains extension methods to initialize connections with Azure SignalR in ASP.NET Core 2.x
[Microsoft.Azure.SignalR](#microsoftazuresignalr-namespace) | Contains core functionalities to interact with Azure SignalR.
[Owin](#owin-namespace) | Contains extension methods to initialize connections with Azure SignalR in OWIN startup.


### `Microsoft.Extensions.DependencyInjection` Namespace

Class | Description
---|---
[AzureSignalRDependencyInjectionExtensions](#azuresignalrdependencyinjectionextensions-class) | Add Azure SignalR dependencies in ASP.NET Core 2.x


### `Microsoft.AspNetCore.Builder` Namespace

Class | Description
---|---
[AzureSignalRApplicationBuilderExtensions](#azuresignalrapplicationbuilderextensions-class) | Initialize connections with Azure SignalR in ASP.NET Core 2.x


### `Microsoft.Azure.SignalR` Namespace

Class | Description
---|---
[CloudSignalR](#cloudsignalr-class) | Entry class to create Azure SignalR objects from connection string.
[EndpointProvider](#endpointprovider-class) | Provide endpoint for clients and hub hosts.
[TokenProvider](#tokenprovider-class) | Provide access tokens for clients and hub hosts.
[HubProxy](#hubproxy-class) | Proxy class to send message to Azure SignalR instance with REST API calls.
[HubClientsProxy](#hubclientsproxy-class) | Proxy class to send message to Azure SignalR clients with REST API calls.
[GroupManagerProxy](#groupmanagerproxy-class) | Proxy class to manage groups in Azure SignalR with REST API calls.


### `Owin` Namespace

Class | Description
---|---
[AzureSignalRAppBuilderExtensions](#azuresignalrappbuilderextensions-class) | Initialize connections with Azure SignalR in OWIN startup.



## Classes

### `AzureSignalRDependencyInjectionExtensions` Class

**Namespace**: `Microsoft.Extensions.DependencyInjection`<br>
**Assembly**: `Microsoft.Azure.SignalR.dll`

#### `AddAzureSignalR(this IServiceCollection services, Action<HubHostOptions> configure = null)` Method
**Return**: `Microsoft.Extensions.DependencyInjection.IServiceCollection`

---

### `AzureSignalRApplicationBuilderExtensions` Class

**Namespace**: `Microsoft.AspNetCore.Builder`<br>
**Assembly**: `Microsoft.Azure.SignalR.dll`

#### `UseAzureSignalR(this IApplicationBuilder app, string connectionString, Action<HubHostBuilder> configure)` Method
**Return**: `Microsoft.AspNetCore.Builder.IApplicationBuilder`

---

### `AzureSignalRAppBuilderExtensions` Class

**Namespace**: `Owin`<br>
**Assembly**: `Microsoft.Azure.SignalR.Owin.dll`

#### `UseAzureSignalR(this IAppBuilder app, string connectionString, Action<HubHostBuilder> configure)` Method
**Return**: `Owin.IAppBuilder`

---

### `CloudSignalR` Class

**Namespace**: `Microsoft.Azure.SignalR`<br>
**Assembly**: `Microsoft.Azure.SignalR.dll`

Method | Description
---|---
`CreateEndpointProviderFromConnectionString<THub>` | Create an instance of `EndpointProvider` for type `THub` from Azure SignalR connection string. Type `THub` is subclass of `Microsoft.Azure.SignalR.Hub` class.
`CreateEndpointProviderFromConnectionString` | Create an instance of `EndpointProvider` for a named hub from Azure SignalR connection string.
`CreateTokenProviderFromConnectionString<THub>` | Create an instance of `TokenProvider` for type `THub` from Azure SignalR connection string.
`CreateTokenProviderFromConnectionString` | Create an instance of `TokenProvider` for a named hub from Azure SignalR connection string.
`CreateHubProxyFromConnectionString<THub>` | Create an instance of `HubProxy` for type `THub` from Azure SignalR connection string.
`CreateHubProxyFromConnectionString` | Create an instance of `HubProxy` for a named hub from Azure SignalR connection string.

#### `CloudSignalR.CreateEndpointProviderFromConnectionString<THub>(string connectionString)` Method
**Return**: `Microsoft.Azure.SignalR.EndpointProvider`

#### `CloudSignalR.CreateEndpointProviderFromConnectionString(string connectionString, string hubName)` Method
**Return**: `Microsoft.Azure.SignalR.EndpointProvider`

#### `CloudSignalR.CreateTokenProviderFromConnectionString<THub>(string connectionString)` Method
**Return**: `Microsoft.Azure.SignalR.TokenProvider`

#### `CloudSignalR.CreateTokenProviderFromConnectionString(string connectionString, string hubName)` Method
**Return**: `Microsoft.Azure.SignalR.TokenProvider`

#### `CloudSignalR.CreateHubProxyFromConnectionString<THub>(string connectionString)` Method
**Return**: `Microsoft.Azure.SignalR.HubProxy`

#### `CloudSignalR.CreateHubProxyFromConnectionString(string connectionString, string hubName)` Method
**Return**: `Microsoft.Azure.SignalR.HubProxy`

#### `CloudSignalR.CreateHubProxyFromConnectionString<THub>(string connectionString, HubHostOptions options)` Method
**Return**: `Microsoft.Azure.SignalR.HubProxy`

#### `CloudSignalR.CreateHubProxyFromConnectionString(string connectionString, string hubName, HubHostOptions options)` Method
**Return**: `Microsoft.Azure.SignalR.HubProxy`

---

### `EndpointProvider` Class

**Namespace**: `Microsoft.Azure.SignalR`<br>
**Assembly**: `Microsoft.Azure.SignalR.dll`

Method | Description
---|---
`GetClientEndpoint<THub>` | Get client endpoint of type `THub`. `THub` is a subclass of `Microsoft.Azure.SignalR.Hub`.
`GetClientEndpoint` | Get client endpoint of a hub named `hubName`.
`GetServerEndpoint<THub>` | Get hub host endpoint of type `THub`. `THub` is a subclass of `Microsoft.Azure.SignalR.Hub`.
`GetServerEndpoint` | Get hub host endpoint of a hub named `hubName`.

#### `EndpointProvider.GetClientEndpoint<THub>() where THub : Hub` Method
**Return**: `System.String`

#### `EndpointProvider.GetClientEndpoint()` Method
**Return**: `System.String`

#### `EndpointProvider.GetServerEndpoint<THub>() where THub : Hub` Method
**Return**: `System.String`

#### `EndpointProvider.GetServerEndpoint()` Method
**Return**: `System.String`

---

### `TokenProvider` Class

**Namespace**: `Microsoft.Azure.SignalR`<br>
**Assembly**: `Microsoft.Azure.SignalR.dll`

Method | Description
---|---
`GenerateClientAccessToken<THub>` | Generate client access token of type `THub`. `THub` is a subclass of `Microsoft.Azure.SignalR.Hub`.
`GenerateClientAccessToken` | Generate client access token of a hub named `hubName`.
`GenerateServerAccessToken<THub>` | Generate hub host access token of type `THub`. `THub` is a subclass of `Microsoft.Azure.SignalR.Hub`.
`GenerateServerAccessToken` | Generate hub host access token of a hub named `hubName`.

#### `TokenProvider.GenerateClientAccessToken<THub>(IEnumerable<Claim> claims = null, TimeSpan? lifetime = null) where THub : Hub` Method
**Return**: `System.String`

#### `TokenProvider.GenerateClientAccessToken(string hubName, IEnumerable<Claim> claims = null, TimeSpan? lifetime = null)` Method
**Return**: `System.String`

#### `TokenProvider.GenerateServerAccessToken<THub>(TimeSpan? lifetime = null) where THub : Hub` Method
**Return**: `System.String`

#### `TokenProvider.GenerateServerAccessToken(string hubName, TimeSpan? lifetime = null)` Method
**Return**: `System.String`

---

### `HubProxy` Class

**Namespace**: `Microsoft.Azure.SignalR`<br>
**Assembly**: `Microsoft.Azure.SignalR.dll`

Property | Description
---|---
`Clients` | Proxy to clients connected to Azure SignalR.
`Groups` | Proxy to groups within Azure SignalR.

#### `IHubClients<IClientProxy> Clients` Property

#### `IGroupManager Groups` Property

---

### `HubClientsProxy : IHubClients<IClientProxy>` Class

**Namespace**: `Microsoft.Azure.SignalR`<br>
**Assembly**: `Microsoft.Azure.SignalR.dll`

Method | Description
---|---
`AllExcept` | Broadcast message except the specified clients.
`Client` | Send message to the specified client.
`Clients` | Send message to the specified clients.
`Group` | Send message to the specified group.
`Groups` | Send message to the specified groups.
`GroupExcept` | Send message to the specified group except the specified client.
`User` | Send message to the specified user.
`Users` | Send message to the specified users.

#### `HubClientsProxy.AllExcept(IReadOnlyList<string> excludedIds)` Method
**Return**: `Task<HttpResponseMessage>`

#### `HubClientsProxy.Client(string connectionId)` Method
**Return**: `Task<HttpResponseMessage>`

#### `HubClientsAProxy.Clients(IReadOnlyList<string> connectionIds)` Method
**Return**: `Task<HttpResponseMessage>`

#### `HubClientsAProxy.Group(string groupName)` Method
**Return**: `Task<HttpResponseMessage>`

#### `HubClientsAProxy.Groups(IReadOnlyList<string> groupNames)` Method
**Return**: `Task<HttpResponseMessage>`

#### `HubClientsAProxy.GroupExcept(string groupName, IReadOnlyList<string> excludeIds)` Method
**Return**: `Task<HttpResponseMessage>`

#### `HubClientsAProxy.User(string userId)` Method
**Return**: `Task<HttpResponseMessage>`

#### `HubClientsAProxy.Users(IReadOnlyList<string> userIds)` Methods
**Return**: `Task<HttpResponseMessage>`

---

### `GroupManagerProxy : IGroupManager` Class

**Namespace**: `Microsoft.Azure.SignalR`<br>
**Assembly**: `Microsoft.Azure.SignalR.dll`

Method | Description
---|---
`AddAsync` | Add a specified connection to a specified group.
`RemoveAsync` | Remove a specified connection fromss a specified group.

#### `GroupManagerProxy.AddAsync(string connectionId, string groupName)` Method
**Return**: `Task`

#### `GroupManagerProxy.RemoveAsync(string connectionId, string groupName)` Method
**Return**: `Task`


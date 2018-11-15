# Support Multiple SignalR Service Endpoints

## Terms
| Term | Description |
| --- | --- |
| ServerId | |
| Server Connection | |
| Client | |
| Client Connection | |
| 

## Configuration

Configuration with name `Azure:SignalR:ConnectionString`, as well as names starting with `Azure:SignalR:ConnectionString:` will be considered as Azure SignalR Service endpoints.

If the name starts with `Azure:SignalR:ConnectionString:`, the naming convention is `Azure:SignalR:ConnectionString:{name}[:{state}]`. From which, `{name}` stands for an alias of the endpoint, `{state}` stands for the state of the endpoint and can be ignored.

The value of `{state}` can be:
* `active`, this is the default value of the endpoint state.
* `new`, this state means the endpoint is newly added.
* `disabled`, this state means the endpoint is no longer used.

## Server connection
For ASP.NET Core SignalR, server initiates `ConnectionCount(default 5) * hub` connections to each Azure SignalR Service endpoint.
For ASP.NET SignalR, server initiates `ConnectionCount(default 5) * (hub + 1(app))` connections to each Azure SignalR Service endpoint.

Strong connections and Week connections:

* Strong connections: a websocket connection is established between the app server and the Azure SignalR service. Clients will be routed to this connection. When the connection disconnected, all the clients routed to this connection will be automatically disconnected.
* Week connections: a websocket connection is established between the app server and the Azure SignalR service. Clients will NOT be routed to this connection.

## Server stages
The app server has 3 stages:
1. `New`: This is the default stage of an app server.
1. `Online`: When the server is taken online, the server is ready to take traffic.
2. `Offline`: When the server is taken offline, the server is not going to take new traffic.


## Server deployment
### New deployment
All servers are in `Online` stage, taking no traffic

### App server upgrade with endpoint config changes
* Existing `conn1`, `conn2`, upgrade with new `conn3`
1. `conn3` is marked as `conn3:new` and add to the upgraded app server, the server connection is established
2. `conn3:new` is not yet accepting client connection so app server's `/negotiate` does not return it as the redirect url.
3. After all the app servers are upgraded, the app servers are marked as `Online`
4. `/negotiate` now returns `conn3:new` as a redirect service

* Existing `conn1`, `conn2`, `conn3`, upgrade with `conn3` removed
1. `conn3` is marked as `conn3:disabled` and add to the upgraded app server, the server connection is established
2. `conn3:disabled` no longer accepts client connection so app server's `/negotiate` does not return it as the redirect url
3. After all the app servers are upgraded, mark the app servers as `Online`
4. Disconnect the `conn3` server connections

## Routing

### Client request routing
Client first sends a `/negotiate` request to server, and server decides which service endpoint the client should be redirected to.

#### Route Algorithm
1. `new`

### Server message routing
1. For ECHO case, the message goes back to the incoming server
2. For other cases, the message is broadcasted to every service. For group related messages, MUST make sure they goes into the same connection

## When app server upgrade with ConnectionString configuration updated
### Add new Endpoints
`Azure:SignalR:ConnectionString:<name>:new` connection string added.
Servers are not returning this **new** endpoint when client `/negotiate`, until a `OnlineServerAsync` method get called.

### Remove Endpoints

Update the to-be-disabled connection string to `Azure:SignalR:ConnectionString:<name>:disabled`.
Servers are no longer returning this **disabled** endpoint when client `/negotiate`,
When starting the server connection, messages are still broadcasted to this endpoint.

After all app servers are upgraded, call `OnlineServerAsync` method in the server, the server will disconnect to the to-be-removed endpoint, and all the clients connected to this endpoint will be disconnected if any.

| Status | `/negotiate` | server-connection | When `GetReady` called |
|---|---|---|--|
| `Active` | Return | Establish | No change |
| `Disabled` | Not Return | Establish | Disconnect server-connection |
| `New` | Not Return | Establish | Return when `/negotiate` |


### Update Endpoints
Following both [Add new Endpoints] and [Remove Endpoints].

## Changes


## Options
1. Provide a `OnlineServerAsync` to switch endpoint status from new => active, 
    * Cons: not straight forward, there are 2 places controlling the status of the service endpoint, one is from config, another is from the API call
    * Pros: little changes to current structure

2. Provide a hot reload config mechanism
    * Cons: big change to current IOptions DI structure
    * Pros: one source of truth, all status are in config, straight-forward status control through new=>active=>disabled

## Gaps
1. The ability for server-connection to stop connection
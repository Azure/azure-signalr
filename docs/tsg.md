# Troubleshooting Guide

## 🚄Access token too long

### ⏱Possible errors:

1. Client side `ERR_CONNECTION_`
2. 414 URI Too Long
3. 413 Payload Too Large

### 🧾Root cause:
For HTTP/2, the max length for a single header is **4K**, so if you are using browser to access Azure service, you will encounter this limitation with `ERR_CONNECTION_` error.

For HTTP/1.1, or c# clients, the max URI length is **12K**, max header length is **16K**.

With SDK version **1.0.6** or higher, `/negotiate` will throw `413 Payload Too Large` when the generated access token is larger than **4K**.

### 🔨Solution:
By default, claims from `context.User.Claims` are included when generating JWT access token to **ASRS**(**A**zure **S**ignal**R** **S**ervice), so that the claims are preserved and can be passed from **ASRS** to the `Hub` when the client connects to the `Hub`.

In some cases, `context.User.Claims` are leveraged to store lots of information for app server, most of which are not used by `Hub`s but by other components. 

The generated access token are passed through network, and for websocket/SSE connections, access tokens are passed through query strings. So as best practice, we suggest only passing **neccessory** claims from client through **ASRS** to your app server.

There is a ClaimsProvider for you to customize the claims you want to pass into service then to server.

```cs
services.AddSignalR()
        .AddAzureSignalR(options =>
            {
                // pick up neccessory claims
                options.ClaimsProvider = context => context.User.Claims.Where(...);
            });
```

## Server side connection drop

### Possible errors:

1. Connection closed without complete handshake
2. Websocket connection closed

### Root cause:

### Troubleshooting Guide
1. Open server side log
2. create dump

### Solution:
Client side retry
Server side 



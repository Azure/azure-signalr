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

## 🚄Tls1.2 required

### ⏱Possible errors:

1. ASP.Net "No server available" error #279
2. ASP.Net "The connection is not active, data cannot be sent to the service." error #324

### 🧾Root cause:
Azure Service only support TLS1.2 for security concerns. The server connection to ASRS was not successfully established.

### 🧷Troubleshooting Guide
1. Add following code to your `Startup.cs` to enable detailed trace:
```cs
GlobalHost.TraceManager.Switch.Level = SourceLevels.Information;
```
2. Enable local debugging exception throw
![](https://user-images.githubusercontent.com/668244/49123286-fe76b500-f2f2-11e8-908b-4b7bcb0508c3.png)
![](https://user-images.githubusercontent.com/668244/49123306-13534880-f2f3-11e8-80b6-576a3c10bfc8.png)

### 🔨Solution:

Add following code to your Startup:
```cs
ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
```

## ⌛️[TODO]Client side connection drop

### ⏱Possible errors:
1. Client side error log: "The remote party closed the WebSocket connection without completing the close handshake"
2. Client side error log: "Service timeout. 30.00ms elapsed without receiving a message from service."

### 🧾Root cause:
Possibility 1. App server restarts
Possibility 2. **ASRS**(**A**zure **S**ignal**R** **S**ervice) internal error

### 🧷Troubleshooting Guide
1. Open app server side log to see if anything abnormal took place
2. Check app server side event log to see if the app server restarted
3. Create an issue to us providing time frame, and email the resource name to us

## ⌛️[TODO]Server side connection drop

### ⏱Possible errors:
1. Server side error log: "[Error]Connection "..." to the service was dropped"
2. Server side error: "The remote party closed the WebSocket connection without completing the close handshake"

### 🧾Root cause:
Server-service connection is closed by **ASRS**(**A**zure **S**ignal**R** **S**ervice).

### 🧷Troubleshooting Guide
1. Open app server side log to see if anything abnormal took place
2. Check app server side event log to see if the app server restarted
3. Create an issue to us providing time frame, and email the resource name to us

## ⌛️[TODO]Heartbeat failed

### ⏱Possible errors:
1. Server side log: "Service timeout. 30.00ms elapsed without receiving a message from service."
2. Client side error log: "Service timeout. 30.00ms elapsed without receiving a message from service."

### 🧷Troubleshooting Guide
1. Open app server side log to see if anything abnormal took place
2. Create server side dump file to see if the app server is thread starving

# Troubleshooting Guide

## Access token too long

### Probable errors:

1. Client side `ERR_CONNECTION_` error
2. 414 URI Too Long
3. 413 Payload Too Large

### Root cause:
For HTTP/2, the max length for a single header is 4K, so if you are using browser to access Azure service, you will encounter this limitation with ERR_CONNECTION_ error.

For HTTP/1.1, or c# clients, the max URI length is 12K, max header length is 16K.

### Solution:
So in general, we suggest the access token under 4K.

There is a ClaimsProvider for you to customize the claims you want to pass into service then to server.

```cs
services.AddSignalR()
        .AddAzureSignalR(options =>
            {
                // select only neccessory claims
                options.ClaimsProvider = context => context.User.Claims.Where(...);
            });
```

## Server side connection drop

### Probable errors:

1. Connection closed without complete handshake
2. Websocket connection closed

### Root cause:

### Troubleshooting Guide
1. Open server side log
2. create dump

### Solution:
Client side retry
Server side 



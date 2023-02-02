# Negotiate

At the beginning, client will initialize a `POST [endpoint-base]/negotiate` request to establish a connection between the client and the server.

In the POST request the client sends a query string parameter with the key "negotiateVersion" and the value as the negotiate protocol version it would like to use. If the query string is omitted, the server treats the version as zero. The server will include a "negotiateVersion" property in the json response that says which version it will be using. The version is chosen as described below:
* If the servers minimum supported protocol version is greater than the version requested by the client it will send an error response and close the connection
* If the server supports the request version it will respond with the requested version
* If the requested version is greater than the servers largest supported version the server will respond with its largest supported version
The client may close the connection if the "negotiateVersion" in the response is not acceptable.

The content type of the response is `application/json` and is a JSON payload containing properties to assist the client in establishing a persistent connection. Extra JSON properties that the client does not know about should be ignored. This allows for future additions without breaking older clients.

__Now Azure SignalR service supports `Version 0` only__. So client with the "negotiateVersion" greater than zero will get a reponse with `negotiateVersion=0` by design.

### Version 0

When the server and client agree on version 0 the server response will include a "connectionId" property that is used in the "id" query string for the HTTP requests described below.

A successful negotiate response will look similar to the following payload:
  ```json
  {
    "connectionId":"807809a5-31bf-470d-9e23-afaee35d8a0d",
    "negotiateVersion":0,
    "availableTransports":[
      {
        "transport": "WebSockets",
        "transferFormats": [ "Text", "Binary" ]
      },
      {
        "transport": "ServerSentEvents",
        "transferFormats": [ "Text" ]
      },
      {
        "transport": "LongPolling",
        "transferFormats": [ "Text", "Binary" ]
      }
    ]
  }
  ```

  The payload returned from this endpoint provides the following data:

  * The `connectionId` which is **required** by the Long Polling and Server-Sent Events transports (in order to correlate sends and receives).
  * The `negotiateVersion` which is the negotiation protocol version being used between the server and client.
  * The `availableTransports` list which describes the transports the server supports. For each transport, the name of the transport (`transport`) is listed, as is a list of "transfer formats" supported by the transport (`transferFormats`)

For more detail about SignalR transport protocol from [HERE](https://github.com/dotnet/aspnetcore/blob/main/src/SignalR/docs/specs/TransportProtocols.md).
# Protocol for SignalR Azure SDK

The protocol used between SignalR Azure SDK and SignalR Azure Service is a wrapper of SignalR Hub Protocol. It is specially designed for lower latency of SignalR service in Azure environment.

## Terms

* Clients - browser or the other client which want to access the SignalR App.
* MessageWrapper - Protocol used between SignalR Azure SDK and SignalR Service. It wrapps original Clients message.

## Transport

The protocol is inherited from [SignalR HubProtocol](https://github.com/aspnet/SignalR/blob/dev/specs/HubProtocol.md) and applied for connecting to SignalR Azure Service. Therefore, it has the same requirement as SignalR HubProtocol, for example, it requires reliable, in-order, and delivery of messages.

Different from the SignalR HubProtocol which is used between Clients and SignalR Azure Service, this protocol is transparent to developers compared. This document targets to provide the implementation details to developers about how to efficiently forward message from the point of SignalR Azure Service. The assumption is you have already been familiar with SignalR HubProtocol.

## Overview

This protocol supports [JSON](http://www.json.org/) and [MessagePack](http://msgpack.org/) format. The default protocol is MessagePack.

In this protocol, the following types of messages can be sent:

| Message Name          | Sender                             | Description                                                                                                                    |
| ------------------    | ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| `HandshakeRequest`    | SignalR Azure SDK                  | Sent by the SDK to agree on the message format.                                                                                |
| `HandshakeResponse`   | SignalR Service                    | Sent by the SignalR service as an acknowledgment of the previous `HandshakeRequest` message. Contains an error if the handshake failed. |
| `Close`               | SignalR Azure SDK, SignalR Service | Sent by the server when a connection is closed. Contains an error if the connection was closed because of an error.            |
| `MessageWrapper`      | SignalR Azure SDK, SignalR Service | Indicates a message of `OnConnected`, `OnDisconnected`, `Invocation`, `StreamInvocation` or `CancelInvocation`, is sending to SignalR Service, or `StreamItem`, `Completion`, `Completion` is receiving from SignalR Service. |
| `Ping`                | SignalR Azure SDK, SignalR Service | Sent by either party to check if the connection is active.                                                                     |

After opening a connection to the SignalR Service the SDK must send a `HandshakeRequest` message to the SignalR Service as its first message. The handshake message is **always** a JSON message and contains the name of the format (protocol) as well as the version of the protocol that will be used for the duration of the connection. The SignalR Service will reply with a `HandshakeResponse`, also always JSON, containing an error if the server does not support the protocol. If the SignalR Service does not support the protocol requested by the SDK or the first message received from the SDK is not a `HandshakeRequest` message the SignalR Service must close the connection.

The `HandshakeRequest` message contains the following properties:

* `protocol` - the name of the protocol to be used for messages exchanged between the SDK and the SignalR Service, it is "messagepackwrapper" or "jsonwrapper"
* `version` - the value must always be 1, for both MessagePack and Json protocols

Example:

```json
{
    "protocol": "messagepackwrapper",
    "version": 1
}
```

The `HandshakeResponse` message contains the following properties:

* `error` - the optional error message if the server does not support the requested protocol

Example:

```json
{
    "error": "Requested protocol 'messagepack' is not available."
}
```

## Communication between SignalR Azure SDK and SignalR Service

SignalR Service plays a role of message broker for Clients and SignalR Azure SDK. Firstly, SignalR Azure SDK connects to SignalR Service, and then a protocol, for example,MessagePack is selected for the following communication between them.

When a Client connects to SignalR Service, SignalR Service sends a MessageWrapper with empty payload but containing `OnConnected` flag, Client ID and protocol: JSON or MessagePack, to SignalR SDK after the Client successfully established the connection. SignalR SDK creates a Client context to save the connection ID and Client protocol.

In the following communication, once SignalR Service recives a message sent from Clients, it wraps the JSON or MessagePack binary payload with some metadata information, for example, the Client connection ID, into a MessageWrapper, and forwards that MessageWrapper to SignalR SDK. SignalR SDK decodes the MessageWrapper and then decodes the payload with the protocol in Client context created in `OnConnected` step. Next step is to invoke the Hub according to the payload and this step is exactly the same as SignalR. After Hub invocation, there will be some information sending back to SignalR Service, for example, calling Client method or sending Completion message, those information is encoded by Client protocol to be the payload, packed toghter with Client ID into a new MessageWrapper which is sent to SignalR Service.

SignalR Service receives the MessageWrapper from SignalR SDK, decodes the MessageWrapper with MessagePack protocol, then forwards the payload to Client.

If Client closes the connection with SignalR Service, `OnDisconnected` MessageWrapper will be sent to SignalR SDK to clean the Client context.

`Close` and `Ping` message will not be packed into MessageWrapper, and they are handled specially.

## Examples

The following example assumes Client sends invocation message to SignalR Service through JSON format. SignalR SDK use JSON or MessagePack to wrap that message.

```json
{  
   "arguments":[  
      "a",
      "1"
   ],
   "target":"echo",
   "type":1
}
```

This message binary is:

```
0x7b 0x22 0x61 0x72 0x67 0x75 0x6d 0x65 0x6e 0x74 0x73 0x22 0x3a 0x5b 0x22 0x61 0x22 0x2c 0x22 0x31 0x22 0x5d 0x2c 0x22 0x74 0x61 0x72 0x67 0x65 0x74 0x22 0x3a 0x22 0x65 0x63 0x68 0x6f 0x22 0x2c 0x22 0x74 0x79 0x70 0x65 0x22 0x3a 0x31 0x7d 0x1e
```

### JSON Encoding

JSON binary must be encoded thorugh Base64 before they are packed into MessageWrapper.

```json
{  
   "type":255,
   "format":2,
   "invocationtype":3,
   "headers":{  
      "connId":"qsqb-d_A5sTFujUk0nplfw"
   },
   "jsonpayload":"eyJhcmd1bWVudHMiOlsiYSIsIjEiXSwidGFyZ2V0IjoiZWNobyIsInR5cGUiOjF9Hg=="
}
```

* `255` - MessageWrapper type
* `2` - Payload format (JSON)
* `3` - Type of Hub invocation (other than OnConnected or OnDisconnected)
* `headers` - A map containing headers where metadata information is saved
* `jsonpayload` - Base64 encoded JSON data (hub invocation message from Client)

### MessagePack Encoding

Client JSON message is packed into a MessageWrapper and then encoded through MessagePack protocol:

```
0x59 0x96 0xd1 0x0 0xff 0x2 0x3 0x81 0xa6 0x63 0x6f 0x6e 0x6e 0x49 0x64 0xb6 0x52 0x47 0x78 0x70 0x67 0x45 0x45 0x66 0x4d 0x76 0x31 0x4e 0x78 0x57 0x59 0x44 0x41 0x64 0x57 0x61 0x37 0x41 0xc4 0x31 0x7b 0x22 0x61 0x72 0x67 0x75 0x6d 0x65 0x6e 0x74 0x73 0x22 0x3a 0x5b 0x22 0x61 0x22 0x2c 0x22 0x31 0x22 0x5d 0x2c 0x22 0x74 0x61 0x72 0x67 0x65 0x74 0x22 0x3a 0x22 0x65 0x63 0x68 0x6f 0x22 0x2c 0x22 0x74 0x79 0x70 0x65 0x22 0x3a 0x31 0x7d 0x1e 0xc0
```

is decoded as follows:

* `0x59` - 89-length of MessagePack content
* `0x96` - 6 fields of MessageWrapper
* `0xd1` - Integer of length 1
* `0x0 0xff` - MessageWrapper type
* `0x2` - Payload format: JSON
* `0x3` - Type of Hub invocation (other than OnConnected or OnDisconnected)
* `0x81` - Map of length 1 (Headers)
* `0xa6` - string of length 6 (connId)
* `0x63 0x6f 0x6e 0x6e 0x49 0x64` - connId
* `0xb6` - string of length 22 (RGxpgEEfMv1NxWYDAdWa7A)
* `0x52 0x47 0x78 0x70 0x67 0x45 0x45 0x66 0x4d 0x76 0x31 0x4e 0x78 0x57 0x59 0x44 0x41 0x64 0x57 0x61 0x37 0x41` - string of RGxpgEEfMv1NxWYDAdWa7A
* `0xc4` - binary format
* `0x31` - binary length of 49 (JSON content and ending flag `0x1e`)
* `0x7b 0x22 0x61 0x72 0x67 0x75 0x6d 0x65 0x6e 0x74 0x73 0x22 0x3a 0x5b 0x22 0x61 0x22 0x2c 0x22 0x31 0x22 0x5d 0x2c 0x22 0x74 0x61 0x72 0x67 0x65 0x74 0x22 0x3a 0x22 0x65 0x63 0x68 0x6f 0x22 0x2c 0x22 0x74 0x79 0x70 0x65 0x22 0x3a 0x31 0x7d 0x1e` - JSON payload
* `0xc0` - nil for MessagePack payload

# Azure SignalR Service Protocol

The Azure SignalR Service Protocol is a protocol between Azure SignalR Service and user application (server side) to provide an abstract transport between application clients and application server.

## Terms

- Service - Azure SignalR Service. It accepts connections from both clients and servers, acting as the abstract transport between them.
- Server - Application server node, which is connected with Azure SignalR Service, using this protocol to receive data from and send data to clients via Azure SignalR Service. 
- Client - Application client node, which is connected with Azure SignalR Service, using SignalR protocols. Azure SignalR Service will look exactly the same as a self-hosted SignalR server from the client.

## Overview

Azure SignalR Service Protocol uses WebSocket transport and MessagePack encoding for better performance, and allows a limited set of messages.
Messages are categorized into two groups:

### Generic Connection Message

These messages are in the context of a single connection.

Message Name | Sender | Description
---|---|---
OpenConnection| Service | Sent by Service to notify Server there is a new client connected.
CloseConnection | Service or Server | Sent by Service or Server to notify the other side that the specified connection should be closed.
ConnectionData | Service or Server | When sent from Service to Server, it contains data from a single client. When sent from Server to Service, it contains data which should be delivered to a single client.

### SignalR-specific Message

These messages map to the various operations in the SignalR framework.

Message Name | Sender | Description
---|---|---
MultiConnectionData | Server | Sent from Server to Service. Payloads in the message will be sent to multiple connections by Service.
UserData | Server | Sent from Server to Service. Payloads in the message will be sent to the target user (possible multiple connections) by Service.
MultiUserData | Server | Sent from Server to Service. Payloads in the message will be sent to multiple connections by Service.
BroadcastData | Server | Sent from Server to Service. Payloads in the message will be broadcasted to all connected clients by Service.
JoinGroup | Server | Sent by server to ask Service adding the target connection to the target group.
LeaveGroup | Server | Sent by server to ask Service removing the target connection from the target group.
GroupBroadcastData | Server | Sent from Server to Service. Payloads in the message will be broadcasted to all connections within the target group by Service.
MultiGroupBroadcastData | Server | Sent from Server to Service. Payloads in the message will be broadcasted to all connections within the target groups by Service.

### Misc

Message Name | Sender | Description
---|---|---
Ping | Service or Server | Sent by Service or Server to check the connection is alive.

## Communication Model

Server should initiate the connection to Service, then the communication begins. Data from/to multiple clients will be multiplexed in this one connection between Server and Service.

### New Client Connect

When a new client is connected to Service, a `OpenConnection` message will be sent by Service to Server.

### Client Disconnect

- When a client is disconnected from Service, a `CloseConnection` message will be sent by Service to Server.
- When Server wants to disconnect a client, a `CloseConnection` message will be sent by Server to Service. Then Service will disconnect the underlying connections with the target client.

### Data Pass Through

- When a client sends data to Service, a `ConnectionData` message will be sent by Service to Server.
- When Server wants to send data to a client, a `ConnectionData` message will be sent by Server to Service. 

### SignalR scenarios

Service supports various scenarios in SignalR to send data from Server to multiple client.
- When Server wants to send data to a specific set of connections, a `MultiConnectionData` message is sent to Service, containing the list of the target connections.
- When Server wants to send data to a specific user, a `UserData` message is sent to Service.
- When Server wants to send data to a specific set of users, a `MultiUserData` message is sent to Service, containing the list of the target users.
- When Server wants to send data to all clients, a `BroadcastData` message is sent to Service.
- When Server wants to send data to a specific group, a `GroupData` message is sent to Service.
- When Server wants to send data to a couple of groups, a `MultiGroupData` message is sent to Service.

## Message Encodings

In Azure SignalR Service Protocol, each message is represented as a single MsgPack array containing items that correspond to properties of the given service message.

MessagePack uses different formats to encode values. Refer to the [MsgPack format spec](https://github.com/msgpack/msgpack/blob/master/spec.md#formats) for format definitions.

### Ping Message
 `Ping` messages have the following structure.
```
[1]
```
- 1 - Message Type, indicating this is a `Ping` message.

#### Example: TODO

### OpenConnection Message
`OpenConnection` messages have the following structure.
```
[2, ConnectionId]
```
- 2 - Message Type, indicating this is a `OpenConnection` message.
- ConnectionId - A `String` encoding unique Id for the connection.

#### Example: TODO

### CloseConnection Message
`CloseConnection` messages have the following structure.
```
[3, ConnectionId, ErrorMessage]
```
- 3 - Message Type, indicating this is a `CloseConnection` message.
- ConnectionId - A `String` encoding unique Id of the connection.
- ErrorMessage - Optional `String` encoding error message.

#### Example: TODO

### ConnectionData Message
`ConnectionData` messages have the following structure.
```
[4, ConnectionId, Payload]
```
- 4 - Message Type, indicating this is a `ConnectionData` message.
- ConnectionId - A `String` encoding unique Id for the connection.
- Payload - `Binary` encoding of the raw bytes from/to the connection.

#### Example: TODO

### MultiConnectionData Message
`MultiConnectionData` messages have the following structure.
```
[5, ConnectionList, Payloads]
```
- 5 - Message Type, indicating this is a `MultiConnectionData` message.
- ConnectionList - An array containing `String` encoding Ids of the target connections.
- Payloads - A MsgPack Map containing payloads, with string keys and byte array values. The key is the protocol name of the value.

#### Example: TODO

### UserData Message
`UserData` messages have the following structure.
```
[6, UserId, Payloads]
```
- 6 - Message Type, indicating this is a `UserData` message.
- UserId - A `String` encoding unique Id for the user.
- Payloads - A MsgPack Map containing payloads, with string keys and byte array values. The key is the protocol name of the value.

#### Example: TODO

### MultiUserData Message
`MultiUserData` messages have the following structure.
```
[7, UserList, Payloads]
```
- 7 - Message Type, indicating this is a `MultiUserData` message.
- UserList - An array containing `String` encoding Ids of the target users.
- Payloads - A MsgPack Map containing payloads, with string keys and byte array values. The key is the protocol name of the value.

#### Example: TODO

### BroadcastData Message
`BroadcastData` messages have the following structure.
```
[8, ExcludedList, Payloads]
```
- 8 - Message Type, indicating this is a `BroadcastData` message.
- ExcludedList - An array containing `String` encoding Ids of the connections, which will not receive payload in this message.
- Payloads - A MsgPack Map containing payloads, with string keys and byte array values. The key is the protocol name of the value.

#### Example: TODO

### JoinGroup Message
`JoinGroup` messages have the following structure.
```
[9, ConnectionId, GroupName]
```
- 9 - Message Type, indicating this is a `JoinGroup` message.
- ConnectionId - A `String` encoding unique Id for the connection.
- GroupName - A `String` encoding group name, which the connection will join.

#### Example: TODO

### LeaveGroup Message
`LeaveGroup` messages have the following structure.
```
[10, ConnectionId, GroupName]
```
- 10 - Message Type, indicating this is a `LeaveGroup` message.
- ConnectionId - A `String` encoding unique Id for the connection.
- GroupName - A `String` encoding group name, which the connection will leave.

#### Example: TODO

### GroupBroadcastData Message
`GroupBroadcastData` messages have the following structure.
```
[11, GroupName, ExcludedList, Payloads]
```
- 11 - Message Type, indicating this is a `GroupBroadcastData` message.
- GroupName - A `String` encoding target group name.
- ExcludedList - An array containing `String` encoding Ids of the connections, which will not receive payload in this message.
- Payloads - A MsgPack Map containing payloads, with string keys and byte array values. The key is the protocol name of the value.

#### Example: TODO

### MultiGroupBroadcastData Message
`MultiGroupBroadcastData` messages have the following structure.
```
[12, GroupList, Payloads]
```
- 12 - Message Type, indicating this is a `MultiGroupBroadcastData` message.
- GroupList - An array containing `String` encoding target group names.
- Payloads - A MsgPack Map containing payloads, with string keys and byte array values. The key is the protocol name of the value.

#### Example: TODO

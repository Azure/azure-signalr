# Azure SignalR Service Protocol

The Azure SignalR Service Protocol is a protocol between Azure SignalR Service and user application (server side) to provide an abstract transport between application clients and application server.

## Terms

- Service - Azure SignalR Service. It accepts connections from both clients and servers, acting as the abstract transport between them. It will internally maintain a one-to-one mapping between clients and servers, to make sure that messages are correctly routed to the recipients as if it is a physical transport.
- Server - Application server node, which is connected to the Azure SignalR Service, using this protocol to receive data from and send data to clients through Azure SignalR Service. 
- Client - The SignalR client connected to the Azure SignalR Service. The Azure SignalR Service will look exactly the same as a self-hosted SignalR server from the client's perspective.

## Overview

Azure SignalR Service Protocol uses WebSockets and MessagePack to proxy messages between Service and Server.
Messages are categorized into three groups:

### Service Connection Message

These messages are used to establish and maintain the physical connection between Service and Server.

Message Name | Sender | Description
---|---|---
HandshakeRequest | Server | Sent by Server to negotiate the protocol version before the physical connection is established.
HandshakeResponse | Service | Sent by Service to tell Server whether the requested protocol version is supported. If yes, connection will be successfully established. Otherwise, connection will be closed.
Ping | Service or Server | Sent by either side to check the connection is alive.

### Generic Client Connection Message

 Multiple logical client connections will be multiplexed in one or a few (far less than the number of client connections) physical connections between Service and Server. These messages are used to operate a single logical client connection within a physical connection.

Message Name | Sender | Description
---|---|---
OpenConnection| Service | Sent by Service to notify Server there is a new client connected.
CloseConnection | Service or Server | Sent by either side to notify the other side that the specified connection should be closed.
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

## Communication Model

This protocol will be used between Service and Server. There will be one or a few physical connections between Service and Server. Data from/to multiple client connections will be multiplexed within these physical connections. Each client connection will be identified by a unique connection Id. 

The number of client connections will be far more (over 100 times) than the number of physical connections between Service and Server.

### Handshake

Server will initiate a physical connection to Service, using WebSocket transport. Once the WebSocket connection is established, Server will send a `HandshakeRequest` message with a requested version number of Azure SignalR Service Protocol to service.
- If the protocol version is supported, Service will send a success `HandshakeResponse` message to Server. Then the communication begins.
- Otherwise, Service will send a `HandshakeResponse` message with error, and the physical connection will be closed.

### New Client Connect

When a new client is connected to Service, a `OpenConnection` message will be sent by Service to Server.

### Client Disconnect

- When a client is disconnected from Service, a `CloseConnection` message will be sent by Service to Server.
- When Server wants to disconnect a client, a `CloseConnection` message will be sent by Server to Service. Then Service will disconnect the phyical connection with the target client.

### Client Data Pass Through

- When a client sends data to Service, a `ConnectionData` message will be sent by Service to Server.
- When Server wants to send data to a client, a `ConnectionData` message will be sent by Server to Service. 

### SignalR scenarios

Service supports various scenarios in SignalR to send data from Server to multiple clients.
- When Server wants to send data to a specific set of connections, a `MultiConnectionData` message is sent to Service, containing the list of the target connections.
- When Server wants to send data to a specific user, a `UserData` message is sent to Service.
- When Server wants to send data to a specific set of users, a `MultiUserData` message is sent to Service, containing the list of the target users.
- When Server wants to send data to all clients, a `BroadcastData` message is sent to Service.
- When Server wants to send data to a specific group, a `GroupBroadcastData` message is sent to Service.
- When Server wants to send data to a couple of groups, a `MultiGroupBroadcastData` message is sent to Service.

## Message Encodings

In Azure SignalR Service Protocol, each message is represented as a single MessagePack array containing items that correspond to properties of the given service message.

MessagePack uses different formats to encode values. Refer to the [MessagePack Format Spec](https://github.com/msgpack/msgpack/blob/master/spec.md#formats) for format definitions.

### HandshakeRequest Message
`HandshakeRequest` messages have the following structure.
```
[1, Version]
```
- 1 - Message Type, indicating this is a `HandshakeRequest` message.
- Version - A `Int32` encoding number of the protocol version.

#### Example: TODO

### HandshakeResponse Message
`HandshakeResponse` messages have the following structure.
```
[2, ErrorMessage]
```
- 2 - Message Type, indicating this is a `HandshakeResponse` message.
- ErrorMessage - A `String` encoding error message. Null means handshake success, otherwise it means there is error.

#### Example: TODO

### Ping Message
 `Ping` messages have the following structure.
```
[3]
```
- 3 - Message Type, indicating this is a `Ping` message.

#### Example: TODO

### OpenConnection Message
`OpenConnection` messages have the following structure.
```
[4, ConnectionId, Claims]
```
- 4 - Message Type, indicating this is a `OpenConnection` message.
- ConnectionId - A `String` encoding unique Id for the connection.
- Claims - A MessagePack Map containing all claims of this client.

#### Example: TODO

### CloseConnection Message
`CloseConnection` messages have the following structure.
```
[5, ConnectionId, ErrorMessage]
```
- 5 - Message Type, indicating this is a `CloseConnection` message.
- ConnectionId - A `String` encoding unique Id of the connection.
- ErrorMessage - Optional `String` encoding error message.

#### Example: TODO

### ConnectionData Message
`ConnectionData` messages have the following structure.
```
[6, ConnectionId, Payload]
```
- 6 - Message Type, indicating this is a `ConnectionData` message.
- ConnectionId - A `String` encoding unique Id for the connection.
- Payload - `Binary` encoding of the raw bytes from/to the connection.

#### Example: TODO

### MultiConnectionData Message
`MultiConnectionData` messages have the following structure.
```
[7, ConnectionList, Payloads]
```
- 7 - Message Type, indicating this is a `MultiConnectionData` message.
- ConnectionList - An array containing `String` encoding Ids of the target connections.
- Payloads - A MessagePack Map containing payloads, with string keys and byte array values. The key is the protocol name of the value.

#### Example: TODO

### UserData Message
`UserData` messages have the following structure.
```
[8, UserId, Payloads]
```
- 8 - Message Type, indicating this is a `UserData` message.
- UserId - A `String` encoding unique Id for the user.
- Payloads - A MessagePack Map containing payloads, with string keys and byte array values. The key is the protocol name of the value.

#### Example: TODO

### MultiUserData Message
`MultiUserData` messages have the following structure.
```
[9, UserList, Payloads]
```
- 9 - Message Type, indicating this is a `MultiUserData` message.
- UserList - An array containing `String` encoding Ids of the target users.
- Payloads - A MessagePack Map containing payloads, with string keys and byte array values. The key is the protocol name of the value.

#### Example: TODO

### BroadcastData Message
`BroadcastData` messages have the following structure.
```
[10, ExcludedList, Payloads]
```
- 10 - Message Type, indicating this is a `BroadcastData` message.
- ExcludedList - An array containing `String` encoding Ids of the connections, which will not receive payload in this message.
- Payloads - A MessagePack Map containing payloads, with string keys and byte array values. The key is the protocol name of the value.

#### Example: TODO

### JoinGroup Message
`JoinGroup` messages have the following structure.
```
[11, ConnectionId, GroupName]
```
- 11 - Message Type, indicating this is a `JoinGroup` message.
- ConnectionId - A `String` encoding unique Id for the connection.
- GroupName - A `String` encoding group name, which the connection will join.

#### Example: TODO

### LeaveGroup Message
`LeaveGroup` messages have the following structure.
```
[12, ConnectionId, GroupName]
```
- 12 - Message Type, indicating this is a `LeaveGroup` message.
- ConnectionId - A `String` encoding unique Id for the connection.
- GroupName - A `String` encoding group name, which the connection will leave.

#### Example: TODO

### GroupBroadcastData Message
`GroupBroadcastData` messages have the following structure.
```
[13, GroupName, ExcludedList, Payloads]
```
- 13 - Message Type, indicating this is a `GroupBroadcastData` message.
- GroupName - A `String` encoding target group name.
- ExcludedList - An array containing `String` encoding Ids of the connections, which will not receive payload in this message.
- Payloads - A MessagePack Map containing payloads, with string keys and byte array values. The key is the protocol name of the value.

#### Example: TODO

### MultiGroupBroadcastData Message
`MultiGroupBroadcastData` messages have the following structure.
```
[14, GroupList, Payloads]
```
- 14 - Message Type, indicating this is a `MultiGroupBroadcastData` message.
- GroupList - An array containing `String` encoding target group names.
- Payloads - A MessagePack Map containing payloads, with string keys and byte array values. The key is the protocol name of the value.

#### Example: TODO

### UserJoinGroup Message
`UserJoinGroup` messages have the following structure.
```
[16, UserId, GroupName]
```
- 16 - Message Type, indicating this is a `UserJoinGroup` message.
- UserId - A `String` encoding unique Id for the user.
- GroupName - A `String` encoding group name, which the user will join.

#### Example: TODO

### UserLeaveGroup Message
`UserLeaveGroup` messages have the following structure.
```
[17, UserId, GroupName]
```
- 17 - Message Type, indicating this is a `UserLeaveGroup` message.
- UserId - A `String` encoding unique Id for the user.
- GroupName - A `String` encoding group name, which the user will leave.

#### Example: TODO

Azure SignalR Service 1.0 Performance Guide
===========================================

Terms
-----

ASRS: Azure SignalR Service

Inbound: the incoming message to Azure SignalR Service

Outbound: the outgoing message from Azure SignalR Service

Bandwidth: total size of all messages

Audience
--------

Developers of Azure SignalR Service

Overview
--------

ASRS defines 7 Standard tiers for different performance capacities, and this
guide intends to answer the following questions:

-   What is the status of ASRS performance for each tier?

-   Does ASRS meet my requirement to send 100,000 message per second?

-   For my specific scenario, which tier is suitable for me?

-   What kind of app server (VM size) is suitable for me and how many of them shall I deploy?


To answer those questions, the performance guide illustrates the max inbound
and outbound message for every tier on 4 typical use cases: echo, broadcast,
send to group, and send to connection (peer to peer chatting).

Different user has different use cases. It is impossible for this document to enumerate all
scenarios (different use case, different message size, or message sending
pattern etc.) in the limited paragraph, however, it provides a formula for users to approximately
evaluate their specific max inbound or outbound based on the 4 known use cases.

Guide
-----

The inbound and outbound capacity is impacted by the following factors:

-   unit tier (CPU/Memory quota)

-   connection number

-   message size

-   message send/receive rate

-   use case

-   app server and server connections (on server mode)

Generally, the inbound and outbound message turn less if the sending message size increases.

Similarly, Increasing or decreasing message sending rate can be considered as enlarging or shrinking message size.

As the connections increase, ASRS needs more CPU and memory to keep them, thus impacts the inbound or outbound capacity.

The known bottleneck of performance is CPU, memory, and ASRS internal message queue. Different use cases may hit different bottleneck.

App server also impacts performance. Azure SignalR SDK default creates 5 server
connections with ASRS. In the below performance test, server connections are
increased to 15 (or more for broadcast and send message to big group) and
StandardF4sV2 VM is chosen as app server. Different use cases have different
requirement on app servers. Broadcast needs very small number of app servers.
Echo or send to client needs many app servers.

This document summarizes 4 typical use cases for Websockets transport: echo, broadcast,
send to group, and send to client, which will be introduced in the following
sections.

For all use cases, the performance guide wants to find the max inbound and
outbound with the criteria that 99% message latency is less than 1s.

In all use cases, the default message size is 2048 bytes, and message send
interval is 1 second.

- [Server mode](#server)
  - [Echo](#echo)
  - [Broadcast message to all clients](#broadcast)
  - [Broadcast message to a group](#sendToGroup)
  - [Send message to a client](#sendToClient)
  - [Evaluate perf for unknown scenario](#evaluatePerf)
  - [AspNet SignalR Echo/Broadcast](#AspNetSignalR)

<a name="server"></a>
## Server mode

Clients, web app servers, and ASRS are included under this mode. Every client
stands for a single connection.

<a name="echo"></a>
### Echo

Firstly, web apps connect to ASRS. Secondly, thousands of clients connect web
app, which redirect the clients to ASRS with the access token. Then, clients
establish Websockets connection with ASRS.

After all clients finish establishing connections, then they start sending message which contains a timestamp to the specific Hub every second, which echos the message back to its original client. Every client calculates the latency when it receives the echo back message.

The step 5\~8 (red highlighted traffic) are in a loop which will run for a
default duration (5 minutes) and get the statistic of all message latency. The
performance guide shows the max client (connection) number.

![Echo](./images/echo.png)

Echo's behavior determines the inbound is equal to outbound. The max
inbound/outbound and bandwidth is summarized in the following table.

|                                   | Unit1 | Unit2 | Unit5 | Unit10 | Unit20 | Unit50 | Unit100 |
|-----------------------------------|-------|-------|-------|--------|--------|--------|---------|
| Connections                       | 1,000 | 2,000 | 5,000 | 10,000 | 20,000 | 50,000 | 100,000 |
| Inbound/Outbound (message/second) | 1,000 | 2,000 | 5,000 | 10,000 | 20,000 | 50,000 | 100,000 |
| Inbound/Outbound Bandwidth (byte) | 2M    | 4M    | 10M   | 20M    | 40M    | 100M   | 200M    |


App server count suggested

|                  | Unit1 | Unit2 | Unit5 | Unit10 | Unit20 | Unit50 | Unit100 |
|------------------|-------|-------|-------|--------|--------|--------|---------|
| Connections      | 1,000 | 2,000 | 5,000 | 10,000 | 20,000 | 50,000 | 100,000 |
| App server count | 2     | 2     | 2     | 3      | 3      | 10     | 20      |


<a name="broadcast"></a>
### Broadcast

For Broadcast, when web app receives the message, it broadcasts to all clients. More clients to broadcast, more message traffic.

The max inbound/outbound and bandwidth is summarized in the following table.

|                           | Unit1 | Unit2 | Unit5  | Unit10 | Unit20 | Unit50  | Unit100 |
|---------------------------|-------|-------|--------|--------|--------|---------|---------|
| Connections               | 1,000 | 2,000 | 5,000  | 10,000 | 20,000 | 50,000  | 100,000 |
| Inbound (message/second)  | 4     | 2     | 2      | 2      | 2      | 2       | 2       |
| Outbound (message/second) | 4,000 | 4,000 | 10,000 | 20,000 | 40,000 | 100,000 | 200,000 |
| Inbound bandwidth (byte)  | 8K    | 4K    | 4K     | 4K     | 4K     | 4K      | 4K      |
| Outbound Bandwidth (byte) | 8M    | 8M    | 20M    | 40M    | 80M    | 200M    | 400M    |

There are very small client connections (less than 10) to send message in
broadcast use case, thus requires less app servers.

Broadcast requires less app servers compared with echo since its the inbound message is very small. 2 app servers are enough for both SLA and performance consideration. But the default server connections should be increased to avoid unbalanced issue especially for Unit50 and Unit100.

> Note:
> Increase the default server connections from 5 to 40 on every app server to
> avoid possible unbalanced server connections to ASRS.


|                  | Unit1 | Unit2 | Unit5 | Unit10 | Unit20 | Unit50 | Unit100 |
|------------------|-------|-------|-------|--------|--------|--------|---------|
| Connections      | 1,000 | 2,000 | 5,000 | 10,000 | 20,000 | 50,000 | 100,000 |
| App server count | 2     | 2     | 2     | 2      | 2      | 2      | 2       |


<a name="broadcast_img"></a>
![Broadcast](./images/broadcast.png)

<a name="sendToGroup"></a>
### Send to group

After clients establish Websockets connections with ASRS, they must join groups
before sending message. The traffic flow is illustrated by the following diagram.

![Send To Group](./images/sendtogroup.png)

Group member and group count are two factors which impact the performance. To
simplify the analysis, here defines two kinds of groups: small group, and big
group.

- `small group`: 10 connections in every group. The group number is equal to (total
connection count) / 10. For example, for Unit 1, if there are 1000 connection
count, then we have 1000 / 10 = 100 groups.

- `Big group`: Group number is always 10. The group member is equal to (total
connection count) / 10. For example, for Unit 1, if there are 1000 connection
count, then every group has 1000 / 10 = 100 members.

#### small group

|                           | Unit1 | Unit2 | Unit5  | Unit10 | Unit20 | Unit50 | Unit100 |
|---------------------------|-------|-------|--------|--------|--------|--------|---------|
| Connections               | 1,000 | 2,000 | 5,000  | 10,000 | 20,000 | 50,000 | 100,000 |
| Inbound (message/second)  | 400   | 400   | 1,000  | 2,500  | 4,000  | 7,000  | 7,000   |
| Inbound bandwidth (byte)  | 800K  | 800K  | 2M     | 5M     | 8M     | 14M    | 14M     |
| Outbound (message/second) | 4,000 | 4,000 | 10,000 | 25,000 | 40,000 | 70,000 | 70,000  |
| Outbound bandwidth (byte) | 8M    | 8M    | 20M    | 5M     | 80M    | 140M   | 140M    |


App server number suggested

|                  | Unit1 | Unit2 | Unit5 | Unit10 | Unit20 | Unit50 | Unit100 |
|------------------|-------|-------|-------|--------|--------|--------|---------|
| Connections      | 1,000 | 2,000 | 5,000 | 10,000 | 20,000 | 50,000 | 100,000 |
| App server count | 2     | 2     | 2     | 3      | 3      | 10     | 20      |

#### big group

|                           | Unit1 | Unit2 | Unit5  | Unit10 | Unit20 | Unit50  | Unit100 |
|---------------------------|-------|-------|--------|--------|--------|---------|---------|
| Connections               | 1,000 | 2,000 | 5,000  | 10,000 | 20,000 | 50,000  | 100,000 |
| Group count               | 100   | 200   | 500    | 1,000  | 2,000  | 5,000   | 10,000  |
| Inbound (message/second)  | 40    | 20    | 20     | 10     | 20     | 20      | 20      |
| Inbound Bandwidth (byte)  | 80K   | 40K   | 40K    | 20K    | 40K    | 40K     | 40K     |
| Outbound (message/second) | 4,000 | 4,000 | 10,000 | 10,000 | 40,000 | 100,000 | 200,000 |
| Outbound Bandwidth (byte) | 8M    | 8M    | 20M    | 20M    | 80M    | 200M    | 400M    |

App server number suggested

|                  | Unit1 | Unit2 | Unit5 | Unit10 | Unit20 | Unit50 | Unit100 |
|------------------|-------|-------|-------|--------|--------|--------|---------|
| Connections      | 1,000 | 2,000 | 5,000 | 10,000 | 20,000 | 50,000 | 100,000 |
| App server count | 2     | 2     | 2     | 2      | 2      | 2      | 2       |


<a name="sendToClient"></a>
### Send to connection

Clients get their own connection ID before starting sending message. The
performance benchmark is responsible to collect all connection IDs, shuffle them and re-assign them to all clients as a sending target. Every client gets a
target connection ID, and the clients will always send message to the target connection.

![Send to client](./images/sendtoclient.png)

|                                    | Unit1 | Unit2 | Unit5 | Unit10 | Unit20 | Unit50          | Unit100         |
|------------------------------------|-------|-------|-------|--------|--------|-----------------|-----------------|
| Connections                        | 1,000 | 2,000 | 5,000 | 10,000 | 20,000 | 50,000          | 100,000         |
| Inbound/ Outbound (message/second) | 1,000 | 2,000 | 5,000 | 8,000  | 9,000  | 23,000\~ 25,000 | 21,000\~ 27,000 |
| Inbound/ Outbound Bandwidth (byte) | 2M    | 4M    | 10M   | 16M    | 18M    | 46M\~ 50M       | 42M\~ 54M       |


App server number suggested

|                  | Unit1 | Unit2 | Unit5 | Unit10 | Unit20 | Unit50 | Unit100 |
|------------------|-------|-------|-------|--------|--------|--------|---------|
| Connections      | 1,000 | 2,000 | 5,000 | 10,000 | 20,000 | 50,000 | 100,000 |
| App server count | 2     | 2     | 2     | 3      | 3      | 10     | 20      |

<a name="evaluatePerf"></a>
### Evaluate a different case

As this document said, the max inbound/outbound is impacted by message size,
connection count, and message sending rate. If there is a use case which has different parameters as above listed, you cannot get its performance capacity directly (what is the max inbound or outbound?), how can you evaluate its performance behavior? Let us take Unit 100's broadcast as an example.

The following table gives a real case of broadcast, but the message size, connection count, and message sending rate(interval) are different from what we introduced in previous sections. The question is how we can deduce any of those items (message size, connection count, or message sending rate) if we only know 2 of them.

|   | Message size (byte) | Inbound (message/second) | Connections | Send intervals (second) |
|---|---------------------|--------------------------|-------------|-------------------------|
| 1 | 20K                 | 1                        | 100,000     | 5                       |
| 2 | 256K                | 1                        | 8,000       | 5                       |

A simple formula can help:

**Max_connections = Bandwidth \* Send_interval / Message_size**

For Unit 100, we know the max outbound bandwidth is 400M from previous table,
then for 20K message size, the max connections should be 400M \* 5 / 20K =
100,000. We can see it gives the same as real result.

<a name="AspNetSignalR"></a>
### AspNet SignalR

ASRS provides the same performance capacity for AspNet SignalR. This section gives the suggested web app count for AspNet SignalR echo, broadcast to all, and broadcast to a small group.

The performance test uses Azure Web App of [Standard Service Plan S3](https://azure.microsoft.com/en-us/pricing/details/app-service/windows/) for AspNet SignalR.

- `echo`

|                  | Unit1 | Unit2 | Unit5 | Unit10 | Unit20 | Unit50 | Unit100 |
|------------------|-------|-------|-------|--------|--------|--------|---------|
| Connections      | 1,000 | 2,000 | 5,000 | 10,000 | 20,000 | 50,000 | 100,000 |
| App server count | 2     | 2     | 4     | 4      | 8      | 32      | 40       |

- `broadcast all`

|                  | Unit1 | Unit2 | Unit5 | Unit10 | Unit20 | Unit50 | Unit100 |
|------------------|-------|-------|-------|--------|--------|--------|---------|
| Connections      | 1,000 | 2,000 | 5,000 | 10,000 | 20,000 | 50,000 | 100,000 |
| App server count | 2     | 2     | 2     | 2      | 2      | 2      | 2       |

- `broadcast to a small group`

|                  | Unit1 | Unit2 | Unit5 | Unit10 | Unit20 | Unit50 | Unit100 |
|------------------|-------|-------|-------|--------|--------|--------|---------|
| Connections      | 1,000 | 2,000 | 5,000 | 10,000 | 20,000 | 50,000 | 100,000 |
| App server count | 2     | 2     | 4     | 4      | 8      | 32      | 40       |

Performance environments
------------------------

The performance test for all uses cases listed above were conducted in Azure
environment. At most 50 client VMs, and 20 app server VMs are used.

Client VM size: StandardDS2V2 (2 vCPU, 7 G memory)

App server VM size: StandardF4sV2 (4 vCPU, 8 G memory)

Azure SignalR SDK server connections: 15

Performance tools
-----------------

https://github.com/Azure/azure-signalr-bench/tree/master/SignalRServiceBenchmarkPlugin

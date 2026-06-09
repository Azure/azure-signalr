# Azure SignalR Service Chat CLI Sample

This sample demonstrates how to use Azure SignalR Service with ASP.NET Core SignalR.

## Prerequisites

- .NET 8.0 SDK or later
- An Azure SignalR Service instance
- Git (for submodule dependencies)

## Setup

1. Initialize the required submodules:

```bash
git submodule update --init --recursive
```

## Running the Sample

Build and run the project:

```bash
dotnet build
dotnet run
```

## Commands

### Run as Server
``` 
connect <connection-string> <hub> [--transient/-t]
```

#### Example

```
connect <your_connection_string> <your_hub_name> --transient
```

This command connects to the Azure SignalR Service and starts a server instance. The `--transient` option allows for transient connections, which can be useful for testing or development purposes.

### Run as Client
```
client <connection-string> <hub> [--messagepack/-m]
```

#### Example
```
client <your_connection_string> <your_hub_name> --messagepack
```

This command connects to the Azure SignalR Service as a client. The `--messagepack` option enables MessagePack serialization for data transfer.

### Interactive Mode

After running the server or client command, you can enter interactive mode by typing `exit` to terminate the connection.
This allows you to send messages or commands directly to the server or client.

#### Define a method

Before sending messages, you need to define the method and the paramters.

```
define <method>(<paramType1>, <paramType2>, ... <paramTypeN>)
```

#### Example

```
define Hello(string)
```

This command defines a method named `Hello` that takes a single string parameter. You can then call this method with the specified parameter type.

#### Send messages in server mode

```
all.Hello("Hello Everyone!")
group("GroupName").Hello("Hello Group!")
user("UserId").Hello("Hello User!")
connection("ConnectionId").Hello("Hello Connection!")
``` 

These commands send a message to clients using the `Hello` method defined earlier.

#### Send messages in client mode

```
send Hello("Hello from Client!")
```

This command sends a message to the server using the `Hello` method defined earlier.

#### Listen for messages in client mode

To listen for messages in client mode, use the following command:

```
listen <method>(<paramType1>, <paramType2>, ... <paramTypeN>)
```

#### Example

```
listen Hello(string)
```

This command sets up a listener for the `Hello` method, allowing the client to receive messages sent by the server.

#### Load a script

```
load <script_path>
```

This command loads a script file containing predefined methods and commands. The script can be used to automate tasks or define complex interactions.

#### Example
```
load chat.txt
```

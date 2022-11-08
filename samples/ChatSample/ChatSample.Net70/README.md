## Example app that makes use of SignalR Client Results

Requires NET7.0 Preview7 or later SDK/Runtime. Please installed from https://dotnet.microsoft.com/en-us/download/dotnet/7.0.

### Usage

Update `Program.cs` file of below lines to replace the placeholder `<connection-string>` with your Azure SignalR Service connection string.

```cs
builder.Services.AddSignalR(o =>
{
    o.MaximumParallelInvocationsPerClient = 2;
}).AddAzureSignalR("<connection-string>");
```

Run dotnet command to start the server.

```bash
dotnet run
```

#### Using client results

1. Browse to the site with your favorite browser and it will connect with the SignalR Javascript client.
2. It creates 2 clients by default. Grab an ID from the connected connections and paste it in the ID text box.
3. Press 'Get Message' to invoke a Hub method which will ask the specified ID for a result.
4. The client invoked will unlock 'Ack Message' button and you can type something in the text box above.
5. Press 'Ack Message' to return the message to the server which will return it to the original client that asked for a result.

#### Using broadcast method

1. Browse to the site with your favorite browser and it will connect with the SignalR Javascript client.
2. It creates 2 clients by default.
3. Etner some message in the text box above 'Broadcast'.
4. Press 'Broadcast' to send message to all connected clients.

#### Multiple server cases

1. Run `dotnet run` to start default profile.
2. Run `dotnet run --launch-profile Server1` to start another server.
3. Open default server under `https://localhost:7243`.
4. In any of the iframe update the url to second server's port __7245__, `https://localhost:7245/chats` in this sample to access from second server.
5. Now you're able to test clients on different servers.

#### Using client results from anywhere with `IHubContext`

1. Browse to the site with your favorite browser and it will connect with the SignalR Javascript client.
2. Copy the ID for a connected connection.
3. Navigate to `/get/<ID>` in a new tab. Replace `<ID>` with the copied connection ID.
5. Go to the browser tab for the chosen ID and write a message in the Message text box.
6. Press 'Send Message' to return the message to the server which will return it to the `/get/<ID>` request.

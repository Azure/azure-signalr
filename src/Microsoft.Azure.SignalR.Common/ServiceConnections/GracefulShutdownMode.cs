namespace Microsoft.Azure.SignalR;

/// <summary>
/// This mode defines the server's behavior after receiving a `Ctrl+C` (SIGINT).
/// </summary>
public enum GracefulShutdownMode
{
    /// <summary>
    /// The server will stop immediately, all existing connections will be dropped immediately.
    /// </summary>
    Off = 0,

    /// <summary>
    /// We will immediately remove this server from Azure SignalR, 
    /// which means no more new connections will be assigned to this server,
    /// the existing connections won't be influenced until a default timeout (30s).
    /// Once all connections on this server are closed properly, the server stops.
    /// </summary>
    WaitForClientsClose = 1,

    /// <summary>
    /// Similar to `WaitForClientsClose`, the server will be removed from Azure SignalR.
    /// But instead of waiting existing connections to close, we will try to migrate client connections to another valid server,
    /// which may save most of your connections during this process.
    ///
    /// It happens on the message boundaries, considering if each of your message consist of 3 packages. The migration will happen at here:
    /// 
    /// | P1 - P2 - P3 | [HERE] | P4 - P5 - P6 |
    /// | Message 1    |        | Message 2    |
    ///
    /// We do this by finding message boundaries on-fly,
    /// For JSON protocol, we simply find seperators (,)
    /// For MessagePack protocol, we preserve the length header and count body length to determine if the message was finished.
    /// 
    /// This mode always works well with context-free scenarios.
    /// Since the `connectionId` will not change before-and-after migration, 
    /// you may also benifit from this feature by using a distributed storage even if your scenario is not context-free.
    /// </summary>
    MigrateClients = 2,
}

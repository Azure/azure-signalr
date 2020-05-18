namespace Microsoft.Azure.SignalR
{
    internal enum GracefulShutdownMode
    {
        Off = 0,
        WaitForClientsClose = 1,
        MigrateClients = 2,
    }
}

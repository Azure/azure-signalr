namespace Microsoft.Azure.SignalR.ServerConnections
{
    internal interface IConnectionMigrationFeature
    {
        string MigrateFrom { get; }
        string MigrateTo { get; }
    }
}

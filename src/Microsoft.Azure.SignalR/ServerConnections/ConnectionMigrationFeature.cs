namespace Microsoft.Azure.SignalR.ServerConnections
{
    internal class ConnectionMigrationFeature : IConnectionMigrationFeature
    {
        public string MigrateTo { get; private set; }

        public string MigrateFrom { get; private set; }

        public ConnectionMigrationFeature(string from, string to)
        {
            MigrateFrom = from;
            MigrateTo = to;
        }
    }
}
